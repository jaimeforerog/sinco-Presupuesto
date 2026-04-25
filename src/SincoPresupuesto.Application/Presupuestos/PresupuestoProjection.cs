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

    public void Apply(RubroAgregado e, PresupuestoReadModel model)
    {
        var nivel = e.RubroPadreId is Guid padreId
            ? model.Rubros.First(r => r.RubroId == padreId).Nivel + 1
            : 1;

        model.Rubros.Add(new RubroReadModel
        {
            RubroId = e.RubroId,
            Codigo = e.Codigo,
            Nombre = e.Nombre,
            PadreId = e.RubroPadreId,
            Nivel = nivel,
            MontoValor = 0m,
            MontoMoneda = model.MonedaBase,
        });
    }

    public void Apply(MontoAsignadoARubro e, PresupuestoReadModel model)
    {
        var rubro = model.Rubros.First(r => r.RubroId == e.RubroId);
        rubro.MontoValor = e.Monto.Valor;
        rubro.MontoMoneda = e.Monto.Moneda.Codigo;
    }

    public void Apply(PresupuestoAprobado e, PresupuestoReadModel model)
    {
        model.Estado = EstadoPresupuesto.Aprobado;
        model.MontoTotalValor = e.MontoTotal.Valor;
        model.MontoTotalMoneda = e.MontoTotal.Moneda.Codigo;
        model.SnapshotTasas = e.SnapshotTasas.ToDictionary(
            kvp => kvp.Key.Codigo,
            kvp => kvp.Value);
        model.AprobadoEn = e.AprobadoEn;
        model.AprobadoPor = e.AprobadoPor;
    }
}
