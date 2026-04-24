using Marten;
using SincoPresupuesto.Domain.Presupuestos;
using SincoPresupuesto.Domain.Presupuestos.Commands;
using SincoPresupuesto.Domain.Presupuestos.Events;

namespace SincoPresupuesto.Application.Presupuestos;

/// <summary>
/// Handler Wolverine para <see cref="CrearPresupuesto"/>.
/// Usa Marten para iniciar un stream nuevo con el evento <see cref="PresupuestoCreado"/>.
/// </summary>
public static class CrearPresupuestoHandler
{
    public static async Task<PresupuestoCreado> Handle(
        CrearPresupuesto cmd,
        IDocumentSession session,
        TimeProvider clock,
        CancellationToken ct)
    {
        var presupuestoId = Guid.NewGuid();
        var ahora = clock.GetUtcNow();

        var evento = Presupuesto.Crear(cmd, presupuestoId, ahora);

        // Stream nuevo por agregado. Marten infiere el tenant del IDocumentSession
        // (conjoint multi-tenant) si se obtuvo con IDocumentStore.LightweightSession(tenantId).
        session.Events.StartStream<Presupuesto>(presupuestoId, evento);
        await session.SaveChangesAsync(ct);

        return evento;
    }
}
