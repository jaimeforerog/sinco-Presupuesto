using FluentAssertions;
using SincoPresupuesto.Domain.CatalogosDeTasas;
using SincoPresupuesto.Domain.CatalogosDeTasas.Commands;
using SincoPresupuesto.Domain.CatalogosDeTasas.Events;
using SincoPresupuesto.Domain.SharedKernel;
using SincoPresupuesto.Domain.Tests.TestKit;
using Xunit;

namespace SincoPresupuesto.Domain.Tests.Slices;

/// <summary>
/// Slice 06 — RegistrarTasaDeCambio.
/// Spec: slices/06-tasa-de-cambio-registrada/spec.md §6 (firmada 2026-04-24, 11 escenarios).
/// Estilo: Given/When/Then sobre eventos. El agregado <see cref="CatalogoDeTasas"/> es
/// singleton por tenant (mismo patrón que ConfiguracionTenant — slice 02): el camino
/// "stream vacío" invoca la fábrica estática <c>Crear</c>; el camino "stream existente"
/// invoca el método de instancia <c>Ejecutar</c> sobre el agregado reconstruido por fold.
///
/// Las excepciones se aserta por tipo + propiedades estructurales (no por mensaje), de
/// acuerdo a INV-SK-4 / INV-SK-5 del slice 00.
/// </summary>
public class Slice06_RegistrarTasaDeCambioTests
{
    // ── Fixtures de conveniencia ─────────────────────────────────────
    private static readonly DateTimeOffset T0 =
        new(2026, 4, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset T1 =
        new(2026, 4, 24, 13, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset T2 =
        new(2026, 4, 24, 14, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly Hoy =
        new(2026, 4, 24);

    // ═══════════════════════════════════════════════════════════════
    // §6.1 Happy path — primer registro del catálogo (stream vacío)
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void RegistrarTasaDeCambio_sobre_stream_vacio_emite_TasaDeCambioRegistrada()
    {
        // Given: stream vacío.

        // When
        var cmd = new RegistrarTasaDeCambio(
            MonedaDesde: Moneda.USD,
            MonedaHacia: Moneda.COP,
            Tasa: 4200m,
            Fecha: Hoy,
            Fuente: "BanRep",
            RegistradoPor: "admin-alice");

        var evento = CatalogoDeTasas.Crear(cmd, T0);

        // Then
        evento.Should().BeOfType<TasaDeCambioRegistrada>();
        evento.MonedaDesde.Should().Be(Moneda.USD);
        evento.MonedaHacia.Should().Be(Moneda.COP);
        evento.Tasa.Should().Be(4200m);
        evento.Fecha.Should().Be(Hoy);
        evento.Fuente.Should().Be("BanRep");
        evento.RegistradaEn.Should().Be(T0);
        evento.RegistradaPor.Should().Be("admin-alice");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.2 Happy path — acumulación: par/fecha distintos sobre stream existente
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void RegistrarTasaDeCambio_sobre_stream_existente_con_par_distinto_emite_segundo_evento()
    {
        // Given: stream con un evento previo.
        var eventoPrevio = new TasaDeCambioRegistrada(
            MonedaDesde: Moneda.USD,
            MonedaHacia: Moneda.COP,
            Tasa: 4170.50m,
            Fecha: Hoy,
            Fuente: "BanRep",
            RegistradaEn: T0,
            RegistradaPor: "admin-alice");

        var agg = AggregateBehavior<CatalogoDeTasas>.Reconstruir(eventoPrevio);

        // When: par distinto, mismo día.
        var cmd = new RegistrarTasaDeCambio(
            MonedaDesde: Moneda.EUR,
            MonedaHacia: Moneda.COP,
            Tasa: 4520.75m,
            Fecha: Hoy,
            Fuente: "BanRep",
            RegistradoPor: "admin-alice");

        var evento = agg.Ejecutar(cmd, T1);

        // Then
        evento.Should().BeOfType<TasaDeCambioRegistrada>();
        evento.MonedaDesde.Should().Be(Moneda.EUR);
        evento.MonedaHacia.Should().Be(Moneda.COP);
        evento.Tasa.Should().Be(4520.75m);
        evento.Fecha.Should().Be(Hoy);
        evento.Fuente.Should().Be("BanRep");
        evento.RegistradaEn.Should().Be(T1);
        evento.RegistradaPor.Should().Be("admin-alice");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.3 Last-write-wins — re-registración del mismo par/fecha (INV-CT-1)
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void RegistrarTasaDeCambio_re_registra_mismo_par_y_fecha_emite_segundo_evento()
    {
        // Given: stream con USD→COP @ 2026-04-24, tasa 4200.
        var eventoPrevio = new TasaDeCambioRegistrada(
            MonedaDesde: Moneda.USD,
            MonedaHacia: Moneda.COP,
            Tasa: 4200m,
            Fecha: Hoy,
            Fuente: "BanRep",
            RegistradaEn: T0,
            RegistradaPor: "admin-alice");

        var agg = AggregateBehavior<CatalogoDeTasas>.Reconstruir(eventoPrevio);

        // When: re-registración del mismo par/fecha con tasa distinta (corrección).
        var cmd = new RegistrarTasaDeCambio(
            MonedaDesde: Moneda.USD,
            MonedaHacia: Moneda.COP,
            Tasa: 4250m,
            Fecha: Hoy,
            Fuente: "manual-correccion",
            RegistradoPor: "admin-alice");

        var evento = agg.Ejecutar(cmd, T1);

        // Then: el agregado emite el segundo evento sin lanzar (last-write-wins).
        evento.Should().BeOfType<TasaDeCambioRegistrada>();
        evento.MonedaDesde.Should().Be(Moneda.USD);
        evento.MonedaHacia.Should().Be(Moneda.COP);
        evento.Tasa.Should().Be(4250m);
        evento.Fecha.Should().Be(Hoy);
        evento.Fuente.Should().Be("manual-correccion");
        evento.RegistradaEn.Should().Be(T1);
        evento.RegistradaPor.Should().Be("admin-alice");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.4 Normalización — RegistradoPor vacío / whitespace / tab → "sistema"
    // ═══════════════════════════════════════════════════════════════
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void RegistrarTasaDeCambio_con_RegistradoPor_vacio_o_whitespace_usa_sistema_como_default(
        string registradoPor)
    {
        // Given: stream vacío.

        // When
        var cmd = new RegistrarTasaDeCambio(
            MonedaDesde: Moneda.USD,
            MonedaHacia: Moneda.COP,
            Tasa: 4200m,
            Fecha: Hoy,
            Fuente: "BanRep",
            RegistradoPor: registradoPor);

        var evento = CatalogoDeTasas.Crear(cmd, T0);

        // Then
        evento.RegistradaPor.Should().Be("sistema");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.5 Normalización — Fuente null / vacío / whitespace → null
    // ═══════════════════════════════════════════════════════════════
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RegistrarTasaDeCambio_con_Fuente_null_o_whitespace_emite_evento_con_Fuente_null(
        string? fuente)
    {
        // Given: stream vacío.

        // When
        var cmd = new RegistrarTasaDeCambio(
            MonedaDesde: Moneda.USD,
            MonedaHacia: Moneda.COP,
            Tasa: 4200m,
            Fecha: Hoy,
            Fuente: fuente,
            RegistradoPor: "admin-alice");

        var evento = CatalogoDeTasas.Crear(cmd, T0);

        // Then: la Fuente queda como null (preserva la semántica del opcional).
        evento.Fuente.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.6 PRE-1 stream vacío — MonedaDesde == MonedaHacia
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void RegistrarTasaDeCambio_con_monedas_iguales_sobre_stream_vacio_lanza_MonedasIgualesEnTasa()
    {
        // Given: stream vacío.
        var cmd = new RegistrarTasaDeCambio(
            MonedaDesde: Moneda.USD,
            MonedaHacia: Moneda.USD,
            Tasa: 1m,
            Fecha: Hoy,
            Fuente: null,
            RegistradoPor: "admin-alice");

        // When
        var act = () => CatalogoDeTasas.Crear(cmd, T0);

        // Then
        act.Should().Throw<MonedasIgualesEnTasaException>()
           .Which.Moneda.Should().Be(Moneda.USD);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.7 PRE-2 — Tasa <= 0 (theory: 0m, -1m, -0.0001m)
    // ═══════════════════════════════════════════════════════════════
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.0001)]
    public void RegistrarTasaDeCambio_con_tasa_no_positiva_lanza_TasaDeCambioInvalida(double tasaDouble)
    {
        // Given: stream vacío. (decimal vía double para inline data.)
        var tasa = (decimal)tasaDouble;

        var cmd = new RegistrarTasaDeCambio(
            MonedaDesde: Moneda.USD,
            MonedaHacia: Moneda.COP,
            Tasa: tasa,
            Fecha: Hoy,
            Fuente: null,
            RegistradoPor: "admin-alice");

        // When
        var act = () => CatalogoDeTasas.Crear(cmd, T0);

        // Then
        act.Should().Throw<TasaDeCambioInvalidaException>()
           .Which.TasaIntentada.Should().Be(tasa);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.8 PRE-3 — Fecha en el futuro
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void RegistrarTasaDeCambio_con_fecha_futura_lanza_FechaDeTasaEnElFuturo()
    {
        // Given: stream vacío. ahora = T0 (cuyo .Date == 2026-04-24).
        var fechaFutura = Hoy.AddDays(1);

        var cmd = new RegistrarTasaDeCambio(
            MonedaDesde: Moneda.USD,
            MonedaHacia: Moneda.COP,
            Tasa: 4200m,
            Fecha: fechaFutura,
            Fuente: null,
            RegistradoPor: "admin-alice");

        // When
        var act = () => CatalogoDeTasas.Crear(cmd, T0);

        // Then
        act.Should().Throw<FechaDeTasaEnElFuturoException>()
           .Which.Should().Match<FechaDeTasaEnElFuturoException>(ex =>
               ex.Fecha == fechaFutura && ex.Hoy == Hoy);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.9 Caso límite — Fecha == hoy se acepta (PRE-3 inclusivo)
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void RegistrarTasaDeCambio_con_fecha_igual_a_hoy_emite_evento()
    {
        // Given: stream vacío. ahora = T0 (cuyo .Date == Hoy).

        // When
        var cmd = new RegistrarTasaDeCambio(
            MonedaDesde: Moneda.USD,
            MonedaHacia: Moneda.COP,
            Tasa: 4200m,
            Fecha: Hoy,
            Fuente: "BanRep",
            RegistradoPor: "admin-alice");

        var evento = CatalogoDeTasas.Crear(cmd, T0);

        // Then: la condición es Fecha <= ahora.Date (inclusiva).
        evento.Should().BeOfType<TasaDeCambioRegistrada>();
        evento.Fecha.Should().Be(Hoy);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.10 PRE-1 desde stream existente (camino de instancia)
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void RegistrarTasaDeCambio_con_monedas_iguales_sobre_stream_existente_lanza_MonedasIgualesEnTasa()
    {
        // Given: stream con un evento previo válido.
        var eventoPrevio = new TasaDeCambioRegistrada(
            MonedaDesde: Moneda.USD,
            MonedaHacia: Moneda.COP,
            Tasa: 4200m,
            Fecha: Hoy,
            Fuente: "BanRep",
            RegistradaEn: T0,
            RegistradaPor: "admin-alice");

        var agg = AggregateBehavior<CatalogoDeTasas>.Reconstruir(eventoPrevio);

        // When: comando con monedas iguales (EUR→EUR).
        var cmd = new RegistrarTasaDeCambio(
            MonedaDesde: Moneda.EUR,
            MonedaHacia: Moneda.EUR,
            Tasa: 1m,
            Fecha: Hoy,
            Fuente: null,
            RegistradoPor: "admin-alice");

        var act = () => agg.Ejecutar(cmd, T1);

        // Then: las precondiciones se aplican idéntico en Crear y Ejecutar.
        act.Should().Throw<MonedasIgualesEnTasaException>()
           .Which.Moneda.Should().Be(Moneda.EUR);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.11 Fold — CatalogoDeTasas refleja el historial completo
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void Fold_de_TasaDeCambioRegistrada_deja_el_agregado_con_historial_en_orden()
    {
        // Given: tres eventos en orden cronológico (con un par USD→COP repetido en fechas distintas).
        var ayer = Hoy.AddDays(-1);

        var e1 = new TasaDeCambioRegistrada(
            MonedaDesde: Moneda.USD,
            MonedaHacia: Moneda.COP,
            Tasa: 4170.50m,
            Fecha: ayer,
            Fuente: "BanRep",
            RegistradaEn: T0,
            RegistradaPor: "admin-alice");

        var e2 = new TasaDeCambioRegistrada(
            MonedaDesde: Moneda.EUR,
            MonedaHacia: Moneda.COP,
            Tasa: 4520m,
            Fecha: ayer,
            Fuente: "BanRep",
            RegistradaEn: T1,
            RegistradaPor: "admin-alice");

        var e3 = new TasaDeCambioRegistrada(
            MonedaDesde: Moneda.USD,
            MonedaHacia: Moneda.COP,
            Tasa: 4175m,
            Fecha: Hoy,
            Fuente: "BanRep",
            RegistradaEn: T2,
            RegistradaPor: "admin-alice");

        // When: reconstruir el agregado aplicando los tres eventos.
        var agg = AggregateBehavior<CatalogoDeTasas>.Reconstruir(e1, e2, e3);

        // Then
        agg.Registros.Should().HaveCount(3);

        agg.Registros[0].Should().BeEquivalentTo(new RegistroDeTasa(
            MonedaDesde: Moneda.USD,
            MonedaHacia: Moneda.COP,
            Tasa: 4170.50m,
            Fecha: ayer,
            Fuente: "BanRep",
            RegistradaEn: T0,
            RegistradaPor: "admin-alice"));

        agg.Registros[1].Should().BeEquivalentTo(new RegistroDeTasa(
            MonedaDesde: Moneda.EUR,
            MonedaHacia: Moneda.COP,
            Tasa: 4520m,
            Fecha: ayer,
            Fuente: "BanRep",
            RegistradaEn: T1,
            RegistradaPor: "admin-alice"));

        agg.Registros[2].Should().BeEquivalentTo(new RegistroDeTasa(
            MonedaDesde: Moneda.USD,
            MonedaHacia: Moneda.COP,
            Tasa: 4175m,
            Fecha: Hoy,
            Fuente: "BanRep",
            RegistradaEn: T2,
            RegistradaPor: "admin-alice"));
    }
}
