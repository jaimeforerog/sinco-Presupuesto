namespace SincoPresupuesto.Domain.Presupuestos.Events;

/// <summary>
/// Hecho de dominio: se agregó un rubro al árbol del presupuesto.
/// Payload alineado al event-storming §5.
/// </summary>
/// <param name="PresupuestoId">Id del stream del agregado donde vive el rubro.</param>
/// <param name="RubroId">Id único del rubro dentro del presupuesto.</param>
/// <param name="Codigo">Código jerárquico canónico (formato ^\d{2}(\.\d{2}){0,14}$).</param>
/// <param name="Nombre">Nombre legible del rubro.</param>
/// <param name="RubroPadreId">Id del rubro padre o null si es raíz.</param>
/// <param name="AgregadoEn">Timestamp UTC en el que se agregó el rubro.</param>
public sealed record RubroAgregado(
    Guid PresupuestoId,
    Guid RubroId,
    string Codigo,
    string Nombre,
    Guid? RubroPadreId,
    DateTimeOffset AgregadoEn);
