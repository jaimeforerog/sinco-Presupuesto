# Slice 04 — AsignarMontoARubro

**Autor:** domain-modeler
**Fecha:** 2026-04-24
**Estado:** firmado
**Agregado afectado:** `Presupuesto` (los `Rubro` viven como entities dentro del agregado — ver `01-event-storming-mvp.md` §3 y `slices/03-agregar-rubro/spec.md`).
**Decisiones previas relevantes:**
- `01-event-storming-mvp.md` §3 (agregado + rubros como entities), §4 (comando `AsignarMontoARubro`, precondiciones "Borrador" y "Monto ≥ 0"), §5 (evento original `MontoAsignadoARubro` con `decimal` — reemplazado), §6 (invariantes 1–7).
- `02-decisiones-hotspots-mvp.md` §1 (árbol n-ario; Agrupador vs Terminal: un Agrupador no tiene monto directo, su total = suma de hijos), §2 (multimoneda a nivel de partida: partidas pueden estar en cualquier ISO 4217; `Monto: Dinero(valor, moneda)` en vez de `decimal`; `TasaASnapshot` informativo hacia `MonedaBase`; INV-13, INV-14, INV-15).
- `slices/00-shared-kernel/spec.md` (contrato público de `Dinero`, `Moneda`, `MonedasDistintasException`, `FactorDeConversionInvalidoException`, `DominioException`, `Requerir.Campo`).
- `slices/01-crear-presupuesto/spec.md` (precedente: `DominioException`, tests por tipo y propiedades; `MonedaBase` del presupuesto fijada al crear; patrón `CreadoPor = "sistema"` default).
- `slices/02-configurar-moneda-local-del-tenant/spec.md` (precedente: patrón `ConfiguradoPor = "sistema"` default; refactor transversal acotado al slice).
- `slices/03-agregar-rubro/spec.md` (precedente: entity `Rubro { Id, Codigo, Nombre, PadreId?, Nivel }`, firma del caso de uso como método de instancia del agregado, INV-3 diferida, followups #12 y #13).
- `FOLLOWUPS.md` #8 (validación `ConfiguracionTenantActual` — pertenece al handler, no al dominio), #12 (`RubroTipo` / INV-9 — este slice **no lo introduce**, usa la presencia de hijos como proxy operativo — ver §10 y §13), #13 (INV-3 diferida a `AprobarPresupuesto`).

---

## 1. Intención

El responsable de presupuesto necesita **asignar un monto** a un rubro existente del árbol **mientras el presupuesto está en Borrador**, pudiendo declarar el monto en **cualquier moneda ISO 4217** (no necesariamente la `MonedaBase` del presupuesto — decisión multimoneda, hotspots §2). El monto puede ser **cero** (forma válida de "resetear" o "dejar explícitamente sin asignar"). La asignación puede ser la **primera** (monto nuevo) o una **reasignación** (ya había monto previo, potencialmente en moneda distinta). El comando queda prohibido sobre rubros que tienen al menos un hijo, porque en esa situación el rubro es **Agrupador** y su total se calcula desde los hijos, no se asigna directamente (hotspots §1).

Este slice **introduce la propiedad `Monto` en la entity `Rubro`** — hoy inexistente — para que el fold de `MontoAsignadoARubro` pueda reflejar el estado post-asignación. Es también el **primer slice con `Dinero` en el payload de un evento del dominio**, cumpliendo INV-13.

## 2. Comando

```csharp
public sealed record AsignarMontoARubro(
    Guid RubroId,
    Dinero Monto,
    string AsignadoPor = "sistema");
```

- `RubroId`: identidad del rubro destino **dentro del agregado**. No se pasa `PresupuestoId` en el comando: el agregado ya está rehidratado y expone `Id` vía fold (patrón idéntico a slice 03 — ver `slices/03-agregar-rubro/spec.md` §2).
- `Monto`: `Dinero` con valor y moneda. La `Moneda` llega validada por construcción desde el VO (ver slice 00 §6.15/§6.16); el dominio **no** revalida ISO 4217 aquí. El `Valor` sí se valida (≥ 0) como precondición explícita.
- `AsignadoPor`: string libre. Vacío / whitespace → se normaliza a `"sistema"` al emitir el evento (patrón slice 01 `CreadoPor`, slice 02 `ConfiguradoPor`).

Firma del caso de uso como método de instancia del agregado ya reconstruido (patrón slice 03):

```csharp
public MontoAsignadoARubro AsignarMontoARubro(
    Commands.AsignarMontoARubro cmd,
    DateTimeOffset ahora);
```

No recibe `id` desde fuera porque no crea identidad nueva: opera sobre un `Rubro` ya existente identificado por `cmd.RubroId`.

### Nota sobre `TasaASnapshot` (diferido)

Hotspots §2 menciona `TasaASnapshot` como campo informativo del evento: "tasa a `MonedaBase` del presupuesto en el momento del registro". Decisión del modeler: **NO se incluye en este slice** y se difiere como followup. Justificación:

- El snapshot de tasas es responsabilidad del slice `AprobarPresupuesto` (INV-15 — snapshot congelado en `PresupuestoAprobado`).
- El agregado `TasaDeCambio` / proyección `TasasDeCambioVigentes` **no existe aún** — sin catálogo, `TasaASnapshot` sería un campo siempre `null`, ruido innecesario que mantener en el payload.
- La decisión es reversible sin migrar eventos si se añade como propiedad opcional nueva en el futuro (append-only al record), pero mejor introducirla cuando tenga semántica real.

Followup #20 en §13.

## 3. Evento(s) emitido(s)

| Evento | Payload | Cuándo |
|---|---|---|
| `MontoAsignadoARubro` | `PresupuestoId: Guid`, `RubroId: Guid`, `Monto: Dinero`, `MontoAnterior: Dinero`, `AsignadoEn: DateTimeOffset`, `AsignadoPor: string` | Al aceptar el comando sobre un presupuesto en Borrador cuyo rubro destino exista, **no** sea Agrupador (no tenga hijos), y el `Monto` tenga `Valor ≥ 0`. |

- Nombre del evento: **`MontoAsignadoARubro`** (conservado del event-storming §5; el cambio vs. event-storming es el **payload**, no el nombre — ver hotspots §2).
- `Monto: Dinero` cumple **INV-13** (ninguna cantidad monetaria en eventos es `decimal` pelado).
- `MontoAnterior: Dinero`:
  - **Primera asignación** al rubro → `MontoAnterior = Dinero.Cero(cmd.Monto.Moneda)`. Se elige la moneda del comando (no la `MonedaBase`) porque es la más informativa del "delta" que representa la transición; si `Monto.Valor = 0` también, el par `(Monto, MontoAnterior)` queda `(0, 0)` en la misma moneda y es auto-documentado.
  - **Reasignación en la misma moneda** → `MontoAnterior = Rubro.Monto` actual.
  - **Reasignación cambiando moneda** → `MontoAnterior` queda en la **moneda anterior** del rubro, `Monto` queda en la **moneda nueva**. El par `(Monto, MontoAnterior)` es auditable y no viola `MonedasDistintasException` porque son dos `Dinero` separados en campos distintos, no una operación aritmética.

## 4. Precondiciones

Todas las excepciones heredan de `SincoPresupuesto.Domain.SharedKernel.DominioException`. Los tests verifican **tipo + propiedades**, nunca mensajes.

- `PRE-1`: `cmd.RubroId != Guid.Empty` — excepción: `CampoRequeridoException` con `NombreCampo = "RubroId"`. Precedente idéntico a slice 03 §4 PRE-3.
- `PRE-2`: el rubro con `Id = cmd.RubroId` **existe** dentro del agregado — excepción: **`RubroNoExisteException(Guid RubroId)`** (nueva — ver §12). Se distingue conceptualmente de `RubroPadreNoExisteException` (slice 03) porque aquí el rubro es el **destino** de la operación, no un padre referenciado. La propiedad de la excepción es `RubroId` (no `RubroPadreId`).
- `PRE-3`: `cmd.Monto.Valor >= 0m` — excepción: **`MontoNegativoException(Dinero MontoIntentado)`** (nueva — ver §12). El propio `Dinero` admite valores negativos semánticamente (slice 00 §6.3, §6.9 — representa "saldo a favor" o "contra-asiento"), pero **en el contexto de asignación a un rubro** un negativo no tiene sentido (INV-2 del event-storming §6).
- `PRE-4` (normalización, no fallo): `cmd.AsignadoPor` nulo / vacío / whitespace → `"sistema"` en el evento emitido. No lanza. Precedente slice 01 §6.2, slice 02 §6.2.

Nota: **no hay PRE de coincidencia de moneda**. La moneda del `Monto` puede ser cualquier ISO 4217 válida y **no** se exige que sea igual a `presupuesto.MonedaBase`. Justificación en §5 y hotspots §2 ("multimoneda a nivel de partida").

Nota: la validación "el tenant tiene `ConfiguracionTenantActual`" pertenece al **handler**, no al agregado (followup #8). Fuera del alcance de este slice.

## 5. Invariantes tocadas

- **`INV-2`** (event-storming §6): "El monto asignado a un rubro no puede ser negativo." Se ejercita en §6.6. Excepción nueva: `MontoNegativoException`.
- **`INV-3`** (event-storming §6): "No se pueden agregar, retirar ni reasignar rubros en estado distinto a **Borrador**." **Diferida** al slice `AprobarPresupuesto` con el mismo criterio que slice 03 (ver `slices/03-agregar-rubro/spec.md` §6.7 y followup #13). La rama `if (Estado != Borrador) throw new PresupuestoNoEsBorradorException(Estado);` **sí existe** en el método del agregado (la excepción ya está en SharedKernel desde slice 03); el test de sanidad en §6.8 verifica que en Borrador no lanza.
- **`INV-7`** (reformulada por hotspots §2): "La `MonedaBase` del presupuesto es inmutable tras crearse." Este slice **no** la modifica: una asignación en moneda distinta a `MonedaBase` no implica cambiar la base del presupuesto (hotspots §2 deja claro que `MonedaBase` es la moneda de **reporte agregado**, no la moneda obligatoria de cada partida).
- **`INV-13`** (hotspots §2): "Toda cantidad monetaria en un evento se almacena como `Dinero(valor, moneda)` — nunca `decimal` pelado." Este slice **es el primero del dominio en ejercitarla** — el payload de `MontoAsignadoARubro` usa `Dinero` en `Monto` y `MontoAnterior`. Se valida implícitamente en todos los happy paths (§6.1–§6.3) y en el fold (§6.10–§6.11).
- **Nueva — `INV-NEW-SLICE04-1`**: "No se puede asignar monto a un rubro que ya tiene hijos (Agrupador)." Derivada de hotspots §1 ("Agrupador: total = suma de hijos"). Excepción nueva: `RubroEsAgrupadorException`. Esta invariante **emerge de la presencia de hijos** en la colección `_rubros` del agregado — no requiere introducir el campo `RubroTipo` todavía (followup #12 lo hará cuando se implemente `RubroConvertidoAAgrupador`). Operacionalmente: `_rubros.Any(r => r.PadreId == cmd.RubroId) ⇒ lanza`.

Invariantes **tocadas pero no ejercitables en este slice** (ver §10 para tratamiento):
- **`INV-9`** (hotspots §1): "Un rubro terminal no puede tener hijos." Este slice es el **inverso operativo** — prohíbe asignar monto a quien ya tiene hijos (vía `INV-NEW-SLICE04-1`). La formulación simétrica "no se puede añadir hijo a un terminal con monto asignado" sigue diferida a followup #12 (depende de introducir `RubroTipo` o de que `AgregarRubro` consulte `Rubro.Monto`).

## 6. Escenarios Given / When / Then

Cada escenario empieza con un `PresupuestoCreado` en `dados` (para establecer `MonedaBase` y `ProfundidadMaxima`) y, cuando aplica, uno o varios `RubroAgregado` para poblar el árbol. El fold de `Presupuesto` deja el estado en Borrador con la estructura de rubros esperada.

### 6.1 Happy path — primera asignación a rubro Terminal sin hijos (misma moneda que `MonedaBase`)

**Given**
- `PresupuestoCreado(PresupuestoId=P, TenantId="acme", Codigo="OBRA-2026-01", …, MonedaBase=Moneda.COP, ProfundidadMaxima=10, …)`
- `RubroAgregado(RubroId=R1, Codigo="01", Nombre="Costos Directos", RubroPadreId=null, …)`

**When**
- `AsignarMontoARubro(RubroId=R1, Monto=new Dinero(1_000_000m, Moneda.COP), AsignadoPor="alice")` con `ahora=T`.

**Then**
- Emite un único `MontoAsignadoARubro` con:
  - `PresupuestoId = P`
  - `RubroId = R1`
  - `Monto = Dinero(1_000_000, COP)`
  - `MontoAnterior = Dinero(0, COP)` (primera asignación → `Dinero.Cero(Monto.Moneda)`)
  - `AsignadoEn = T`
  - `AsignadoPor = "alice"`

### 6.2 Happy path — reasignación en la misma moneda

**Given**
- `PresupuestoCreado(…, MonedaBase=COP)`
- `RubroAgregado(RubroId=R1, Codigo="01", RubroPadreId=null, …)`
- `MontoAsignadoARubro(RubroId=R1, Monto=Dinero(1_000_000, COP), MontoAnterior=Dinero(0, COP), …)`

**When**
- `AsignarMontoARubro(RubroId=R1, Monto=Dinero(2_500_000, COP), AsignadoPor="alice")` con `ahora=T2`.

**Then**
- Emite `MontoAsignadoARubro` con `Monto = Dinero(2_500_000, COP)`, `MontoAnterior = Dinero(1_000_000, COP)` (refleja el monto previo del rubro), `AsignadoEn = T2`.

### 6.3 Happy path — reasignación cambiando moneda (COP → USD)

**Given**
- `PresupuestoCreado(…, MonedaBase=COP)`
- `RubroAgregado(RubroId=R1, …)`
- `MontoAsignadoARubro(RubroId=R1, Monto=Dinero(1_000_000, COP), MontoAnterior=Dinero(0, COP), …)`

**When**
- `AsignarMontoARubro(RubroId=R1, Monto=Dinero(250m, Moneda.USD), AsignadoPor="alice")` con `ahora=T2`.

**Then**
- Emite `MontoAsignadoARubro` con:
  - `Monto = Dinero(250, USD)`
  - `MontoAnterior = Dinero(1_000_000, COP)` (en la moneda **anterior** — auditable; las dos monedas coexisten en campos distintos del evento).
  - `AsignadoEn = T2`.
- **No** lanza `MonedasDistintasException`: no hay operación aritmética que mezcle monedas; son dos `Dinero` independientes en campos distintos.

### 6.4 Happy path — moneda del monto distinta a `MonedaBase` desde la primera asignación

**Given**
- `PresupuestoCreado(…, MonedaBase=Moneda.COP)`
- `RubroAgregado(RubroId=R1, Codigo="02", Nombre="Insumos Importados", RubroPadreId=null, …)`

**When**
- `AsignarMontoARubro(RubroId=R1, Monto=Dinero(5_000m, Moneda.USD), AsignadoPor="alice")`.

**Then**
- Emite `MontoAsignadoARubro` con `Monto = Dinero(5_000, USD)`, `MontoAnterior = Dinero(0, USD)` (cero en la moneda del comando — la primera asignación nunca pelea con `MonedaBase`).

### 6.5 Happy path — monto cero es válido

**Given**
- `PresupuestoCreado(…)`
- `RubroAgregado(RubroId=R1, …)`

**When**
- `AsignarMontoARubro(RubroId=R1, Monto=Dinero.Cero(Moneda.COP), AsignadoPor="")`.

**Then**
- Emite `MontoAsignadoARubro` con `Monto = Dinero(0, COP)`, `MontoAnterior = Dinero(0, COP)`, `AsignadoPor = "sistema"` (normalización).
- INV-2 se cumple (≥ 0 incluye 0 explícitamente; event-storming §4 define la cota como "Monto ≥ 0").

### 6.6 Violación `INV-2` — monto negativo

**Given**
- `PresupuestoCreado(…)`
- `RubroAgregado(RubroId=R1, …)`

**When**
- `AsignarMontoARubro(RubroId=R1, Monto=new Dinero(-1m, Moneda.COP))`.

**Then**
- Lanza `MontoNegativoException` con `MontoIntentado = Dinero(-1, COP)`.

### 6.7 Violación `INV-NEW-SLICE04-1` — el rubro es Agrupador (tiene hijos)

**Given**
- `PresupuestoCreado(…, ProfundidadMaxima=10)`
- `RubroAgregado(RubroId=R1, Codigo="01", RubroPadreId=null, …)`
- `RubroAgregado(RubroId=R2, Codigo="01.01", RubroPadreId=R1, …)` (hace de `R1` un Agrupador)

**When**
- `AsignarMontoARubro(RubroId=R1, Monto=Dinero(100_000, COP))`.

**Then**
- Lanza `RubroEsAgrupadorException` con `RubroId = R1`.

### 6.8 Sanidad `INV-3` — en Borrador no lanza (escenario negativo diferido)

**Diferido al slice `AprobarPresupuesto`** (criterio idéntico a slice 03 §6.7, firmado por el usuario para slice 03 opción (a)). INV-3 queda declarada en §5 y la rama `if (Estado != Borrador) throw` existe en el método (reutiliza `PresupuestoNoEsBorradorException` ya introducida en slice 03 §12). Test de sanidad en slice 04: con el presupuesto en Borrador (único estado posible hoy) la invocación happy-path de §6.1 ejerce el camino "no lanza" implícitamente. El followup #13 compromete el escenario negativo cuando exista `AprobarPresupuesto`.

### 6.9 Violación `PRE-1` — `RubroId` vacío

**Given** `PresupuestoCreado(…)` + al menos un `RubroAgregado`.
**When** `AsignarMontoARubro(RubroId=Guid.Empty, Monto=Dinero(100, COP))`.
**Then** lanza `CampoRequeridoException` con `NombreCampo = "RubroId"`.

### 6.10 Violación `PRE-2` — el rubro destino no existe

**Given**
- `PresupuestoCreado(…)`
- `RubroAgregado(RubroId=R1, …)`

**When** `AsignarMontoARubro(RubroId=R_inexistente, Monto=Dinero(100, COP))` con `R_inexistente != R1` (otro `Guid.NewGuid()`).
**Then** lanza `RubroNoExisteException` con `RubroId = R_inexistente`.

### 6.11 Normalización `AsignadoPor` vacío / whitespace → `"sistema"`

**Given** `PresupuestoCreado(…)` + `RubroAgregado(RubroId=R1, …)`.
**When** `AsignarMontoARubro(RubroId=R1, Monto=Dinero(100, COP), AsignadoPor=X)` con `X` en `{ "", "   ", null }` (el `null` solo aplica si el compilador lo permite — el parámetro del record tiene default `"sistema"`, así que un `null` explícito es el caso de interés real).
**Then** emite `MontoAsignadoARubro` con `AsignadoPor = "sistema"`.

### 6.12 Fold — primera asignación deja `Rubro.Monto` con el valor asignado

**Given**
- Eventos ordenados: `PresupuestoCreado(…, MonedaBase=COP)`, `RubroAgregado(RubroId=R1, Codigo="01", RubroPadreId=null, …)`, `MontoAsignadoARubro(RubroId=R1, Monto=Dinero(1_000_000, COP), MontoAnterior=Dinero(0, COP), …)`.

**When** reconstruir el agregado aplicando los tres eventos (fold).

**Then**
- `agg.Rubros` contiene exactamente un rubro con `Id=R1`, `Codigo="01"`, y el rubro tiene `Monto` accesible con valor `Dinero(1_000_000, COP)`.

### 6.13 Fold — reasignación cambiando moneda deja `Rubro.Monto` en la moneda nueva

**Given**
- Eventos ordenados: `PresupuestoCreado(…, MonedaBase=COP)`, `RubroAgregado(RubroId=R1, …)`, `MontoAsignadoARubro(RubroId=R1, Monto=Dinero(1_000_000, COP), MontoAnterior=Dinero(0, COP), …)`, `MontoAsignadoARubro(RubroId=R1, Monto=Dinero(250, USD), MontoAnterior=Dinero(1_000_000, COP), …)`.

**When** reconstruir el agregado aplicando los cuatro eventos (fold).

**Then** el rubro `R1` tiene `Monto = Dinero(250, USD)` (la moneda cambió junto con el valor; el estado del rubro siempre refleja la **última** asignación).

### 6.14 Moneda bien formada del VO `Moneda` — el dominio no revalida ISO 4217

**Given** `PresupuestoCreado(…)` + `RubroAgregado(RubroId=R1, …)`.
**When** `AsignarMontoARubro(RubroId=R1, Monto=new Dinero(42m, new Moneda("EUR")))`.
**Then** emite `MontoAsignadoARubro` con `Monto.Moneda.Codigo = "EUR"`. (Este escenario documenta que el agregado confía en la invariante del VO — ver slice 00 §6.15/§6.16 — y **no** ejecuta validaciones adicionales sobre el código. La validación está centralizada en `Moneda.ctor`.)

## 7. Idempotencia / retries

- **No idempotente por diseño del comando**: cada `AsignarMontoARubro` quiere registrar una **asignación nueva** (primera o reasignación). Reintentos con el mismo `RubroId` y `Monto` producirán dos eventos `MontoAsignadoARubro` sucesivos, el segundo con `MontoAnterior = Monto` — es decir, un "delta cero". Semánticamente no es un error, pero sí es ruido auditable. La responsabilidad de no reintentar es del caller.
- **Protección anti-reintento**: el handler usa `expected version` de Marten al hacer `session.Events.Append(streamId, event)` — si hay colisión de versión entre dos `AsignarMontoARubro` concurrentes, Marten lanza y el handler retornará 409. Fuera del alcance del dominio.
- **IdempotencyKey**: no se introduce. Criterio idéntico a slice 01 §7 y slice 03 §7.
- **Sobre la "suma de asignados = monto total"** (event-storming §3): ese invariante global del presupuesto **no se evalúa aquí**. Su verificación requiere (a) conocer la conversión a `MonedaBase` de cada asignación en distintas monedas — que es rol del snapshot de tasas en `AprobarPresupuesto` — y (b) comparar contra un "monto total" que aún no modelamos explícitamente. Queda cubierto por INV-15 y el slice `AprobarPresupuesto`.

## 8. Impacto en proyecciones / read models

- **`PresupuestoReadModel`** (existente tras slice 01 y 03): la colección `Rubros` debe ganar el campo `Monto` (por ejemplo `RubroReadModel.Monto: { valor, moneda }` serializado como objeto plano — decisión final del `green`/infra-wire dentro de lo que Marten serializa limpio). La proyección `PresupuestoProjection` amplía su `Apply(MontoAsignadoARubro)` para actualizar el rubro correspondiente en la lista. **Dentro del alcance del slice** por coherencia con slice 03 (que ya incluye `Apply(RubroAgregado)` en la proyección).
- **`SaldoPorRubro`** (event-storming §7): proyección candidata natural — `(PresupuestoId, RubroId) → Dinero`. En el MVP el saldo = monto asignado (sin ejecución). Se sugiere **diferir** a un slice dedicado para mantener este slice acotado al agregado + su read model primario. Followup #21.
- **`EstructuraPresupuestal`** (event-storming §7): sigue diferida, mismo criterio que slice 03 §8.

## 9. Impacto en endpoints HTTP

- **`POST /api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros/{rubroId}/monto`** — asigna (o reasigna) el monto del rubro.
  - Request body: `{ monto: { valor: 1000000, moneda: "COP" }, asignadoPor?: "alice" }`. La forma del DTO de `Dinero` la elige infra-wire (lo habitual: objeto anidado con `valor` decimal y `moneda` string ISO 4217; la deserialización construye `new Moneda(string)` y por tanto valida ISO en el borde).
  - **201 Created** — primera asignación. `Location: GET /api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros/{rubroId}`. Body: `{ rubroId, monto, montoAnterior, asignadoEn, asignadoPor }`. (Criterio: los handlers previos del slice 01/02/03 usan 201 para cualquier `Append` exitoso; se mantiene por consistencia.)
  - **200 OK** — reasignación (si infra-wire quiere distinguir; alternativamente 201 siempre por consistencia con el resto). Recomendación del modeler: **201 siempre**, porque el caso es "se crea un nuevo evento" y el cliente puede inferir primera/reasignación del `montoAnterior` devuelto. Decisión final de infra-wire.
  - **400 Bad Request** ante:
    - `CampoRequeridoException` (PRE-1, `RubroId` vacío — aunque viene en la ruta, un `Guid.Empty` sintáctico sí es posible).
    - `CodigoMonedaInvalidoException` (lanzada por `Moneda.ctor` al deserializar moneda inválida — validación en borde).
    - `MontoNegativoException` (INV-2).
  - **404 Not Found** — el `presupuestoId` no existe (Marten retorna stream vacío → handler lanza `PresupuestoNoEncontradoException`). Criterio coherente con slice 03.
  - **409 Conflict** ante:
    - `RubroNoExisteException` (el rubro destino no existe en el agregado — el presupuesto sí, pero el rubro dentro no). El 409 es lógico-estado idéntico al uso que slice 03 hace de `RubroPadreNoExisteException` (ver slice 03 §9).
    - `RubroEsAgrupadorException` (el rubro tiene hijos — estado incompatible).
    - `PresupuestoNoEsBorradorException` (cuando exista transición a Aprobado — followup #13).
  - `PresupuestoId` y `RubroId` provienen de la ruta; el handler **no** genera `Guid` nuevo en este slice (la identidad del evento es del stream, no del rubro).

Nota sobre `Dinero` en la capa HTTP: el DTO deserializa a `Dinero` construyendo `new Moneda(string)` y un `decimal`. Cualquier fallo de construcción del `Moneda` lanza `CodigoMonedaInvalidoException`, que ya es `DominioException` → 400 vía `DomainExceptionHandler` (slice 00 §9). **No se requiere mapeo nuevo** en el handler para este slice — los tipos introducidos en §12 se añaden al `switch` junto con los de slice 03.

## 10. Preguntas abiertas

Cuatro preguntas candidatas evaluadas. **Tres** quedan resueltas por el modeler (no requieren firma del usuario); **una** se eleva porque tiene impacto transversal en el ciclo de vida de los rubros.

- [x] **¿Permitir `Monto.Moneda != MonedaBase`?** — **Resuelto: sí.** Justificación directa en hotspots §2 "partidas en cualquier ISO 4217". Si se exigiera igualdad, el slice violaría la decisión firmada de multimoneda. No se añade PRE de coincidencia.
- [x] **¿Permitir `Monto = 0`?** — **Resuelto: sí.** La precondición del event-storming §4 es `Monto ≥ 0` (no `> 0`). Cero es un valor útil: "reset explícito", "dejar rubro sin asignar pero marcado como revisado". Escenario §6.5 lo ejercita.
- [x] **¿Cómo representar `MontoAnterior` al cambiar moneda?** — **Resuelto: en la moneda anterior.** El evento lleva dos `Dinero` independientes (no hay operación aritmética que los combine), por lo que no viola `MonedasDistintasException`. Escenario §6.3 y §6.13 lo ejercitan.
- [x] **¿Nombre de la excepción "rubro destino no existe"?** — **Resuelto: `RubroNoExisteException(Guid RubroId)`.** No se reutiliza `RubroPadreNoExisteException` (slice 03) porque la semántica del error — "destino de la operación" vs. "padre referenciado" — es distinta y la propiedad se llama `RubroId` (no `RubroPadreId`). El costo marginal de tener dos excepciones con el mismo mapeo HTTP es bajo comparado con el beneficio de que la propiedad estructurada del error se lea natural al catchear.
- [x] **¿`TasaASnapshot` entra en el evento?** — **Resuelto: diferido a followup #20.** Justificación en §2 "Nota sobre `TasaASnapshot`". Sin catálogo de tasas, el campo sería siempre `null`.
- [x] **Q1 — ¿Qué pasa con el `Monto` previamente asignado a un rubro cuando ese rubro recibe su primer hijo vía `AgregarRubro` (i.e. cuando se convierte de Terminal a Agrupador implícitamente)?** El slice 03 no contempla hoy que el padre pudiera tener `Monto` asignado (campo aún no existe). Al introducir `Rubro.Monto` en este slice, se abre la pregunta transversal: cuando slice 03 ejecute `AgregarRubro(..., RubroPadreId=R1)` sobre un `R1` con `Monto != Cero`, ¿se debe:
  - **(a) Bloquear** con una excepción (p.ej. `RubroPadreTieneMontoAsignadoException`)? Alinea con hotspots §1 ("Agrupador no tiene monto directo") y obliga a un comando explícito (futuro `RubroConvertidoAAgrupador`) que limpie el monto antes de poder anidar.
  - **(b) Permitir + emitir evento implícito** (`RubroConvertidoAAgrupador` automático) que resetee el monto del padre. Es más "amigable" pero introduce eventos implícitos que el cliente no pidió — anti-patrón event-storming.
  - **(c) Permitir sin tocar** — el padre queda con `Monto` residual que la proyección ignora al calcular totales. Deja el estado inconsistente con hotspots §1 (un Agrupador no debería tener monto directo).
  - **(d) Diferir la decisión** a followup dedicado — este slice 04 **no toca `AgregarRubro`**, así que el problema no bloquea. La recomendación del modeler es **(d) + anotar followup #22** con la preferencia por **(a)** cuando se aborde (consistencia con el rechazo simétrico de `AsignarMontoARubro` sobre Agrupadores en §6.7). La firma del usuario puede aceptar (d) o pedir otra.

_Esta Q1 se eleva porque involucra a otro slice (03) y a un slice futuro (`RubroConvertidoAAgrupador`), y la respuesta condiciona si hay algún trabajo adicional **dentro** de este slice 04 (no lo hay bajo (d); habría si el usuario prefiere (a) con refuerzo inmediato)._

**Firma del usuario (2026-04-24): Q1 = (d).** Diferir la decisión a followup #22, con preferencia por (a) cuando se aborde. Slice 04 NO toca `AgregarRubro` ni introduce restricción en el padre — solo rechaza asignación a rubros que ya son Agrupadores por tener hijos.

## 11. Checklist pre-firma

- [x] Todas las precondiciones mapean a un escenario Then (PRE-1 → §6.9; PRE-2 → §6.10; PRE-3 → §6.6; PRE-4 → §6.11).
- [x] Todas las invariantes tocadas y ejercitables mapean a un escenario Then (INV-2 → §6.6; INV-3 diferida con justificación §5 + §6.8 + followup #13; INV-13 → §6.1–§6.4 y §6.12–§6.13; INV-NEW-SLICE04-1 → §6.7). INV-9 documentada como no ejercitable en este slice (§5 + followup #12).
- [x] El happy path está presente (§6.1 primera asignación, §6.2 reasignación misma moneda, §6.3 reasignación cambiando moneda, §6.4 moneda ≠ MonedaBase desde primera asignación, §6.5 monto cero).
- [x] Fold del evento documentado (§6.12 primera asignación, §6.13 reasignación con cambio de moneda).
- [x] Impactos en SharedKernel (§12), proyecciones (§8), endpoints HTTP (§9) y followups (§13) documentados.
- [x] Idempotencia decidida no en blanco (§7).
- [x] Q1 de §10 resuelta (firma del usuario 2026-04-24, opción **(d)**: diferir a followup con preferencia por (a)).

## 12. Impacto en SharedKernel (refactor transversal incluido en el slice)

Este slice introduce tipos nuevos en `SincoPresupuesto.Domain.SharedKernel` y extiende la entity `Rubro` del agregado `Presupuesto`.

### 12.1 Excepciones nuevas

Todas heredan de `DominioException`, viven cada una en su propio archivo bajo `SharedKernel/`, y exponen propiedades fuertemente tipadas para aserción estructural.

1. **`RubroNoExisteException(Guid RubroId) : DominioException`**
   - Propiedad: `RubroId: Guid`.
   - Uso: PRE-2. Se lanza cuando `AsignarMontoARubro.RubroId` no coincide con ningún rubro reconstruido del agregado.
   - No se reutiliza `RubroPadreNoExisteException` de slice 03 — ver §10 tercera decisión.

2. **`MontoNegativoException(Dinero MontoIntentado) : DominioException`**
   - Propiedad: `MontoIntentado: Dinero`.
   - Uso: INV-2. Se lanza cuando `cmd.Monto.Valor < 0m`.
   - La propiedad conserva la `Moneda` del intento para contexto del error (útil en logs/mapeo HTTP).

3. **`RubroEsAgrupadorException(Guid RubroId) : DominioException`**
   - Propiedad: `RubroId: Guid`.
   - Uso: INV-NEW-SLICE04-1. Se lanza cuando el rubro destino tiene al menos un hijo en `_rubros`.
   - Nombre semántico: se habla de "Agrupador" (terminología de hotspots §1), no de "tiene hijos" (formulación operativa).

Mapeo HTTP (ver §9): `RubroNoExisteException → 409`, `RubroEsAgrupadorException → 409`, `MontoNegativoException → 400`. Se añaden al `switch` de `DomainExceptionHandler.Mapear` dentro del mismo slice (consistente con el patrón slice 02 §12 y slice 03 §12 de incluir el refactor transversal del handler HTTP en el slice que introduce la excepción).

`CampoRequeridoException` y `PresupuestoNoEsBorradorException` ya existen (slice 00 §2.3, slice 03 §12) y se reutilizan.

### 12.2 Extensión de la entity `Rubro`

La entity `Rubro` (`src/SincoPresupuesto.Domain/Presupuestos/Rubro.cs`) gana una propiedad `Monto`:

```csharp
public sealed class Rubro
{
    public Guid Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public Guid? PadreId { get; init; }
    public int Nivel { get; init; }
    public Dinero Monto { get; init; }   // ← nuevo en slice 04
}
```

**Decisión del modeler: `Dinero` no-nullable inicializado a `Dinero.Cero(MonedaBase)` en `Apply(RubroAgregado)`, no `Dinero?` nullable.**

Justificación:
- `Dinero` es `readonly record struct` (slice 00 §2.1) → el `default(Dinero)` es `Dinero(0m, default(Moneda))`. `default(Moneda)` tiene `Codigo = null` — estado inválido que rompe la invariante INV-SK-3 del VO. Por tanto **no se puede** dejar `Monto` en `default`; hay que inicializarlo explícitamente.
- Alternativa A — **`Dinero?` nullable**: "un rubro sin monto asignado" queda semánticamente explícito (`null`), pero fuerza al resto del código (fold, proyección, aserciones de test) a distinguir `null` vs. `Cero`. La diferencia es sutil y ya existe `Dinero.Cero` como neutro aditivo (slice 00 §6.8).
- Alternativa B — **inicializar a `Dinero.Cero(presupuesto.MonedaBase)` en `Apply(RubroAgregado)`** [elegida]: más simple, el `MontoAnterior` en la primera asignación es `Dinero.Cero(cmd.Monto.Moneda)` (no la `MonedaBase` — ver §3), por lo que los dos conceptos (valor inicial del entity vs. valor "anterior" del evento) son coherentes aunque pueden estar en monedas distintas. El fold `Apply(MontoAsignadoARubro)` reemplaza `Monto` por `e.Monto` entero, haciendo irrelevante la moneda inicial tras la primera asignación.

Consecuencia operativa: el método `AsignarMontoARubro` del agregado, al armar el evento, determina `MontoAnterior` así:
```
MontoAnterior = rubroDestino.Monto.EsCero
              ? Dinero.Cero(cmd.Monto.Moneda)    // primera asignación: alinea a la moneda del comando
              : rubroDestino.Monto;              // reasignación: en la moneda del rubro anterior
```
El `.EsCero` se usa como proxy de "no ha habido asignación real"; esto es suficiente porque la asignación explícita a cero (§6.5) también resulta en `.EsCero == true` y la regla sigue válida (un `MontoAnterior = Cero(moneda)` que refleja el estado previo es equivalente al fallback, modulo la `Moneda` del Cero — que se alinea a la moneda del comando, comportamiento sobre el que el usuario no puede notar diferencia visible en proyecciones).

### 12.3 Nuevo evento

Archivo nuevo: `src/SincoPresupuesto.Domain/Presupuestos/Events/MontoAsignadoARubro.cs`.

```csharp
public sealed record MontoAsignadoARubro(
    Guid PresupuestoId,
    Guid RubroId,
    Dinero Monto,
    Dinero MontoAnterior,
    DateTimeOffset AsignadoEn,
    string AsignadoPor);
```

### 12.4 Nuevo comando

Archivo nuevo: `src/SincoPresupuesto.Domain/Presupuestos/Commands/AsignarMontoARubro.cs`. Firma en §2.

### 12.5 Nuevo `Apply` en `Presupuesto`

El agregado gana `Apply(MontoAsignadoARubro e)` que busca el rubro por `e.RubroId` y lo **reemplaza** en `_rubros` por una copia con `Monto = e.Monto` (las demás propiedades preservadas). Patrón: inmutable por `init`, mutación del array por index.

### 12.6 Sin cambios de comportamiento en SharedKernel existente

- `Dinero`, `Moneda`, `DominioException`, `Requerir.Campo`, excepciones previas: sin tocar.
- `DomainExceptionHandler.Mapear`: suma de tres casos al `switch`, sin tocar los existentes.

## 13. Follow-ups generados por este slice

Se proponen a `FOLLOWUPS.md` al firmar la spec. Los números son tentativos — el reviewer los confirma al cerrar.

- **#20** — **`TasaASnapshot` en el payload de `MontoAsignadoARubro`**. Origen: slice-04, spec §2 "Nota sobre `TasaASnapshot`". Añadir campo informativo `TasaASnapshot: decimal?` (o estructura más rica) al evento cuando exista el agregado/proyección `TasaDeCambio` / `TasasDeCambioVigentes`. Es evolución del record (nuevo parámetro con default → no rompe eventos históricos). Disparador: slice `TasaDeCambioRegistrada` (pre-requisito de `AprobarPresupuesto`).

- **#21** — **Proyección `SaldoPorRubro`**. Origen: slice-04, spec §8. `(PresupuestoId, RubroId) → Dinero` alimentada por `MontoAsignadoARubro`. En MVP saldo = asignado (sin ejecución). Evolucionará con compromiso/ejecución cuando aparezca el BC de Ejecución. Disparador: primera UI que muestre saldo por rubro o slice de consulta dedicado.

- **#22** — **Interacción `AgregarRubro` ↔ `Rubro.Monto` del padre**. Origen: slice-04, spec §10 Q1. Al introducir `Monto` en la entity `Rubro` en slice 04, el slice 03 queda con una rama implícita: `AgregarRubro(RubroPadreId=R)` sobre un `R` con `Monto != Cero` actualmente **pasa sin señal alguna**. Decidir (con el usuario) entre (a) bloquear con `RubroPadreTieneMontoAsignadoException`, (b) emitir `RubroConvertidoAAgrupador` implícito al agregar el primer hijo, (c) permitir + ignorar en proyección, o (d) tratarlo en un slice dedicado (`RubroConvertidoAAgrupador` explícito) que sea pre-requisito de `AgregarRubro` cuando el padre tenga monto. Recomendación del modeler: **(a) bloquear** para simetría con §6.7 de este slice (Agrupador → no asignar monto; con hijos → convertir explícitamente vía evento dedicado). Disparador: firma de §10 Q1.

- **Cierre condicional de #12** (existente, slice 03): este slice **avanza parcialmente** en INV-9 introduciendo el rechazo simétrico "no asignar monto a un rubro con hijos" (§6.7). La prohibición dual "no agregar hijo a un rubro con monto" queda pendiente (ver followup #22). `RubroTipo` explícito sigue no introducido — se infiere de la estructura. #12 permanece abierto hasta que haya un `RubroConvertidoAAgrupador` que modele la transición explícita.
