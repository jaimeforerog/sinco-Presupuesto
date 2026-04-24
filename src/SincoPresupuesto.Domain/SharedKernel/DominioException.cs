namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// Clase base para todas las excepciones del dominio de Sinco Presupuesto.
/// Cualquier violación de precondición, invariante o regla de negocio lanza una subclase de ésta.
/// El reviewer verifica que los tests aserten **el tipo** de excepción, no el mensaje,
/// para evitar acoplamiento a texto (mensajes pueden internacionalizarse o reformularse
/// sin cambiar el comportamiento).
/// </summary>
public abstract class DominioException : Exception
{
    protected DominioException(string mensaje) : base(mensaje) { }
    protected DominioException(string mensaje, Exception? causa) : base(mensaje, causa) { }
}
