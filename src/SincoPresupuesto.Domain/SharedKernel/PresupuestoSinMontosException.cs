namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// PRE-2 (slice 05) — Se intentó aprobar un presupuesto sin sustancia: o no tiene rubros,
/// o ningún rubro terminal tiene <c>Monto.EsPositivo</c>. Spec slice 05 §4 PRE-2 y §12.1.
/// </summary>
public sealed class PresupuestoSinMontosException : DominioException
{
    public Guid PresupuestoId { get; }

    public PresupuestoSinMontosException(Guid presupuestoId)
        : base($"El presupuesto '{presupuestoId}' no tiene rubros terminales con monto positivo y no puede aprobarse.")
    {
        PresupuestoId = presupuestoId;
    }
}
