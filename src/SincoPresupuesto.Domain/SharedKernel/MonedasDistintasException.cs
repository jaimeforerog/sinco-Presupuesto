namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// Operación aritmética o de comparación invocada sobre dos <see cref="Dinero"/> con monedas distintas.
/// La conversión entre monedas debe hacerse explícitamente vía <see cref="Dinero.En"/>.
/// </summary>
public sealed class MonedasDistintasException : DominioException
{
    public Moneda Izquierda { get; }
    public Moneda Derecha { get; }

    public MonedasDistintasException(Moneda izquierda, Moneda derecha)
        : base(
            $"Operación aritmética no permitida entre monedas distintas: {izquierda} vs {derecha}. " +
            $"Convierte explícitamente con Dinero.En(destino, factor).")
    {
        Izquierda = izquierda;
        Derecha = derecha;
    }
}
