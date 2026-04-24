using SincoPresupuesto.Domain.SharedKernel;

namespace SincoPresupuesto.Domain.Presupuestos.Commands;

/// <summary>
/// Intención: crear un presupuesto nuevo en estado Borrador para un tenant.
/// La validación de unicidad (TenantId, Codigo, PeriodoFiscal) se delega a la proyección
/// inline <c>PresupuestoCodigoIndex</c> con un UniqueIndex compuesto.
/// </summary>
public sealed record CrearPresupuesto(
    string TenantId,
    string Codigo,
    string Nombre,
    DateOnly PeriodoInicio,
    DateOnly PeriodoFin,
    Moneda MonedaBase,
    int ProfundidadMaxima = 10,
    string CreadoPor = "sistema");
