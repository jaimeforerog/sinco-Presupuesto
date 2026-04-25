namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// La fecha de vigencia de una tasa de cambio no puede estar en el futuro respecto al "ahora" del sistema.
/// El catálogo modela snapshots históricos — la pre-registración de tasas anunciadas para mañana
/// es un caso distinto que se modelaría con un comando dedicado si surge.
/// PRE-3 del comando RegistrarTasaDeCambio (slice 06 spec §4 / §6.8 / §6.9 caso límite).
/// </summary>
public sealed class FechaDeTasaEnElFuturoException : DominioException
{
    public DateOnly Fecha { get; }
    public DateOnly Hoy { get; }

    public FechaDeTasaEnElFuturoException(DateOnly fecha, DateOnly hoy)
        : base($"La fecha de la tasa ({fecha:yyyy-MM-dd}) está en el futuro respecto a hoy ({hoy:yyyy-MM-dd}). " +
               "Las tasas se registran con fecha de vigencia <= hoy.")
    {
        Fecha = fecha;
        Hoy = hoy;
    }
}
