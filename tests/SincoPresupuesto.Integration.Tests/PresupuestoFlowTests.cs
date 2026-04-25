using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SincoPresupuesto.Integration.Tests.Fixtures;
using Xunit;

namespace SincoPresupuesto.Integration.Tests;

/// <summary>
/// Tests de integración end-to-end HTTP→PG que cubren los happy paths de
/// slices 01–04 y errores representativos (400/404/409) por cada slice.
/// Cada test usa un <c>tenantId</c> único para aislar los datos (conjoint
/// multi-tenancy de Marten discrimina por tenant). El contenedor Postgres
/// es compartido vía <see cref="ApiCollection"/>.
/// </summary>
[Collection(nameof(ApiCollection))]
public class PresupuestoFlowTests
{
    private readonly HttpClient _client;

    public PresupuestoFlowTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ════════════════════════════════════════════════════════════════
    // Slice 01 — CrearPresupuesto
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Slice01_CrearPresupuesto_happy_201_y_GET_devuelve_read_model()
    {
        var tenantId = NewTenantId();
        var body = new
        {
            codigo = "OBRA-2026-01",
            nombre = "Torre Norte",
            periodoInicio = "2026-01-01",
            periodoFin = "2026-12-31",
            monedaBase = "COP",
            profundidadMaxima = 10,
        };

        var post = await _client.PostAsJsonAsync($"/api/tenants/{tenantId}/presupuestos/", body);
        post.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await ReadJson(post);
        var presupuestoId = created.GetProperty("presupuestoId").GetGuid();
        presupuestoId.Should().NotBe(Guid.Empty);

        var get = await _client.GetAsync($"/api/tenants/{tenantId}/presupuestos/{presupuestoId}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var read = await ReadJson(get);
        read.GetProperty("codigo").GetString().Should().Be("OBRA-2026-01");
        read.GetProperty("monedaBase").GetString().Should().Be("COP");
        read.GetProperty("estado").GetInt32().Should().Be(0); // Borrador
    }

    [Fact]
    public async Task Slice01_CrearPresupuesto_con_periodo_invertido_devuelve_400()
    {
        var tenantId = NewTenantId();
        var body = new
        {
            codigo = "OBRA-X",
            nombre = "Obra X",
            periodoInicio = "2026-12-31",
            periodoFin = "2026-01-01",
            monedaBase = "COP",
        };

        var response = await _client.PostAsJsonAsync($"/api/tenants/{tenantId}/presupuestos/", body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Slice01_GET_presupuesto_inexistente_devuelve_404()
    {
        var tenantId = NewTenantId();
        var response = await _client.GetAsync($"/api/tenants/{tenantId}/presupuestos/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ════════════════════════════════════════════════════════════════
    // Slice 02 — ConfigurarMonedaLocalDelTenant
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Slice02_ConfigurarMonedaLocal_happy_201_y_GET_devuelve_configuracion()
    {
        var tenantId = NewTenantId();
        var body = new { monedaLocal = "COP", configuradoPor = "admin-integration" };

        var post = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/configuracion/moneda-local", body);
        post.StatusCode.Should().Be(HttpStatusCode.Created);

        var get = await _client.GetAsync($"/api/tenants/{tenantId}/configuracion/");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await ReadJson(get);
        doc.GetProperty("tenantId").GetString().Should().Be(tenantId);
        doc.GetProperty("monedaLocal").GetString().Should().Be("COP");
    }

    [Fact]
    public async Task Slice02_ConfigurarMonedaLocal_reintento_devuelve_409()
    {
        var tenantId = NewTenantId();
        var body = new { monedaLocal = "COP" };

        var first = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/configuracion/moneda-local", body);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/configuracion/moneda-local",
            new { monedaLocal = "USD" });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Slice02_ConfigurarMonedaLocal_codigo_invalido_devuelve_400()
    {
        var tenantId = NewTenantId();
        var response = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/configuracion/moneda-local",
            new { monedaLocal = "XYZ" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Slice02_GET_configuracion_inexistente_devuelve_404()
    {
        var tenantId = NewTenantId();
        var response = await _client.GetAsync($"/api/tenants/{tenantId}/configuracion/");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ════════════════════════════════════════════════════════════════
    // Slice 03 — AgregarRubro
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Slice03_AgregarRubro_raiz_y_hijo_happy_y_GET_devuelve_rubros()
    {
        var tenantId = NewTenantId();
        var presupuestoId = await CrearPresupuestoAsync(tenantId);

        var raiz = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros",
            new { codigo = "01", nombre = "Costos Directos" });
        raiz.StatusCode.Should().Be(HttpStatusCode.Created);
        var raizJson = await ReadJson(raiz);
        var raizId = raizJson.GetProperty("rubroId").GetGuid();

        var hijo = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros",
            new { codigo = "01.01", nombre = "Materiales", rubroPadreId = raizId });
        hijo.StatusCode.Should().Be(HttpStatusCode.Created);

        var get = await _client.GetAsync($"/api/tenants/{tenantId}/presupuestos/{presupuestoId}");
        var doc = await ReadJson(get);
        var rubros = doc.GetProperty("rubros").EnumerateArray().ToList();
        rubros.Should().HaveCount(2);
        rubros.Should().Contain(r => r.GetProperty("codigo").GetString() == "01"
                                  && r.GetProperty("nivel").GetInt32() == 1);
        rubros.Should().Contain(r => r.GetProperty("codigo").GetString() == "01.01"
                                  && r.GetProperty("nivel").GetInt32() == 2);
    }

    [Fact]
    public async Task Slice03_AgregarRubro_codigo_duplicado_devuelve_409()
    {
        var tenantId = NewTenantId();
        var presupuestoId = await CrearPresupuestoAsync(tenantId);

        var first = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros",
            new { codigo = "02", nombre = "Indirectos" });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros",
            new { codigo = "02", nombre = "Otro" });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Slice03_AgregarRubro_formato_invalido_devuelve_400()
    {
        var tenantId = NewTenantId();
        var presupuestoId = await CrearPresupuestoAsync(tenantId);

        var response = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros",
            new { codigo = "1.1", nombre = "Formato inválido" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ════════════════════════════════════════════════════════════════
    // Slice 04 — AsignarMontoARubro
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Slice04_AsignarMonto_happy_201_y_GET_muestra_MontoValor_y_Moneda()
    {
        var tenantId = NewTenantId();
        var presupuestoId = await CrearPresupuestoAsync(tenantId);
        var rubroId = await AgregarRubroAsync(tenantId, presupuestoId, "01", "Materiales");

        var post = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros/{rubroId}/monto",
            new { valor = 1_500_000m, moneda = "COP" });
        post.StatusCode.Should().Be(HttpStatusCode.Created);

        var get = await _client.GetAsync($"/api/tenants/{tenantId}/presupuestos/{presupuestoId}");
        var doc = await ReadJson(get);
        var rubro = doc.GetProperty("rubros").EnumerateArray().Single();
        rubro.GetProperty("montoValor").GetDecimal().Should().Be(1_500_000m);
        rubro.GetProperty("montoMoneda").GetString().Should().Be("COP");
    }

    [Fact]
    public async Task Slice04_AsignarMonto_moneda_distinta_a_MonedaBase_es_permitida()
    {
        var tenantId = NewTenantId();
        var presupuestoId = await CrearPresupuestoAsync(tenantId, monedaBase: "COP");
        var rubroId = await AgregarRubroAsync(tenantId, presupuestoId, "01", "Importados");

        var post = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros/{rubroId}/monto",
            new { valor = 5_000m, moneda = "USD" });
        post.StatusCode.Should().Be(HttpStatusCode.Created);

        var get = await _client.GetAsync($"/api/tenants/{tenantId}/presupuestos/{presupuestoId}");
        var doc = await ReadJson(get);
        var rubro = doc.GetProperty("rubros").EnumerateArray().Single();
        rubro.GetProperty("montoMoneda").GetString().Should().Be("USD");
    }

    [Fact]
    public async Task Slice04_AsignarMonto_a_rubro_con_hijos_devuelve_409()
    {
        var tenantId = NewTenantId();
        var presupuestoId = await CrearPresupuestoAsync(tenantId);
        var padreId = await AgregarRubroAsync(tenantId, presupuestoId, "01", "Agrupador");
        await AgregarRubroAsync(tenantId, presupuestoId, "01.01", "Hijo", padreId);

        var response = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros/{padreId}/monto",
            new { valor = 100m, moneda = "COP" });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Slice04_AsignarMonto_negativo_devuelve_400()
    {
        var tenantId = NewTenantId();
        var presupuestoId = await CrearPresupuestoAsync(tenantId);
        var rubroId = await AgregarRubroAsync(tenantId, presupuestoId, "01", "Terminal");

        var response = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros/{rubroId}/monto",
            new { valor = -100m, moneda = "COP" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ════════════════════════════════════════════════════════════════
    // Slice 05 — AprobarPresupuesto
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Slice05_Aprobar_happy_201_y_GET_muestra_estado_Aprobado_y_MontoTotal()
    {
        var tenantId = NewTenantId();
        var presupuestoId = await CrearPresupuestoAsync(tenantId);
        var rubroId = await AgregarRubroAsync(tenantId, presupuestoId, "01", "Materiales");
        var asignar = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros/{rubroId}/monto",
            new { valor = 1_500_000m, moneda = "COP" });
        asignar.StatusCode.Should().Be(HttpStatusCode.Created);

        var aprobar = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/aprobar",
            new { aprobadoPor = "admin-test" });
        aprobar.StatusCode.Should().Be(HttpStatusCode.Created);

        var get = await _client.GetAsync($"/api/tenants/{tenantId}/presupuestos/{presupuestoId}");
        var doc = await ReadJson(get);
        doc.GetProperty("estado").GetInt32().Should().Be(1); // Aprobado
        doc.GetProperty("montoTotalValor").GetDecimal().Should().Be(1_500_000m);
        doc.GetProperty("montoTotalMoneda").GetString().Should().Be("COP");
        doc.GetProperty("aprobadoPor").GetString().Should().Be("admin-test");
        doc.TryGetProperty("aprobadoEn", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Slice05_Aprobar_sin_montos_devuelve_400()
    {
        var tenantId = NewTenantId();
        var presupuestoId = await CrearPresupuestoAsync(tenantId);
        await AgregarRubroAsync(tenantId, presupuestoId, "01", "Sin monto");

        var response = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/aprobar",
            new { });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Slice05_Aprobar_con_partida_en_otra_moneda_devuelve_400()
    {
        var tenantId = NewTenantId();
        var presupuestoId = await CrearPresupuestoAsync(tenantId);
        var rubroId = await AgregarRubroAsync(tenantId, presupuestoId, "01", "Importado");
        var asignar = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros/{rubroId}/monto",
            new { valor = 5_000m, moneda = "USD" });
        asignar.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/aprobar",
            new { });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Slice05_Aprobar_dos_veces_devuelve_409_y_AgregarRubro_post_aprobado_devuelve_409()
    {
        var tenantId = NewTenantId();
        var presupuestoId = await CrearPresupuestoAsync(tenantId);
        var rubroId = await AgregarRubroAsync(tenantId, presupuestoId, "01", "Único");
        await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros/{rubroId}/monto",
            new { valor = 100m, moneda = "COP" });

        var primera = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/aprobar", new { });
        primera.StatusCode.Should().Be(HttpStatusCode.Created);

        // Segunda aprobación → 409 (cierra retroactivamente followup #13).
        var segunda = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/aprobar", new { });
        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // AgregarRubro post-aprobación → 409 (también cierra #13).
        var agregarPost = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros",
            new { codigo = "02", nombre = "Tarde" });
        agregarPost.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // AsignarMonto post-aprobación → 409.
        var asignarPost = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros/{rubroId}/monto",
            new { valor = 200m, moneda = "COP" });
        asignarPost.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ════════════════════════════════════════════════════════════════
    // Slice 06 — RegistrarTasaDeCambio
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Slice06_RegistrarTasa_happy_201_y_GET_lista_la_tasa_vigente()
    {
        var tenantId = NewTenantId();
        var post = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/tasas-de-cambio/",
            new { monedaDesde = "USD", monedaHacia = "COP", tasa = 4200m, fecha = "2026-04-24", fuente = "BanRep" });
        post.StatusCode.Should().Be(HttpStatusCode.Created);

        var get = await _client.GetAsync($"/api/tenants/{tenantId}/tasas-de-cambio/");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await ReadJson(get);
        var tasas = doc.GetProperty("tasas").EnumerateArray().ToList();
        tasas.Should().ContainSingle();
        tasas[0].GetProperty("monedaDesde").GetString().Should().Be("USD");
        tasas[0].GetProperty("monedaHacia").GetString().Should().Be("COP");
        tasas[0].GetProperty("tasa").GetDecimal().Should().Be(4200m);
    }

    [Fact]
    public async Task Slice06_RegistrarTasa_misma_tupla_actualiza_tasa_last_write_wins()
    {
        var tenantId = NewTenantId();
        await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/tasas-de-cambio/",
            new { monedaDesde = "USD", monedaHacia = "COP", tasa = 4200m, fecha = "2026-04-24" });

        var second = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/tasas-de-cambio/",
            new { monedaDesde = "USD", monedaHacia = "COP", tasa = 4250m, fecha = "2026-04-24" });
        second.StatusCode.Should().Be(HttpStatusCode.Created);

        var get = await _client.GetAsync($"/api/tenants/{tenantId}/tasas-de-cambio/");
        var doc = await ReadJson(get);
        var tasas = doc.GetProperty("tasas").EnumerateArray().ToList();
        tasas.Should().ContainSingle("la proyección hace last-write-wins por par");
        tasas[0].GetProperty("tasa").GetDecimal().Should().Be(4250m);
    }

    [Fact]
    public async Task Slice06_RegistrarTasa_monedas_iguales_devuelve_400()
    {
        var tenantId = NewTenantId();
        var response = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/tasas-de-cambio/",
            new { monedaDesde = "USD", monedaHacia = "USD", tasa = 1m, fecha = "2026-04-24" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Slice06_RegistrarTasa_tasa_negativa_devuelve_400()
    {
        var tenantId = NewTenantId();
        var response = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/tasas-de-cambio/",
            new { monedaDesde = "USD", monedaHacia = "COP", tasa = -100m, fecha = "2026-04-24" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Slice06_GET_tasas_inexistente_devuelve_404()
    {
        var tenantId = NewTenantId();
        var response = await _client.GetAsync($"/api/tenants/{tenantId}/tasas-de-cambio/");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════

    private static string NewTenantId() => $"it-{Guid.NewGuid():N}";

    private async Task<Guid> CrearPresupuestoAsync(string tenantId, string monedaBase = "COP")
    {
        var body = new
        {
            codigo = $"P-{Guid.NewGuid():N}".Substring(0, 10),
            nombre = "Presupuesto integración",
            periodoInicio = "2026-01-01",
            periodoFin = "2026-12-31",
            monedaBase,
            profundidadMaxima = 10,
        };
        var response = await _client.PostAsJsonAsync($"/api/tenants/{tenantId}/presupuestos/", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await ReadJson(response);
        return json.GetProperty("presupuestoId").GetGuid();
    }

    private async Task<Guid> AgregarRubroAsync(
        string tenantId, Guid presupuestoId, string codigo, string nombre, Guid? padreId = null)
    {
        var body = padreId is null
            ? (object)new { codigo, nombre }
            : new { codigo, nombre, rubroPadreId = padreId };
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
