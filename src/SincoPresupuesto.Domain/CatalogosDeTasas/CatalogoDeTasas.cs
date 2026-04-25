using SincoPresupuesto.Domain.CatalogosDeTasas.Commands;
using SincoPresupuesto.Domain.CatalogosDeTasas.Events;
using SincoPresupuesto.Domain.SharedKernel;

namespace SincoPresupuesto.Domain.CatalogosDeTasas;

/// <summary>
/// Agregado event-sourced singleton por tenant que materializa el catálogo de tasas de cambio.
/// Mismo patrón que <see cref="ConfiguracionesTenant.ConfiguracionTenant"/> (slice 02): stream-id
/// bien-conocido por tenant + conjoined multi-tenancy de Marten discrimina por <c>tenant_id</c>.
///
/// Diferencia con slice 02: el comando <see cref="Ejecutar"/> sobre un stream existente
/// NO lanza por "ya configurado" — el catálogo acumula registros a lo largo del tiempo.
///
/// Slice 06 spec §2 / §3 / §12.4.
/// </summary>
public class CatalogoDeTasas
{
    /// <summary>
    /// Identificador del agregado. Requerido por Marten (SingleStreamProjection interna).
    /// Igual al stream-id bien-conocido <c>CatalogoDeTasasStreamId.Value</c>.
    /// Marten lo asigna al rehidratar; en tests unitarios queda como <see cref="Guid.Empty"/>.
    /// </summary>
    public Guid Id { get; set; }

    private readonly List<RegistroDeTasa> _registros = new();

    /// <summary>
    /// Vista del historial completo de registros aplicados al fold.
    /// Solo para verificación en tests — la proyección <c>TasasDeCambioVigentes</c>
    /// consume los eventos directamente.
    /// </summary>
    public IReadOnlyList<RegistroDeTasa> Registros => _registros;

    /// <summary>
    /// Factory: ejecuta el comando RegistrarTasaDeCambio sobre un stream vacío.
    /// Devuelve el evento TasaDeCambioRegistrada.
    /// Valida PRE-1, PRE-2, PRE-3 y normaliza Fuente / RegistradoPor.
    /// </summary>
    public static TasaDeCambioRegistrada Crear(RegistrarTasaDeCambio cmd, DateTimeOffset ahora)
    {
        return ValidarYConstruir(cmd, ahora);
    }

    /// <summary>
    /// Ejecuta el comando RegistrarTasaDeCambio sobre el agregado reconstruido (fold anterior).
    /// Acumula registros (no lanza por "ya configurado" — diferencia con slice 02).
    /// Aplica las mismas precondiciones / normalizaciones que <see cref="Crear"/>.
    /// </summary>
    public TasaDeCambioRegistrada Ejecutar(RegistrarTasaDeCambio cmd, DateTimeOffset ahora)
    {
        return ValidarYConstruir(cmd, ahora);
    }

    /// <summary>
    /// Apply: agrega un <see cref="RegistroDeTasa"/> derivado del evento al historial (fold).
    /// </summary>
    public void Apply(TasaDeCambioRegistrada e)
    {
        ArgumentNullException.ThrowIfNull(e);

        _registros.Add(new RegistroDeTasa(
            MonedaDesde: e.MonedaDesde,
            MonedaHacia: e.MonedaHacia,
            Tasa: e.Tasa,
            Fecha: e.Fecha,
            Fuente: e.Fuente,
            RegistradaEn: e.RegistradaEn,
            RegistradaPor: e.RegistradaPor));
    }

    /// <summary>
    /// Validación + normalización compartida por <see cref="Crear"/> y <see cref="Ejecutar"/>.
    /// Orden: PRE-1 (monedas distintas) → PRE-2 (tasa &gt; 0) → PRE-3 (fecha &lt;= hoy) → PRE-4 (normalización).
    /// </summary>
    private static TasaDeCambioRegistrada ValidarYConstruir(RegistrarTasaDeCambio cmd, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (cmd.MonedaDesde == cmd.MonedaHacia)
            throw new MonedasIgualesEnTasaException(cmd.MonedaDesde);

        if (cmd.Tasa <= 0m)
            throw new TasaDeCambioInvalidaException(cmd.Tasa);

        var hoy = DateOnly.FromDateTime(ahora.UtcDateTime);
        if (cmd.Fecha > hoy)
            throw new FechaDeTasaEnElFuturoException(cmd.Fecha, hoy);

        var fuenteNormalizada = string.IsNullOrWhiteSpace(cmd.Fuente)
            ? null
            : cmd.Fuente.Trim();

        var registradoPorNormalizado = string.IsNullOrWhiteSpace(cmd.RegistradoPor)
            ? "sistema"
            : cmd.RegistradoPor.Trim();

        return new TasaDeCambioRegistrada(
            MonedaDesde: cmd.MonedaDesde,
            MonedaHacia: cmd.MonedaHacia,
            Tasa: cmd.Tasa,
            Fecha: cmd.Fecha,
            Fuente: fuenteNormalizada,
            RegistradaEn: ahora,
            RegistradaPor: registradoPorNormalizado);
    }
}

/// <summary>
/// Entrada del historial expuesta por el agregado para verificación en tests de fold.
/// Réplica de los campos del evento <see cref="TasaDeCambioRegistrada"/>. Slice 06 spec §12.4.
/// </summary>
public sealed record RegistroDeTasa(
    Moneda MonedaDesde,
    Moneda MonedaHacia,
    decimal Tasa,
    DateOnly Fecha,
    string? Fuente,
    DateTimeOffset RegistradaEn,
    string RegistradaPor);
