namespace SincoPresupuesto.Domain.SharedKernel;

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
