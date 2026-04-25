using Marten;
using SincoPresupuesto.Domain.Presupuestos;
using SincoPresupuesto.Domain.Presupuestos.Events;
using SincoPresupuesto.Domain.SharedKernel;
using DomainAprobar = SincoPresupuesto.Domain.Presupuestos.Commands.AprobarPresupuesto;

namespace SincoPresupuesto.Application.Presupuestos;

/// <summary>
/// Handler para <see cref="DomainAprobar"/>. Rehidrata el stream del presupuesto,
/// invoca <see cref="Presupuesto.AprobarPresupuesto"/> (valida invariantes) y apendea
/// el evento <see cref="PresupuestoAprobado"/>. En MVP <c>SnapshotTasas</c> queda
/// vacío (followup #24).
/// </summary>
public static class AprobarPresupuestoHandler
{
    public static async Task<PresupuestoAprobado> Handle(
        Guid presupuestoId,
        DomainAprobar cmd,
        IDocumentSession session,
        TimeProvider clock,
        CancellationToken ct)
    {
        var presupuesto = await session.Events.AggregateStreamAsync<Presupuesto>(presupuestoId, token: ct)
            ?? throw new PresupuestoNoEncontradoException(presupuestoId);

        var ahora = clock.GetUtcNow();

        var evento = presupuesto.AprobarPresupuesto(cmd, ahora);

        session.Events.Append(presupuestoId, evento);
        await session.SaveChangesAsync(ct);

        return evento;
    }
}
