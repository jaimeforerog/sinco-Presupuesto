namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// El código de moneda integrante no es un código ISO 4217 válido.
/// Se lanza al intentar construir un VO <see cref="Moneda"/> con un código inválido.
/// </summary>
public sealed class CodigoMonedaInvalidoException : DominioException
{
    public string CodigoIntentado { get; }

    public CodigoMonedaInvalidoException(string codigoIntentado)
        : base($"'{codigoIntentado}' no es un código ISO 4217 válido. Esperado: tres letras A-Z.")
    {
        CodigoIntentado = codigoIntentado;
    }
}
