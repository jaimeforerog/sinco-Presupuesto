using SincoPresupuesto.Domain.ConfiguracionesTenant.Commands;
using SincoPresupuesto.Domain.ConfiguracionesTenant.Events;
using SincoPresupuesto.Domain.SharedKernel;

namespace SincoPresupuesto.Domain.ConfiguracionesTenant;

/// <summary>
/// Agregado event-sourced que representa la configuración del tenant en el BC de Gestión Presupuestal.
/// StreamId = TenantId (relación uno-a-uno).
///
/// Responsabilidades:
/// - Aceptar el primer comando ConfigurarMonedaLocalDelTenant (stream vacío).
/// - Rechazar reintentos con TenantYaConfiguradoException.
/// - Aplicar el evento MonedaLocalDelTenantConfigurada vía fold.
///
/// La MonedaLocal es inmutable tras la configuración inicial.
/// </summary>
public class ConfiguracionTenant
{
    public string? TenantId { get; private set; }
    public Moneda? MonedaLocal { get; private set; }
    public DateTimeOffset? ConfiguradaEn { get; private set; }
    public string? ConfiguradaPor { get; private set; }

    /// <summary>
    /// Factory: ejecuta el comando ConfigurarMonedaLocalDelTenant sobre un stream vacío.
    /// Devuelve el evento MonedaLocalDelTenantConfigurada.
    /// Valida PRE-1: TenantId no vacío.
    /// Valida PRE-2: normaliza ConfiguradoPor vacío a "sistema".
    /// </summary>
    public static MonedaLocalDelTenantConfigurada Create(
        ConfigurarMonedaLocalDelTenant cmd,
        DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(cmd.TenantId))
        {
            throw new CampoRequeridoException("TenantId");
        }

        var tenantIdNormalizado = cmd.TenantId.Trim();
        var configuradoPorNormalizado = string.IsNullOrWhiteSpace(cmd.ConfiguradoPor)
            ? "sistema"
            : cmd.ConfiguradoPor.Trim();

        return new MonedaLocalDelTenantConfigurada(
            TenantId: tenantIdNormalizado,
            Moneda: cmd.MonedaLocal,
            ConfiguradaEn: ahora,
            ConfiguradaPor: configuradoPorNormalizado);
    }

    /// <summary>
    /// Ejecuta el comando sobre el agregado reconstruido (fold anterior).
    /// Verifica INV-NEW-1: si ya existe configuración, lanza TenantYaConfiguradoException.
    /// La ejecución sobre un stream existente siempre falla porque el tenant ya fue configurado.
    /// Este método se invoca exclusivamente cuando hay un fold previo, por lo que TenantId siempre
    /// será no-nulo (el agregado está ya configurado); de ahí que siempre lanza.
    /// </summary>
    public MonedaLocalDelTenantConfigurada Ejecutar(
        ConfigurarMonedaLocalDelTenant cmd,
        DateTimeOffset ahora)
    {
        throw new TenantYaConfiguradoException(TenantId!, MonedaLocal!.Value);
    }

    /// <summary>
    /// Apply: aplica el evento MonedaLocalDelTenantConfigurada al estado del agregado (fold).
    /// Actualiza todas las propiedades del estado con los valores del evento.
    /// </summary>
    public void Apply(MonedaLocalDelTenantConfigurada evt)
    {
        TenantId = evt.TenantId;
        MonedaLocal = evt.Moneda;
        ConfiguradaEn = evt.ConfiguradaEn;
        ConfiguradaPor = evt.ConfiguradaPor;
    }
}
