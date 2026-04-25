namespace SincoPresupuesto.Application.CatalogosDeTasas;

/// <summary>
/// Stream id bien-conocido para el agregado <see cref="Domain.CatalogosDeTasas.CatalogoDeTasas"/>.
/// Hay un solo catálogo de tasas por tenant (slice 06 spec §1: singleton, mismo patrón que
/// <see cref="ConfiguracionesTenant.ConfiguracionTenantStreamId"/>). El conjoined multi-tenancy
/// de Marten discrimina por <c>tenant_id</c>, así que cada tenant obtiene su propio stream
/// bajo esta identidad fija.
/// </summary>
public static class CatalogoDeTasasStreamId
{
    public static readonly Guid Value = new("ca7a1060-a5a5-4a5a-a5a5-000000000001");
}
