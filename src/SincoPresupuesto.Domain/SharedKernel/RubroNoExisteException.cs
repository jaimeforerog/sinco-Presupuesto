namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// PRE-2 — El <c>RubroId</c> del comando no coincide con ningún rubro del agregado.
/// Distinta de <see cref="RubroPadreNoExisteException"/>: aquí el rubro es el <b>destino</b>
/// de la operación, no un padre referenciado. Spec slice 04 §10 (decisión del modeler)
/// y §12.1.
/// </summary>
public sealed class RubroNoExisteException : DominioException
{
    public Guid RubroId { get; }

    public RubroNoExisteException(Guid rubroId)
        : base($"El rubro con Id '{rubroId}' no existe en el presupuesto.")
    {
        RubroId = rubroId;
    }
}
