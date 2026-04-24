using SincoPresupuesto.Domain.SharedKernel;

namespace SincoPresupuesto.Domain.ConfiguracionesTenant.Events;

/// <summary>
/// Evento: la moneda local del tenant fue configurada exitosamente.
/// Almacena la snapshop de la configuración en el momento de emisión.
/// StreamId = TenantId (identificador del agregado ConfiguracionTenant).
/// </summary>
public sealed record MonedaLocalDelTenantConfigurada(
    string TenantId,
    Moneda Moneda,
    DateTimeOffset ConfiguradaEn,
    string ConfiguradaPor);
