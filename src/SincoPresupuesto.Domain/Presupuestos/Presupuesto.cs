using SincoPresupuesto.Domain.Presupuestos.Commands;
using SincoPresupuesto.Domain.Presupuestos.Events;
using SincoPresupuesto.Domain.SharedKernel;

namespace SincoPresupuesto.Domain.Presupuestos;

/// <summary>
/// Agregado raíz. Estado reconstruido a partir del stream de eventos por Marten
/// (convención <c>Apply(TEvent)</c>).
/// </summary>
public sealed class Presupuesto
{
    public const int ProfundidadMaximaAbsoluta = 15;

    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public DateOnly PeriodoInicio { get; private set; }
    public DateOnly PeriodoFin { get; private set; }
    public Moneda MonedaBase { get; private set; }
    public int ProfundidadMaxima { get; private set; }
    public EstadoPresupuesto Estado { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }
    public string CreadoPor { get; private set; } = string.Empty;

    // Constructor sin parámetros requerido por Marten para materialización.
    public Presupuesto() { }

    /// <summary>
    /// Fábrica de dominio: valida las invariantes y devuelve el evento <see cref="PresupuestoCreado"/>.
    /// No muta estado — esa es responsabilidad de <see cref="Apply(PresupuestoCreado)"/>.
    /// Lanza subclases de <see cref="DominioException"/> ante violaciones; los tests deben
    /// asertar sobre el tipo y propiedades, no sobre el mensaje.
    /// </summary>
    public static PresupuestoCreado Create(CrearPresupuesto cmd, Guid presupuestoId, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (string.IsNullOrWhiteSpace(cmd.TenantId))
        {
            throw new CampoRequeridoException(nameof(cmd.TenantId));
        }

        if (string.IsNullOrWhiteSpace(cmd.Codigo))
        {
            throw new CampoRequeridoException(nameof(cmd.Codigo));
        }

        if (string.IsNullOrWhiteSpace(cmd.Nombre))
        {
            throw new CampoRequeridoException(nameof(cmd.Nombre));
        }

        if (cmd.PeriodoFin < cmd.PeriodoInicio)
        {
            throw new PeriodoInvalidoException(cmd.PeriodoInicio, cmd.PeriodoFin);
        }

        if (cmd.ProfundidadMaxima < 1 || cmd.ProfundidadMaxima > ProfundidadMaximaAbsoluta)
        {
            throw new ProfundidadMaximaFueraDeRangoException(
                cmd.ProfundidadMaxima,
                minimoInclusivo: 1,
                maximoInclusivo: ProfundidadMaximaAbsoluta);
        }

        return new PresupuestoCreado(
            PresupuestoId: presupuestoId,
            TenantId: cmd.TenantId,
            Codigo: cmd.Codigo.Trim(),
            Nombre: cmd.Nombre.Trim(),
            PeriodoInicio: cmd.PeriodoInicio,
            PeriodoFin: cmd.PeriodoFin,
            MonedaBase: cmd.MonedaBase,
            ProfundidadMaxima: cmd.ProfundidadMaxima,
            CreadoEn: ahora,
            CreadoPor: string.IsNullOrWhiteSpace(cmd.CreadoPor) ? "sistema" : cmd.CreadoPor);
    }

    // ────────────── Apply methods (Marten los invoca al reconstruir el stream) ──────────────

    public void Apply(PresupuestoCreado e)
    {
        Id = e.PresupuestoId;
        TenantId = e.TenantId;
        Codigo = e.Codigo;
        Nombre = e.Nombre;
        PeriodoInicio = e.PeriodoInicio;
        PeriodoFin = e.PeriodoFin;
        MonedaBase = e.MonedaBase;
        ProfundidadMaxima = e.ProfundidadMaxima;
        Estado = EstadoPresupuesto.Borrador;
        CreadoEn = e.CreadoEn;
        CreadoPor = e.CreadoPor;
    }
}
