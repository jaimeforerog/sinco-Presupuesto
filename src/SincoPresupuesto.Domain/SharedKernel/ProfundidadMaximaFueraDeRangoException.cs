namespace SincoPresupuesto.Domain.SharedKernel;

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
