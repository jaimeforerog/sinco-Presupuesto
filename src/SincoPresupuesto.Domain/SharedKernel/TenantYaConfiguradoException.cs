namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// Se intentó configurar la moneda local de un tenant que ya fue configurado.
/// La moneda de un tenant es inmutable después de la configuración inicial.
/// </summary>
public sealed class TenantYaConfiguradoException : DominioException
{
    public string TenantId { get; }
    public Moneda MonedaLocalActual { get; }

    public TenantYaConfiguradoException(string tenantId, Moneda monedaLocalActual)
        : base($"El tenant '{tenantId}' ya fue configurado con moneda {monedaLocalActual.Codigo}. " +
               "La moneda es inmutable bajo este comando.")
    {
        TenantId = tenantId;
        MonedaLocalActual = monedaLocalActual;
    }
}
