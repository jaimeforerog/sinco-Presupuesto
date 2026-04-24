using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SincoPresupuesto.Integration.Tests.Fixtures;
using Xunit;

namespace SincoPresupuesto.Integration.Tests;

/// <summary>
/// Tests de integración para los endpoints del visor de eventos.
/// Spec: <c>slices/_obs-visor-eventos/README.md</c>.
///
/// Cada test prepara datos via endpoints de dominio (CrearPresupuesto,
/// AgregarRubro, etc.) y verifica que <c>/diag/*</c> los expone bien.
/// </summary>
[Collection(nameof(ApiCollection))]
public class DiagEndpointsTests
{
    private readonly HttpClient _client;

    public DiagEndpointsTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Diag_tenants_devuelve_al_menos_el_tenant_creado()
    {
        var tenantId = NewTenantId();
        await CrearPresupuestoAsync(tenantId);

        var response = await _client.GetAsync("/diag/tenants");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tenants = await response.Content.ReadFromJsonAsync<List<string>>();
        tenants.Should().NotBeNull().And.Contain(tenantId);
    }

    [Fact]
    public async Task Diag_streams_del_tenant_incluye_el_stream_del_presupuesto()
    {
        var tenantId = NewTenantId();
        var presupuestoId = await CrearPresupuestoAsync(tenantId);

        var response = await _client.GetAsync($"/diag/tenants/{tenantId}/streams");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = await ReadJson(response);
        var streams = doc.EnumerateArray().ToList();
        streams.Should().NotBeEmpty();
        streams.Should().Contain(s => s.GetProperty("streamId").GetGuid() == presupuestoId);
    }

    [Fact]
    public async Task Diag_events_del_stream_lista_PresupuestoCreado_y_RubroAgregado()
    {
        var tenantId = NewTenantId();
        var presupuestoId = await CrearPresupuestoAsync(tenantId);
        await AgregarRubroAsync(tenantId, presupuestoId, "01", "Materiales");

        var response = await _client.GetAsync(
            $"/diag/tenants/{tenantId}/streams/{presupuestoId}/events");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = await ReadJson(response);
        var events = doc.EnumerateArray().ToList();
        events.Should().HaveCount(2);
        events[0].GetProperty("eventType").GetString().Should().Be("PresupuestoCreado");
        events[0].GetProperty("version").GetInt64().Should().Be(1);
        events[1].GetProperty("eventType").GetString().Should().Be("RubroAgregado");
        events[1].GetProperty("version").GetInt64().Should().Be(2);

        // El payload "data" es el evento serializado completo.
        events[0].GetProperty("data").GetProperty("codigo").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Diag_projections_presupuestos_devuelve_el_read_model()
    {
        var tenantId = NewTenantId();
        var presupuestoId = await CrearPresupuestoAsync(tenantId);

        var response = await _client.GetAsync(
            $"/diag/tenants/{tenantId}/projections/presupuestos");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = await ReadJson(response);
        var rm = doc.EnumerateArray().Single();
        rm.GetProperty("id").GetGuid().Should().Be(presupuestoId);
    }

    [Fact]
    public async Task Diag_projections_configuracion_404_si_tenant_sin_configurar()
    {
        var tenantId = NewTenantId();

        var response = await _client.GetAsync(
            $"/diag/tenants/{tenantId}/projections/configuracion");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Diag_index_html_sirve_la_UI()
    {
        var response = await _client.GetAsync("/diag/index.html");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("<title>Visor de Eventos");
        html.Should().Contain("/diag/tenants");
    }

    // ─── Helpers (mismo patrón que PresupuestoFlowTests) ─────────────
    private static string NewTenantId() => $"diag-{Guid.NewGuid():N}";

    private async Task<Guid> CrearPresupuestoAsync(string tenantId)
    {
        var body = new
        {
            codigo = $"P-{Guid.NewGuid():N}".Substring(0, 10),
            nombre = "Presupuesto visor",
            periodoInicio = "2026-01-01",
            periodoFin = "2026-12-31",
            monedaBase = "COP",
            profundidadMaxima = 10,
        };
        var response = await _client.PostAsJsonAsync($"/api/tenants/{tenantId}/presupuestos/", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await ReadJson(response);
        return json.GetProperty("presupuestoId").GetGuid();
    }

    private async Task<Guid> AgregarRubroAsync(
        string tenantId, Guid presupuestoId, string codigo, string nombre)
    {
        var body = new { codigo, nombre };
        var response = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await ReadJson(response);
        return json.GetProperty("rubroId").GetGuid();
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        return doc.RootElement.Clone();
    }
}
