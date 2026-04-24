namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// INV-NEW-SLICE04-1 — Se intentó asignar monto a un rubro que ya tiene hijos (Agrupador).
/// Un Agrupador no tiene monto directo: su total se calcula como la suma de sus hijos
/// (hotspots §1). Spec slice 04 §5, §6.7 y §12.1.
/// </summary>
public sealed class RubroEsAgrupadorException : DominioException
{
    public Guid RubroId { get; }

    public RubroEsAgrupadorException(Guid rubroId)
        : base($"El rubro con Id '{rubroId}' es un Agrupador (tiene hijos) y no admite asignación directa de monto.")
    {
        RubroId = rubroId;
    }
}
