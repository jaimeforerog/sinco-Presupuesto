# Slice 05 — AprobarPresupuesto

**Autor:** domain-modeler
**Fecha:** 2026-04-24
**Estado:** firmado
**Agregado afectado:** `Presupuesto` (transición de estado `Borrador → Aprobado`).
**Decisiones previas relevantes:**
- `01-event-storming-mvp.md` §3.2 (máquina de estados: `Borrador → Aprobado → Activo → Cerrado`), §4 (precondiciones: presupuesto en Borrador + al menos un rubro con monto > 0), §5 (evento `PresupuestoAprobado` payload original: `PresupuestoId`, `MontoTotal`, `AprobadoEn`, `AprobadoPor`).
- `02-decisiones-hotspots-mvp.md` §1 (Agrupador no tiene monto directo — total = suma de hijos terminales), §2 (multimoneda a nivel de partida; `SnapshotTasas` congela el baseline al aprobar; INV-14, INV-15), §3.2 (estados — texto: "a partir de Aprobado el presupuesto queda congelado en el MVP").
- `slices/00-shared-kernel/spec.md` (contrato de `Dinero`, `Moneda`, `DominioException`, jerarquía de excepciones del kernel).
- `slices/01-crear-presupuesto/spec.md` (precedente: `MonedaBase` inmutable, `EstadoPresupuesto.Borrador` como estado inicial post `PresupuestoCreado`, patrón `CreadoPor = "sistema"` default).
- `slices/03-agregar-rubro/spec.md` §5 + `FOLLOWUPS.md` #13 (INV-3 diferida; este slice la **cierra retroactivamente** con tests sobre `AgregarRubro` post-aprobación).
- `slices/04-asignar-monto-a-rubro/spec.md` §5 + §6.8 (INV-3 también diferida en `AsignarMontoARubro`; este slice la cierra con test simétrico).
- `FOLLOWUPS.md` #20 (`TasaASnapshot` en `MontoAsignadoARubro`), #13 (compromiso INV-3), #22 (interacción `AgregarRubro`/`Monto` del padre — fuera de alcance acá).
- `src/SincoPresupuesto.Domain/Presupuestos/Presupuesto.cs` (estado actual: rama `if (Estado != Borrador) throw new PresupuestoNoEsBorradorException(Estado);` ya existente en `AgregarRubro` línea 114 y en `AsignarMontoARubro` línea 176 — sin cubrir hoy por test).
- `src/SincoPresupuesto.Domain/Presupuestos/EstadoPresupuesto.cs` (enum `Borrador=0, Aprobado=1, Activo=2, Cerrado=3`).
- `src/SincoPresupuesto.Domain/Presupuestos/Rubro.cs` (entity con `Monto: Dinero` desde slice 04).

---

## 1. Intención

El responsable de presupuesto necesita **congelar el presupuesto en estado `Aprobado`** una vez la estructura de rubros y los montos en Borrador estén listos. La aprobación calcula el `MontoTotal` agregado del presupuesto (suma de los rubros **terminales** en `MonedaBase`), congela ese baseline, y bloquea cualquier modificación estructural posterior (`AgregarRubro`, `AsignarMontoARubro`, futuros `RubroRetirado`, `RubroMovido`, etc.). Este slice **cierra la rama** que slices 03 y 04 dejaron declarada pero no ejercitada — desde ahora `INV-3` es una invariante con testimonio rojo→verde completo.

Multimoneda en este slice: `SnapshotTasas` (hotspots §2) se modela como campo del evento, pero su **populado real** se difiere a un slice futuro (`AprobarPresupuesto` con catálogo `TasaDeCambio` — followup #20/#24). En el MVP de slice 05, `SnapshotTasas` se emite **vacío** (`IReadOnlyDictionary<Moneda, decimal>` con cero entradas), y la **precondición** es que **todos los rubros terminales con monto > 0 estén en `MonedaBase`**: si hay rubros terminales con `Monto.Moneda != MonedaBase` y `Monto.Valor > 0`, la aprobación falla con excepción específica. Esta restricción es temporal y se levantará cuando exista el agregado/proyección `TasaDeCambio` (followup #24).

## 2. Comando

```csharp
public sealed record AprobarPresupuesto(
    string AprobadoPor = "sistema");
```

- Sin `PresupuestoId` en el payload del comando: el agregado ya está rehidratado por el caller (mismo patrón que slice 03 `AgregarRubro` y slice 04 `AsignarMontoARubro`). El `PresupuestoId` se toma de `agg.Id` al emitir el evento.
- `AprobadoPor`: string libre. Vacío / whitespace / null → `"sistema"` al emitir el evento (patrón slice 01 `CreadoPor`, slice 02 `ConfiguradoPor`, slice 04 `AsignadoPor`).

Firma del caso de uso (método de instancia del agregado ya reconstruido — patrón slice 03 / 04):

```csharp
public PresupuestoAprobado AprobarPresupuesto(
    Commands.AprobarPresupuesto cmd,
    DateTimeOffset ahora);
```

No recibe `id` desde fuera porque no crea identidad nueva: opera sobre el agregado existente (identidad = `agg.Id`).

### Nota sobre `SnapshotTasas`

Hotspots §2 define `SnapshotTasas: { [moneda]: tasaAMonedaBase }` como campo congelado del evento `PresupuestoAprobado`, alimentado por la proyección `TasasDeCambioVigentes`. **Decisión del modeler para slice 05:**

- El campo `SnapshotTasas` se **incluye** en el payload del evento desde ya, tipado como `IReadOnlyDictionary<Moneda, decimal>`.
- En slice 05 el handler emite **siempre `SnapshotTasas` vacío** (`new Dictionary<Moneda, decimal>()`), porque la precondición §4 PRE-3 garantiza que todos los rubros terminales con monto > 0 están en `MonedaBase` — no hay otras monedas que mapear.
- Cuando exista el agregado `TasaDeCambio` (slice futuro), el handler de `AprobarPresupuesto` consultará `TasasDeCambioVigentes` y populará el diccionario; la precondición de "solo `MonedaBase`" se reemplazará por "existe tasa para cada moneda presente". Followup #24.

Justificación de incluir el campo aunque vacío: evolución de esquema de evento menos invasiva. Añadir `SnapshotTasas` después implicaría versionado de evento o un upcaster — más caro que llevar un diccionario vacío hoy. Alternativa rechazada: omitir el campo y agregarlo en slice 06.

## 3. Evento(s) emitido(s)

| Evento | Payload | Cuándo |
|---|---|---|
| `PresupuestoAprobado` | `PresupuestoId: Guid`, `MontoTotal: Dinero`, `SnapshotTasas: IReadOnlyDictionary<Moneda, decimal>`, `AprobadoEn: DateTimeOffset`, `AprobadoPor: string` | Al aceptar el comando sobre un presupuesto en `Borrador` con al menos un rubro terminal cuyo `Monto.Valor > 0` y donde **todos** los rubros terminales con monto positivo están denominados en `MonedaBase`. |

- **`MontoTotal`**: suma de `Rubro.Monto` de todos los rubros **terminales** (sin hijos en `_rubros`) cuya `Monto.Moneda == MonedaBase`. Tipo `Dinero` en `MonedaBase` del presupuesto. Los Agrupadores **no** aportan a `MontoTotal` (hotspots §1: "Agrupador: total = suma de hijos"). Los rubros terminales con `Monto.Valor == 0` aportan `Dinero.Cero(MonedaBase)` (neutro aditivo) — no afectan el resultado.
- **`SnapshotTasas`**: vacío en este slice. Se populará en slice 06+.
- **`AprobadoEn`**: `DateTimeOffset` inyectado (`TimeProvider` en handler).
- **`AprobadoPor`**: string normalizado vía `string.IsNullOrWhiteSpace(...) ? "sistema" : ...` (patrón slice 01/02/04).

## 4. Precondiciones

Todas las excepciones heredan de `SincoPresupuesto.Domain.SharedKernel.DominioException`. Los tests verifican **tipo + propiedades**, nunca mensajes.

- `PRE-1`: el presupuesto está en estado `Borrador` — excepción: `PresupuestoNoEsBorradorException(EstadoPresupuesto EstadoActual)` (ya existe desde slice 03 §12). Cubre el sub-caso "ya aprobado" (idempotencia rechazada — ver §7) y los futuros estados `Activo`/`Cerrado`.
- `PRE-2`: existe **al menos un rubro terminal** (sin hijos en `_rubros`) cuyo `Monto.EsPositivo` (i.e. `Monto.Valor > 0`). Cubre dos sub-casos:
  - "presupuesto sin rubros" — `_rubros` vacío.
  - "presupuesto con rubros pero ninguno con monto > 0" — todos los terminales tienen `Monto.EsCero` (o solo hay Agrupadores, que no aportan).
  - Excepción: `PresupuestoSinMontosException(Guid PresupuestoId)` (nueva — ver §12).
- `PRE-3` (multimoneda mínima — temporal hasta followup #24): **todos** los rubros terminales con `Monto.EsPositivo` deben tener `Monto.Moneda == MonedaBase`. Si al menos uno tiene `Monto.Moneda != MonedaBase`, la aprobación falla con `AprobacionConMultimonedaNoSoportadaException(Guid PresupuestoId, IReadOnlyList<Guid> RubrosConMonedaDistinta, Moneda MonedaBase)` (nueva — ver §12). La excepción reporta **todos** los `RubroId` conflictivos (no solo el primero) para que la UX pueda destacarlos en bloque.
- `PRE-4` (normalización, no fallo): `cmd.AsignadoPor` (renombrado a `cmd.AprobadoPor`) nulo / vacío / whitespace → `"sistema"` en el evento emitido. No lanza. Patrón slice 01 §6.2, slice 02 §6.2, slice 04 §6.11.

Orden de evaluación (importa para tests deterministas):

1. PRE-1 (estado).
2. PRE-2 (al menos un rubro terminal con monto > 0).
3. PRE-3 (toda moneda de terminal con monto > 0 es `MonedaBase`).
4. PRE-4 (normalización antes de emitir).

## 5. Invariantes tocadas

- **`INV-3`** (event-storming §6): "No se pueden agregar, retirar ni reasignar rubros en estado distinto a Borrador." Este slice **es el primero que la ejercita en una rama positiva** (lanza al re-aprobar) y **además cierra retroactivamente** las ramas de `AgregarRubro` (slice 03 §6.7 diferido) y `AsignarMontoARubro` (slice 04 §6.8 diferido). Ver §6.7, §6.8, §6.9.
- **`INV-7`** (reformulada por hotspots §2): "La `MonedaBase` del presupuesto es inmutable tras crearse." Este slice **la respeta** — `MontoTotal` se expresa en `MonedaBase` (la del presupuesto), y `MonedaBase` no se redefine. Sin escenario directo (los happy paths la observan implícitamente).
- **`INV-13`** (hotspots §2): "Toda cantidad monetaria en un evento se almacena como `Dinero(valor, moneda)` — nunca `decimal` pelado." Cumplida — `MontoTotal: Dinero`. Las **tasas** del `SnapshotTasas` son `decimal` desnudo intencionalmente: representan factores de conversión, no cantidades monetarias (alineado con la signatura `Dinero.En(Moneda destino, decimal factor)` del slice 00 §2.1). Decisión del modeler ratificada.
- **`INV-14`** (hotspots §2 — nueva): "Un presupuesto solo puede aprobarse si existen tasas de cambio vigentes hacia `MonedaBase` para todas las monedas presentes en sus partidas." En slice 05 esta invariante se **fuerza por contradicción**: como no hay catálogo de tasas, la única forma de aprobar válida es que **no haya** monedas distintas a `MonedaBase` en las partidas con monto > 0 (PRE-3). Cuando exista `TasaDeCambio` (followup #24), PRE-3 se reemplaza y INV-14 se ejercita en su forma plena.
- **`INV-15`** (hotspots §2 — nueva): "El baseline del presupuesto aprobado se calcula con `SnapshotTasas` del evento `PresupuestoAprobado` — inmutable." Cumplida vacuamente en slice 05 (no hay tasas que aplicar; `MontoTotal` se calcula sumando los `Rubro.Monto` directamente, todos en `MonedaBase`). Cuando `SnapshotTasas` se popule (followup #24), `MontoTotal` se calculará como suma de `rubro.Monto.En(MonedaBase, snapshotTasas[rubro.Monto.Moneda])`.
- **Nueva — `INV-NEW-SLICE05-1`**: "Una vez aprobado, las propiedades `Estado`, `AprobadoEn`, `AprobadoPor`, `MontoTotal` y `SnapshotTasas` del presupuesto son inmutables (no hay comando que las modifique en este slice)." Se valida implícitamente en §6.10 (fold) y por la rama de `INV-3` en §6.7.

Invariantes **tocadas pero no ejercitables en este slice** (tratamiento abajo):

- **`INV-9`** (hotspots §1, "un terminal no tiene hijos"): el cálculo de `MontoTotal` distingue Agrupadores (con hijos) de Terminales (sin hijos). Si la entity `Rubro` aún no tiene `RubroTipo` explícito (followup #12 sigue abierto), la distinción se hace **operacionalmente** vía `_rubros.Any(r => r.PadreId == padre.Id)`. Ya hay precedente en slice 04 §6.7. Este slice NO introduce `RubroTipo`; la distinción operacional basta.
- **`INV-A`/`INV-B`/`INV-C`/`INV-D`** (hotspots §1): no relevantes acá — no se mueven ni agregan rubros.
- **`INV-NEW-SLICE04-1`** (slice 04, "no asignar monto a Agrupador"): no relevante — no asignamos.

## 6. Escenarios Given / When / Then

Cada escenario empieza con un `PresupuestoCreado` (para fijar `MonedaBase`, `ProfundidadMaxima`) y, cuando aplica, una secuencia de `RubroAgregado` + `MontoAsignadoARubro` para poblar el árbol con montos. Los escenarios 6.8 y 6.9 son **retroactivos** y cierran followup #13 — verifican comportamiento de `AgregarRubro` y `AsignarMontoARubro` post-aprobación, ejerciendo ramas ya implementadas en slices 03 y 04.

### 6.1 Happy path — un terminal con monto > 0 en `MonedaBase`

**Given**
- `PresupuestoCreado(PresupuestoId=P, TenantId="acme", Codigo="OBRA-2026-01", …, MonedaBase=Moneda.COP, ProfundidadMaxima=10, …)`
- `RubroAgregado(RubroId=R1, Codigo="01", Nombre="Costos Directos", RubroPadreId=null, …)`
- `MontoAsignadoARubro(RubroId=R1, Monto=Dinero(1_000_000, COP), MontoAnterior=Dinero(0, COP), …)`

**When**
- `AprobarPresupuesto(AprobadoPor="alice")` con `ahora=T`.

**Then**
- Emite un único `PresupuestoAprobado` con:
  - `PresupuestoId = P`
  - `MontoTotal = Dinero(1_000_000, COP)`
  - `SnapshotTasas` es un diccionario **vacío** (`Count == 0`).
  - `AprobadoEn = T`
  - `AprobadoPor = "alice"`

### 6.2 Happy path — árbol con un Agrupador y dos terminales

**Given**
- `PresupuestoCreado(…, MonedaBase=COP, ProfundidadMaxima=10, …)`
- `RubroAgregado(RubroId=R1, Codigo="01", Nombre="Costos Directos", RubroPadreId=null, …)` → será Agrupador (recibe hijo).
- `RubroAgregado(RubroId=R2, Codigo="01.01", Nombre="Materiales", RubroPadreId=R1, …)` → terminal.
- `RubroAgregado(RubroId=R3, Codigo="01.02", Nombre="Mano de obra", RubroPadreId=R1, …)` → terminal.
- `MontoAsignadoARubro(RubroId=R2, Monto=Dinero(700_000, COP), MontoAnterior=Dinero(0, COP), …)`
- `MontoAsignadoARubro(RubroId=R3, Monto=Dinero(300_000, COP), MontoAnterior=Dinero(0, COP), …)`

**When**
- `AprobarPresupuesto(AprobadoPor="alice")` con `ahora=T`.

**Then**
- Emite `PresupuestoAprobado` con `MontoTotal = Dinero(1_000_000, COP)` (suma de R2 + R3 — el Agrupador R1 no aporta), `SnapshotTasas` vacío, `AprobadoEn=T`, `AprobadoPor="alice"`.

### 6.3 Normalización `AprobadoPor` vacío / whitespace / null → `"sistema"`

**Given** Mismo Given que §6.1 (un terminal con monto > 0).
**When** `AprobarPresupuesto(AprobadoPor=X)` con `X` en `{ "", "   ", null }` (los tres casos como un theory).
**Then** Emite `PresupuestoAprobado` con `AprobadoPor = "sistema"` y los demás campos del happy path §6.1.

### 6.4 Violación `PRE-2` — presupuesto sin rubros

**Given** Solo `PresupuestoCreado(…, MonedaBase=COP, …)`. Sin `RubroAgregado`.
**When** `AprobarPresupuesto(AprobadoPor="alice")`.
**Then** Lanza `PresupuestoSinMontosException` con `PresupuestoId = P`.

### 6.5 Violación `PRE-2` — todos los rubros tienen monto cero

**Given**
- `PresupuestoCreado(…, MonedaBase=COP, …)`
- `RubroAgregado(RubroId=R1, Codigo="01", RubroPadreId=null, …)` (sin asignación → `Monto = Dinero.Cero(COP)` por inicialización del fold, slice 04 §12.2).
- `RubroAgregado(RubroId=R2, Codigo="02", RubroPadreId=null, …)`
- `MontoAsignadoARubro(RubroId=R2, Monto=Dinero.Cero(COP), MontoAnterior=Dinero(0, COP), …)` (asignación explícita a cero — slice 04 §6.5 lo permite).

**When** `AprobarPresupuesto(AprobadoPor="alice")`.
**Then** Lanza `PresupuestoSinMontosException` con `PresupuestoId = P`.

_(Cubre la sub-rama "rubros existen pero ninguno aporta": ningún terminal con `Monto.EsPositivo`.)_

### 6.6 Violación `PRE-3` — terminal con moneda distinta a `MonedaBase`

**Given**
- `PresupuestoCreado(…, MonedaBase=Moneda.COP, …)`
- `RubroAgregado(RubroId=R1, Codigo="01", Nombre="Importados", RubroPadreId=null, …)`
- `RubroAgregado(RubroId=R2, Codigo="02", Nombre="Locales", RubroPadreId=null, …)`
- `RubroAgregado(RubroId=R3, Codigo="03", Nombre="Equipo", RubroPadreId=null, …)`
- `MontoAsignadoARubro(RubroId=R1, Monto=Dinero(5_000, USD), MontoAnterior=Dinero(0, USD), …)` ← USD ≠ COP.
- `MontoAsignadoARubro(RubroId=R2, Monto=Dinero(2_000_000, COP), MontoAnterior=Dinero(0, COP), …)` ← OK.
- `MontoAsignadoARubro(RubroId=R3, Monto=Dinero(800, EUR), MontoAnterior=Dinero(0, EUR), …)` ← EUR ≠ COP.

**When** `AprobarPresupuesto(AprobadoPor="alice")`.
**Then** Lanza `AprobacionConMultimonedaNoSoportadaException` con:
- `PresupuestoId = P`
- `RubrosConMonedaDistinta` contiene **exactamente** `{ R1, R3 }` (ambos rubros conflictivos, no solo el primero) — orden estable según orden de inserción en `_rubros`.
- `MonedaBase = Moneda.COP`

_(El test asserta sobre el `IReadOnlyList<Guid>` con `Should().BeEquivalentTo` o equivalente para comparar el contenido.)_

### 6.7 Violación `INV-3` — re-aprobar un presupuesto ya aprobado

**Given**
- Mismo Given que §6.1 (un terminal con monto > 0 en COP).
- Más: `PresupuestoAprobado(PresupuestoId=P, MontoTotal=Dinero(1_000_000, COP), SnapshotTasas={}, AprobadoEn=T1, AprobadoPor="alice")` (ya aprobó previamente).

**When** `AprobarPresupuesto(AprobadoPor="bob")` con `ahora=T2 > T1`.
**Then** Lanza `PresupuestoNoEsBorradorException` con `EstadoActual = EstadoPresupuesto.Aprobado`.

_(Cubre la sub-rama positiva de PRE-1: el estado tras el fold de `PresupuestoAprobado` es `Aprobado`, no `Borrador`. Ejerce por primera vez la rama `if (Estado != Borrador) throw` en `AprobarPresupuesto` — y a la vez confirma que `INV-3` se aplica también al propio comando que dispara la transición.)_

### 6.8 INV-3 retroactivo (a) — `AgregarRubro` sobre presupuesto aprobado lanza

**Given**
- `PresupuestoCreado(…, MonedaBase=COP, ProfundidadMaxima=10, …)`
- `RubroAgregado(RubroId=R1, Codigo="01", RubroPadreId=null, …)`
- `MontoAsignadoARubro(RubroId=R1, Monto=Dinero(500_000, COP), MontoAnterior=Dinero(0, COP), …)`
- `PresupuestoAprobado(PresupuestoId=P, MontoTotal=Dinero(500_000, COP), SnapshotTasas={}, AprobadoEn=T1, AprobadoPor="alice")` (presupuesto en `Aprobado`).

**When** `AgregarRubro(Codigo="02", Nombre="Otro", RubroPadreId=null)` con `rubroId=R2`, `ahora=T2`.

**Then** Lanza `PresupuestoNoEsBorradorException` con `EstadoActual = EstadoPresupuesto.Aprobado`.

_(Cierra followup #13 para `AgregarRubro`. Ejerce la rama `if (Estado != Borrador) throw` en `Presupuesto.AgregarRubro` línea 114 — declarada en slice 03 §6.7 pero diferida hasta tener un comando transicionante. El test vive en `Slice05_AprobarPresupuestoTests.cs` con un comentario que referencia slice 03 §6.7.)_

### 6.9 INV-3 retroactivo (b) — `AsignarMontoARubro` sobre presupuesto aprobado lanza

**Given**
- Mismo Given que §6.8 (presupuesto aprobado con un rubro R1 con monto previo).

**When** `AsignarMontoARubro(RubroId=R1, Monto=Dinero(750_000, COP), AsignadoPor="bob")` con `ahora=T2`.

**Then** Lanza `PresupuestoNoEsBorradorException` con `EstadoActual = EstadoPresupuesto.Aprobado`.

_(Cierra followup #13 para `AsignarMontoARubro`. Ejerce la rama `if (Estado != Borrador) throw` en `Presupuesto.AsignarMontoARubro` línea 176 — declarada en slice 04 §6.8 pero diferida. Test en `Slice05_AprobarPresupuestoTests.cs` con referencia a slice 04 §6.8.)_

### 6.10 Fold — `PresupuestoAprobado` muta el estado correctamente

**Given**
- Eventos ordenados: `PresupuestoCreado(…, MonedaBase=COP, …)`, `RubroAgregado(R1, "01", null, …)`, `MontoAsignadoARubro(R1, Dinero(1_000_000, COP), Dinero(0, COP), T0, "alice")`, `PresupuestoAprobado(PresupuestoId=P, MontoTotal=Dinero(1_000_000, COP), SnapshotTasas={}, AprobadoEn=T1, AprobadoPor="alice")`.

**When** Reconstruir el agregado aplicando los cuatro eventos (fold).

**Then**
- `agg.Id == P`
- `agg.Estado == EstadoPresupuesto.Aprobado`
- `agg.AprobadoEn == T1` (nueva propiedad).
- `agg.AprobadoPor == "alice"` (nueva propiedad).
- `agg.MontoTotal == Dinero(1_000_000, COP)` (nueva propiedad).
- `agg.SnapshotTasas` accesible (nueva propiedad), `Count == 0`.
- Las propiedades preexistentes (`MonedaBase`, `Codigo`, `TenantId`, `Rubros`, etc.) no se ven afectadas por el fold de `PresupuestoAprobado`.

### 6.11 Cálculo de `MontoTotal` ignora terminales con monto cero

**Given**
- `PresupuestoCreado(…, MonedaBase=COP, …)`
- `RubroAgregado(RubroId=R1, Codigo="01", RubroPadreId=null, …)` → fold inicializa `Monto = Dinero.Cero(COP)` (slice 04 §12.2). Sin `MontoAsignadoARubro`.
- `RubroAgregado(RubroId=R2, Codigo="02", RubroPadreId=null, …)`
- `MontoAsignadoARubro(RubroId=R2, Monto=Dinero(750_000, COP), MontoAnterior=Dinero(0, COP), …)`

**When** `AprobarPresupuesto(AprobadoPor="alice")` con `ahora=T`.

**Then** Emite `PresupuestoAprobado` con `MontoTotal = Dinero(750_000, COP)` (R1 con `Monto.EsCero` no aporta — pero su presencia tampoco bloquea PRE-2 porque R2 sí tiene monto > 0).

### 6.12 Agrupadores con `Monto.Valor != 0` residual no aportan a `MontoTotal`

_(Escenario defensivo: hoy slice 04 §6.7 prohíbe asignar monto a un Agrupador, pero el fold inicializa `Monto = Dinero.Cero(MonedaBase)` para todos los rubros y los Agrupadores nunca reciben asignación — quedan con `Monto.EsCero == true`. Sin embargo, este test fija el contrato "**siempre** ignorar Agrupadores en la suma" para futuros casos donde el flujo cambie.)_

**Given**
- `PresupuestoCreado(…, MonedaBase=COP, ProfundidadMaxima=10, …)`
- `RubroAgregado(RubroId=R1, Codigo="01", RubroPadreId=null, …)` (será Agrupador).
- `RubroAgregado(RubroId=R2, Codigo="01.01", RubroPadreId=R1, …)` (terminal hijo de R1).
- `MontoAsignadoARubro(RubroId=R2, Monto=Dinero(400_000, COP), MontoAnterior=Dinero(0, COP), …)`

**When** `AprobarPresupuesto(AprobadoPor="alice")`.

**Then** Emite `PresupuestoAprobado` con `MontoTotal = Dinero(400_000, COP)` (solo R2 aporta; R1 es Agrupador, identificado por tener al menos un hijo en `_rubros`).

## 7. Idempotencia / retries

- **No idempotente por diseño del comando**: una vez emitido `PresupuestoAprobado`, el agregado transiciona a `Aprobado` y un segundo `AprobarPresupuesto` lanza `PresupuestoNoEsBorradorException` (§6.7). Esto es la **forma deseada** de proteger contra reintento — no permite emitir dos eventos `PresupuestoAprobado` consecutivos para el mismo presupuesto.
- **Protección anti-reintento**: el caller (handler) usa `expected version` de Marten. Si dos `AprobarPresupuesto` llegan concurrentemente sobre un mismo stream en Borrador, uno gana, el otro recibe colisión de versión (Marten lanza `ConcurrencyException` o equivalente) → 409 desde el handler. Solo el ganador deja el agregado en `Aprobado`; el reintento subsiguiente del perdedor fallará por `INV-3` (§6.7) → otro 409. Net effect: exactamente un `PresupuestoAprobado` por stream, idempotencia "por estado" garantizada.
- **IdempotencyKey**: no se introduce. Mismo criterio que slices 01/03/04. Si un caller externo (p.ej. webhook) requiere idempotencia fuerte por clave, abrirá slice dedicado.

## 8. Impacto en proyecciones / read models

- **`PresupuestoReadModel`** (existente tras slices 01/03/04): debe ganar campos para el estado post-aprobación. Propuesta:
  - `Estado: EstadoPresupuesto` (ya existe — actualizar a `Aprobado`).
  - `MontoTotal: Dinero?` (nullable hasta aprobar, no-null tras aprobación).
  - `AprobadoEn: DateTimeOffset?`
  - `AprobadoPor: string?`
  - `SnapshotTasas: IReadOnlyDictionary<Moneda, decimal>?` (vacío por ahora; la deserialización Marten/STJ debe manejarlo — followup operativo si surge).
  - La proyección `PresupuestoProjection` amplía su `Apply(PresupuestoAprobado)` para setear estos campos.
- **`PresupuestoBaselineEnMonedaBase`** (hotspots §2): proyección candidata natural que congela el baseline aprobado. **Diferida** a slice futuro junto con `TasaDeCambio` y `SnapshotTasas` real. Followup #25 (sugerido).
- **`PresupuestosPorPeriodo`**: no impactada en su contrato; sí podría querer reflejar `Estado` para filtrar — fuera de alcance acá.
- **`SaldoPorRubro`**, **`EstructuraPresupuestal`**: sin impacto directo.

## 9. Impacto en endpoints HTTP

- **`POST /api/tenants/{tenantId}/presupuestos/{presupuestoId}/aprobar`** — aprueba el presupuesto.
  - Request body: `{ aprobadoPor?: "alice" }` (opcional; default `"sistema"`).
  - **201 Created** — primera aprobación (consistente con el patrón de slices 01/02/03/04: `Append` de evento ⇒ 201). Body: `{ presupuestoId, montoTotal: { valor, moneda }, snapshotTasas: {}, aprobadoEn, aprobadoPor }`. Header `Location: GET /api/tenants/{tenantId}/presupuestos/{presupuestoId}` (la lectura pasa por el read model existente).
  - **400 Bad Request** ante:
    - `PresupuestoSinMontosException` (PRE-2): el cliente intentó aprobar un presupuesto sin sustancia.
    - `AprobacionConMultimonedaNoSoportadaException` (PRE-3): el cliente debe consolidar monedas antes (o esperar a slice de tasas).
  - **404 Not Found** — el `presupuestoId` no existe (Marten retorna stream vacío → handler lanza `PresupuestoNoEncontradoException`). Criterio coherente con slices previos.
  - **409 Conflict** ante `PresupuestoNoEsBorradorException` (PRE-1, incluyendo el sub-caso "ya aprobado"). Conflicto lógico-estado, mismo criterio que slice 03 / 04 para `PresupuestoNoEsBorradorException` cuando se ejercite (followup #13).
  - El handler **no** genera identidades nuevas — el `PresupuestoId` viene en la ruta y el evento usa el `agg.Id`.

- Mapeo en `DomainExceptionHandler.Mapear`: añadir `PresupuestoSinMontosException → 400`, `AprobacionConMultimonedaNoSoportadaException → 400`. `PresupuestoNoEsBorradorException → 409` ya existe (slice 03 §12). Refactor transversal incluido en el slice (mismo criterio que slice 02/03/04).

## 10. Preguntas abiertas

Cinco preguntas candidatas evaluadas. **Tres** se resuelven directamente por el modeler (no requieren firma del usuario). **Dos** se elevan al usuario por tener impacto transversal o decisión de UX.

- [x] **¿`SnapshotTasas` se incluye en el payload del evento ahora o se difiere a slice 06?** — **Resuelto: incluir vacío.** Justificación en §2 "Nota sobre `SnapshotTasas`". Evolución de esquema de evento menos invasiva.
- [x] **¿Cómo identificar Terminales vs. Agrupadores sin `RubroTipo`?** — **Resuelto: operacionalmente vía `_rubros.Any(r => r.PadreId == nodo.Id)`.** Mismo precedente operacional que slice 04 §6.7. `RubroTipo` explícito sigue diferido a followup #12.
- [x] **¿`MontoTotal` se calcula sumando `Dinero` directamente o vía un acumulador `decimal` + ensamble final?** — **Resuelto: suma directa con operador `+` de `Dinero`.** El operador exige misma moneda — la PRE-3 garantiza que todos los terminales con monto > 0 están en `MonedaBase`, así que la suma es legal sin conversión. Implementación: `_rubros.Where(r => EsTerminal(r) && r.Monto.EsPositivo).Aggregate(Dinero.Cero(MonedaBase), (acc, r) => acc + r.Monto)`. Si no hay terminales con monto > 0, PRE-2 falla antes — el `Aggregate` nunca corre con secuencia vacía.
- [x] **¿La excepción `AprobacionConMultimonedaNoSoportadaException` lleva un único rubro o la lista completa?** — **Resuelto: la lista completa.** Justificación en PRE-3 / §12. Permite a la UX resaltar todos los rubros conflictivos en bloque, no obligar al usuario a iterar errores. Costo marginal mínimo (`IReadOnlyList<Guid>` en el constructor).
- [x] **Q1 — ¿Existe una transición "atajo" `Aprobado → Cerrado` para corregir errores?** — Imaginemos un caso: el responsable aprueba por error, descubre 5 minutos después que faltaba un rubro; ¿debe poder "desaprobar" o "cerrar" directamente? Opciones:
  - **(a) No atajo, seguir la máquina de estados de event-storming §3.2 estrictamente.** El error se trata fuera del sistema (cancelación administrativa) o vía `ActivarPresupuesto` → `CerrarPresupuesto` rápido. Slices futuros pueden introducir un comando explícito `RevertirAprobacion` (que vuelva a Borrador) si el negocio lo demanda — modelado como evento dedicado, no atajo de estado.
  - **(b) Atajo `Aprobado → Cerrado` con motivo.** Un comando `CancelarAprobacion` que emite `AprobacionCancelada` y deja el presupuesto en `Cerrado` sin pasar por `Activo`. Quiebra la máquina de estados pero modela una realidad operativa.

  **Recomendación del modeler: (a).** Respeta event-storming §3.2 y la decisión de hotspots §3.2 ("a partir de Aprobado el presupuesto queda congelado en el MVP; modificaciones vendrán después"). Si surge la necesidad real, slice futuro `RevertirAprobacion` la modela explícitamente como evento, no como atajo.

  **[x] Resuelto por el modeler con (a)** — alineado con event-storming §3.2 y hotspots §3.2 ("congelado en el MVP"). No requiere firma del usuario porque la decisión está implícita en documentos previamente firmados. Si el usuario quiere abrir la pregunta como (b), puede hacerlo en review.

- [ ] **Q2 — ¿`AprobarPresupuesto` debería validar también que `PeriodoFin >= ahora.Date` (no aprobar presupuestos cuyo periodo ya pasó)?** — Event-storming §4 no incluye esta precondición para `AprobarPresupuesto`; sí la incluye para `ActivarPresupuesto` ("fecha actual dentro del rango del periodo"). Pero un usuario podría racionalmente preguntar si tiene sentido aprobar un presupuesto del 2024 hoy en 2026 — el baseline congelado no servirá para activación.

  Opciones:
  - **(a) No validar fecha del periodo en `AprobarPresupuesto`.** La validación temporal vive en `ActivarPresupuesto`. Aprobar un presupuesto extemporáneo es legal pero inútil — tranco claro al activar.
  - **(b) Validar `PeriodoFin >= ahora.Date` aquí también.** Excepción nueva `PeriodoFiscalVencidoException`. Rechaza temprano.

  Recomendación del modeler: **(a)**. Event-storming §4 explícitamente solo carga la regla en `ActivarPresupuesto` — duplicarla acá es scope creep que el PO no firmó. Pero el modeler pregunta porque la respuesta condiciona si hay un escenario §6 adicional o no.

  **[x] Resuelto (firma del usuario 2026-04-24): opción (a)** — NO validar `PeriodoFin >= ahora.Date` en `AprobarPresupuesto`. Aprobar congela el baseline; la regla "fecha actual ∈ periodo" pertenece a `ActivarPresupuesto`. Aprobaciones retroactivas son legítimas. Slice queda con 12 escenarios; sin excepción `PeriodoFiscalVencidoException`.

## 11. Checklist pre-firma

- [x] Todas las precondiciones mapean a un escenario Then (PRE-1 → §6.7 + §6.8 + §6.9; PRE-2 → §6.4 + §6.5; PRE-3 → §6.6; PRE-4 → §6.3).
- [x] Todas las invariantes tocadas y ejercitables mapean a un escenario Then (INV-3 → §6.7/§6.8/§6.9; INV-13 → §6.1–§6.2 vía `MontoTotal: Dinero`; INV-14 → §6.6 por contradicción; INV-15 → §6.10 fold por `SnapshotTasas` accesible; INV-NEW-SLICE05-1 → §6.10 + §6.7 implícitamente).
- [x] El happy path está presente (§6.1 caso simple, §6.2 árbol con Agrupador + dos terminales, §6.11/§6.12 casos de cómputo de `MontoTotal`).
- [x] Fold del evento documentado (§6.10).
- [x] Impactos en SharedKernel (§12), proyecciones (§8), endpoints HTTP (§9) y followups (§13) documentados.
- [x] Idempotencia decidida no en blanco (§7).
- [x] Cierre retroactivo de followup #13 documentado (§6.8 y §6.9).
- [x] Q2 de §10 resuelta (firma del usuario 2026-04-24, opción (a) — no validar `PeriodoFin >= ahora.Date`).

## 12. Impacto en SharedKernel (refactor transversal incluido en el slice)

Este slice introduce excepciones nuevas, comando, evento, métodos del agregado, y propiedades nuevas en `Presupuesto`. Mantiene el patrón slice 02/03/04 de incluir el refactor transversal del `DomainExceptionHandler` dentro del propio slice.

### 12.1 Excepciones nuevas

Todas heredan de `DominioException`, viven cada una en su propio archivo bajo `SharedKernel/`, y exponen propiedades fuertemente tipadas para aserción estructural.

1. **`PresupuestoSinMontosException(Guid PresupuestoId) : DominioException`**
   - Propiedad: `PresupuestoId: Guid`.
   - Uso: PRE-2. Lanza cuando no hay rubros, o cuando ningún rubro terminal tiene `Monto.EsPositivo`.

2. **`AprobacionConMultimonedaNoSoportadaException(Guid PresupuestoId, IReadOnlyList<Guid> RubrosConMonedaDistinta, Moneda MonedaBase) : DominioException`**
   - Propiedades: `PresupuestoId: Guid`, `RubrosConMonedaDistinta: IReadOnlyList<Guid>`, `MonedaBase: Moneda`.
   - Uso: PRE-3. Lanza cuando al menos un rubro terminal con `Monto.EsPositivo` tiene `Monto.Moneda != MonedaBase`. La lista contiene **todos** los `RubroId` conflictivos en orden de aparición en `_rubros`.
   - Excepción **temporal en su forma actual** — cuando exista catálogo de tasas (followup #24), su uso se reduce o desaparece.

`PresupuestoNoEsBorradorException` ya existe (slice 03 §12) y se reutiliza para PRE-1.

Mapeo HTTP (ver §9):
- `PresupuestoSinMontosException → 400`
- `AprobacionConMultimonedaNoSoportadaException → 400`
- `PresupuestoNoEsBorradorException → 409` (ya mapeado).

Se añaden al `switch` de `DomainExceptionHandler.Mapear` dentro del mismo slice (consistente con slice 02/03/04).

### 12.2 Nuevo comando

Archivo nuevo: `src/SincoPresupuesto.Domain/Presupuestos/Commands/AprobarPresupuesto.cs`. Firma en §2.

### 12.3 Nuevo evento

Archivo nuevo: `src/SincoPresupuesto.Domain/Presupuestos/Events/PresupuestoAprobado.cs`.

```csharp
public sealed record PresupuestoAprobado(
    Guid PresupuestoId,
    Dinero MontoTotal,
    IReadOnlyDictionary<Moneda, decimal> SnapshotTasas,
    DateTimeOffset AprobadoEn,
    string AprobadoPor);
```

Nota de serialización: `IReadOnlyDictionary<Moneda, decimal>` debe roundtrip correctamente en STJ con la configuración existente (followup #23 cerrado en slice 00 añade `[JsonConstructor]` a `Moneda`, lo que cubre las **claves** `Moneda` del dict). El `green` debe verificar que el dict serializa como objeto JSON `{"COP": 1.0, …}` o usar `Dictionary<string, decimal>` con conversión en el borde si STJ no soporta `Moneda` como clave de diccionario. Decisión final del `green`/infra-wire — el contrato del dominio es `IReadOnlyDictionary<Moneda, decimal>`; el wire format lo elige infra. Si surge fricción, abrir followup específico (no bloqueante para slice 05 con dict vacío).

### 12.4 Método nuevo en `Presupuesto`

```csharp
public PresupuestoAprobado AprobarPresupuesto(
    Commands.AprobarPresupuesto cmd,
    DateTimeOffset ahora);
```

Lógica (pseudocódigo):

```
ArgumentNullException.ThrowIfNull(cmd);

if (Estado != Borrador) throw new PresupuestoNoEsBorradorException(Estado);

var terminales = _rubros.Where(r => !_rubros.Any(otro => otro.PadreId == r.Id)).ToList();
var conMontoPositivo = terminales.Where(r => r.Monto.EsPositivo).ToList();

if (conMontoPositivo.Count == 0)
    throw new PresupuestoSinMontosException(Id);

var conMonedaDistinta = conMontoPositivo
    .Where(r => r.Monto.Moneda != MonedaBase)
    .Select(r => r.Id)
    .ToList();

if (conMonedaDistinta.Count > 0)
    throw new AprobacionConMultimonedaNoSoportadaException(Id, conMonedaDistinta, MonedaBase);

var montoTotal = conMontoPositivo
    .Aggregate(Dinero.Cero(MonedaBase), (acc, r) => acc + r.Monto);

var aprobadoPor = string.IsNullOrWhiteSpace(cmd.AprobadoPor) ? "sistema" : cmd.AprobadoPor;

return new PresupuestoAprobado(
    PresupuestoId: Id,
    MontoTotal: montoTotal,
    SnapshotTasas: new Dictionary<Moneda, decimal>(),
    AprobadoEn: ahora,
    AprobadoPor: aprobadoPor);
```

### 12.5 Nuevas propiedades en `Presupuesto`

```csharp
public Dinero MontoTotal { get; private set; }
public IReadOnlyDictionary<Moneda, decimal> SnapshotTasas { get; private set; }
    = new Dictionary<Moneda, decimal>();
public DateTimeOffset? AprobadoEn { get; private set; }
public string? AprobadoPor { get; private set; }
```

- `MontoTotal` queda en `default(Dinero)` antes de aprobar (i.e. `Dinero(0, default(Moneda))`). Esto puede romper INV-SK-3 al hacer `ToString()` antes de aprobar — pero como ningún consumidor del agregado debería leer `MontoTotal` antes de `Apply(PresupuestoAprobado)`, la situación no se materializa. Decisión del modeler: **mantener `Dinero` no-nullable** (consistente con `Rubro.Monto` slice 04 §12.2). Alternativa rechazada: `Dinero?` nullable — agrega ramas de chequeo `null` en consumers; el read model (§8) sí lo expone como nullable porque el JSON sí puede serializar `null` antes de aprobar.
- `SnapshotTasas` se inicializa a un diccionario vacío en la declaración para evitar NRE en consumers que lean antes del fold de `PresupuestoAprobado`.
- `AprobadoEn` y `AprobadoPor` sí son nullable: `null` antes de aprobar, no-null tras `Apply(PresupuestoAprobado)`.

### 12.6 Nuevo `Apply` en `Presupuesto`

```csharp
public void Apply(PresupuestoAprobado e)
{
    Estado = EstadoPresupuesto.Aprobado;
    MontoTotal = e.MontoTotal;
    SnapshotTasas = e.SnapshotTasas;
    AprobadoEn = e.AprobadoEn;
    AprobadoPor = e.AprobadoPor;
}
```

### 12.7 Sin cambios de comportamiento en SharedKernel existente

- `Dinero`, `Moneda`, `DominioException`, `Requerir`, `EstadoPresupuesto`, excepciones previas: sin tocar.
- `DomainExceptionHandler.Mapear`: suma de dos casos al `switch` (`PresupuestoSinMontosException`, `AprobacionConMultimonedaNoSoportadaException`), sin tocar los existentes.
- `Presupuesto.AgregarRubro` y `Presupuesto.AsignarMontoARubro`: **sin cambios** — la rama `if (Estado != Borrador) throw` ya existe (slice 03 línea 114, slice 04 línea 176). Slice 05 solo agrega los **tests** que la ejercen (§6.8 y §6.9).

## 13. Follow-ups generados por este slice

Se proponen a `FOLLOWUPS.md` al firmar la spec. Los números son tentativos — el reviewer los confirma al cerrar.

- **Cierre de #13** (existente): este slice **cierra** el followup #13 al incluir los escenarios retroactivos §6.8 (`AgregarRubro` post-aprobación lanza) y §6.9 (`AsignarMontoARubro` post-aprobación lanza). Se marca `[x]` en `FOLLOWUPS.md` cuando el slice cierre review con `approved`.

- **Refinamiento de #20** (existente, `TasaASnapshot` en `MontoAsignadoARubro`): este slice 05 **toca tangencialmente** #20 al introducir `SnapshotTasas` en `PresupuestoAprobado` (aunque vacío). El followup #20 sigue abierto y se refina así: "cuando exista el agregado/proyección `TasaDeCambio`, (a) populate `MontoAsignadoARubro.TasaASnapshot` con la tasa vigente al asignar y (b) populate `PresupuestoAprobado.SnapshotTasas` con todas las tasas requeridas para convertir las partidas a `MonedaBase`." Ambos sub-trabajos comparten el mismo disparador (`TasaDeCambioRegistrada`).

- **#24 (nuevo)** — **`AprobarPresupuesto` con multimoneda real**. Origen: slice-05, spec §2 + §4 PRE-3 + §12.1. Cuando exista el slice `TasaDeCambioRegistrada` y la proyección `TasasDeCambioVigentes`:
  - Eliminar PRE-3 actual (rechazo por moneda distinta a `MonedaBase`).
  - Reemplazar por: el handler de `AprobarPresupuesto` consulta `TasasDeCambioVigentes` para cada `Moneda` distinta presente en partidas con monto > 0, construye el `SnapshotTasas: IReadOnlyDictionary<Moneda, decimal>` y lo pasa al método del agregado (o se inyecta como dependencia del comando — decisión del modeler en su slice).
  - INV-14 pasa a su forma plena: si falta tasa para alguna moneda → nueva excepción (p.ej. `TasaDeCambioFaltanteException(Moneda monedaSinTasa)`).
  - `MontoTotal` se calcula como `partidas.Aggregate(Dinero.Cero(MonedaBase), (acc, r) => acc + r.Monto.En(MonedaBase, snapshotTasas[r.Monto.Moneda]))`.
  - `AprobacionConMultimonedaNoSoportadaException` queda obsoleta — **borrarla** (o documentarla como deprecated si se quiere mantener código legacy).
  - Disparador: cierre del slice `TasaDeCambioRegistrada`.

- **#25 (nuevo)** — **Proyección `PresupuestoBaselineEnMonedaBase`**. Origen: slice-05, spec §8. `(PresupuestoId) → { MontoTotal: Dinero, AprobadoEn: DateTimeOffset, SnapshotTasas: Dict<Moneda, decimal> }`. Alimentada por `PresupuestoAprobado`. En MVP slice 05 los datos están disponibles directamente en `PresupuestoReadModel` (§8); esta proyección dedicada se justifica cuando aparezcan vistas de "live vs. baseline" (hotspots §2). Disparador: primera UI que compare baseline aprobado contra valor live.

- **#26 (nuevo)** — **Comando `RevertirAprobacion` (opcional, post-MVP)**. Origen: slice-05, spec §10 Q1. Si el negocio demanda corregir errores tras aprobar sin pasar por `Activo → Cerrado`, modelar `RevertirAprobacion` como comando que emite `AprobacionRevertida` (vuelve a `Borrador`) con motivo obligatorio. **No bloquea slice 05.** Disparador: PO confirma necesidad real.

- **Followup condicional según firma de §10 Q2** — si el usuario firma Q2 = (b), añadir escenario `§6.X — Violación PeriodoFiscalVencido` y excepción `PeriodoFiscalVencidoException(DateOnly PeriodoFin, DateOnly Hoy)` en `SharedKernel`. Si firma Q2 = (a), descartar.

- **Cierre parcial de #12** (existente, `RubroTipo` / INV-9): este slice **avanza un paso más** al usar la distinción operacional Agrupador/Terminal (presencia de hijos en `_rubros`) en el cálculo de `MontoTotal`. Mismo patrón que slice 04 §6.7. `RubroTipo` explícito sigue diferido — #12 se cierra cuando exista `RubroConvertidoAAgrupador` o cuando un slice declare la promoción del tipo a campo del entity.
