using Marten;
using SincoPresupuesto.Domain.ConfiguracionesTenant;
using SincoPresupuesto.Domain.ConfiguracionesTenant.Commands;
using SincoPresupuesto.Domain.ConfiguracionesTenant.Events;

namespace SincoPresupuesto.Application.ConfiguracionesTenant;

/// <summary>
/// Handler para <see cref="ConfigurarMonedaLocalDelTenant"/>.
/// - Si el stream está vacío: invoca <see cref="ConfiguracionTenant.Create"/> y hace StartStream.
/// - Si el stream ya existe: invoca <see cref="ConfiguracionTenant.Ejecutar"/> que lanza
///   <see cref="SharedKernel.TenantYaConfiguradoException"/> (mapeada a 409 por el exception handler).
/// </summary>
public static class ConfigurarMonedaLocalDelTenantHandler
{
    public static async Task<MonedaLocalDelTenantConfigurada> Handle(
        ConfigurarMonedaLocalDelTenant cmd,
        IDocumentSession session,
        TimeProvider clock,
        CancellationToken ct)
    {
        var streamId = ConfiguracionTenantStreamId.Value;
        var ahora = clock.GetUtcNow();

        var existente = await session.Events.AggregateStreamAsync<ConfiguracionTenant>(streamId, token: ct);

        MonedaLocalDelTenantConfigurada evento;
        if (existente is null)
        {
            evento = ConfiguracionTenant.Crear(cmd, ahora);
            session.Events.StartStream<ConfiguracionTenant>(streamId, evento);
        }
        else
        {
            // Siempre lanza TenantYaConfiguradoException por diseño (spec §6.4).
            evento = existente.Ejecutar(cmd, ahora);
            session.Events.Append(streamId, evento);
        }

        await session.SaveChangesAsync(ct);
        return evento;
    }
}
