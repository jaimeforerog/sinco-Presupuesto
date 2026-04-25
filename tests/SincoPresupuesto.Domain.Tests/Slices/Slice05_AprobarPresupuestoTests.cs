using FluentAssertions;
using SincoPresupuesto.Domain.Presupuestos;
using SincoPresupuesto.Domain.Presupuestos.Commands;
using SincoPresupuesto.Domain.Presupuestos.Events;
using SincoPresupuesto.Domain.SharedKernel;
using SincoPresupuesto.Domain.Tests.TestKit;
using Xunit;

namespace SincoPresupuesto.Domain.Tests.Slices;

/// <summary>
/// Slice 05 — AprobarPresupuesto.
/// Spec: slices/05-aprobar-presupuesto/spec.md §6 (firmada 2026-04-24, Q2=(a) — no validar
/// PeriodoFin en aprobación).
/// Estilo: Given/When/Then sobre eventos. El agregado <see cref="Presupuesto"/> se reconstruye
/// por fold desde un <see cref="PresupuestoCreado"/> + <see cref="RubroAgregado"/> y, cuando
/// aplica, <see cref="MontoAsignadoARubro"/> y <see cref="PresupuestoAprobado"/>; luego se
/// invoca el método de instancia <c>AprobarPresupuesto</c>. Las excepciones se aserta por
/// tipo + propiedades (no por mensaje).
///
/// Notas:
/// - §6.8 y §6.9 cierran retroactivamente el followup #13: ejercen las ramas
///   <c>if (Estado != Borrador) throw</c> ya declaradas en slices 03/04 pero diferidas hasta
///   tener un comando que transicione el estado (este slice).
/// - §6.12 fija el contrato defensivo "los Agrupadores nunca aportan a MontoTotal" usando la
///   distinción operacional Agrupador/Terminal por presencia de hijos en <c>_rubros</c>
///   (mismo precedente que slice 04 §6.7).
/// </summary>
public class Slice05_AprobarPresupuestoTests
{
    // ── Fixtures de conveniencia ─────────────────────────────────────
    private static readonly DateTimeOffset T0 =
        new(2026, 4, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset T1 =
        new(2026, 4, 24, 12, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset T2 =
        new(2026, 4, 24, 13, 0, 0, TimeSpan.Zero);

    private static readonly Guid PresupuestoIdFijo =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid R1 =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid R2 =
        Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly Guid R3 =
        Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static PresupuestoCreado PresupuestoCreadoBase(Moneda? monedaBase = null) =>
        new(
            PresupuestoId: PresupuestoIdFijo,
            TenantId: "acme",
            Codigo: "OBRA-2026-01",
            Nombre: "Torre Norte",
            PeriodoInicio: new DateOnly(2026, 1, 1),
            PeriodoFin: new DateOnly(2026, 12, 31),
            MonedaBase: monedaBase ?? Moneda.COP,
            ProfundidadMaxima: 10,
            CreadoEn: T0,
            CreadoPor: "alice");

    private static RubroAgregado RubroRaiz(Guid rubroId, string codigo, string nombre = "Rubro") =>
        new(
            PresupuestoId: PresupuestoIdFijo,
            RubroId: rubroId,
            Codigo: codigo,
            Nombre: nombre,
            RubroPadreId: null,
            AgregadoEn: T0);

    private static RubroAgregado RubroHijo(Guid rubroId, Guid padreId, string codigo, string nombre = "Hijo") =>
        new(
            PresupuestoId: PresupuestoIdFijo,
            RubroId: rubroId,
            Codigo: codigo,
            Nombre: nombre,
            RubroPadreId: padreId,
            AgregadoEn: T0);

    private static MontoAsignadoARubro MontoAsignado(Guid rubroId, Dinero monto, Dinero? montoAnterior = null) =>
        new(
            PresupuestoId: PresupuestoIdFijo,
            RubroId: rubroId,
            Monto: monto,
            MontoAnterior: montoAnterior ?? Dinero.Cero(monto.Moneda),
            AsignadoEn: T0,
            AsignadoPor: "alice");

    // ═══════════════════════════════════════════════════════════════
    // §6.1 Happy path — un único terminal con monto > 0 en MonedaBase
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AprobarPresupuesto_con_un_terminal_en_MonedaBase_emite_PresupuestoAprobado_con_payload_completo()
    {
        // Given
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaiz(R1, "01", "Costos Directos"),
            MontoAsignado(R1, new Dinero(1_000_000m, Moneda.COP)));

        // When
        var cmd = new AprobarPresupuesto(AprobadoPor: "alice");
        var evento = agg.AprobarPresupuesto(cmd, T1);

        // Then
        evento.Should().BeOfType<PresupuestoAprobado>();
        evento.PresupuestoId.Should().Be(PresupuestoIdFijo);
        evento.MontoTotal.Should().Be(new Dinero(1_000_000m, Moneda.COP));
        evento.SnapshotTasas.Should().NotBeNull();
        evento.SnapshotTasas.Should().BeEmpty();
        evento.AprobadoEn.Should().Be(T1);
        evento.AprobadoPor.Should().Be("alice");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.2 Happy path — árbol con un Agrupador y dos terminales
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AprobarPresupuesto_con_arbol_Agrupador_y_dos_terminales_suma_solo_los_terminales()
    {
        // Given: R1 es Agrupador (tiene a R2 y R3 como hijos terminales).
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaiz(R1, "01", "Costos Directos"),
            RubroHijo(R2, padreId: R1, codigo: "01.01", nombre: "Materiales"),
            RubroHijo(R3, padreId: R1, codigo: "01.02", nombre: "Mano de obra"),
            MontoAsignado(R2, new Dinero(700_000m, Moneda.COP)),
            MontoAsignado(R3, new Dinero(300_000m, Moneda.COP)));

        // When
        var cmd = new AprobarPresupuesto(AprobadoPor: "alice");
        var evento = agg.AprobarPresupuesto(cmd, T1);

        // Then: el Agrupador R1 NO aporta — total = 700.000 + 300.000.
        evento.MontoTotal.Should().Be(new Dinero(1_000_000m, Moneda.COP));
        evento.SnapshotTasas.Should().BeEmpty();
        evento.AprobadoEn.Should().Be(T1);
        evento.AprobadoPor.Should().Be("alice");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.3 PRE-4 — normalización de AprobadoPor vacío/whitespace/null → "sistema"
    //        Igual al patrón slice 04 §6.11: el record declara AprobadoPor como
    //        `string` (no nullable) con default "sistema". Pasar `null!` explícito
    //        produciría warning nullable y no representa la API pública. Se
    //        sustituye `null` por `"\t"` (tab) — cubre la rama `IsNullOrWhiteSpace`
    //        sin recurrir a `null!`.
    // ═══════════════════════════════════════════════════════════════
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void AprobarPresupuesto_con_AprobadoPor_vacio_o_whitespace_normaliza_a_sistema(string aprobadoPor)
    {
        // Given
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaiz(R1, "01"),
            MontoAsignado(R1, new Dinero(1_000_000m, Moneda.COP)));

        // When
        var cmd = new AprobarPresupuesto(AprobadoPor: aprobadoPor);
        var evento = agg.AprobarPresupuesto(cmd, T1);

        // Then
        evento.AprobadoPor.Should().Be("sistema");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.4 Violación PRE-2 — presupuesto sin rubros
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AprobarPresupuesto_sin_rubros_lanza_PresupuestoSinMontosException()
    {
        // Given: solo PresupuestoCreado, sin RubroAgregado.
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase());

        var cmd = new AprobarPresupuesto(AprobadoPor: "alice");

        // When
        var act = () => agg.AprobarPresupuesto(cmd, T1);

        // Then
        act.Should().Throw<PresupuestoSinMontosException>()
           .Which.PresupuestoId.Should().Be(PresupuestoIdFijo);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.5 Violación PRE-2 — todos los terminales tienen Monto.EsCero
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AprobarPresupuesto_con_todos_los_terminales_en_cero_lanza_PresupuestoSinMontosException()
    {
        // Given:
        //  - R1 sin asignación → su Monto queda en Dinero.Cero(COP) por inicialización (slice 04 §12.2).
        //  - R2 con asignación explícita a Dinero.Cero(COP) (slice 04 §6.5 lo permite).
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaiz(R1, "01"),
            RubroRaiz(R2, "02"),
            MontoAsignado(R2, Dinero.Cero(Moneda.COP)));

        var cmd = new AprobarPresupuesto(AprobadoPor: "alice");

        // When
        var act = () => agg.AprobarPresupuesto(cmd, T1);

        // Then
        act.Should().Throw<PresupuestoSinMontosException>()
           .Which.PresupuestoId.Should().Be(PresupuestoIdFijo);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.6 Violación PRE-3 — al menos un terminal con monto > 0 en moneda ≠ MonedaBase
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AprobarPresupuesto_con_terminales_en_moneda_distinta_a_MonedaBase_lanza_AprobacionConMultimonedaNoSoportada()
    {
        // Given: presupuesto en COP con tres terminales — R1 en USD (≠), R2 en COP (=), R3 en EUR (≠).
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(monedaBase: Moneda.COP),
            RubroRaiz(R1, "01", "Importados"),
            RubroRaiz(R2, "02", "Locales"),
            RubroRaiz(R3, "03", "Equipo"),
            MontoAsignado(R1, new Dinero(5_000m, Moneda.USD)),
            MontoAsignado(R2, new Dinero(2_000_000m, Moneda.COP)),
            MontoAsignado(R3, new Dinero(800m, Moneda.EUR)));

        var cmd = new AprobarPresupuesto(AprobadoPor: "alice");

        // When
        var act = () => agg.AprobarPresupuesto(cmd, T1);

        // Then: la lista contiene EXACTAMENTE { R1, R3 } en orden de aparición en _rubros.
        var ex = act.Should().Throw<AprobacionConMultimonedaNoSoportadaException>().Which;
        ex.PresupuestoId.Should().Be(PresupuestoIdFijo);
        ex.MonedaBase.Should().Be(Moneda.COP);
        ex.RubrosConMonedaDistinta.Should().BeEquivalentTo(new[] { R1, R3 });
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.7 Violación INV-3 — re-aprobar un presupuesto ya aprobado
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AprobarPresupuesto_sobre_presupuesto_ya_aprobado_lanza_PresupuestoNoEsBorrador()
    {
        // Given: el mismo Given del §6.1 + un PresupuestoAprobado previo (transiciona a Aprobado).
        var aprobacionPrevia = new PresupuestoAprobado(
            PresupuestoId: PresupuestoIdFijo,
            MontoTotal: new Dinero(1_000_000m, Moneda.COP),
            SnapshotTasas: new Dictionary<Moneda, decimal>(),
            AprobadoEn: T1,
            AprobadoPor: "alice");

        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaiz(R1, "01"),
            MontoAsignado(R1, new Dinero(1_000_000m, Moneda.COP)),
            aprobacionPrevia);

        var cmd = new AprobarPresupuesto(AprobadoPor: "bob");

        // When
        var act = () => agg.AprobarPresupuesto(cmd, T2);

        // Then
        act.Should().Throw<PresupuestoNoEsBorradorException>()
           .Which.EstadoActual.Should().Be(EstadoPresupuesto.Aprobado);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.8 INV-3 retroactivo (a) — AgregarRubro post-aprobación lanza.
    //        Cierra followup #13 para AgregarRubro. Ejerce la rama
    //        `if (Estado != Borrador) throw` declarada en slice 03 §6.7.
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AgregarRubro_sobre_presupuesto_aprobado_lanza_PresupuestoNoEsBorrador()
    {
        // Given: presupuesto aprobado con un terminal R1.
        var aprobacionPrevia = new PresupuestoAprobado(
            PresupuestoId: PresupuestoIdFijo,
            MontoTotal: new Dinero(500_000m, Moneda.COP),
            SnapshotTasas: new Dictionary<Moneda, decimal>(),
            AprobadoEn: T1,
            AprobadoPor: "alice");

        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaiz(R1, "01"),
            MontoAsignado(R1, new Dinero(500_000m, Moneda.COP)),
            aprobacionPrevia);

        var cmd = new AgregarRubro(Codigo: "02", Nombre: "Otro", RubroPadreId: null);

        // When
        var act = () => agg.AgregarRubro(cmd, R2, T2);

        // Then
        act.Should().Throw<PresupuestoNoEsBorradorException>()
           .Which.EstadoActual.Should().Be(EstadoPresupuesto.Aprobado);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.9 INV-3 retroactivo (b) — AsignarMontoARubro post-aprobación lanza.
    //        Cierra followup #13 para AsignarMontoARubro. Ejerce la rama
    //        `if (Estado != Borrador) throw` declarada en slice 04 §6.8.
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AsignarMontoARubro_sobre_presupuesto_aprobado_lanza_PresupuestoNoEsBorrador()
    {
        // Given: mismo Given del §6.8 (presupuesto aprobado con R1 ya con monto).
        var aprobacionPrevia = new PresupuestoAprobado(
            PresupuestoId: PresupuestoIdFijo,
            MontoTotal: new Dinero(500_000m, Moneda.COP),
            SnapshotTasas: new Dictionary<Moneda, decimal>(),
            AprobadoEn: T1,
            AprobadoPor: "alice");

        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaiz(R1, "01"),
            MontoAsignado(R1, new Dinero(500_000m, Moneda.COP)),
            aprobacionPrevia);

        var cmd = new AsignarMontoARubro(
            RubroId: R1,
            Monto: new Dinero(750_000m, Moneda.COP),
            AsignadoPor: "bob");

        // When
        var act = () => agg.AsignarMontoARubro(cmd, T2);

        // Then
        act.Should().Throw<PresupuestoNoEsBorradorException>()
           .Which.EstadoActual.Should().Be(EstadoPresupuesto.Aprobado);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.10 Fold — PresupuestoAprobado muta el estado del agregado correctamente
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void Fold_de_PresupuestoAprobado_deja_el_agregado_en_Aprobado_con_baseline_completo()
    {
        // Given: cuatro eventos en orden — Creado, RubroAgregado, MontoAsignado, Aprobado.
        var aprobacion = new PresupuestoAprobado(
            PresupuestoId: PresupuestoIdFijo,
            MontoTotal: new Dinero(1_000_000m, Moneda.COP),
            SnapshotTasas: new Dictionary<Moneda, decimal>(),
            AprobadoEn: T1,
            AprobadoPor: "alice");

        // When: reconstruir el agregado aplicando los cuatro eventos (fold).
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaiz(R1, "01"),
            MontoAsignado(R1, new Dinero(1_000_000m, Moneda.COP)),
            aprobacion);

        // Then
        agg.Id.Should().Be(PresupuestoIdFijo);
        agg.Estado.Should().Be(EstadoPresupuesto.Aprobado);
        agg.AprobadoEn.Should().Be(T1);
        agg.AprobadoPor.Should().Be("alice");
        agg.MontoTotal.Should().Be(new Dinero(1_000_000m, Moneda.COP));
        agg.SnapshotTasas.Should().NotBeNull();
        agg.SnapshotTasas.Should().BeEmpty();

        // Las propiedades preexistentes no se alteran por el fold de PresupuestoAprobado.
        agg.MonedaBase.Should().Be(Moneda.COP);
        agg.Codigo.Should().Be("OBRA-2026-01");
        agg.TenantId.Should().Be("acme");
        agg.Rubros.Should().HaveCount(1);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.11 Cálculo de MontoTotal ignora terminales con Monto.EsCero
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AprobarPresupuesto_con_un_terminal_en_cero_y_otro_con_monto_calcula_total_solo_con_los_positivos()
    {
        // Given:
        //  - R1 sin asignación → Monto = Dinero.Cero(COP) (no aporta y no bloquea PRE-2 — R2 sí cumple).
        //  - R2 con monto > 0.
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaiz(R1, "01"),
            RubroRaiz(R2, "02"),
            MontoAsignado(R2, new Dinero(750_000m, Moneda.COP)));

        // When
        var cmd = new AprobarPresupuesto(AprobadoPor: "alice");
        var evento = agg.AprobarPresupuesto(cmd, T1);

        // Then: solo R2 aporta — R1 con Monto.EsCero queda fuera de la suma.
        evento.MontoTotal.Should().Be(new Dinero(750_000m, Moneda.COP));
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.12 Defensivo — Agrupadores nunca aportan a MontoTotal.
    //        Aunque hoy el flujo garantiza que un Agrupador queda con Monto.EsCero
    //        (slice 04 §6.7 prohíbe asignarle), este test fija el contrato
    //        "siempre ignorar Agrupadores en la suma" usando la distinción operacional
    //        Agrupador/Terminal por presencia de hijos en _rubros (precedente slice 04 §6.7).
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AprobarPresupuesto_con_un_Agrupador_y_un_terminal_hijo_solo_suma_el_terminal()
    {
        // Given: R1 es Agrupador (tiene un hijo R2); R2 es terminal con monto > 0.
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaiz(R1, "01", "Costos Directos"),
            RubroHijo(R2, padreId: R1, codigo: "01.01", nombre: "Materiales"),
            MontoAsignado(R2, new Dinero(400_000m, Moneda.COP)));

        // When
        var cmd = new AprobarPresupuesto(AprobadoPor: "alice");
        var evento = agg.AprobarPresupuesto(cmd, T1);

        // Then: R1 (Agrupador) no aporta — solo R2 entra al cálculo.
        evento.MontoTotal.Should().Be(new Dinero(400_000m, Moneda.COP));
    }
}
