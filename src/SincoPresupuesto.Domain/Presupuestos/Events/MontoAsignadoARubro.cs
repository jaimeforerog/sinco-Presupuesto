using SincoPresupuesto.Domain.SharedKernel;

namespace SincoPresupuesto.Domain.Presupuestos.Events;

/// <summary>
/// Hecho de dominio: se asignó (o reasignó) un monto a un rubro del presupuesto.
/// Payload alineado a spec §3 y §12.3. Es el primer evento del dominio con <see cref="Dinero"/>
/// en su payload (INV-13 — hotspots §2).
/// </summary>
/// <param name="PresupuestoId">Id del stream del agregado donde vive el rubro.</param>
/// <param name="RubroId">Id del rubro al que se le asignó el monto.</param>
/// <param name="Monto">Monto asignado (valor + moneda). El valor es ≥ 0 por INV-2.</param>
/// <param name="MontoAnterior">
/// Monto que tenía el rubro antes de esta asignación. En la primera asignación es
/// <c>Dinero.Cero(Monto.Moneda)</c>. En reasignación con cambio de moneda, queda en la
/// moneda anterior del rubro (ver spec §3 y §6.3).
/// </param>
/// <param name="AsignadoEn">Timestamp UTC en el que se asignó el monto.</param>
/// <param name="AsignadoPor">Usuario que asignó el monto. Nunca null/whitespace al emitir.</param>
public sealed record MontoAsignadoARubro(
    Guid PresupuestoId,
    Guid RubroId,
    Dinero Monto,
    Dinero MontoAnterior,
    DateTimeOffset AsignadoEn,
    string AsignadoPor);
