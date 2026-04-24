namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// INV-11 — Otro rubro dentro del mismo presupuesto ya usa ese código.
/// </summary>
public sealed class CodigoRubroDuplicadoException : DominioException
{
    public string CodigoIntentado { get; }

    public CodigoRubroDuplicadoException(string codigoIntentado)
        : base($"Ya existe un rubro con el código '{codigoIntentado}' en el presupuesto.")
    {
        CodigoIntentado = codigoIntentado;
    }
}
