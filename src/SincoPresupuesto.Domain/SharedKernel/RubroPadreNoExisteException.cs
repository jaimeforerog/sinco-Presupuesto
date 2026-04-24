namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// INV-D — El <c>RubroPadreId</c> del comando no coincide con ningún rubro del agregado.
/// </summary>
public sealed class RubroPadreNoExisteException : DominioException
{
    public Guid RubroPadreId { get; }

    public RubroPadreNoExisteException(Guid rubroPadreId)
        : base($"El rubro padre con Id '{rubroPadreId}' no existe en el presupuesto.")
    {
        RubroPadreId = rubroPadreId;
    }
}
