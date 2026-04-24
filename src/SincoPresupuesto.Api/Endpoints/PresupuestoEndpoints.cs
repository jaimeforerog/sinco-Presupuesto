using Marten;
using SincoPresupuesto.Application.Presupuestos;
using SincoPresupuesto.Domain.Presupuestos.Commands;
using SincoPresupuesto.Domain.SharedKernel;

namespace SincoPresupuesto.Api.Endpoints;

public static class PresupuestoEndpoints
{
    public static IEndpointRouteBuilder MapPresupuestoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants/{tenantId}/presupuestos")
                       .WithTags("Presupuestos");

        group.MapPost("/", CrearPresupuestoAsync)
             .WithName("CrearPresupuesto");

        group.MapGet("/{id:guid}", ObtenerPresupuestoAsync)
             .WithName("ObtenerPresupuesto");

        return app;
    }

    // DTO del request — evita exponer el record de dominio directamente.
    public sealed record CrearPresupuestoRequest(
        string Codigo,
        string Nombre,
        DateOnly PeriodoInicio,
        DateOnly PeriodoFin,
        string MonedaBase,
        int ProfundidadMaxima = 10);

    private static async Task<IResult> CrearPresupuestoAsync(
        string tenantId,
        CrearPresupuestoRequest body,
        IDocumentStore store,
        TimeProvider clock,
        CancellationToken ct,
        HttpContext http)
    {
        await using var session = store.LightweightSession(tenantId);

        var cmd = new CrearPresupuesto(
            TenantId: tenantId,
            Codigo: body.Codigo,
            Nombre: body.Nombre,
            PeriodoInicio: body.PeriodoInicio,
            PeriodoFin: body.PeriodoFin,
            MonedaBase: new Moneda(body.MonedaBase),
            ProfundidadMaxima: body.ProfundidadMaxima,
            CreadoPor: http.User.Identity?.Name ?? "sistema");

        var evento = await CrearPresupuestoHandler.Handle(cmd, session, clock, ct);

        return Results.CreatedAtRoute(
            "ObtenerPresupuesto",
            new { tenantId, id = evento.PresupuestoId },
            new { evento.PresupuestoId, evento.Codigo, evento.Nombre, Estado = "Borrador" });
    }

    private static async Task<IResult> ObtenerPresupuestoAsync(
        string tenantId,
        Guid id,
        IDocumentStore store,
        CancellationToken ct)
    {
        await using var session = store.QuerySession(tenantId);
        var readModel = await session.LoadAsync<PresupuestoReadModel>(id, ct);
        return readModel is null ? Results.NotFound() : Results.Ok(readModel);
    }
}
