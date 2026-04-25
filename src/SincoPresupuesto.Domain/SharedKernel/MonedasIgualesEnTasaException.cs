namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// Se intentó registrar una tasa de cambio donde MonedaDesde == MonedaHacia.
/// Una conversión MISMA→MISMA es identidad por definición — no tiene sentido financiero.
/// PRE-1 del comando RegistrarTasaDeCambio (slice 06 spec §4 / §6.6 / §6.10).
/// </summary>
public sealed class MonedasIgualesEnTasaException : DominioException
{
    public Moneda Moneda { get; }

    public MonedasIgualesEnTasaException(Moneda moneda)
        : base($"No se puede registrar una tasa de cambio donde MonedaDesde y MonedaHacia coinciden ({moneda.Codigo}).")
    {
        Moneda = moneda;
    }
}
