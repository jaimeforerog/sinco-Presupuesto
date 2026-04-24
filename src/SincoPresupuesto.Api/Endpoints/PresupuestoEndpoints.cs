using Marten;
using SincoPresupuesto.Application.Presupuestos;
using SincoPresupuesto.Domain.Presupuestos.Commands;
using SincoPresupuesto.Domain.SharedKernel;
using DomainAgregarRubro = SincoPresupuesto.Domain.Presupuestos.Commands.AgregarRubro;

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

        group.MapPost("/{presupuestoId:guid}/rubros", AgregarRubroAsync)
             .WithName("AgregarRubro");

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

    public sealed record AgregarRubroRequest(
        string Codigo,
        string Nombre,
        Guid? RubroPadreId = null);

    private static async Task<IResult> AgregarRubroAsync(
        string tenantId,
        Guid presupuestoId,
        AgregarRubroRequest body,
        IDocumentStore store,
        TimeProvider clock,
        CancellationToken ct)
    {
        await using var session = store.LightweightSession(tenantId);

        var cmd = new DomainAgregarRubro(
            Codigo: body.Codigo,
            Nombre: body.Nombre,
            RubroPadreId: body.RubroPadreId);

        var evento = await AgregarRubroHandler.Handle(presupuestoId, cmd, session, clock, ct);

        return Results.CreatedAtRoute(
            "ObtenerPresupuesto",
            new { tenantId, id = presupuestoId },
            new
            {
                evento.RubroId,
                evento.Codigo,
                evento.Nombre,
                evento.RubroPadreId,
                evento.AgregadoEn,
            });
    }
}
