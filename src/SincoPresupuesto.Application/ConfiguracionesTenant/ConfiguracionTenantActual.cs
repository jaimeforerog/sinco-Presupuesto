namespace SincoPresupuesto.Application.ConfiguracionesTenant;

/// <summary>
/// Read model plano de la configuración actual del tenant en el BC de Gestión Presupuestal.
/// Un documento por tenant. Proyectado inline desde el stream de <c>ConfiguracionTenant</c>.
/// </summary>
public sealed class ConfiguracionTenantActual
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string MonedaLocal { get; set; } = string.Empty;
    public DateTimeOffset ConfiguradaEn { get; set; }
    public string ConfiguradaPor { get; set; } = string.Empty;
}
