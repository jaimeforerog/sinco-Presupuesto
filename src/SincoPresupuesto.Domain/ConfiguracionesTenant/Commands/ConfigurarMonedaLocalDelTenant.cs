using SincoPresupuesto.Domain.SharedKernel;

namespace SincoPresupuesto.Domain.ConfiguracionesTenant.Commands;

/// <summary>
/// Comando: el administrador del tenant configura la moneda local de operación.
/// Se ejecuta una sola vez por tenant (flujo de onboarding).
/// Si se reintenta, lanza TenantYaConfiguradoException.
/// </summary>
public sealed record ConfigurarMonedaLocalDelTenant(
    string TenantId,
    Moneda MonedaLocal,
    string ConfiguradoPor = "sistema");
