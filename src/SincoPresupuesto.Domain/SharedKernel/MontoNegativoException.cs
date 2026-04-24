namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// INV-2 — Se intentó asignar un monto con valor negativo a un rubro.
/// El propio <see cref="Dinero"/> admite valores negativos (contra-asiento), pero en el
/// contexto de asignación a un rubro no tiene sentido. Spec slice 04 §4 PRE-3 y §12.1.
/// </summary>
public sealed class MontoNegativoException : DominioException
{
    public Dinero MontoIntentado { get; }

    public MontoNegativoException(Dinero montoIntentado)
        : base($"No se puede asignar un monto negativo a un rubro. Intentado: {montoIntentado}.")
    {
        MontoIntentado = montoIntentado;
    }
}
