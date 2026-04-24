namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// INV-8 — El rubro agregado quedaría en un nivel mayor a la <c>ProfundidadMaxima</c> del presupuesto.
/// </summary>
public sealed class ProfundidadExcedidaException : DominioException
{
    public int ProfundidadMaxima { get; }
    public int NivelIntentado { get; }

    public ProfundidadExcedidaException(int profundidadMaxima, int nivelIntentado)
        : base($"Profundidad excedida. Máxima permitida: {profundidadMaxima}, nivel intentado: {nivelIntentado}.")
    {
        ProfundidadMaxima = profundidadMaxima;
        NivelIntentado = nivelIntentado;
    }
}
