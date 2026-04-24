using Marten;
using SincoPresupuesto.Domain.Presupuestos;
using SincoPresupuesto.Domain.Presupuestos.Events;
using SincoPresupuesto.Domain.SharedKernel;
using DomainAgregarRubro = SincoPresupuesto.Domain.Presupuestos.Commands.AgregarRubro;

namespace SincoPresupuesto.Application.Presupuestos;

/// <summary>
/// Handler para <see cref="DomainAgregarRubro"/>. Rehidrata el stream del presupuesto,
/// invoca <see cref="Presupuesto.AgregarRubro"/> (valida invariantes) y apendea el evento
/// <see cref="RubroAgregado"/>. El rubroId se genera aquí — no en el dominio.
/// </summary>
public static class AgregarRubroHandler
{
    public static async Task<RubroAgregado> Handle(
        Guid presupuestoId,
        DomainAgregarRubro cmd,
        IDocumentSession session,
        TimeProvider clock,
        CancellationToken ct)
    {
        var presupuesto = await session.Events.AggregateStreamAsync<Presupuesto>(presupuestoId, token: ct)
            ?? throw new PresupuestoNoEncontradoException(presupuestoId);

        var rubroId = Guid.NewGuid();
        var ahora = clock.GetUtcNow();

        var evento = presupuesto.AgregarRubro(cmd, rubroId, ahora);

        session.Events.Append(presupuestoId, evento);
        await session.SaveChangesAsync(ct);

        return evento;
    }
}
