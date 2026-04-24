namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// No se encontró ningún presupuesto con el <see cref="PresupuestoId"/> indicado dentro del tenant actual.
/// </summary>
public sealed class PresupuestoNoEncontradoException : DominioException
{
    public Guid PresupuestoId { get; }

    public PresupuestoNoEncontradoException(Guid presupuestoId)
        : base($"Presupuesto '{presupuestoId}' no existe en el tenant actual.")
    {
        PresupuestoId = presupuestoId;
    }
}
