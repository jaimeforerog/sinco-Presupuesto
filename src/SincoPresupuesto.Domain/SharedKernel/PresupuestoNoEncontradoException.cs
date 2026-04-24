namespace SincoPresupuesto.Domain.SharedKernel;

public sealed class PresupuestoNoEncontradoException : DominioException
{
    public Guid PresupuestoId { get; }

    public PresupuestoNoEncontradoException(Guid presupuestoId)
        : base($"Presupuesto '{presupuestoId}' no existe en el tenant actual.")
    {
        PresupuestoId = presupuestoId;
    }
}
