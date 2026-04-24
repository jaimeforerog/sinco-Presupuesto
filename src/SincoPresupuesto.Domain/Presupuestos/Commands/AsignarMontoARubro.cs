using SincoPresupuesto.Domain.SharedKernel;

namespace SincoPresupuesto.Domain.Presupuestos.Commands;

/// <summary>
/// Comando de dominio: asignar (o reasignar) un monto a un rubro existente del árbol del
/// presupuesto. Spec: slices/04-asignar-monto-a-rubro/spec.md §2.
/// </summary>
/// <param name="RubroId">Id del rubro destino dentro del agregado (no se pasa PresupuestoId).</param>
/// <param name="Monto"><see cref="Dinero"/> con valor y moneda. La moneda llega validada por el VO.</param>
/// <param name="AsignadoPor">Usuario que asigna el monto. Vacío/whitespace → "sistema" al emitir.</param>
public sealed record AsignarMontoARubro(
    Guid RubroId,
    Dinero Monto,
    string AsignadoPor = "sistema");
