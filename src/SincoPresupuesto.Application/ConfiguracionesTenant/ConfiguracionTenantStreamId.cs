namespace SincoPresupuesto.Application.ConfiguracionesTenant;

/// <summary>
/// Stream id bien-conocido para el agregado <c>ConfiguracionTenant</c>.
/// Hay una sola configuración por tenant (spec slice 02 §1: StreamId coincide con TenantId).
/// El conjoined multi-tenancy de Marten discrimina por <c>tenant_id</c>, así que cada tenant
/// obtiene su propio stream bajo esta identidad fija.
/// </summary>
public static class ConfiguracionTenantStreamId
{
    public static readonly Guid Value = new("cf61f2d7-a9b7-4d6f-81a1-000000000001");
}
