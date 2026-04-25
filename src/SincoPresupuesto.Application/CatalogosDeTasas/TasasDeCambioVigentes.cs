namespace SincoPresupuesto.Application.CatalogosDeTasas;

/// <summary>
/// Read model con la tasa más reciente registrada por par de monedas. Slice 06 spec §8.
/// Un documento por tenant (Id = stream-id bien-conocido del agregado <c>CatalogoDeTasas</c>).
/// </summary>
public sealed class TasasDeCambioVigentes
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Lista de tasas vigentes (la última registrada por par). Plana para serialización JSON
    /// — la clave compuesta vive en los campos <c>MonedaDesde</c>/<c>MonedaHacia</c>.
    /// </summary>
    public List<TasaVigente> Tasas { get; set; } = new();
}

/// <summary>
/// Tasa vigente para un par <c>(MonedaDesde, MonedaHacia)</c>. La proyección reemplaza el
/// elemento existente cuando llega un nuevo evento del mismo par (last-write-wins, INV-CT-1).
/// </summary>
public sealed class TasaVigente
{
    public string MonedaDesde { get; set; } = string.Empty;
    public string MonedaHacia { get; set; } = string.Empty;
    public decimal Tasa { get; set; }
    public DateOnly Fecha { get; set; }
    public string? Fuente { get; set; }
    public DateTimeOffset RegistradaEn { get; set; }
    public string RegistradaPor { get; set; } = string.Empty;
}
