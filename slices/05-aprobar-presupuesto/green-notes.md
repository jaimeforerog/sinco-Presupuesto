# Green notes — Slice 05 — AprobarPresupuesto

**Implementador:** green
**Fecha:** 2026-04-24
**Spec consumida:** `slices/05-aprobar-presupuesto/spec.md` (firmada 2026-04-24, Q2=(a) — no validar `PeriodoFin >= ahora.Date`).
**Red-notes consumidas:** `slices/05-aprobar-presupuesto/red-notes.md`.
**Estado:** 14 rojos Slice05 → verdes. Suite completa **142/142 verdes en dominio + 20/20 integration = 162/162**, 0 warnings, 0 errors.

---

## 1. Archivos modificados

### Modificados

1. **`src/SincoPresupuesto.Domain/Presupuestos/Presupuesto.cs`**
   - **`AprobarPresupuesto(cmd, ahora)`** — reemplazado el stub `NotImplementedException` por la implementación real. Orden de validación (estricto, coincidente con spec §4 y brief):
     1. `ArgumentNullException.ThrowIfNull(cmd)`.
     2. **PRE-1 / INV-3**: `Estado != Borrador` → `PresupuestoNoEsBorradorException(Estado)`. Cierra retroactivamente followup #13: la misma rama ya existente en `AgregarRubro` (línea 135) y `AsignarMontoARubro` (línea 197) ahora se ejercita por primera vez en violación, desde §6.7 (re-aprobar), §6.8 (`AgregarRubro` post-aprobación) y §6.9 (`AsignarMontoARubro` post-aprobación).
     3. Identificación de **terminales con monto positivo**: `_rubros.Where(r => !_rubros.Any(otro => otro.PadreId == r.Id) && r.Monto.EsPositivo).ToList()`. Una sola pasada combina la distinción operacional Agrupador/Terminal (presencia de hijos en `_rubros`, mismo precedente que slice 04 §6.7) con el filtro `EsPositivo`.
     4. **PRE-2**: si la lista está vacía → `PresupuestoSinMontosException(Id)`. Cubre los dos sub-casos §6.4 (sin rubros) y §6.5 (todos los terminales en cero).
     5. **PRE-3**: `terminalesConMontoPositivo.Where(r => r.Monto.Moneda != MonedaBase).Select(r => r.Id).ToList()`. Si la lista no está vacía → `AprobacionConMultimonedaNoSoportadaException(Id, lista, MonedaBase)`. La lista contiene **todos** los conflictivos en orden de aparición en `_rubros` — §6.6 asserta `{R1, R3}` exactos.
     6. **Cómputo de `MontoTotal`**: `terminalesConMontoPositivo.Aggregate(Dinero.Cero(MonedaBase), (acc, r) => acc + r.Monto)`. PRE-3 garantiza homogeneidad de moneda; el operador `+` de `Dinero` lanza `MonedasDistintasException` si se viola — pero PRE-3 lo previene. PRE-2 garantiza que la secuencia no es vacía; el seed `Dinero.Cero(MonedaBase)` mantiene el tipo correcto independientemente.
     7. **PRE-4**: `string.IsNullOrWhiteSpace(cmd.AprobadoPor) ? "sistema" : cmd.AprobadoPor` (mismo patrón slice 01/02/04).
     8. Devuelve `new PresupuestoAprobado(Id, montoTotal, new Dictionary<Moneda, decimal>(), ahora, aprobadoPor)`. `SnapshotTasas` queda como diccionario vacío en MVP — followup #24 lo populará.
   - **`Apply(PresupuestoAprobado e)`** — reemplazado el stub. Setea `Estado = Aprobado`, `MontoTotal = e.MontoTotal`, `SnapshotTasas = e.SnapshotTasas`, `AprobadoEn = e.AprobadoEn`, `AprobadoPor = e.AprobadoPor`. Se añadió `ArgumentNullException.ThrowIfNull(e)` por simetría con `AprobarPresupuesto` y patrón de los demás `Apply` (defensivo contra fold con `null`, aunque Marten no lo invoque así).

### No tocados (ya estaban correctos tras red)

- `src/SincoPresupuesto.Domain/Presupuestos/Commands/AprobarPresupuesto.cs` — record correcto.
- `src/SincoPresupuesto.Domain/Presupuestos/Events/PresupuestoAprobado.cs` — record con `IReadOnlyDictionary<Moneda, decimal> SnapshotTasas` correcto.
- `src/SincoPresupuesto.Domain/SharedKernel/PresupuestoSinMontosException.cs` — propiedad `PresupuestoId: Guid`, hereda de `DominioException`. Sin cambios.
- `src/SincoPresupuesto.Domain/SharedKernel/AprobacionConMultimonedaNoSoportadaException.cs` — propiedades `PresupuestoId`, `RubrosConMonedaDistinta`, `MonedaBase`. Sin cambios.
- `src/SincoPresupuesto.Domain/Presupuestos/Rubro.cs` — entity con `Monto` correcto desde slice 04.
- Propiedades nuevas en `Presupuesto` (`MontoTotal`, `SnapshotTasas`, `AprobadoEn`, `AprobadoPor`) — ya estaban añadidas por red según spec §12.5.
- Tests de Slice05 — intactos.
- Otros slices — intactos.
- `PresupuestoProjection` / `PresupuestoReadModel` — no tocados (mismo criterio que slice 03/04: ningún test del slice ejercita la proyección).
- `DomainExceptionHandler.Mapear` — no tocado (mismo criterio que slice 02/03/04: el refactor transversal pertenece a un slice de wire/API; no hay tests HTTP en el proyecto de dominio puro).

---

## 2. Impulsos de refactor descartados (candidatos para `refactorer`)

### 2.1 Helper `RequerirBorrador()` privado — ahora con 3 usos

Slice 03 green-notes §3.5 lo dejó marcado como "candidato cuando aparezca el tercer comando". **Hoy hay tres usos idénticos** del bloque:

```csharp
if (Estado != EstadoPresupuesto.Borrador)
{
    throw new PresupuestoNoEsBorradorException(Estado);
}
```

en `AgregarRubro` (línea 135), `AsignarMontoARubro` (línea 197) y `AprobarPresupuesto` (recién añadido). **Disparador alcanzado** — slice 04 green-notes §2.6 ya lo marcó como pendiente para el tercer caso. Refactorer puede extraer `private void RequerirBorrador()` con confianza: comportamiento idéntico, tres llamadores, cero ramas observables nuevas.

### 2.2 Cálculo de `MontoTotal` y distinción Terminal/Agrupador

El cálculo `_rubros.Where(r => !_rubros.Any(otro => otro.PadreId == r.Id) && r.Monto.EsPositivo)` es **O(n²)** por la subconsulta `Any`. Para presupuestos pequeños (MVP) es aceptable; para árboles grandes (cientos de rubros) deberá indexarse por `PadreId` o materializar `EsTerminal` como propiedad calculada en `Rubro`. Mismo escenario que slice 04 green-notes §3.3.

Adicionalmente, la distinción operacional Agrupador/Terminal por `_rubros.Any(otro => otro.PadreId == r.Id)` se repite en tres lugares (`AsignarMontoARubro` para INV-NEW-SLICE04-1, `AprobarPresupuesto` para PRE-2 y PRE-3, futuros). **Candidato** para método helper `private bool EsTerminal(Rubro r)` o promoción a propiedad de `Rubro` (cierra parcialmente followup #12 cuando `RubroTipo` se modele explícitamente).

### 2.3 `SnapshotTasas` siempre vacío en MVP

El handler emite `new Dictionary<Moneda, decimal>()` en cada llamada — alocación garantizada e inútil. **Microoptimización candidata**: usar un singleton `IReadOnlyDictionary<Moneda, decimal>` vacío (p.ej. `ReadOnlyDictionary<Moneda, decimal>.Empty` si .NET 9 lo expone, o un `static readonly` propio). No lo hago en green: la asignación es semánticamente clara y el caller (Marten) ya serializa/deserializa el evento de todas formas. Tarea de refactorer si la perf de aprobación importa.

### 2.4 `PresupuestoProjection` / `PresupuestoReadModel` — campos nuevos pendientes

Spec §8 enumera campos a añadir: `MontoTotal: Dinero?`, `AprobadoEn: DateTimeOffset?`, `AprobadoPor: string?`, `SnapshotTasas`. Cero tests del slice los ejercitan. Mismo criterio que slice 03/04: deuda heredada que este slice **agrava** al introducir más campos congelados. Slice de proyección dedicado.

### 2.5 `DomainExceptionHandler.Mapear` — dos casos pendientes

Spec §9 / §12.1: `PresupuestoSinMontosException → 400`, `AprobacionConMultimonedaNoSoportadaException → 400`, `PresupuestoNoEsBorradorException → 409` (este último ya mapeado desde slice 03). Sin tests HTTP en el proyecto de dominio. **Candidato para slice de wire/API** — consistente con slice 02/03/04 que dejaron mapeo pendiente.

### 2.6 Followup #10 — helper `RequireCampo` / `Requerir.X`

Sigue abierto. Este slice no añade nuevos usos del patrón `IsNullOrWhiteSpace → CampoRequeridoException` (ya hay 8 en dominio). Sí añade un uso del patrón `IsNullOrWhiteSpace → "sistema"` (normalización, no throw, en `AprobadoPor`) — décimo uso del patrón si se cuenta. Refactor transversal — sigue siendo del refactorer.

### 2.7 Cierre retroactivo de followup #13

Este slice **cierra** followup #13 al ejercitar la rama `if (Estado != Borrador) throw` desde tres puntos: §6.7 (`AprobarPresupuesto` re-aprobar), §6.8 (`AgregarRubro` post-aprobación), §6.9 (`AsignarMontoARubro` post-aprobación). Reviewer marca `[x]` al cerrar slice.

---

## 3. Decisiones deliberadas (código más simple que debería ser)

### 3.1 Orden de filtros: Terminal + EsPositivo combinados en una sola pasada

La spec §12.4 sugiere dos pasos:

```
var terminales = _rubros.Where(r => !_rubros.Any(otro => otro.PadreId == r.Id)).ToList();
var conMontoPositivo = terminales.Where(r => r.Monto.EsPositivo).ToList();
```

Implementado en una sola pasada combinando ambos filtros:

```csharp
var terminalesConMontoPositivo = _rubros
    .Where(r => !_rubros.Any(otro => otro.PadreId == r.Id) && r.Monto.EsPositivo)
    .ToList();
```

**Justificación**: equivalente observacional (los tests no distinguen entre las dos formas), menos código intermedio, una sola materialización (`ToList`). Si en el futuro se quisiera un mensaje de error más específico (p.ej. "hay terminales pero todos en cero" vs "no hay terminales"), habría que separarlos — pero la spec §12.1 declara una sola excepción `PresupuestoSinMontosException` para ambos casos, así que la unificación es correcta.

### 3.2 `Aggregate` con seed `Dinero.Cero(MonedaBase)` en vez de reducción sin seed

Alternativa: `terminalesConMontoPositivo.Select(r => r.Monto).Aggregate((a, b) => a + b)` — falla con `InvalidOperationException` si la secuencia es vacía. Elegí la versión con seed porque:

1. **Defensa en profundidad**: PRE-2 garantiza que la secuencia no es vacía, pero si un futuro refactor moviera el cómputo antes de PRE-2, la versión con seed seguiría devolviendo `Dinero.Cero(MonedaBase)` en lugar de lanzar.
2. **Tipo explícito**: el seed fija que el tipo del acumulador es `Dinero` en `MonedaBase` desde el inicio.
3. **Consistente con la spec §12.4** — usa exactamente esta forma como pseudocódigo.

### 3.3 Una sola allocation de `Dictionary<Moneda, decimal>` por evento

Cada `AprobarPresupuesto` crea un nuevo `new Dictionary<Moneda, decimal>()` vacío. No se reutiliza una instancia compartida — ver §2.3.

### 3.4 `Apply(PresupuestoAprobado)` con `ArgumentNullException.ThrowIfNull`

Los demás folds en `Presupuesto` (`Apply(PresupuestoCreado)`, `Apply(RubroAgregado)`, `Apply(MontoAsignadoARubro)`) **no** lo tienen — confían en Marten. Añadir `ThrowIfNull` aquí es deliberado y minimal: es defensa contra invocación directa por tests/handlers que pasen `null`. Como ningún test asserta sobre este chequeo, podría removerse sin afectar verde — pero el costo es una línea, y lo quito si refactorer lo pide para uniformidad. **Candidato menor** — uniformizar entre todos los `Apply`.

### 3.5 Sin abstracciones nuevas para "qué es Terminal"

La distinción Terminal/Agrupador podría haberse extraído a `private bool EsTerminal(Rubro r) => !_rubros.Any(otro => otro.PadreId == r.Id);`. **No lo hice** porque green prohíbe refactor: solo se usa una vez en la línea de filtro. Si el refactorer extrae el helper (§2.2), también limpia el uso simétrico en `AsignarMontoARubro` (slice 04, INV-NEW-SLICE04-1).

### 3.6 INV-3 ahora con tres testimonios verde-rojo-verde

Slice 03 declaró la rama `if (Estado != Borrador) throw` en `AgregarRubro` sin ejercitarla en violación. Slice 04 hizo lo mismo en `AsignarMontoARubro`. Slice 05 cierra el ciclo: §6.7 ejercita la rama dentro del propio `AprobarPresupuesto` (segunda invocación), §6.8 la ejercita en `AgregarRubro`, §6.9 en `AsignarMontoARubro`. Las tres ramas son **el mismo bloque de código copiado** — refactorer puede unificar (§2.1).

---

## 4. Verificación

### 4.1 `dotnet build`

```
Compilación correcta.
    0 Advertencia(s)
    0 Errores
```

### 4.2 `dotnet test --filter "FullyQualifiedName~Slice05"`

```
Correctas! - Con error: 0, Superado: 14, Omitido: 0, Total: 14
```

Los 14 casos xUnit (12 métodos, §6.3 Theory con 3 cases) pasan — incluye los retroactivos §6.8 y §6.9 que cierran followup #13.

### 4.3 `dotnet test` (suite completa)

```
SincoPresupuesto.Domain.Tests:      Correctas! - Superado: 142, Total: 142
SincoPresupuesto.Integration.Tests: Correctas! - Superado:  20, Total:  20
```

**142/142 dominio + 20/20 integration = 162/162 verdes.** Sin regresiones en Slices 00, 01, 02, 03, 04 ni en SharedKernel ni en integration tests.

---

## 5. Hand-off a refactorer

Lista priorizada (suma a la de slices 03 y 04):

1. **Helper `RequerirBorrador()` privado** (§2.1). Disparador alcanzado: 3 usos idénticos. Extracción trivial, cero riesgo.
2. **Helper `EsTerminal(Rubro)` o promoción a propiedad de `Rubro`** (§2.2). Dos usos hoy (`AsignarMontoARubro` y `AprobarPresupuesto`); cierra parcialmente followup #12 (`RubroTipo`).
3. **`PresupuestoProjection` / `PresupuestoReadModel`** (§2.4). Deuda heredada de slice 03/04, agravada por slice 05 con 4 campos nuevos congelados.
4. **`DomainExceptionHandler.Mapear`** (§2.5). Añadir mapeos de `PresupuestoSinMontosException → 400` y `AprobacionConMultimonedaNoSoportadaException → 400`. Slice wire/API.
5. **Followup #10 — helper `RequireCampo` / `Requerir.X`** (§2.6). 8 usos del patrón throw, 10 usos contando normalización. Sigue siendo refactor transversal.
6. **Cierre de followup #13** (§2.7). Reviewer marca `[x]` al cerrar slice 05.
7. **`SnapshotTasas` singleton vacío** (§2.3). Microoptimización opcional, baja prioridad.
8. **Uniformidad de `ArgumentNullException.ThrowIfNull` entre todos los `Apply`** (§3.4). Cosmético.
