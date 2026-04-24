namespace SincoPresupuesto.Domain.Presupuestos;

/// <summary>
/// Entity que vive dentro del agregado <see cref="Presupuesto"/>. Representa un nodo del
/// árbol jerárquico de rubros. Inmutable tras construcción: sólo se materializa desde el
/// fold de <see cref="Events.RubroAgregado"/>. Las mutaciones futuras (mover rubro,
/// renombrar, etc.) emitirán eventos propios.
/// </summary>
public sealed class Rubro
{
    public Guid Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public Guid? PadreId { get; init; }
    public int Nivel { get; init; }
}
