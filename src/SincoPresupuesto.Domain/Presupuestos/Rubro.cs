using SincoPresupuesto.Domain.SharedKernel;

namespace SincoPresupuesto.Domain.Presupuestos;

/// <summary>
/// Entity que vive dentro del agregado <see cref="Presupuesto"/>. Representa un nodo del
/// árbol jerárquico de rubros. Se materializa desde el fold de <see cref="Events.RubroAgregado"/>.
/// El campo <see cref="Monto"/> lo muta el fold de <see cref="Events.MontoAsignadoARubro"/>
/// (slice 04 §12.2 / §12.5) vía <see cref="AsignarMonto(Dinero)"/>. Las demás mutaciones
/// futuras emitirán eventos propios.
/// </summary>
public sealed class Rubro
{
    public Guid Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public Guid? PadreId { get; init; }
    public int Nivel { get; init; }

    /// <summary>
    /// Monto asignado al rubro. El setter es <c>internal</c> para que solo el agregado
    /// <see cref="Presupuesto"/> pueda inicializarlo vía object initializer en
    /// <c>Apply(RubroAgregado)</c>; el camino público para reasignarlo es
    /// <see cref="AsignarMonto(Dinero)"/>, invocado por <c>Apply(MontoAsignadoARubro)</c>.
    /// </summary>
    public Dinero Monto { get; internal set; }

    /// <summary>
    /// Reemplaza el monto del rubro. Uso exclusivo del fold del agregado
    /// (<c>Presupuesto.Apply(MontoAsignadoARubro)</c>) — expresa explícitamente la intención
    /// de "mutar como efecto de un evento ya validado" sin exponer un setter público.
    /// </summary>
    internal void AsignarMonto(Dinero monto) => Monto = monto;
}
