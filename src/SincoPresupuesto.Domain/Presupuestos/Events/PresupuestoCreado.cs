using SincoPresupuesto.Domain.SharedKernel;

namespace SincoPresupuesto.Domain.Presupuestos.Events;

/// <summary>
/// Hecho de dominio: se creó un presupuesto nuevo en estado Borrador.
/// </summary>
/// <param name="PresupuestoId">Identificador del stream del agregado.</param>
/// <param name="TenantId">Tenant al que pertenece (multi-tenant conjoint de Marten).</param>
/// <param name="Codigo">Código de negocio, único por (TenantId, Codigo, PeriodoFiscal).</param>
/// <param name="Nombre">Nombre legible.</param>
/// <param name="PeriodoInicio">Inicio del periodo fiscal (inclusive).</param>
/// <param name="PeriodoFin">Fin del periodo fiscal (inclusive).</param>
/// <param name="MonedaBase">Moneda base del presupuesto (inmutable tras la creación).</param>
/// <param name="ProfundidadMaxima">Profundidad máxima del árbol de rubros (tope rígido 15).</param>
/// <param name="CreadoEn">Timestamp UTC de creación.</param>
/// <param name="CreadoPor">Usuario que creó el presupuesto.</param>
public sealed record PresupuestoCreado(
    Guid PresupuestoId,
    string TenantId,
    string Codigo,
    string Nombre,
    DateOnly PeriodoInicio,
    DateOnly PeriodoFin,
    Moneda MonedaBase,
    int ProfundidadMaxima,
    DateTimeOffset CreadoEn,
    string CreadoPor);
