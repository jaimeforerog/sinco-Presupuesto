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

/// <summary>
/// Un campo obligatorio del comando llegó nulo o vacío.
/// </summary>
public sealed class CampoRequeridoException : DominioException
{
    public string NombreCampo { get; }

    public CampoRequeridoException(string nombreCampo)
        : base($"El campo '{nombreCampo}' es obligatorio.")
    {
        NombreCampo = nombreCampo;
    }
}

/// <summary>
/// El periodo fiscal del presupuesto quedó invertido (fin anterior al inicio).
/// </summary>
public sealed class PeriodoInvalidoException : DominioException
{
    public DateOnly PeriodoInicio { get; }
    public DateOnly PeriodoFin { get; }

    public PeriodoInvalidoException(DateOnly inicio, DateOnly fin)
        : base($"El fin del periodo ({fin:yyyy-MM-dd}) no puede ser anterior al inicio ({inicio:yyyy-MM-dd}).")
    {
        PeriodoInicio = inicio;
        PeriodoFin = fin;
    }
}

/// <summary>
/// La profundidad máxima del árbol de rubros está fuera del rango aceptado [1, máximo absoluto].
/// </summary>
public sealed class ProfundidadMaximaFueraDeRangoException : DominioException
{
    public int Valor { get; }
    public int MinimoInclusivo { get; }
    public int MaximoInclusivo { get; }

    public ProfundidadMaximaFueraDeRangoException(int valor, int minimoInclusivo, int maximoInclusivo)
        : base($"ProfundidadMaxima debe estar entre {minimoInclusivo} y {maximoInclusivo}. Valor recibido: {valor}.")
    {
        Valor = valor;
        MinimoInclusivo = minimoInclusivo;
        MaximoInclusivo = maximoInclusivo;
    }
}
