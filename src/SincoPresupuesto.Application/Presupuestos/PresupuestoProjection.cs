using Marten.Events.Aggregation;
using SincoPresupuesto.Domain.Presupuestos;
using SincoPresupuesto.Domain.Presupuestos.Events;

namespace SincoPresupuesto.Application.Presupuestos;

/// <summary>
/// Proyección single-stream que mantiene un documento <see cref="PresupuestoReadModel"/>
/// por cada stream de Presupuesto. Registrada como <c>Inline</c> en Program.cs.
/// </summary>
public sealed class PresupuestoProjection : SingleStreamProjection<PresupuestoReadModel>
{
    public PresupuestoReadModel Create(PresupuestoCreado e) => new()
    {
        Id = e.PresupuestoId,
        TenantId = e.TenantId,
        Codigo = e.Codigo,
        Nombre = e.Nombre,
        PeriodoInicio = e.PeriodoInicio,
        PeriodoFin = e.PeriodoFin,
        MonedaBase = e.MonedaBase.Codigo,
        ProfundidadMaxima = e.ProfundidadMaxima,
        Estado = EstadoPresupuesto.Borrador,
        CreadoEn = e.CreadoEn,
        CreadoPor = e.CreadoPor,
    };
}
