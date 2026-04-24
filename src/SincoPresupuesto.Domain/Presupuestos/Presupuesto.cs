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
    public static PresupuestoCreado Crear(CrearPresupuesto cmd, Guid presupuestoId, DateTimeOffset ahora)
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
    /// Asigna (o reasigna) un monto a un rubro existente del árbol. Spec slice 04 §2/§6.
    /// Valida PRE-1 (RubroId no vacío), PRE-2 (rubro existe), PRE-3/INV-2 (Monto ≥ 0),
    /// PRE-4 (normalización AsignadoPor), INV-3 declarada (Borrador — diferida a followup #13),
    /// e INV-NEW-SLICE04-1 (rechazo si el rubro es Agrupador). Devuelve el evento
    /// <see cref="MontoAsignadoARubro"/>; no muta estado.
    /// </summary>
    public MontoAsignadoARubro AsignarMontoARubro(
        Commands.AsignarMontoARubro cmd,
        DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // PRE-1 — RubroId no puede ser Guid.Empty.
        if (cmd.RubroId == Guid.Empty)
        {
            throw new CampoRequeridoException("RubroId");
        }

        // INV-3 — declarada en spec §5. La rama de violación (estado ≠ Borrador) queda
        // cubierta por followup #13 (slice AprobarPresupuesto). El test de sanidad §6.8
        // ejerce el camino "estado Borrador → no lanza".
        if (Estado != EstadoPresupuesto.Borrador)
        {
            throw new PresupuestoNoEsBorradorException(Estado);
        }

        // PRE-2 — el rubro destino existe.
        var rubroDestino = _rubros.FirstOrDefault(r => r.Id == cmd.RubroId)
            ?? throw new RubroNoExisteException(cmd.RubroId);

        // INV-NEW-SLICE04-1 — el rubro destino no debe tener hijos (no es Agrupador).
        if (_rubros.Any(r => r.PadreId == cmd.RubroId))
        {
            throw new RubroEsAgrupadorException(cmd.RubroId);
        }

        // PRE-3 / INV-2 — Monto ≥ 0.
        if (cmd.Monto.Valor < 0m)
        {
            throw new MontoNegativoException(cmd.Monto);
        }

        // PRE-4 — normalización de AsignadoPor (patrón slice 01/02).
        var asignadoPor = string.IsNullOrWhiteSpace(cmd.AsignadoPor)
            ? "sistema"
            : cmd.AsignadoPor;

        // MontoAnterior — spec §3 y §12.2: si el rubro aún no tiene monto asignado
        // (Monto.EsCero tras la inicialización en Apply(RubroAgregado)), se alinea a
        // la moneda del comando para el "delta" auto-documentado; si ya hubo asignación
        // real, se usa el monto previo tal cual (posiblemente en otra moneda).
        var montoAnterior = rubroDestino.Monto.EsCero
            ? Dinero.Cero(cmd.Monto.Moneda)
            : rubroDestino.Monto;

        return new MontoAsignadoARubro(
            PresupuestoId: Id,
            RubroId: cmd.RubroId,
            Monto: cmd.Monto,
            MontoAnterior: montoAnterior,
            AsignadoEn: ahora,
            AsignadoPor: asignadoPor);
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
            // Inicializa Monto a Dinero.Cero(MonedaBase) — spec §12.2. Evita que el
            // Monto quede en default(Dinero) (Moneda con Codigo=null, que violaría
            // INV-SK-3 del VO Moneda). El fold de AsignarMontoARubro lo reemplaza
            // entero cuando llega la primera asignación.
            Monto = Dinero.Cero(MonedaBase),
        });
    }

    /// <summary>
    /// Fold del evento <see cref="MontoAsignadoARubro"/>. Spec §12.5: localiza el rubro
    /// con <c>e.RubroId</c> y reemplaza su <see cref="Rubro.Monto"/> por <c>e.Monto</c>.
    /// Se usa <c>.First(...)</c> — si el rubro no existiera sería un bug del stream
    /// (el comando garantiza que el rubro existe antes de emitir).
    /// </summary>
    public void Apply(MontoAsignadoARubro e)
    {
        var rubro = _rubros.First(r => r.Id == e.RubroId);
        rubro.AsignarMonto(e.Monto);
    }
}
