using FluentAssertions;
using SincoPresupuesto.Domain.Presupuestos;
using SincoPresupuesto.Domain.Presupuestos.Commands;
using SincoPresupuesto.Domain.Presupuestos.Events;
using SincoPresupuesto.Domain.SharedKernel;
using SincoPresupuesto.Domain.Tests.TestKit;
using Xunit;

namespace SincoPresupuesto.Domain.Tests.Slices;

/// <summary>
/// Slice 03 — AgregarRubro.
/// Spec: slices/03-agregar-rubro/spec.md §6.
/// Estilo: Given/When/Then sobre eventos. El agregado Presupuesto se reconstruye por fold
/// desde un <see cref="PresupuestoCreado"/> (más eventos previos cuando aplica) y luego se
/// invoca el método de instancia <c>AgregarRubro</c>.
/// Las excepciones se aserta por tipo + propiedades (no por mensaje).
/// Nota: §6.7 (INV-3 — estado no Borrador) está diferido al slice AprobarPresupuesto
/// por decisión §10 Q1 opción (a). Aquí solo se ejercita el camino Borrador-no-lanza.
/// </summary>
public class Slice03_AgregarRubroTests
{
    // ── Fixtures de conveniencia ─────────────────────────────────────
    private static readonly DateTimeOffset AhoraFijo =
        new(2026, 4, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid PresupuestoIdFijo =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid R1 =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid R2 =
        Guid.Parse("33333333-3333-3333-3333-333333333333");

    /// <summary>
    /// Evento base para los Given: presupuesto recién creado en Borrador.
    /// <paramref name="profundidadMaxima"/> por default 10; para §6.12 se baja a 2.
    /// </summary>
    private static PresupuestoCreado PresupuestoCreadoBase(int profundidadMaxima = 10) =>
        new(
            PresupuestoId: PresupuestoIdFijo,
            TenantId: "acme",
            Codigo: "OBRA-2026-01",
            Nombre: "Torre Norte",
            PeriodoInicio: new DateOnly(2026, 1, 1),
            PeriodoFin: new DateOnly(2026, 12, 31),
            MonedaBase: Moneda.COP,
            ProfundidadMaxima: profundidadMaxima,
            CreadoEn: AhoraFijo,
            CreadoPor: "alice");

    private static AgregarRubro CmdValido(
        string codigo = "01",
        string nombre = "Costos Directos",
        Guid? rubroPadreId = null) =>
        new(Codigo: codigo, Nombre: nombre, RubroPadreId: rubroPadreId);

    // ═══════════════════════════════════════════════════════════════
    // §6.1 Happy path — rubro raíz (sin padre)
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AgregarRubro_raiz_en_presupuesto_en_borrador_emite_RubroAgregado_con_todos_los_campos()
    {
        // Given
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(PresupuestoCreadoBase());

        // When
        var cmd = CmdValido(codigo: "01", nombre: "Costos Directos", rubroPadreId: null);
        var evento = agg.AgregarRubro(cmd, R1, AhoraFijo);

        // Then
        evento.Should().BeOfType<RubroAgregado>();
        evento.PresupuestoId.Should().Be(PresupuestoIdFijo);
        evento.RubroId.Should().Be(R1);
        evento.Codigo.Should().Be("01");
        evento.Nombre.Should().Be("Costos Directos");
        evento.RubroPadreId.Should().BeNull();
        evento.AgregadoEn.Should().Be(AhoraFijo);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.2 Happy path — rubro hijo extiende al padre
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AgregarRubro_hijo_que_extiende_al_padre_emite_RubroAgregado_con_RubroPadreId()
    {
        // Given: presupuesto con un rubro raíz "01" ya agregado.
        var eventoRaiz = new RubroAgregado(
            PresupuestoId: PresupuestoIdFijo,
            RubroId: R1,
            Codigo: "01",
            Nombre: "Costos Directos",
            RubroPadreId: null,
            AgregadoEn: AhoraFijo);

        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            eventoRaiz);

        // When
        var cmd = CmdValido(codigo: "01.01", nombre: "Materiales", rubroPadreId: R1);
        var evento = agg.AgregarRubro(cmd, R2, AhoraFijo);

        // Then
        evento.Should().BeOfType<RubroAgregado>();
        evento.PresupuestoId.Should().Be(PresupuestoIdFijo);
        evento.RubroId.Should().Be(R2);
        evento.Codigo.Should().Be("01.01");
        evento.Nombre.Should().Be("Materiales");
        evento.RubroPadreId.Should().Be(R1);
        evento.AgregadoEn.Should().Be(AhoraFijo);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.3 Normalización de espacios en Codigo y Nombre
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AgregarRubro_con_Codigo_y_Nombre_con_espacios_emite_evento_con_trim_aplicado()
    {
        // Given
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(PresupuestoCreadoBase());

        // When
        var cmd = CmdValido(codigo: "  01  ", nombre: "  Costos Directos  ", rubroPadreId: null);
        var evento = agg.AgregarRubro(cmd, R1, AhoraFijo);

        // Then
        evento.Codigo.Should().Be("01");
        evento.Nombre.Should().Be("Costos Directos");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.4 PRE-1: Codigo vacío
    // ═══════════════════════════════════════════════════════════════
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AgregarRubro_con_Codigo_vacio_lanza_CampoRequerido(string codigo)
    {
        // Given
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(PresupuestoCreadoBase());
        var cmd = CmdValido(codigo: codigo);

        // When
        var act = () => agg.AgregarRubro(cmd, R1, AhoraFijo);

        // Then
        act.Should().Throw<CampoRequeridoException>()
           .Which.NombreCampo.Should().Be("Codigo");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.5 PRE-2: Nombre vacío
    // ═══════════════════════════════════════════════════════════════
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AgregarRubro_con_Nombre_vacio_lanza_CampoRequerido(string nombre)
    {
        // Given
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(PresupuestoCreadoBase());
        var cmd = CmdValido(nombre: nombre);

        // When
        var act = () => agg.AgregarRubro(cmd, R1, AhoraFijo);

        // Then
        act.Should().Throw<CampoRequeridoException>()
           .Which.NombreCampo.Should().Be("Nombre");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.6 PRE-3: rubroId = Guid.Empty
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AgregarRubro_con_rubroId_vacio_lanza_CampoRequerido()
    {
        // Given
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(PresupuestoCreadoBase());
        var cmd = CmdValido();

        // When
        var act = () => agg.AgregarRubro(cmd, Guid.Empty, AhoraFijo);

        // Then
        act.Should().Throw<CampoRequeridoException>()
           .Which.NombreCampo.Should().Be("RubroId");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.7 (diferido) — test de sanidad: estado Borrador NO lanza
    // INV-3 — PresupuestoNoEsBorradorException se ejercita en slice
    // AprobarPresupuesto (followup #13). Aquí solo se verifica que el
    // camino "estado Borrador" no bloquea.
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AgregarRubro_sobre_presupuesto_en_Borrador_no_lanza_PresupuestoNoEsBorrador()
    {
        // Given: presupuesto recién creado está en Borrador.
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(PresupuestoCreadoBase());
        var cmd = CmdValido();

        // When
        var act = () => agg.AgregarRubro(cmd, R1, AhoraFijo);

        // Then: no se debe lanzar PresupuestoNoEsBorradorException (INV-3).
        act.Should().NotThrow<PresupuestoNoEsBorradorException>();
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.8 INV-10: formato de código inválido
    // ═══════════════════════════════════════════════════════════════
    [Theory]
    [InlineData("1")]
    [InlineData("1.1")]
    [InlineData("01.1")]
    [InlineData("01-01")]
    [InlineData("a1")]
    [InlineData("01.01.01.01.01.01.01.01.01.01.01.01.01.01.01.01")] // 16 niveles
    public void AgregarRubro_con_Codigo_formato_invalido_lanza_CodigoRubroInvalido(string codigo)
    {
        // Given
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(PresupuestoCreadoBase());
        var cmd = CmdValido(codigo: codigo);

        // When
        var act = () => agg.AgregarRubro(cmd, R1, AhoraFijo);

        // Then
        act.Should().Throw<CodigoRubroInvalidoException>()
           .Which.CodigoIntentado.Should().Be(codigo);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.9 INV-11: código duplicado dentro del presupuesto
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AgregarRubro_con_Codigo_duplicado_lanza_CodigoRubroDuplicado()
    {
        // Given: ya existe un rubro con código "01".
        var eventoRaiz = new RubroAgregado(
            PresupuestoId: PresupuestoIdFijo,
            RubroId: R1,
            Codigo: "01",
            Nombre: "Costos Directos",
            RubroPadreId: null,
            AgregadoEn: AhoraFijo);

        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            eventoRaiz);

        var cmd = CmdValido(codigo: "01", nombre: "Otro", rubroPadreId: null);

        // When
        var act = () => agg.AgregarRubro(cmd, R2, AhoraFijo);

        // Then
        act.Should().Throw<CodigoRubroDuplicadoException>()
           .Which.CodigoIntentado.Should().Be("01");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.10 INV-F: hijo no extiende al padre
    // ═══════════════════════════════════════════════════════════════
    [Theory]
    [InlineData("02.01")]       // no empieza por "01."
    [InlineData("01")]          // no añade segmento
    [InlineData("01.01.01")]    // añade dos segmentos
    [InlineData("011.01")]      // prefijo literal pero rompe boundary de segmento
    public void AgregarRubro_hijo_que_no_extiende_al_padre_lanza_CodigoHijoNoExtiendeAlPadre(string codigoHijo)
    {
        // Given: ya existe un rubro raíz "01" con Id R1.
        var eventoRaiz = new RubroAgregado(
            PresupuestoId: PresupuestoIdFijo,
            RubroId: R1,
            Codigo: "01",
            Nombre: "Costos Directos",
            RubroPadreId: null,
            AgregadoEn: AhoraFijo);

        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            eventoRaiz);

        var cmd = CmdValido(codigo: codigoHijo, nombre: "X", rubroPadreId: R1);

        // When
        var act = () => agg.AgregarRubro(cmd, R2, AhoraFijo);

        // Then
        act.Should().Throw<CodigoHijoNoExtiendeAlPadreException>()
           .Which.Should().Match<CodigoHijoNoExtiendeAlPadreException>(ex =>
               ex.CodigoPadre == "01" && ex.CodigoHijo == codigoHijo);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.11 INV-D: padre no existe en el presupuesto
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AgregarRubro_con_RubroPadreId_inexistente_lanza_RubroPadreNoExiste()
    {
        // Given: existe un rubro raíz con Id R1, pero el comando referencia otro Id.
        var eventoRaiz = new RubroAgregado(
            PresupuestoId: PresupuestoIdFijo,
            RubroId: R1,
            Codigo: "01",
            Nombre: "Costos Directos",
            RubroPadreId: null,
            AgregadoEn: AhoraFijo);

        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            eventoRaiz);

        var padreInexistente = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var cmd = CmdValido(codigo: "99.01", nombre: "X", rubroPadreId: padreInexistente);

        // When
        var act = () => agg.AgregarRubro(cmd, R2, AhoraFijo);

        // Then
        act.Should().Throw<RubroPadreNoExisteException>()
           .Which.RubroPadreId.Should().Be(padreInexistente);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.12 INV-8: profundidad excedida
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void AgregarRubro_que_excede_ProfundidadMaxima_lanza_ProfundidadExcedida()
    {
        // Given: presupuesto con ProfundidadMaxima=2, rubro nivel 1 y nivel 2 ya agregados.
        var eventoNivel1 = new RubroAgregado(
            PresupuestoId: PresupuestoIdFijo,
            RubroId: R1,
            Codigo: "01",
            Nombre: "Nivel 1",
            RubroPadreId: null,
            AgregadoEn: AhoraFijo);

        var eventoNivel2 = new RubroAgregado(
            PresupuestoId: PresupuestoIdFijo,
            RubroId: R2,
            Codigo: "01.01",
            Nombre: "Nivel 2",
            RubroPadreId: R1,
            AgregadoEn: AhoraFijo);

        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(profundidadMaxima: 2),
            eventoNivel1,
            eventoNivel2);

        var rubroIdNivel3 = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var cmd = CmdValido(codigo: "01.01.01", nombre: "Nivel 3", rubroPadreId: R2);

        // When
        var act = () => agg.AgregarRubro(cmd, rubroIdNivel3, AhoraFijo);

        // Then
        act.Should().Throw<ProfundidadExcedidaException>()
           .Which.Should().Match<ProfundidadExcedidaException>(ex =>
               ex.ProfundidadMaxima == 2 && ex.NivelIntentado == 3);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.13 Fold — Presupuesto refleja el rubro agregado
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void Fold_de_RubroAgregado_deja_el_agregado_con_el_rubro_raiz_registrado()
    {
        // Given: evento creado + evento del rubro raíz (del §6.1).
        var eventoRaiz = new RubroAgregado(
            PresupuestoId: PresupuestoIdFijo,
            RubroId: R1,
            Codigo: "01",
            Nombre: "Costos Directos",
            RubroPadreId: null,
            AgregadoEn: AhoraFijo);

        // When: reconstruir el agregado aplicando los dos eventos.
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(
            PresupuestoCreadoBase(),
            eventoRaiz);

        // Then
        agg.Id.Should().Be(PresupuestoIdFijo);
        agg.Estado.Should().Be(EstadoPresupuesto.Borrador);

        // La colección interna de rubros se expone como propiedad pública de solo lectura
        // (nombre exacto a criterio de green — los tests referencian .Rubros). Se espera
        // que contenga exactamente un rubro con los campos del evento.
        var rubros = (System.Collections.IEnumerable)agg.GetType()
            .GetProperty("Rubros")!
            .GetValue(agg)!;

        var lista = rubros.Cast<object>().ToList();
        lista.Should().HaveCount(1);

        var rubro = lista[0];
        var tipo = rubro.GetType();
        tipo.GetProperty("Id")!.GetValue(rubro).Should().Be(R1);
        tipo.GetProperty("Codigo")!.GetValue(rubro).Should().Be("01");
        tipo.GetProperty("Nombre")!.GetValue(rubro).Should().Be("Costos Directos");
        tipo.GetProperty("PadreId")!.GetValue(rubro).Should().BeNull();
        tipo.GetProperty("Nivel")!.GetValue(rubro).Should().Be(1);
    }
}
