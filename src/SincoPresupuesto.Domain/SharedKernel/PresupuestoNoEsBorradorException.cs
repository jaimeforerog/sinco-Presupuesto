using SincoPresupuesto.Domain.Presupuestos;

namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// INV-3 — Se intentó modificar estructuralmente un presupuesto que no está en <see cref="EstadoPresupuesto.Borrador"/>.
/// </summary>
public sealed class PresupuestoNoEsBorradorException : DominioException
{
    public EstadoPresupuesto EstadoActual { get; }

    public PresupuestoNoEsBorradorException(EstadoPresupuesto estadoActual)
        : base($"El presupuesto no está en Borrador. Estado actual: {estadoActual}.")
    {
        EstadoActual = estadoActual;
    }
}
