namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// PRE-3 (slice 05, temporal hasta followup #24) — Se intentó aprobar un presupuesto cuyos
/// rubros terminales con monto positivo incluyen al menos una moneda distinta a
/// <c>MonedaBase</c>. La aprobación con multimoneda real requiere catálogo de tasas (followup #24);
/// en el MVP del slice 05 la única vía válida es que todas las partidas con monto > 0 estén
/// en <c>MonedaBase</c>. Spec slice 05 §4 PRE-3 y §12.1.
/// </summary>
public sealed class AprobacionConMultimonedaNoSoportadaException : DominioException
{
    public Guid PresupuestoId { get; }
    public IReadOnlyList<Guid> RubrosConMonedaDistinta { get; }
    public Moneda MonedaBase { get; }

    public AprobacionConMultimonedaNoSoportadaException(
        Guid presupuestoId,
        IReadOnlyList<Guid> rubrosConMonedaDistinta,
        Moneda monedaBase)
        : base(
            $"El presupuesto '{presupuestoId}' tiene {rubrosConMonedaDistinta.Count} rubro(s) " +
            $"terminal(es) con moneda distinta a {monedaBase.Codigo}. La aprobación con " +
            $"multimoneda no se soporta en este MVP.")
    {
        PresupuestoId = presupuestoId;
        RubrosConMonedaDistinta = rubrosConMonedaDistinta;
        MonedaBase = monedaBase;
    }
}
