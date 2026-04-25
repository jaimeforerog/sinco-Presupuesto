using System.Text.Json.Serialization;

namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// Código de moneda ISO 4217 (tres letras mayúsculas, p. ej. "COP", "USD", "EUR").
/// Value object inmutable.
/// Valida contra una lista embebida de códigos ISO 4217 vigentes.
/// </summary>
public readonly record struct Moneda
{
    /// <summary>
    /// Lista mínima de códigos ISO 4217 soportados en el MVP.
    /// Puede ampliarse en futuras versiones (followup #9).
    /// </summary>
    private static readonly HashSet<string> CodigosIso4217Validos = new()
    {
        "AED", "AFN", "ALL", "AMD", "ANG", "AOA", "ARS", "AUD", "AWG", "AZN",
        "BAM", "BBD", "BDT", "BGN", "BHD", "BIF", "BMD", "BND", "BOB", "BRL", "BSD", "BTC", "BTN", "BWP", "BYN", "BZD",
        "CAD", "CDF", "CHE", "CHF", "CHW", "CLF", "CLP", "CNH", "CNY", "COP", "COU", "CRC", "CUC", "CUP", "CVE", "CZK",
        "DJF", "DKK", "DOP", "DZD",
        "EGP", "ERN", "ETB", "EUR",
        "FJD", "FKP",
        "GBP", "GEL", "GHS", "GIP", "GMD", "GNF", "GTQ", "GYD",
        "HKD", "HNL", "HRK", "HTG", "HUF",
        "IDR", "ILS", "INR", "IQD", "IRR", "ISK",
        "JMD", "JOD", "JPY",
        "KES", "KGS", "KHR", "KMF", "KPW", "KRW", "KWD", "KYD", "KZT",
        "LAK", "LBP", "LKR", "LRD", "LSL", "LYD",
        "MAD", "MDL", "MGA", "MKD", "MMK", "MNT", "MOP", "MRU", "MUR", "MVR", "MWK", "MXN", "MYR", "MZN",
        "NAD", "NGN", "NIO", "NOK", "NPR", "NZD",
        "OMR",
        "PAB", "PEN", "PGK", "PHP", "PKR", "PLN", "PYG",
        "QAR",
        "RON", "RSD", "RUB", "RWF",
        "SAR", "SBD", "SCR", "SDG", "SEK", "SGD", "SHP", "SLE", "SLL", "SOS", "SRD", "SSP", "STN", "SYP", "SZL",
        "THB", "TJS", "TMT", "TND", "TOP", "TRY", "TTD", "TWD", "TZS",
        "UAH", "UGX", "USD", "USN", "UYI", "UYU", "UYW", "UZS",
        "VED", "VES", "VND", "VUV",
        "WST",
        "XAF", "XAG", "XAU", "XBA", "XBB", "XBC", "XBD", "XCD", "XDR", "XOF", "XPD", "XPF", "XPT", "XSU", "XTS", "XUA", "XXX",
        "YER",
        "ZAR", "ZMW", "ZWL"
    };

    public string Codigo { get; }

    /// <summary>
    /// Cardinalidad de la lista ISO 4217 embebida. Expuesta para tests que validen
    /// que la lista no se redujo accidentalmente a un set trivial (spec slice 00 §6.17).
    /// </summary>
    public static int CantidadCodigosIso4217Soportados => CodigosIso4217Validos.Count;

    /// <summary>
    /// Constructor seleccionado por System.Text.Json para deserialización.
    /// Sin <see cref="JsonConstructorAttribute"/>, STJ usa el constructor default
    /// implícito del struct (yielding <c>Codigo = null</c>) porque la propiedad
    /// es get-only y no puede setearse post-construcción. Followup #23: bug
    /// descubierto por el visor de eventos (spec <c>slices/_obs-visor-eventos</c>).
    /// El parámetro matchea case-insensitive contra "Codigo"/"codigo" en el JSON.
    /// </summary>
    [JsonConstructor]
    public Moneda(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            throw new CodigoMonedaInvalidoException(codigo ?? string.Empty);
        }

        var normalizado = codigo.Trim().ToUpperInvariant();
        if (normalizado.Length != 3 || !normalizado.All(c => c >= 'A' && c <= 'Z'))
        {
            throw new CodigoMonedaInvalidoException(normalizado);
        }

        if (!CodigosIso4217Validos.Contains(normalizado))
        {
            throw new CodigoMonedaInvalidoException(normalizado);
        }

        Codigo = normalizado;
    }

    public override string ToString() => Codigo;

    public static implicit operator string(Moneda m) => m.Codigo;

    // Atajos para las monedas más comunes en el mercado objetivo.
    public static readonly Moneda COP = new("COP");
    public static readonly Moneda USD = new("USD");
    public static readonly Moneda EUR = new("EUR");
    public static readonly Moneda MXN = new("MXN");
    public static readonly Moneda CLP = new("CLP");
    public static readonly Moneda PEN = new("PEN");
    public static readonly Moneda ARS = new("ARS");
}
