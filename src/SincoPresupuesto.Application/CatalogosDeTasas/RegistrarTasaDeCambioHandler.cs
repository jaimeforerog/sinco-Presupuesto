using Marten;
using SincoPresupuesto.Domain.CatalogosDeTasas;
using SincoPresupuesto.Domain.CatalogosDeTasas.Events;
using DomainCmd = SincoPresupuesto.Domain.CatalogosDeTasas.Commands.RegistrarTasaDeCambio;

namespace SincoPresupuesto.Application.CatalogosDeTasas;

/// <summary>
/// Handler para <see cref="DomainCmd"/>. Patrón análogo a
/// <see cref="ConfiguracionesTenant.ConfigurarMonedaLocalDelTenantHandler"/>: si el stream
/// está vacío usa <see cref="CatalogoDeTasas.Crear"/> + <c>StartStream</c>; si ya existe
/// usa <see cref="CatalogoDeTasas.Ejecutar"/> + <c>Append</c>. A diferencia de
/// `ConfiguracionTenant`, la rama existente NO lanza — el catálogo acumula registros.
/// </summary>
public static class RegistrarTasaDeCambioHandler
{
    public static async Task<TasaDeCambioRegistrada> Handle(
        DomainCmd cmd,
        IDocumentSession session,
        TimeProvider clock,
        CancellationToken ct)
    {
        var streamId = CatalogoDeTasasStreamId.Value;
        var ahora = clock.GetUtcNow();

        var existente = await session.Events.AggregateStreamAsync<CatalogoDeTasas>(streamId, token: ct);

        TasaDeCambioRegistrada evento;
        if (existente is null)
        {
            evento = CatalogoDeTasas.Crear(cmd, ahora);
            session.Events.StartStream<CatalogoDeTasas>(streamId, evento);
        }
        else
        {
            evento = existente.Ejecutar(cmd, ahora);
            session.Events.Append(streamId, evento);
        }

        await session.SaveChangesAsync(ct);
        return evento;
    }
}
