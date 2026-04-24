namespace SincoPresupuesto.Domain.Presupuestos;

/// <summary>
/// Estados del ciclo de vida de un Presupuesto.
/// Transiciones válidas: Borrador → Aprobado → Activo → Cerrado.
/// </summary>
public enum EstadoPresupuesto
{
    Borrador = 0,
    Aprobado = 1,
    Activo = 2,
    Cerrado = 3,
}
