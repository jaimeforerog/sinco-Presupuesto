namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// La tasa pasada a RegistrarTasaDeCambio no es válida (cero o negativa).
/// Una tasa de cambio es un factor de conversión estrictamente positivo.
/// PRE-2 del comando RegistrarTasaDeCambio (slice 06 spec §4 / §6.7).
///
/// Distinta de <see cref="FactorDeConversionInvalidoException"/>: aquélla es para
/// <c>Dinero.En(destino, factor)</c> (operación de conversión, error del caller con
/// dato corrupto); ésta es para registrar una tasa en el catálogo (input del usuario o
/// servicio externo de FX). Mantener excepciones separadas permite mapeos HTTP y mensajes
/// de UX diferentes sin acoplar contextos (slice 06 spec §4 PRE-2).
/// </summary>
public sealed class TasaDeCambioInvalidaException : DominioException
{
    public decimal TasaIntentada { get; }

    public TasaDeCambioInvalidaException(decimal tasaIntentada)
        : base($"La tasa de cambio '{tasaIntentada}' es inválida. Debe ser estrictamente mayor que cero.")
    {
        TasaIntentada = tasaIntentada;
    }
}
