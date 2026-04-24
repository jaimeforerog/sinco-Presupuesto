using FluentAssertions;
using SincoPresupuesto.Domain.ConfiguracionesTenant;
using SincoPresupuesto.Domain.ConfiguracionesTenant.Commands;
using SincoPresupuesto.Domain.ConfiguracionesTenant.Events;
using SincoPresupuesto.Domain.SharedKernel;
using SincoPresupuesto.Domain.Tests.TestKit;
using Xunit;

namespace SincoPresupuesto.Domain.Tests.Slices;

/// <summary>
/// Slice 02 — ConfigurarMonedaLocalDelTenant.
/// Spec: slices/02-configurar-moneda-local-del-tenant/spec.md §6.
/// Estilo: Given/When/Then sobre eventos.
/// El agregado ConfiguracionTenant vive en un stream con StreamId = TenantId (uno-a-uno).
/// Las excepciones se aserta por tipo (no por mensaje) para desacoplar del texto.
/// </summary>
public class Slice02_ConfigurarMonedaLocalDelTenantTests
{
    // ── Fixtures de conveniencia ─────────────────────────────────────
    private static readonly DateTimeOffset AhoraFijo =
        new(2026, 4, 24, 12, 0, 0, TimeSpan.Zero);

    private static ConfigurarMonedaLocalDelTenant CmdValido(
        string tenantId = "acme",
        Moneda? monedaLocal = null,
        string configuradoPor = "admin-alice") =>
        new(
            TenantId: tenantId,
            MonedaLocal: monedaLocal ?? Moneda.COP,
            ConfiguradoPor: configuradoPor);

    // ═══════════════════════════════════════════════════════════════
    // §6.1 Happy path
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void ConfigurarMonedaLocalDelTenant_sobre_stream_vacio_emite_MonedaLocalDelTenantConfigurada()
    {
        // Given: stream vacío — no hay configuración previa.

        // When
        var cmd = CmdValido();
        var evento = ConfiguracionTenant.Crear(cmd, AhoraFijo);

        // Then
        evento.Should().BeOfType<MonedaLocalDelTenantConfigurada>();
        evento.TenantId.Should().Be("acme");
        evento.Moneda.Should().Be(Moneda.COP);
        evento.ConfiguradaEn.Should().Be(AhoraFijo);
        evento.ConfiguradaPor.Should().Be("admin-alice");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.2 ConfiguradoPor vacío → default "sistema"
    // ═══════════════════════════════════════════════════════════════
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ConfigurarMonedaLocalDelTenant_con_ConfiguradoPor_vacio_usa_sistema_como_default(
        string configuradoPor)
    {
        // Given: stream vacío.

        // When
        var cmd = CmdValido(configuradoPor: configuradoPor);
        var evento = ConfiguracionTenant.Crear(cmd, AhoraFijo);

        // Then
        evento.ConfiguradaPor.Should().Be("sistema");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.3 PRE-1: TenantId vacío
    // ═══════════════════════════════════════════════════════════════
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ConfigurarMonedaLocalDelTenant_con_TenantId_vacio_lanza_CampoRequerido(string tenantId)
    {
        // Given: stream vacío.
        var cmd = CmdValido(tenantId: tenantId);

        // When
        var act = () => ConfiguracionTenant.Crear(cmd, AhoraFijo);

        // Then
        act.Should().Throw<CampoRequeridoException>()
           .Which.NombreCampo.Should().Be("TenantId");
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.4 INV-NEW-1: Tenant ya configurado
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void ConfigurarMonedaLocalDelTenant_sobre_stream_existente_lanza_TenantYaConfigurado()
    {
        // Given: evento previo que ya configuró el tenant.
        var eventoAnterior = new MonedaLocalDelTenantConfigurada(
            TenantId: "acme",
            Moneda: Moneda.COP,
            ConfiguradaEn: AhoraFijo,
            ConfiguradaPor: "sistema");

        var agg = AggregateBehavior<ConfiguracionTenant>.Reconstruir(eventoAnterior);

        // When: reintento con moneda diferente.
        var cmd = new ConfigurarMonedaLocalDelTenant(
            TenantId: "acme",
            MonedaLocal: Moneda.USD,
            ConfiguradoPor: "admin-bob");

        var act = () => agg.Ejecutar(cmd, AhoraFijo);

        // Then
        act.Should().Throw<TenantYaConfiguradoException>()
           .Which.Should().Match<TenantYaConfiguradoException>(ex =>
               ex.TenantId == "acme" && ex.MonedaLocalActual == Moneda.COP);
    }

    // ═══════════════════════════════════════════════════════════════
    // §6.5 Fold del evento — ConfiguracionTenant refleja el estado
    // ═══════════════════════════════════════════════════════════════
    [Fact]
    public void Fold_de_MonedaLocalDelTenantConfigurada_deja_el_agregado_con_datos_consistentes()
    {
        // Given: el evento producido por Create.
        var cmd = CmdValido();
        var evento = ConfiguracionTenant.Crear(cmd, AhoraFijo);

        // When: reconstruir el agregado aplicando el evento.
        var agg = AggregateBehavior<ConfiguracionTenant>.Reconstruir(evento);

        // Then
        agg.TenantId.Should().Be("acme");
        agg.MonedaLocal.Should().Be(Moneda.COP);
        agg.ConfiguradaEn.Should().Be(AhoraFijo);
        agg.ConfiguradaPor.Should().Be("admin-alice");
    }
}
