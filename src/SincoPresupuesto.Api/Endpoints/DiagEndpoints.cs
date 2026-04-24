using Marten;
using Npgsql;
using SincoPresupuesto.Application.ConfiguracionesTenant;
using SincoPresupuesto.Application.Presupuestos;

namespace SincoPresupuesto.Api.Endpoints;

/// <summary>
/// Visor de Eventos — endpoints de solo lectura sobre el event store y las
/// proyecciones existentes. Ver <c>slices/_obs-visor-eventos/README.md</c>.
///
/// Límites duros: no dispara comandos, no define proyecciones nuevas, no
/// cruza tenants en endpoints de dominio (el único cross-tenant es
/// <c>GET /diag/tenants</c>), no contiene lógica de negocio.
/// </summary>
public static class DiagEndpoints
{
    public static IEndpointRouteBuilder MapDiagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/diag").WithTags("Diagnóstico");

        group.MapGet("/", () => Results.Redirect("/diag/index.html"))
             .ExcludeFromDescription();

        group.MapGet("/index.html", () =>
                Results.Content(DiagIndexHtml.Content, "text/html; charset=utf-8"))
             .WithName("DiagIndex")
             .ExcludeFromDescription();

        group.MapGet("/tenants", ListarTenantsAsync)
             .WithName("DiagListarTenants");

        group.MapGet("/tenants/{tenantId}/streams", ListarStreamsAsync)
             .WithName("DiagListarStreams");

        group.MapGet("/tenants/{tenantId}/streams/{streamId:guid}/events", ListarEventosAsync)
             .WithName("DiagListarEventos");

        group.MapGet("/tenants/{tenantId}/projections/presupuestos", ListarPresupuestosAsync)
             .WithName("DiagListarPresupuestos");

        group.MapGet("/tenants/{tenantId}/projections/configuracion", ObtenerConfiguracionAsync)
             .WithName("DiagObtenerConfiguracion");

        return app;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Tenants — cross-tenant por necesidad: la UI debe listar para selección.
    // Se considera metadata operativa, no datos de negocio.
    // ══════════════════════════════════════════════════════════════════════
    private static async Task<IResult> ListarTenantsAsync(
        IConfiguration config, CancellationToken ct)
    {
        var connectionString = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres no configurada.");
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        var tenants = new List<string>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT DISTINCT tenant_id
            FROM public.mt_events
            ORDER BY tenant_id";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            tenants.Add(reader.GetString(0));
        }
        return Results.Ok(tenants);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Streams del tenant — query directa a mt_streams filtrada por tenant_id.
    // ══════════════════════════════════════════════════════════════════════
    private static async Task<IResult> ListarStreamsAsync(
        string tenantId,
        IConfiguration config,
        CancellationToken ct,
        int page = 1,
        int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 500) pageSize = 50;

        var connectionString = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres no configurada.");
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, type, version, created, timestamp
            FROM public.mt_streams
            WHERE tenant_id = @tenant
            ORDER BY timestamp DESC
            LIMIT @limit OFFSET @offset";
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("limit", pageSize);
        cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize);

        var streams = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            streams.Add(new
            {
                streamId = reader.GetGuid(0),
                aggregateType = reader.IsDBNull(1) ? null : reader.GetString(1),
                version = reader.GetInt64(2),
                createdAt = reader.GetDateTime(3),
                updatedAt = reader.GetDateTime(4),
            });
        }
        return Results.Ok(streams);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Eventos de un stream — usa Marten API (FetchStreamAsync).
    // ══════════════════════════════════════════════════════════════════════
    private static async Task<IResult> ListarEventosAsync(
        string tenantId,
        Guid streamId,
        IDocumentStore store,
        CancellationToken ct)
    {
        await using var session = store.QuerySession(tenantId);
        var events = await session.Events.FetchStreamAsync(streamId, token: ct);

        var payload = events.Select(e => new
        {
            sequence = e.Sequence,
            version = e.Version,
            timestamp = e.Timestamp,
            eventType = e.EventType.Name,
            data = e.Data,
        });
        return Results.Ok(payload);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Proyección PresupuestoReadModel del tenant.
    // ══════════════════════════════════════════════════════════════════════
    private static async Task<IResult> ListarPresupuestosAsync(
        string tenantId,
        IDocumentStore store,
        CancellationToken ct)
    {
        await using var session = store.QuerySession(tenantId);
        var presupuestos = await session.Query<PresupuestoReadModel>().ToListAsync(ct);
        return Results.Ok(presupuestos);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Proyección ConfiguracionTenantActual del tenant.
    // ══════════════════════════════════════════════════════════════════════
    private static async Task<IResult> ObtenerConfiguracionAsync(
        string tenantId,
        IDocumentStore store,
        CancellationToken ct)
    {
        await using var session = store.QuerySession(tenantId);
        var doc = await session.LoadAsync<ConfiguracionTenantActual>(
            ConfiguracionTenantStreamId.Value, ct);
        return doc is null ? Results.NotFound() : Results.Ok(doc);
    }
}
