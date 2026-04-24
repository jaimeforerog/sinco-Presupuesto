using FluentAssertions;
using SincoPresupuesto.Domain.Presupuestos;
using SincoPresupuesto.Domain.Presupuestos.Commands;
using SincoPresupuesto.Domain.Presupuestos.Events;
using SincoPresupuesto.Domain.SharedKernel;
using SincoPresupuesto.Domain.Tests.TestKit;
using Xunit;

namespace SincoPresupuesto.Domain.Tests.Slices;

/// <summary>
/// Slice 01 — CrearPresupuesto.
/// Spec: slices/01-crear-presupuesto/spec.md §6.
/// Estilo: Given/When/Then sobre eventos. Given vacío porque el comando crea el stream.
/// Las excepciones se aserta por tipo (no por mensaje) para desacoplar del texto.
/// </summary>
public class Slice01_CrearPresupuestoTests
{
    // ── Fixtures de conveniencia ─────────────────────────────────────
    private static readonly Guid PresupuestoIdFijo =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly DateTimeOffset AhoraFijo =
        new(2026, 4, 24, 12, 0, 0, TimeSpan.Zero);

    private static CrearPresupuesto CmdValido(
        string tenantId = "acme",
        string codigo = "OBRA-2026-01",
        string nombre = "Torre Norte",
        DateOnly? periodoInicio = null,
        DateOnly? periodoFin = null,
        Moneda? monedaBase = null,
        int profundidadMaxima = 10,
        string creadoPor = "alice") =>
        new(
            TenantId: tenantId,
            Codigo: codigo,
            Nombre: nombre,
            PeriodoInicio: periodoInicio ?? new DateOnly(2026, 1, 1),
            PeriodoFin: periodoFin ?? new DateOnly(2026, 12, 31),
            MonedaBase: monedaBase ?? Moneda.COP,
            ProfundidadMaxima: profundidadMaxima,
            CreadoPor: creadoPor);

    // ═══════════════════════════════════════════════════════════════
    // §6.1 Happy path
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void CrearPresupuesto_sobre_stream_vacio_emite_PresupuestoCreado_con_todos_los_campos()
    {
        // Given: stream vacío — nada que aplicar.

        // When
        var evento = Presupuesto.Crear(CmdValido(), PresupuestoIdFijo, AhoraFijo);

        // Then
        evento.PresupuestoId.Should().Be(PresupuestoIdFijo);
        evento.TenantId.Should().Be("acme");
        evento.Codigo.Should().Be("OBRA-2026-01");
        evento.Nombre.Should().Be("Torre Norte");
        evento.PeriodoInicio.Should().Be(new DateOnly(2026, 1, 1));
        evento.PeriodoFin.Should().Be(new DateOnly(2026, 12, 31));
        evento.MonedaBase.Should().Be(Moneda.COP);
        evento.ProfundidadMaxima.Should().Be(10);
        evento.CreadoEn.Should().Be(AhoraFijo);
        evento.CreadoPor.Should().Be("alice");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.2 CreadoPor vacío → default "sistema"
    // ═══════════════════════════════════════════════════════════════
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CrearPresupuesto_con_CreadoPor_vacio_usa_sistema_como_default(string creadoPor)
    {
        // Given: stream vacío.

        // When
        var evento = Presupuesto.Crear(
            CmdValido(creadoPor: creadoPor),
            PresupuestoIdFijo,
            AhoraFijo);

        // Then
        evento.CreadoPor.Should().Be("sistema");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.3 PRE-1: TenantId vacío
    // ═══════════════════════════════════════════════════════════════
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CrearPresupuesto_con_TenantId_vacio_lanza_CampoRequerido(string tenantId)
    {
        // Given: stream vacío.
        var cmd = CmdValido(tenantId: tenantId);

        // When
        var act = () => Presupuesto.Crear(cmd, PresupuestoIdFijo, AhoraFijo);

        // Then
        act.Should().Throw<CampoRequeridoException>()
           .Which.NombreCampo.Should().Be("TenantId");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.4 PRE-2: Codigo vacío
    // ═══════════════════════════════════════════════════════════════
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CrearPresupuesto_con_Codigo_vacio_lanza_CampoRequerido(string codigo)
    {
        // Given: stream vacío.
        var cmd = CmdValido(codigo: codigo);

        // When
        var act = () => Presupuesto.Crear(cmd, PresupuestoIdFijo, AhoraFijo);

        // Then
        act.Should().Throw<CampoRequeridoException>()
           .Which.NombreCampo.Should().Be("Codigo");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.5 PRE-3: Nombre vacío
    // ═══════════════════════════════════════════════════════════════
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CrearPresupuesto_con_Nombre_vacio_lanza_CampoRequerido(string nombre)
    {
        // Given: stream vacío.
        var cmd = CmdValido(nombre: nombre);

        // When
        var act = () => Presupuesto.Crear(cmd, PresupuestoIdFijo, AhoraFijo);

        // Then
        act.Should().Throw<CampoRequeridoException>()
           .Which.NombreCampo.Should().Be("Nombre");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.6 PRE-4: periodo invertido
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void CrearPresupuesto_con_PeriodoFin_anterior_a_PeriodoInicio_lanza_PeriodoInvalido()
    {
        // Given: stream vacío.
        var inicio = new DateOnly(2026, 12, 31);
        var fin = new DateOnly(2026, 1, 1);
        var cmd = CmdValido(periodoInicio: inicio, periodoFin: fin);

        // When
        var act = () => Presupuesto.Crear(cmd, PresupuestoIdFijo, AhoraFijo);

        // Then
        act.Should().Throw<PeriodoInvalidoException>()
           .Which.Should().Match<PeriodoInvalidoException>(ex =>
               ex.PeriodoInicio == inicio && ex.PeriodoFin == fin);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.7 PRE-5: profundidad fuera de rango
    // ═══════════════════════════════════════════════════════════════
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(99)]
    public void CrearPresupuesto_con_ProfundidadMaxima_fuera_de_rango_lanza_ProfundidadMaximaFueraDeRango(
        int profundidad)
    {
        // Given: stream vacío.
        var cmd = CmdValido(profundidadMaxima: profundidad);

        // When
        var act = () => Presupuesto.Crear(cmd, PresupuestoIdFijo, AhoraFijo);

        // Then
        act.Should().Throw<ProfundidadMaximaFueraDeRangoException>()
           .Which.Should().Match<ProfundidadMaximaFueraDeRangoException>(ex =>
               ex.Valor == profundidad &&
               ex.MinimoInclusivo == 1 &&
               ex.MaximoInclusivo == Presupuesto.ProfundidadMaximaAbsoluta);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.8 Normalización de espacios
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void CrearPresupuesto_con_Codigo_y_Nombre_con_espacios_emite_evento_con_trim_aplicado()
    {
        // Given: stream vacío.
        var cmd = CmdValido(codigo: "  OBRA-2026-01  ", nombre: "  Torre Norte  ");

        // When
        var evento = Presupuesto.Crear(cmd, PresupuestoIdFijo, AhoraFijo);

        // Then
        evento.Codigo.Should().Be("OBRA-2026-01");
        evento.Nombre.Should().Be("Torre Norte");
    }

    // ═══════════════════════════════════════════════════════════════
    // Fold — complementario a INV-7 (MonedaBase queda fijada tras crear)
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void Fold_de_PresupuestoCreado_deja_el_agregado_en_Borrador_con_MonedaBase_fijada()
    {
        // Given: el evento producido por Create.
        var evento = Presupuesto.Crear(CmdValido(), PresupuestoIdFijo, AhoraFijo);

        // When: reconstruir el agregado aplicando el evento.
        var agg = AggregateBehavior<Presupuesto>.Reconstruir(evento);

        // Then
        agg.Id.Should().Be(PresupuestoIdFijo);
        agg.Estado.Should().Be(EstadoPresupuesto.Borrador);
        agg.MonedaBase.Should().Be(Moneda.COP);
        agg.ProfundidadMaxima.Should().Be(10);
    }
}
