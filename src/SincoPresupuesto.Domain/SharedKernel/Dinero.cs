namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// Value object que representa un monto con moneda explícita.
/// Las operaciones aritméticas entre monedas distintas lanzan <see cref="MonedasDistintasException"/>
/// — la conversión debe ser explícita vía <see cref="En"/>.
/// </summary>
public readonly record struct Dinero(decimal Valor, Moneda Moneda)
{
    public static Dinero Cero(Moneda moneda) => new(0m, moneda);

    public bool EsCero => Valor == 0m;
    public bool EsPositivo => Valor > 0m;
    public bool EsNegativo => Valor < 0m;

    /// <summary>
    /// Convierte a otra moneda aplicando un factor. Si la moneda destino es la misma, se retorna tal cual.
    /// El caller es responsable de obtener un factor válido (del SnapshotTasas al aprobar, o del catálogo vigente).
    /// </summary>
    public Dinero En(Moneda destino, decimal factor)
    {
        if (destino == Moneda)
        {
            return this;
        }

        if (factor <= 0m)
        {
            throw new FactorDeConversionInvalidoException(factor);
        }

        return new Dinero(Valor * factor, destino);
    }

    public static Dinero operator +(Dinero a, Dinero b)
    {
        GuardarMismaMoneda(a, b);
        return new Dinero(a.Valor + b.Valor, a.Moneda);
    }

    public static Dinero operator -(Dinero a, Dinero b)
    {
        GuardarMismaMoneda(a, b);
        return new Dinero(a.Valor - b.Valor, a.Moneda);
    }

    public static Dinero operator *(Dinero a, decimal factor) => new(a.Valor * factor, a.Moneda);
    public static Dinero operator *(decimal factor, Dinero a) => a * factor;

    public static bool operator <(Dinero a, Dinero b)
    {
        GuardarMismaMoneda(a, b);
        return a.Valor < b.Valor;
    }

    public static bool operator >(Dinero a, Dinero b)
    {
        GuardarMismaMoneda(a, b);
        return a.Valor > b.Valor;
    }

    public static bool operator <=(Dinero a, Dinero b) => !(a > b);
    public static bool operator >=(Dinero a, Dinero b) => !(a < b);

    public override string ToString() => $"{Valor:0.####} {Moneda.Codigo}";

    private static void GuardarMismaMoneda(Dinero a, Dinero b)
    {
        if (a.Moneda != b.Moneda)
        {
            throw new MonedasDistintasException(a.Moneda, b.Moneda);
        }
    }
}
