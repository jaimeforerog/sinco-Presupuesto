using FluentAssertions;
using SincoPresupuesto.Domain.Presupuestos;
using SincoPresupuesto.Domain.Presupuestos.Commands;
using SincoPresupuesto.Domain.Presupuestos.Events;
using SincoPresupuesto.Domain.SharedKernel;
using SincoPresupuesto.Domain.Tests.TestKit;
using Xunit;

namespace SincoPresupuesto.Domain.Tests.Slices;

/// <summary>
/// Slice 04 — AsignarMontoARubro.
/// Spec: slices/04-asignar-monto-a-rubro/spec.md §6 (firmada 2026-04-24, Q1=(d)).
/// Estilo: Given/When/Then sobre eventos. El agregado Presupuesto se reconstruye por fold
/// desde un <see cref="PresupuestoCreado"/> + <see cref="RubroAgregado"/> (y, cuando aplica,
/// un <see cref="MontoAsignadoARubro"/> previo) y luego se invoca el método de instancia
/// <c>AsignarMontoARubro</c>. Las excepciones se aserta por tipo + propiedades (no mensaje).
///
/// Nota: §6.8 (INV-3 — estado no Borrador) se difiere al slice AprobarPresupuesto
/// (followup #13). Aquí sólo se ejercita el camino Borrador-no-lanza (test de sanidad,
/// mismo patrón que slice 03 §6.7).
/// </summary>
public class Slice04_AsignarMontoARubroTests
{
    // ── Fixtures de conveniencia ─────────────────────────────────────
    private static readonly DateTimeOffset AhoraFijo =
        new(2026, 4, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset T2 =
        new(2026, 4, 24, 13, 0, 0, TimeSpan.Zero);

    private static readonly Guid PresupuestoIdFijo =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid R1 =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid R2 =
        Guid.Parse("33333333-3333-3333-3333-333333333333");

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
            CreadoEn: AhoraFijo,
            CreadoPor: "alice");

    private static RubroAgregado RubroRaizAgregado(Guid rubroId, string codigo = "01", string nombre = "Costos Directos") =>
        new(
            PresupuestoId: PresupuestoIdFijo,
            RubroId: rubroId,
            Codigo: codigo,
            Nombre: nombre,
            RubroPadreId: null,
            AgregadoEn: AhoraFijo);

    private static RubroAgregado RubroHijoAgregado(Guid rubroId, Guid padreId, string codigo = "01.01", string nombre = "Hijo") =>
        new(
            PresupuestoId: PresupuestoIdFijo,
            RubroId: rubroId,
            Codigo: codigo,
            Nombre: nombre,
            RubroPadreId: padreId,
            AgregadoEn: AhoraFijo);

    // ═══════════════════════════════════════════════════════════════
    // §6.1 Happy path — primera asignación a rubro Terminal (MonedaBase = COP)
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AsignarMontoARubro_primera_asignacion_a_rubro_terminal_emite_MontoAsignadoARubro_con_MontoAnterior_cero()
    {
        // Given
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaizAgregado(R1));

        // When
        var cmd = new AsignarMontoARubro(
            RubroId: R1,
            Monto: new Dinero(1_000_000m, Moneda.COP),
            AsignadoPor: "alice");
        var evento = agg.AsignarMontoARubro(cmd, AhoraFijo);

        // Then
        evento.Should().BeOfType<MontoAsignadoARubro>();
        evento.PresupuestoId.Should().Be(PresupuestoIdFijo);
        evento.RubroId.Should().Be(R1);
        evento.Monto.Should().Be(new Dinero(1_000_000m, Moneda.COP));
        evento.MontoAnterior.Should().Be(Dinero.Cero(Moneda.COP));
        evento.AsignadoEn.Should().Be(AhoraFijo);
        evento.AsignadoPor.Should().Be("alice");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.2 Happy path — reasignación en la misma moneda
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AsignarMontoARubro_reasignacion_misma_moneda_emite_evento_con_MontoAnterior_igual_al_monto_previo()
    {
        // Given: rubro con una asignación previa de 1.000.000 COP.
        var asignacionPrevia = new MontoAsignadoARubro(
            PresupuestoId: PresupuestoIdFijo,
            RubroId: R1,
            Monto: new Dinero(1_000_000m, Moneda.COP),
            MontoAnterior: Dinero.Cero(Moneda.COP),
            AsignadoEn: AhoraFijo,
            AsignadoPor: "alice");

        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaizAgregado(R1),
            asignacionPrevia);

        // When
        var cmd = new AsignarMontoARubro(
            RubroId: R1,
            Monto: new Dinero(2_500_000m, Moneda.COP),
            AsignadoPor: "alice");
        var evento = agg.AsignarMontoARubro(cmd, T2);

        // Then
        evento.Monto.Should().Be(new Dinero(2_500_000m, Moneda.COP));
        evento.MontoAnterior.Should().Be(new Dinero(1_000_000m, Moneda.COP));
        evento.AsignadoEn.Should().Be(T2);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.3 Happy path — reasignación cambiando moneda (COP → USD)
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AsignarMontoARubro_reasignacion_cambiando_moneda_emite_evento_con_MontoAnterior_en_moneda_previa()
    {
        // Given: rubro con asignación previa de 1.000.000 COP.
        var asignacionPrevia = new MontoAsignadoARubro(
            PresupuestoId: PresupuestoIdFijo,
            RubroId: R1,
            Monto: new Dinero(1_000_000m, Moneda.COP),
            MontoAnterior: Dinero.Cero(Moneda.COP),
            AsignadoEn: AhoraFijo,
            AsignadoPor: "alice");

        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaizAgregado(R1),
            asignacionPrevia);

        // When: se reasigna en USD.
        var cmd = new AsignarMontoARubro(
            RubroId: R1,
            Monto: new Dinero(250m, Moneda.USD),
            AsignadoPor: "alice");
        var evento = agg.AsignarMontoARubro(cmd, T2);

        // Then: el MontoAnterior queda en la moneda anterior (COP) — no se mezclan monedas.
        evento.Monto.Should().Be(new Dinero(250m, Moneda.USD));
        evento.MontoAnterior.Should().Be(new Dinero(1_000_000m, Moneda.COP));
        evento.AsignadoEn.Should().Be(T2);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.4 Happy path — moneda del monto ≠ MonedaBase desde la primera asignación
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AsignarMontoARubro_primera_asignacion_en_moneda_distinta_a_MonedaBase_se_permite()
    {
        // Given: presupuesto con MonedaBase=COP pero la primera asignación llega en USD.
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(monedaBase: Moneda.COP),
            RubroRaizAgregado(R1, codigo: "02", nombre: "Insumos Importados"));

        // When
        var cmd = new AsignarMontoARubro(
            RubroId: R1,
            Monto: new Dinero(5_000m, Moneda.USD),
            AsignadoPor: "alice");
        var evento = agg.AsignarMontoARubro(cmd, AhoraFijo);

        // Then
        evento.Monto.Should().Be(new Dinero(5_000m, Moneda.USD));
        // MontoAnterior en la moneda del comando (USD), no en MonedaBase (COP).
        evento.MontoAnterior.Should().Be(Dinero.Cero(Moneda.USD));
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.5 Happy path — monto = 0 es válido (INV-2: ≥ 0)
    //       y AsignadoPor="" normaliza a "sistema" (PRE-4).
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AsignarMontoARubro_con_monto_cero_y_AsignadoPor_vacio_emite_evento_con_AsignadoPor_sistema()
    {
        // Given
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaizAgregado(R1));

        // When
        var cmd = new AsignarMontoARubro(
            RubroId: R1,
            Monto: Dinero.Cero(Moneda.COP),
            AsignadoPor: "");
        var evento = agg.AsignarMontoARubro(cmd, AhoraFijo);

        // Then
        evento.Monto.Should().Be(Dinero.Cero(Moneda.COP));
        evento.MontoAnterior.Should().Be(Dinero.Cero(Moneda.COP));
        evento.AsignadoPor.Should().Be("sistema");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.6 Violación INV-2 — monto negativo
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AsignarMontoARubro_con_monto_negativo_lanza_MontoNegativoException()
    {
        // Given
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaizAgregado(R1));

        var montoIntentado = new Dinero(-1m, Moneda.COP);
        var cmd = new AsignarMontoARubro(RubroId: R1, Monto: montoIntentado);

        // When
        var act = () => agg.AsignarMontoARubro(cmd, AhoraFijo);

        // Then
        act.Should().Throw<MontoNegativoException>()
           .Which.MontoIntentado.Should().Be(montoIntentado);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.7 Violación INV-NEW-SLICE04-1 — el rubro es Agrupador (tiene hijos)
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AsignarMontoARubro_sobre_rubro_con_hijos_lanza_RubroEsAgrupadorException()
    {
        // Given: R1 tiene un hijo R2 → R1 es Agrupador.
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaizAgregado(R1, codigo: "01", nombre: "Padre"),
            RubroHijoAgregado(R2, padreId: R1, codigo: "01.01", nombre: "Hijo"));

        var cmd = new AsignarMontoARubro(
            RubroId: R1,
            Monto: new Dinero(100_000m, Moneda.COP));

        // When
        var act = () => agg.AsignarMontoARubro(cmd, AhoraFijo);

        // Then
        act.Should().Throw<RubroEsAgrupadorException>()
           .Which.RubroId.Should().Be(R1);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.8 Sanidad INV-3 — en Borrador NO lanza PresupuestoNoEsBorradorException.
    //       Escenario negativo (estado ≠ Borrador) diferido a slice AprobarPresupuesto
    //       (followup #13). Mismo patrón que slice 03 §6.7.
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AsignarMontoARubro_sobre_presupuesto_en_Borrador_no_lanza_PresupuestoNoEsBorrador()
    {
        // Given: presupuesto recién creado está en Borrador.
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaizAgregado(R1));

        var cmd = new AsignarMontoARubro(
            RubroId: R1,
            Monto: new Dinero(100m, Moneda.COP));

        // When
        var act = () => agg.AsignarMontoARubro(cmd, AhoraFijo);

        // Then
        act.Should().NotThrow<PresupuestoNoEsBorradorException>();
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.9 PRE-1 — RubroId = Guid.Empty
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AsignarMontoARubro_con_RubroId_vacio_lanza_CampoRequerido()
    {
        // Given
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaizAgregado(R1));

        var cmd = new AsignarMontoARubro(
            RubroId: Guid.Empty,
            Monto: new Dinero(100m, Moneda.COP));

        // When
        var act = () => agg.AsignarMontoARubro(cmd, AhoraFijo);

        // Then
        act.Should().Throw<CampoRequeridoException>()
           .Which.NombreCampo.Should().Be("RubroId");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.10 PRE-2 — el rubro destino no existe en el agregado
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AsignarMontoARubro_con_RubroId_inexistente_lanza_RubroNoExiste()
    {
        // Given: existe R1, pero el comando referencia otro Id.
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaizAgregado(R1));

        var rubroInexistente = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var cmd = new AsignarMontoARubro(
            RubroId: rubroInexistente,
            Monto: new Dinero(100m, Moneda.COP));

        // When
        var act = () => agg.AsignarMontoARubro(cmd, AhoraFijo);

        // Then
        act.Should().Throw<RubroNoExisteException>()
           .Which.RubroId.Should().Be(rubroInexistente);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.11 PRE-4 — normalización de AsignadoPor vacío/whitespace → "sistema".
    //        Se parametriza sobre "" y "   ". El caso null se omite por la firma
    //        del record: AsignadoPor es `string` (no nullable) con default "sistema",
    //        por lo que un `null` explícito crearía un warning nullable. La spec
    //        §6.11 indica "el null explícito es el caso de interés real" sólo si el
    //        compilador lo permite — aquí no aplica. §6.5 ya cubre "" con monto cero.
    // ═══════════════════════════════════════════════════════════════
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void AsignarMontoARubro_con_AsignadoPor_vacio_o_whitespace_normaliza_a_sistema(string asignadoPor)
    {
        // Given
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaizAgregado(R1));

        var cmd = new AsignarMontoARubro(
            RubroId: R1,
            Monto: new Dinero(100m, Moneda.COP),
            AsignadoPor: asignadoPor);

        // When
        var evento = agg.AsignarMontoARubro(cmd, AhoraFijo);

        // Then
        evento.AsignadoPor.Should().Be("sistema");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.12 Fold — primera asignación deja Rubro.Monto con el valor asignado
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void Fold_de_MontoAsignadoARubro_primera_asignacion_deja_el_rubro_con_el_Monto_asignado()
    {
        // Given
        var asignacion = new MontoAsignadoARubro(
            PresupuestoId: PresupuestoIdFijo,
            RubroId: R1,
            Monto: new Dinero(1_000_000m, Moneda.COP),
            MontoAnterior: Dinero.Cero(Moneda.COP),
            AsignadoEn: AhoraFijo,
            AsignadoPor: "alice");

        // When: reconstruir el agregado aplicando los tres eventos (fold).
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaizAgregado(R1, codigo: "01", nombre: "Costos Directos"),
            asignacion);

        // Then
        agg.Rubros.Should().HaveCount(1);
        var rubro = agg.Rubros[0];
        rubro.Id.Should().Be(R1);
        rubro.Codigo.Should().Be("01");
        rubro.Monto.Should().Be(new Dinero(1_000_000m, Moneda.COP));
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.13 Fold — reasignación cambiando moneda deja Rubro.Monto en la moneda nueva
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void Fold_de_MontoAsignadoARubro_reasignacion_cambiando_moneda_deja_el_rubro_con_la_moneda_nueva()
    {
        // Given: dos asignaciones sucesivas — primero COP, luego USD.
        var asignacionCop = new MontoAsignadoARubro(
            PresupuestoId: PresupuestoIdFijo,
            RubroId: R1,
            Monto: new Dinero(1_000_000m, Moneda.COP),
            MontoAnterior: Dinero.Cero(Moneda.COP),
            AsignadoEn: AhoraFijo,
            AsignadoPor: "alice");

        var asignacionUsd = new MontoAsignadoARubro(
            PresupuestoId: PresupuestoIdFijo,
            RubroId: R1,
            Monto: new Dinero(250m, Moneda.USD),
            MontoAnterior: new Dinero(1_000_000m, Moneda.COP),
            AsignadoEn: T2,
            AsignadoPor: "alice");

        // When
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaizAgregado(R1),
            asignacionCop,
            asignacionUsd);

        // Then: el rubro refleja la última asignación (moneda y valor nuevos).
        agg.Rubros.Should().HaveCount(1);
        var rubro = agg.Rubros[0];
        rubro.Id.Should().Be(R1);
        rubro.Monto.Should().Be(new Dinero(250m, Moneda.USD));
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.14 Confianza en VO Moneda — el dominio NO revalida ISO 4217.
    //        Documenta que construir el comando con `new Moneda("EUR")` no explota
    //        en el método. §6.1 ya ejerce el happy-path con Moneda.COP; aquí se
    //        cubre la ruta con otro código ISO construido ad-hoc desde string.
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AsignarMontoARubro_con_Moneda_construida_desde_string_ISO_4217_valida_emite_evento_sin_revalidar()
    {
        // Given
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            RubroRaizAgregado(R1));

        var cmd = new AsignarMontoARubro(
            RubroId: R1,
            Monto: new Dinero(42m, new Moneda("EUR")),
            AsignadoPor: "alice");

        // When
        var evento = agg.AsignarMontoARubro(cmd, AhoraFijo);

        // Then
        evento.Monto.Valor.Should().Be(42m);
        evento.Monto.Moneda.Codigo.Should().Be("EUR");
    }
}
