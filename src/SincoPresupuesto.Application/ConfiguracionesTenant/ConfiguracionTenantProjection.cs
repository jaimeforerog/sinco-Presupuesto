using Marten.Events.Aggregation;
using SincoPresupuesto.Domain.ConfiguracionesTenant.Events;

namespace SincoPresupuesto.Application.ConfiguracionesTenant;

/// <summary>
/// Proyección single-stream que materializa la configuración actual del tenant.
/// Inline: los slices que consultan MonedaLocal (p.ej. default de MonedaBase al crear
/// presupuesto, followup #8) necesitan el documento consistente en la misma transacción.
/// </summary>
public sealed class ConfiguracionTenantProjection : SingleStreamProjection<ConfiguracionTenantActual>
{
    public ConfiguracionTenantActual Create(MonedaLocalDelTenantConfigurada e) => new()
    {
        Id = ConfiguracionTenantStreamId.Value,
        TenantId = e.TenantId,
        MonedaLocal = e.Moneda.Codigo,
        ConfiguradaEn = e.ConfiguradaEn,
        ConfiguradaPor = e.ConfiguradaPor,
    };
}
