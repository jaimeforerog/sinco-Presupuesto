namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// El factor de conversión pasado a <see cref="Dinero.En"/> no es válido (cero o negativo).
/// Un factor válido debe ser estrictamente mayor que cero.
/// </summary>
public sealed class FactorDeConversionInvalidoException : DominioException
{
    public decimal FactorIntentado { get; }

    public FactorDeConversionInvalidoException(decimal factorIntentado)
        : base($"El factor de conversión '{factorIntentado}' es inválido. Debe ser mayor que cero.")
    {
        FactorIntentado = factorIntentado;
    }
}
