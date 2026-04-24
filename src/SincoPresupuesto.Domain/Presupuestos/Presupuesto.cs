using System.Text.RegularExpressions;
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

    /// <summary>
    /// Formato canónico del código de rubro: dos dígitos, seguido opcionalmente de entre
    /// 0 y 14 segmentos adicionales <c>.DD</c>. INV-10 (hotspots §4).
    /// </summary>
    private static readonly Regex CodigoRubroRegex = new(
        @"^\d{2}(\.\d{2}){0,14}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly List<Rubro> _rubros = new();

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

    /// <summary>Vista de solo lectura de los rubros reconstruidos por fold.</summary>
    public IReadOnlyList<Rubro> Rubros => _rubros;

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

        Requerir.Campo(cmd.TenantId, nameof(cmd.TenantId));
        Requerir.Campo(cmd.Codigo, nameof(cmd.Codigo));
        Requerir.Campo(cmd.Nombre, nameof(cmd.Nombre));

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

    /// <summary>
    /// Agrega un rubro al árbol del presupuesto. Valida INV-3, INV-8, INV-10, INV-11, INV-D, INV-F
    /// y las precondiciones del comando; si todo cumple, devuelve el evento <see cref="RubroAgregado"/>.
    /// No muta estado — esa es responsabilidad de <see cref="Apply(RubroAgregado)"/>.
    /// </summary>
    public RubroAgregado AgregarRubro(
        Commands.AgregarRubro cmd,
        Guid rubroId,
        DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // PRE-1 / PRE-2 — campos string requeridos.
        Requerir.Campo(cmd.Codigo, "Codigo");
        Requerir.Campo(cmd.Nombre, "Nombre");

        // PRE-3 — rubroId no puede ser Guid.Empty (equivalente a "campo requerido" para identidades).
        if (rubroId == Guid.Empty)
        {
            throw new CampoRequeridoException("RubroId");
        }

        // PRE-4 — normalización.
        var codigo = cmd.Codigo.Trim();
        var nombre = cmd.Nombre.Trim();

        // INV-3 — declarada en spec §5. La rama de violación (estado ≠ Borrador) queda
        // cubierta por el followup #13 (slice AprobarPresupuesto), cuando exista un
        // comando que transicione el estado. El test de sanidad §6.7 ejerce el camino
        // "estado Borrador → no lanza".
        if (Estado != EstadoPresupuesto.Borrador)
        {
            throw new PresupuestoNoEsBorradorException(Estado);
        }

        // INV-D — el padre referenciado debe existir. Se chequea antes que el formato
        // porque INV-F ("hijo extiende al padre") es más específica y cubre códigos
        // que tampoco pasan INV-10 (p.ej. "011.01" bajo padre "01"): el test §6.10
        // exige CodigoHijoNoExtiendeAlPadreException para ese caso.
        Rubro? padre = null;
        if (cmd.RubroPadreId is Guid padreId)
        {
            padre = _rubros.FirstOrDefault(r => r.Id == padreId)
                ?? throw new RubroPadreNoExisteException(padreId);
        }

        ValidarFormatoDelCodigo(padre, codigo);

        // INV-8 — nivel calculado ≤ ProfundidadMaxima.
        var nivel = padre is null ? 1 : padre.Nivel + 1;
        if (nivel > ProfundidadMaxima)
        {
            throw new ProfundidadExcedidaException(ProfundidadMaxima, nivel);
        }

        // INV-11 — unicidad del código dentro del presupuesto.
        if (_rubros.Any(r => r.Codigo == codigo))
        {
            throw new CodigoRubroDuplicadoException(codigo);
        }

        return new RubroAgregado(
            PresupuestoId: Id,
            RubroId: rubroId,
            Codigo: codigo,
            Nombre: nombre,
            RubroPadreId: cmd.RubroPadreId,
            AgregadoEn: ahora);
    }

    /// <summary>
    /// Valida el formato del código del rubro según tenga o no padre.
    /// Con padre: INV-F (el hijo extiende al padre con exactamente un segmento <c>.DD</c>).
    /// Sin padre: INV-10 (formato canónico <c>^\d{2}(\.\d{2}){0,14}$</c>).
    /// Nota: para hijos, INV-F es más estricta que INV-10 y la implica, por lo que no se
    /// valida INV-10 redundantemente.
    /// </summary>
    private static void ValidarFormatoDelCodigo(Rubro? padre, string codigo)
    {
        if (padre is not null)
        {
            var esperadoLen = padre.Codigo.Length + 3;
            var prefijo = padre.Codigo + ".";
            if (codigo.Length != esperadoLen
                || !codigo.StartsWith(prefijo, StringComparison.Ordinal)
                || !char.IsDigit(codigo[esperadoLen - 2])
                || !char.IsDigit(codigo[esperadoLen - 1]))
            {
                throw new CodigoHijoNoExtiendeAlPadreException(padre.Codigo, codigo);
            }
        }
        else if (!CodigoRubroRegex.IsMatch(codigo))
        {
            throw new CodigoRubroInvalidoException(codigo);
        }
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

    public void Apply(RubroAgregado e)
    {
        var nivel = e.RubroPadreId is Guid padreId
            ? _rubros.First(r => r.Id == padreId).Nivel + 1
            : 1;

        _rubros.Add(new Rubro
        {
            Id = e.RubroId,
            Codigo = e.Codigo,
            Nombre = e.Nombre,
            PadreId = e.RubroPadreId,
            Nivel = nivel,
        });
    }
}
