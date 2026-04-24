using Marten;
using SincoPresupuesto.Domain.Presupuestos;
using SincoPresupuesto.Domain.Presupuestos.Events;
using SincoPresupuesto.Domain.SharedKernel;
using DomainAsignarMonto = SincoPresupuesto.Domain.Presupuestos.Commands.AsignarMontoARubro;

namespace SincoPresupuesto.Application.Presupuestos;

/// <summary>
/// Handler para <see cref="DomainAsignarMonto"/>. Rehidrata el stream del presupuesto,
/// invoca <see cref="Presupuesto.AsignarMontoARubro"/> (valida invariantes) y apendea
/// el evento <see cref="MontoAsignadoARubro"/>. El <c>rubroId</c> lo provee el caller
/// desde la ruta — no se genera aquí.
/// </summary>
public static class AsignarMontoARubroHandler
{
    public static async Task<MontoAsignadoARubro> Handle(
        Guid presupuestoId,
        DomainAsignarMonto cmd,
        IDocumentSession session,
        TimeProvider clock,
        CancellationToken ct)
    {
        var presupuesto = await session.Events.AggregateStreamAsync<Presupuesto>(presupuestoId, token: ct)
            ?? throw new PresupuestoNoEncontradoException(presupuestoId);

        var ahora = clock.GetUtcNow();

        var evento = presupuesto.AsignarMontoARubro(cmd, ahora);

        session.Events.Append(presupuestoId, evento);
        await session.SaveChangesAsync(ct);

        return evento;
    }
}
