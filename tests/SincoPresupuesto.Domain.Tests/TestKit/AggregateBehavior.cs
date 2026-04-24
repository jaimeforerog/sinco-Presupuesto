namespace SincoPresupuesto.Domain.Tests.TestKit;

/// <summary>
/// Helper genérico para tests event-sourced de agregados.
/// Estilo Given/When/Then: "dado un historial de eventos, cuando se ejecuta un comando,
/// entonces se emiten ciertos eventos (o se lanza una excepción de dominio)".
///
/// Para comandos que crean el stream (p.ej. CrearPresupuesto), Given es vacío y When
/// invoca la fábrica estática del agregado.
///
/// Para comandos que actúan sobre un stream existente (slices futuros), Given contiene
/// los eventos previos, que se aplican vía reflexión sobre los métodos <c>Apply(TEvent)</c>.
///
/// Genérico para cualquier agregado T: busca por reflexión los métodos Apply(TEvent)
/// presentes en la clase del agregado y los invoca en orden.
/// </summary>
public static class AggregateBehavior<T> where T : new()
{
    /// <summary>
    /// Reconstruye el agregado aplicando el historial de eventos dado (fold).
    /// Busca por reflexión un método <c>Apply(TEvent)</c> por cada tipo de evento.
    /// </summary>
    public static T Reconstruir(params object[] historial)
    {
        var agg = new T();
        foreach (var evento in historial)
        {
            var apply = typeof(T).GetMethod(
                "Apply",
                [evento.GetType()])
                ?? throw new InvalidOperationException(
                    $"{typeof(T).Name} no tiene Apply({evento.GetType().Name}). " +
                    "¿Olvidaste agregar el manejador del evento?");
            apply.Invoke(agg, [evento]);
        }
        return agg;
    }
}
