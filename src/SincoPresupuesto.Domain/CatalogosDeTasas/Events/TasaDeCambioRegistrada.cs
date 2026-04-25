using SincoPresupuesto.Domain.SharedKernel;

namespace SincoPresupuesto.Domain.CatalogosDeTasas.Events;

/// <summary>
/// Evento: una tasa de cambio fue registrada exitosamente en el catálogo del tenant.
/// Append-only: una vez emitido no se modifica ni se borra (INV-NEW-CT-2).
/// Las correcciones se modelan como nuevos eventos del mismo tipo en MVP
/// (la proyección "última gana" — INV-CT-1) — slice 06 spec §3 / §5.
/// </summary>
public sealed record TasaDeCambioRegistrada(
    Moneda MonedaDesde,
    Moneda MonedaHacia,
    decimal Tasa,
    DateOnly Fecha,
    string? Fuente,
    DateTimeOffset RegistradaEn,
    string RegistradaPor);
