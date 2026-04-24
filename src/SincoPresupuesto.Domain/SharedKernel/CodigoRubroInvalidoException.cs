namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// INV-10 — El código del rubro no coincide con el formato canónico
/// <c>^\d{2}(\.\d{2}){0,14}$</c>.
/// </summary>
public sealed class CodigoRubroInvalidoException : DominioException
{
    public string CodigoIntentado { get; }

    public CodigoRubroInvalidoException(string codigoIntentado)
        : base($"Código de rubro inválido: '{codigoIntentado}'.")
    {
        CodigoIntentado = codigoIntentado;
    }
}
