# Slice 03 — AgregarRubro

**Autor:** domain-modeler
**Fecha:** 2026-04-24
**Estado:** firmado
**Agregado afectado:** `Presupuesto` (los `Rubro` viven como entities dentro del agregado — ver `01-event-storming-mvp.md` §3).
**Decisiones previas relevantes:**
- `01-event-storming-mvp.md` §3 (agregado + rubros como entities), §3.2 (estados), §4 (comando `AgregarRubro`), §5 (payload de `RubroAgregado`), §6 (invariantes 1–7).
- `02-decisiones-hotspots-mvp.md` §1 (árbol n-ario, INV-A..INV-D), §4 (numeración jerárquica autogenerada con override, INV-E..INV-G), invariantes actualizadas INV-8..INV-12.
- `slices/01-crear-presupuesto/spec.md` (precedente: `DominioException`, tests por tipo y propiedades).
- `slices/02-configurar-moneda-local-del-tenant/spec.md` (precedente: §12 impacto en SharedKernel, §13 followups).
- `FOLLOWUPS.md` #5 (firma uniforme `CasoDeUso.Decidir`), #8 (validación `ConfiguracionTenantActual` — no afecta dominio, sí handler).

---

## 1. Intención

El responsable de presupuesto necesita incorporar un rubro (nodo) al árbol del presupuesto **mientras está en Borrador**, con un código validado y opcionalmente bajo un rubro padre ya existente. Este slice **introduce la estructura interna de rubros** dentro del agregado `Presupuesto` (hoy inexistente) y habilita la construcción del árbol n-ario. En MVP el código llega validado desde fuera (el caller puede autogenerarlo o recibir override del usuario); el dominio verifica formato, unicidad, relación con el padre y profundidad. La autogeneración jerárquica (`GeneradorCodigosJerarquicos` de hotspot §4) queda como servicio externo al agregado y se documenta como followup.

## 2. Comando

```csharp
public sealed record AgregarRubro(
    string Codigo,
    string Nombre,
    Guid? RubroPadreId = null);
```

La **firma del caso de uso** (en el agregado) es método de instancia, siguiendo el precedente del slice 01 donde `Create` fue factory por tratarse del stream vacío. Aquí el agregado ya existe:

```csharp
// Sobre la instancia ya reconstruida por fold:
public RubroAgregado AgregarRubro(AgregarRubro cmd, Guid rubroId, DateTimeOffset ahora);
```

- `rubroId`: `Guid` no vacío, provisto por el caller (handler) — **no** se genera en dominio. Patrón idéntico a `presupuestoId` en slice 01.
- `ahora`: `DateTimeOffset` inyectado (patrón `TimeProvider` en handler).
- `cmd.RubroPadreId = null` ⇒ rubro raíz del árbol del presupuesto.

### Nota sobre `RubroTipo` (Agrupador vs Terminal)

El event-storming §5 **no** incluye `RubroTipo` en el payload de `RubroAgregado`. En la jerarquía de hotspots §1 un rubro es Agrupador o Terminal, pero esa distinción solo **emerge del comportamiento**:
- Un rubro recién agregado es *conceptualmente* hoja (potencialmente terminal) hasta que recibe un hijo.
- Se convierte en Agrupador en uno de dos escenarios: (a) se le agrega un hijo (slice 03, implícito), o (b) se emite `RubroConvertidoAAgrupador` explícito para limpiar su monto (slice futuro, hotspot §1).

Decisión para slice 03: **no se introduce un campo `RubroTipo` en el payload de `RubroAgregado`.** Mantiene el payload idéntico al event-storming §5 y evita sobre-ingenieria. Ver §10 para el tratamiento de INV-9 bajo esta decisión.

## 3. Evento(s) emitido(s)

| Evento | Payload | Cuándo |
|---|---|---|
| `RubroAgregado` | `PresupuestoId`, `RubroId`, `Codigo`, `Nombre`, `RubroPadreId?`, `AgregadoEn` | Al aceptar el comando sobre un presupuesto en estado Borrador cuyas invariantes INV-8/INV-10/INV-11/INV-F se cumplan para el rubro propuesto. |

Payload alineado al event-storming §5. El `PresupuestoId` se toma del agregado (es el `Id` del presupuesto reconstruido), **no** del comando.

## 4. Precondiciones

Todas las excepciones heredan de `SincoPresupuesto.Domain.SharedKernel.DominioException`. Los tests verifican **tipo + propiedades**, nunca mensajes.

- `PRE-1`: `Codigo` no es nulo, vacío ni whitespace — excepción: `CampoRequeridoException` con `NombreCampo = "Codigo"`.
- `PRE-2`: `Nombre` no es nulo, vacío ni whitespace — excepción: `CampoRequeridoException` con `NombreCampo = "Nombre"`.
- `PRE-3`: `rubroId` recibido desde fuera no puede ser `Guid.Empty` — excepción: `CampoRequeridoException` con `NombreCampo = "RubroId"`.
- `PRE-4` (normalización, no fallo): `Codigo` y `Nombre` recibidos con espacios circundantes se `Trim` antes de validar y emitir.
- `PRE-5` (implícita, no se testea en este slice): el `presupuestoId` implícito del agregado debe estar reconstruido (el caller hace `AggregateBehavior<Presupuesto>.Reconstruir`). Un agregado con `Id == Guid.Empty` implicaría un stream vacío — escenario cubierto por §6.10.

## 5. Invariantes tocadas

- `INV-3` (event-storming §6): no se pueden agregar rubros en estado distinto a **Borrador**. Excepción nueva: `PresupuestoNoEsBorradorException(EstadoActual)`.
- `INV-8` (hotspots §1 renombrada de INV-A): nivel del rubro ≤ `ProfundidadMaxima` del presupuesto. Excepción nueva: `ProfundidadExcedidaException(ProfundidadMaxima, NivelIntentado)`.
- `INV-10` (hotspots §4 renombrada de INV-E): formato canónico del código `^\d{2}(\.\d{2}){0,14}$`. Excepción nueva: `CodigoRubroInvalidoException(CodigoIntentado)`.
- `INV-11` (hotspots §4 renombrada de INV-G): `Codigo` único dentro del presupuesto. Excepción nueva: `CodigoRubroDuplicadoException(CodigoIntentado)`.
- `INV-F` (hotspots §4): si `RubroPadreId` está presente, el `Codigo` del hijo debe extender el del padre con exactamente un segmento `\.\d{2}`. Excepción nueva: `CodigoHijoNoExtiendeAlPadreException(CodigoPadre, CodigoHijo)`.
- `INV-D` (hotspots §1, referencial): si `RubroPadreId` está presente, ese `Rubro` debe existir dentro del presupuesto. Excepción nueva: `RubroPadreNoExisteException(RubroPadreId)`.

Invariantes **tocadas pero no ejercitables en este slice** (ver §10 para tratamiento):
- `INV-9` (un terminal no tiene hijos). En slice 03 todos los rubros existentes son "hojas potenciales" sin distinción Agrupador/Terminal. La prohibición "no añadir hijo a un terminal" adquiere sentido solo tras `AsignarMontoARubro` o `RubroConvertidoAAgrupador`. Se difiere a slice posterior.
- `INV-A`/`INV-B`/`INV-C`/`INV-D` quedan parcialmente cubiertas: D sí (existencia del padre); B (terminal) no ejercitable acá; C (padre debe ser Agrupador en mover) aplica a `RubroMovido`, no a `AgregarRubro`.

## 6. Escenarios Given / When / Then

Cada escenario empieza con un `PresupuestoCreado` en `dados` (salvo §6.10). El fold de `AggregateBehavior<Presupuesto>.Reconstruir(...)` deja el agregado en estado Borrador con la `ProfundidadMaxima` del evento inicial.

### 6.1 Happy path — rubro raíz (sin padre)

**Given**
- `PresupuestoCreado(PresupuestoId=P, TenantId="acme", Codigo="OBRA-2026-01", …, ProfundidadMaxima=10, …)`.

**When**
- `AgregarRubro(Codigo="01", Nombre="Costos Directos", RubroPadreId=null)` con `rubroId=R1`, `ahora=T`.

**Then**
- Emite un único `RubroAgregado` con:
  - `PresupuestoId = P`
  - `RubroId = R1`
  - `Codigo = "01"`
  - `Nombre = "Costos Directos"`
  - `RubroPadreId = null`
  - `AgregadoEn = T`

### 6.2 Happy path — rubro hijo extiende al padre

**Given**
- `PresupuestoCreado(…, ProfundidadMaxima=10)`
- `RubroAgregado(RubroId=R1, Codigo="01", Nombre="Costos Directos", RubroPadreId=null, …)`

**When**
- `AgregarRubro(Codigo="01.01", Nombre="Materiales", RubroPadreId=R1)` con `rubroId=R2`, `ahora=T`.

**Then**
- Emite `RubroAgregado` con `RubroPadreId = R1`, `Codigo = "01.01"`, `AgregadoEn = T`.

### 6.3 Normalización de espacios en `Codigo` y `Nombre`

**Given** `PresupuestoCreado(…)`.
**When** `AgregarRubro(Codigo="  01  ", Nombre="  Costos Directos  ", RubroPadreId=null)`.
**Then** emite `RubroAgregado` con `Codigo="01"` y `Nombre="Costos Directos"` (trim aplicado).

### 6.4 Violación `PRE-1` — Codigo vacío

**Given** `PresupuestoCreado(…)`.
**When** `AgregarRubro(Codigo="", Nombre="X")` o `Codigo="   "`.
**Then** lanza `CampoRequeridoException` con `NombreCampo = "Codigo"`.

### 6.5 Violación `PRE-2` — Nombre vacío

**Given** `PresupuestoCreado(…)`.
**When** `AgregarRubro(Codigo="01", Nombre="")` o `Nombre="   "`.
**Then** lanza `CampoRequeridoException` con `NombreCampo = "Nombre"`.

### 6.6 Violación `PRE-3` — `rubroId` vacío

**Given** `PresupuestoCreado(…)`.
**When** invocar `AgregarRubro(cmd válido, rubroId=Guid.Empty, ahora=T)`.
**Then** lanza `CampoRequeridoException` con `NombreCampo = "RubroId"`.

### 6.7 Violación `INV-3` — presupuesto no está en Borrador

**Diferido al slice `AprobarPresupuesto`** (decisión firmada: §10 Q1 opción a). INV-3 queda declarada en §5 pero sin escenario ejercitable en slice 03 (no existe comando que transicione el estado). El followup #13 garantiza que el test se escriba cuando `AprobarPresupuesto` se implemente.

La excepción `PresupuestoNoEsBorradorException` (§12) **sí se introduce en este slice** (necesaria para que `AgregarRubro` la lance cuando la condición se presente — aunque green no pueda ejercerla hasta que exista un evento transicionante). Un test de cobertura mínima se escribe en slice 03: crear `Presupuesto` vía fold, invocar `AgregarRubro`, **no** lanza (porque el estado es Borrador). Con ello se verifica que el camino "estado Borrador" no bloquea y que la excepción existe para importarla desde tests futuros.

### 6.8 Violación `INV-10` — formato de código inválido

**Given** `PresupuestoCreado(…)`.
**When** `AgregarRubro(Codigo=c, Nombre="X", RubroPadreId=null)` con `c` en: `"1"`, `"1.1"`, `"01.1"`, `"01-01"`, `"a1"`, `"01.01.01.01.01.01.01.01.01.01.01.01.01.01.01.01"` (16 niveles, excede el regex con 15).
**Then** lanza `CodigoRubroInvalidoException` con `CodigoIntentado = c`.

### 6.9 Violación `INV-11` — código duplicado dentro del presupuesto

**Given**
- `PresupuestoCreado(…)`
- `RubroAgregado(RubroId=R1, Codigo="01", RubroPadreId=null, …)`

**When** `AgregarRubro(Codigo="01", Nombre="Otro", RubroPadreId=null)` con `rubroId=R2`.
**Then** lanza `CodigoRubroDuplicadoException` con `CodigoIntentado = "01"`.

### 6.10 Violación `INV-F` — hijo no extiende al padre

**Given**
- `PresupuestoCreado(…)`
- `RubroAgregado(RubroId=R1, Codigo="01", RubroPadreId=null, …)`

**When** `AgregarRubro(Codigo=c, Nombre="X", RubroPadreId=R1)` con `c` en: `"02.01"` (no empieza por `01.`), `"01"` (no añade segmento), `"01.01.01"` (añade dos segmentos), `"011.01"` (prefijo literal pero rompe boundary de segmento).
**Then** lanza `CodigoHijoNoExtiendeAlPadreException` con `CodigoPadre = "01"` y `CodigoHijo = c`.

### 6.11 Violación `INV-D` — padre no existe en el presupuesto

**Given**
- `PresupuestoCreado(…)`
- `RubroAgregado(RubroId=R1, Codigo="01", RubroPadreId=null, …)`

**When** `AgregarRubro(Codigo="99.01", Nombre="X", RubroPadreId=R_inexistente)` con `R_inexistente` distinto de todos los `RubroId` del agregado.
**Then** lanza `RubroPadreNoExisteException` con `RubroPadreId = R_inexistente`.

### 6.12 Violación `INV-8` — profundidad excedida

**Given**
- `PresupuestoCreado(…, ProfundidadMaxima=2)` (valor pequeño para no inflar el given).
- `RubroAgregado(RubroId=R1, Codigo="01", RubroPadreId=null, …)` (nivel 1)
- `RubroAgregado(RubroId=R2, Codigo="01.01", RubroPadreId=R1, …)` (nivel 2)

**When** `AgregarRubro(Codigo="01.01.01", Nombre="X", RubroPadreId=R2)` (intentaría ser nivel 3 > 2).
**Then** lanza `ProfundidadExcedidaException` con `ProfundidadMaxima = 2` y `NivelIntentado = 3`.

### 6.13 Fold — `Presupuesto` refleja el rubro agregado

**Given**
- `PresupuestoCreado(…)`
- evento `RubroAgregado` producido por el comando del §6.1.

**When** reconstruir el agregado aplicando los dos eventos (fold).

**Then**
- `agg.Id == P`.
- `agg.Estado == EstadoPresupuesto.Borrador`.
- La colección interna de rubros (nombre exacto a criterio de green) contiene **exactamente un** rubro con `Id=R1`, `Codigo="01"`, `Nombre="Costos Directos"`, `PadreId=null`, `Nivel=1`.
- Invariante de fold: el mismo escenario §6.2 (raíz + hijo) resulta en dos rubros, y el hijo tiene `Nivel=2` y `PadreId=R1`.

## 7. Idempotencia / retries

- **No idempotente por diseño del comando**: `AgregarRubro` quiere crear un rubro nuevo cada vez. Reintentos producen duplicados naturalmente detectados por `INV-11` (código duplicado) si el caller reusa el mismo código, o por colisión de `RubroId` si el caller reusa el mismo `rubroId` — en ese último caso el fold detecta al reaplicar el evento existente y el resultado sería lógico, pero el red-writer no cubre ese camino porque no es un camino del dominio (es un invariante del stream que Marten protege por versión).
- **Protección anti-reintento**: el caller (handler) usa `Guid.NewGuid()` para `rubroId` en cada invocación y protege la concurrencia del stream con `expected version` de Marten. Si hay colisión de versión, Marten lanza y el handler retornará 409. Fuera del alcance del dominio.
- **IdempotencyKey**: no se introduce. Si un caller externo (p.ej. webhook) necesita idempotencia fuerte, abrirá slice dedicado (mismo criterio que slice 01 §7).

## 8. Impacto en proyecciones / read models

- `PresupuestoReadModel`: debe ganar una colección de rubros (`Rubros: IReadOnlyList<RubroReadModel>` con `{ RubroId, Codigo, Nombre, PadreId?, Nivel }`). La proyección `PresupuestoProjection` amplía su `Apply(RubroAgregado)` para insertarlo en la lista. **Esta adición está dentro del alcance del slice** (la proyección se actualiza junto al agregado).
- `EstructuraPresupuestal` (event-storming §7): proyección natural para el árbol. En MVP se puede posponer hasta tener más eventos (`RubroRetirado`, `MontoAsignadoARubro`, `RubroMovido`). Se documenta como followup si aún no existe.
- `SaldoPorRubro`, `PresupuestosPorPeriodo`: no impactadas por este slice.

## 9. Impacto en endpoints HTTP

- **`POST /api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros`** — agrega un rubro al presupuesto.
  - Request body: `{ codigo: "01.01", nombre: "Materiales", rubroPadreId?: "<guid>" }`.
  - `201 Created` con `Location` a `GET /api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros/{rubroId}` (o un endpoint de listado si no hay detalle individual). Body: `{ rubroId, codigo, nombre, rubroPadreId, agregadoEn }`.
  - `400 Bad Request` ante violaciones de `CampoRequeridoException`, `CodigoRubroInvalidoException`, `CodigoHijoNoExtiendeAlPadreException`, `ProfundidadExcedidaException`.
  - `404 Not Found` si el `presupuestoId` no existe (Marten retorna stream vacío → handler lanza).
  - `409 Conflict` ante `CodigoRubroDuplicadoException` (choque de unicidad intra-presupuesto), `PresupuestoNoEsBorradorException` (estado incompatible), `RubroPadreNoExisteException`. Criterio: el conflicto es lógico-estado; `400` queda para datos mal formados.
  - El handler hace `Guid.NewGuid()` para `rubroId` — nunca el dominio.

## 10. Preguntas abiertas

- [x] **¿El caller pasa `Codigo` o el dominio lo autogenera?** — **Resuelto**: el dominio **recibe** el código y lo valida (INV-10, INV-11, INV-F). La autogeneración (`GeneradorCodigosJerarquicos` del hotspot §4) queda como servicio/slice posterior. Motivo: mantener slice acotado y aislar la complejidad de generación de códigos del comportamiento del agregado. Ver §13 (followup nuevo).
- [x] **¿Se introduce `RubroTipo` en el payload de `RubroAgregado`?** — **Resuelto**: **no**. El payload es idéntico al event-storming §5. La distinción Agrupador/Terminal emerge del comportamiento (rubro con hijo ⇒ Agrupador; rubro sin hijo con monto ⇒ Terminal). Consecuencia: `INV-9` (terminal no tiene hijos) **no es testeable en slice 03** y se difiere al slice que introduzca `AsignarMontoARubro` o `RubroConvertidoAAgrupador` (hotspots §1). Ver §13 (followup nuevo).
- [x] **¿El código se autogenera si el caller lo omite?** — **Resuelto (por consecuencia de Q1)**: no. El comando lo declara **requerido**; la autogeneración es capa superior al agregado.
- [x] **¿Se modela INV-D con lookup de hijos en colección privada del agregado?** — **Resuelto (es decisión de green)**: la spec exige la validación; el diseño de la estructura interna (`List<Rubro>` privada con entity `Rubro { Id, Codigo, Nombre, PadreId?, Nivel }`) lo decide green. El domain-modeler se limita a declarar los conceptos sin prescribir código.
- [x] **Q1 — ¿Cómo prueba `red` la violación de INV-3 (§6.7) si `AprobarPresupuesto` no existe todavía?** — **Resuelto (firma del usuario): opción (a)**. Se postpone el escenario §6.7 al slice `AprobarPresupuesto`. INV-3 se declara en §5 y la excepción `PresupuestoNoEsBorradorException` se introduce en SharedKernel (§12) para estar lista. El escenario de violación se escribirá en el slice que introduzca la transición de estado. Followup #13 (§13) documenta el compromiso.

## 11. Checklist pre-firma

- [x] Todas las precondiciones mapean a un escenario Then (§6.4 PRE-1, §6.5 PRE-2, §6.6 PRE-3; PRE-4 se valida transversalmente por §6.3).
- [x] Todas las invariantes tocadas y ejercitables mapean a un escenario Then (INV-3 diferida a slice `AprobarPresupuesto` por decisión §10 Q1 opción (a); INV-8 §6.12; INV-10 §6.8; INV-11 §6.9; INV-F §6.10; INV-D §6.11). INV-9 documentada como no ejercitable en este slice (§5 + §10 Q2).
- [x] El happy path está presente (§6.1 raíz, §6.2 hijo).
- [x] Fold del evento documentado (§6.13).
- [x] Impactos en SharedKernel (§12), proyecciones (§8), endpoints HTTP (§9) y followups (§13) documentados.
- [x] Q1 de §10 resuelta (firma del usuario 2026-04-24, opción a).

## 12. Impacto en SharedKernel

Este slice introduce excepciones nuevas. Viven en `SincoPresupuesto.Domain.SharedKernel` (mismo criterio que slice 01 y 02). Todas heredan de `DominioException` y exponen propiedades fuertemente tipadas para aserción por estructura en tests.

1. **`PresupuestoNoEsBorradorException(EstadoPresupuesto estadoActual) : DominioException`**
   - Propiedad: `EstadoActual: EstadoPresupuesto`.
   - Uso: INV-3. Se lanza cuando cualquier comando de modificación estructural encuentra el presupuesto fuera de `Borrador`.

2. **`CodigoRubroInvalidoException(string codigoIntentado) : DominioException`**
   - Propiedad: `CodigoIntentado: string`.
   - Uso: INV-10. Formato no coincide con `^\d{2}(\.\d{2}){0,14}$`.

3. **`CodigoRubroDuplicadoException(string codigoIntentado) : DominioException`**
   - Propiedad: `CodigoIntentado: string`.
   - Uso: INV-11. Otro rubro dentro del mismo presupuesto ya usa ese código.

4. **`CodigoHijoNoExtiendeAlPadreException(string codigoPadre, string codigoHijo) : DominioException`**
   - Propiedades: `CodigoPadre: string`, `CodigoHijo: string`.
   - Uso: INV-F. El hijo no extiende al padre con exactamente un segmento `\.\d{2}`.

5. **`RubroPadreNoExisteException(Guid rubroPadreId) : DominioException`**
   - Propiedad: `RubroPadreId: Guid`.
   - Uso: INV-D. El `RubroPadreId` del comando no coincide con ningún `RubroId` reconstruido del agregado.

6. **`ProfundidadExcedidaException(int profundidadMaxima, int nivelIntentado) : DominioException`**
   - Propiedades: `ProfundidadMaxima: int`, `NivelIntentado: int`.
   - Uso: INV-8. El rubro agregado quedaría en un nivel mayor a `ProfundidadMaxima` del presupuesto.

No se introduce `RubroPadreEsTerminalException` en este slice (INV-9 diferida, ver §10 Q2 y §13).

El `CampoRequeridoException` ya existe (SharedKernel actual) y se reutiliza para PRE-1, PRE-2, PRE-3.

## 13. Follow-ups generados por este slice

Se agregan a `FOLLOWUPS.md`:

- **#11** — **Servicio de dominio `GeneradorCodigosJerarquicos`**. Calcula el siguiente código disponible dado un padre (`padre + "." + siguiente segmento libre`), recodifica subramas en `RubroMovido`, y sugiere overrides válidos a la UI. Vive fuera del agregado `Presupuesto` porque requiere conocer todos los rubros del presupuesto y su política de numeración (ver hotspot §4). Origen: slice-03, spec §10 Q1 y §1 "intención". Disparador: primer slice que agregue UI de creación de rubros o slice `RubroMovido`.

- **#12** — **Introducir `RubroTipo` (Agrupador/Terminal) y cubrir INV-9**. Al implementar `AsignarMontoARubro` (slice futuro) o `RubroConvertidoAAgrupador` (hotspot §1), modelar la distinción de tipo y escribir el escenario "no se puede agregar hijo a un rubro terminal con monto asignado". Implica extender el payload de `RubroAgregado` con `RubroTipo` (o derivarlo del estado al fold). Origen: slice-03, spec §10 Q2.

- **#13** — **Escenario INV-3 en slice `AprobarPresupuesto`**. Cuando exista el comando `AprobarPresupuesto`, garantizar que el test "AgregarRubro sobre presupuesto aprobado lanza `PresupuestoNoEsBorradorException`" se escribe en ese slice o retroactivamente acá. Depende de resolución §10 Q1. Origen: slice-03, spec §10 Q1.

- **Cierre parcial de #5** (firma uniforme `CasoDeUso.Decidir`): con slice 03 ya hay dos métodos mutadores (`Create` factory sobre stream vacío, `AgregarRubro` método de instancia sobre agregado reconstruido). La forma actual es consistente dentro de cada patrón; unificar a `Decidir(dados, cmd, …)` implicaría transformar todo el dominio a funciones libres. Recomendación del modeler: **mantener la forma OO actual y cerrar #5 como "no aplicable"**. Requiere decisión del refactorer/reviewer en pasada final.
