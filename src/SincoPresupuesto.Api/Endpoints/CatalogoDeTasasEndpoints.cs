using Marten;
using SincoPresupuesto.Application.CatalogosDeTasas;
using SincoPresupuesto.Domain.SharedKernel;
using DomainCmd = SincoPresupuesto.Domain.CatalogosDeTasas.Commands.RegistrarTasaDeCambio;

namespace SincoPresupuesto.Api.Endpoints;

public static class CatalogoDeTasasEndpoints
{
    public static IEndpointRouteBuilder MapCatalogoDeTasasEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants/{tenantId}/tasas-de-cambio")
                       .WithTags("CatalogoDeTasas");

        group.MapPost("/", RegistrarTasaAsync)
             .WithName("RegistrarTasaDeCambio");

        group.MapGet("/", ObtenerVigentesAsync)
             .WithName("ObtenerTasasDeCambioVigentes");

        return app;
    }

    public sealed record RegistrarTasaRequest(
        string MonedaDesde,
        string MonedaHacia,
        decimal Tasa,
        DateOnly Fecha,
        string? Fuente = null,
        string? RegistradoPor = null);

    private static async Task<IResult> RegistrarTasaAsync(
        string tenantId,
        RegistrarTasaRequest body,
        IDocumentStore store,
        TimeProvider clock,
        CancellationToken ct,
        HttpContext http)
    {
        await using var session = store.LightweightSession(tenantId);

        var cmd = new DomainCmd(
            MonedaDesde: new Moneda(body.MonedaDesde),
            MonedaHacia: new Moneda(body.MonedaHacia),
            Tasa: body.Tasa,
            Fecha: body.Fecha,
            Fuente: body.Fuente,
            RegistradoPor: body.RegistradoPor ?? http.User.Identity?.Name ?? "sistema");

        var evento = await RegistrarTasaDeCambioHandler.Handle(cmd, session, clock, ct);

        return Results.CreatedAtRoute(
            "ObtenerTasasDeCambioVigentes",
            new { tenantId },
            new
            {
                MonedaDesde = evento.MonedaDesde.Codigo,
                MonedaHacia = evento.MonedaHacia.Codigo,
                evento.Tasa,
                evento.Fecha,
                evento.Fuente,
                evento.RegistradaEn,
                evento.RegistradaPor,
            });
    }

    private static async Task<IResult> ObtenerVigentesAsync(
        string tenantId,
        IDocumentStore store,
        CancellationToken ct)
    {
        await using var session = store.QuerySession(tenantId);
        var doc = await session.LoadAsync<TasasDeCambioVigentes>(
            CatalogoDeTasasStreamId.Value, ct);
        return doc is null ? Results.NotFound() : Results.Ok(doc);
    }
}
