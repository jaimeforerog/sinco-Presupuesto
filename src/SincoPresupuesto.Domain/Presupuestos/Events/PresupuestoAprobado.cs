using SincoPresupuesto.Domain.SharedKernel;

namespace SincoPresupuesto.Domain.Presupuestos.Events;

/// <summary>
/// Hecho de dominio: el presupuesto fue aprobado. Congela el baseline y transiciona
/// el estado de <c>Borrador</c> a <c>Aprobado</c>. Spec: slices/05-aprobar-presupuesto/spec.md §3 y §12.3.
/// </summary>
/// <param name="PresupuestoId">Id del stream del agregado aprobado.</param>
/// <param name="MontoTotal">
/// Total agregado del presupuesto, igual a la suma de los rubros terminales con monto > 0
/// expresada en <c>MonedaBase</c>. INV-13 (hotspots §2): toda cantidad monetaria en eventos
/// se transporta como <see cref="Dinero"/>.
/// </param>
/// <param name="SnapshotTasas">
/// Tasas de cambio congeladas hacia <c>MonedaBase</c> al momento de aprobar. En el MVP del
/// slice 05 se emite vacío (PRE-3 garantiza que todas las partidas con monto > 0 están en
/// <c>MonedaBase</c>). Followup #24 lo populará cuando exista catálogo <c>TasaDeCambio</c>.
/// </param>
/// <param name="AprobadoEn">Timestamp UTC en el que se aprobó el presupuesto.</param>
/// <param name="AprobadoPor">Usuario que aprobó. Nunca null/whitespace al emitir.</param>
public sealed record PresupuestoAprobado(
    Guid PresupuestoId,
    Dinero MontoTotal,
    IReadOnlyDictionary<Moneda, decimal> SnapshotTasas,
    DateTimeOffset AprobadoEn,
    string AprobadoPor);
