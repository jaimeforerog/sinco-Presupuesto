using SincoPresupuesto.Domain.Presupuestos;
using SincoPresupuesto.Domain.SharedKernel;

namespace SincoPresupuesto.Application.Presupuestos;

/// <summary>
/// Read model plano para listados y detalle del presupuesto.
/// Proyectado inline por Marten a partir del stream.
/// </summary>
public sealed class PresupuestoReadModel
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public DateOnly PeriodoInicio { get; set; }
    public DateOnly PeriodoFin { get; set; }
    public string MonedaBase { get; set; } = string.Empty;
    public int ProfundidadMaxima { get; set; }
    public EstadoPresupuesto Estado { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public string CreadoPor { get; set; } = string.Empty;
}
