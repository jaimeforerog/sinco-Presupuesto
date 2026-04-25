using SincoPresupuesto.Domain.SharedKernel;

namespace SincoPresupuesto.Domain.CatalogosDeTasas.Commands;

/// <summary>
/// Comando: registrar una tasa de cambio entre dos monedas para una fecha dada.
/// El catálogo de tasas es un agregado event-sourced singleton por tenant
/// (mismo patrón que ConfiguracionTenant — slice 02).
/// Slice 06 spec §2.
/// </summary>
public sealed record RegistrarTasaDeCambio(
    Moneda MonedaDesde,
    Moneda MonedaHacia,
    decimal Tasa,
    DateOnly Fecha,
    string? Fuente = null,
    string RegistradoPor = "sistema");
