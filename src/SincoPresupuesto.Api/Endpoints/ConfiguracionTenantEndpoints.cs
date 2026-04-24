using Marten;
using SincoPresupuesto.Application.ConfiguracionesTenant;
using SincoPresupuesto.Domain.ConfiguracionesTenant.Commands;
using SincoPresupuesto.Domain.SharedKernel;

namespace SincoPresupuesto.Api.Endpoints;

public static class ConfiguracionTenantEndpoints
{
    public static IEndpointRouteBuilder MapConfiguracionTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants/{tenantId}/configuracion")
                       .WithTags("ConfiguracionTenant");

        group.MapPost("/moneda-local", ConfigurarMonedaLocalAsync)
             .WithName("ConfigurarMonedaLocalDelTenant");

        group.MapGet("/", ObtenerConfiguracionAsync)
             .WithName("ObtenerConfiguracionTenant");

        return app;
    }

    public sealed record ConfigurarMonedaLocalRequest(
        string MonedaLocal,
        string? ConfiguradoPor = null);

    private static async Task<IResult> ConfigurarMonedaLocalAsync(
        string tenantId,
        ConfigurarMonedaLocalRequest body,
        IDocumentStore store,
        TimeProvider clock,
        CancellationToken ct,
        HttpContext http)
    {
        await using var session = store.LightweightSession(tenantId);

        var cmd = new ConfigurarMonedaLocalDelTenant(
            TenantId: tenantId,
            MonedaLocal: new Moneda(body.MonedaLocal),
            ConfiguradoPor: body.ConfiguradoPor ?? http.User.Identity?.Name ?? "sistema");

        var evento = await ConfigurarMonedaLocalDelTenantHandler.Handle(cmd, session, clock, ct);

        return Results.CreatedAtRoute(
            "ObtenerConfiguracionTenant",
            new { tenantId },
            new
            {
                evento.TenantId,
                MonedaLocal = evento.Moneda.Codigo,
                evento.ConfiguradaEn,
                evento.ConfiguradaPor,
            });
    }

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
