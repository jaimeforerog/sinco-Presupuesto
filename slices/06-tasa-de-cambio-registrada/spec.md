# Slice 06 — RegistrarTasaDeCambio

**Autor:** domain-modeler
**Fecha:** 2026-04-24
**Estado:** firmado
**Agregado afectado:** `CatalogoDeTasas` (nuevo agregado, singleton por tenant — patrón análogo a `ConfiguracionTenant`).
**Decisiones previas relevantes:**
- `02-decisiones-hotspots-mvp.md` §2 (multimoneda a nivel de partida; "Catálogo de tasas de cambio: agregado/proyección separada `TasaDeCambio { fecha, desde, hacia, tasa, fuente }`. Alimentado manualmente o por servicio externo"; INV-14, INV-15).
- `01-event-storming-mvp.md` §5 (eventos del MVP — `TasaDeCambioRegistrada` no estaba en el event storming original; entra por hotspots §2).
- `slices/00-shared-kernel/spec.md` §2 (`Dinero`, `Moneda`, `DominioException`, `Requerir.Campo`, jerarquía de excepciones del kernel).
- `slices/02-configurar-moneda-local-del-tenant/spec.md` (precedente clave: agregado singleton por tenant con stream-id bien-conocido + proyección inline; el patrón se replica aquí).
- `slices/05-aprobar-presupuesto/spec.md` §2 nota sobre `SnapshotTasas` + §10 + §13 followup #24 (consumidor futuro del catálogo: `AprobarPresupuestoHandler` populará `SnapshotTasas` desde la proyección `TasasDeCambioVigentes`).
- `src/SincoPresupuesto.Domain/ConfiguracionesTenant/ConfiguracionTenant.cs` (patrón de agregado singleton: `Crear` factory sobre stream vacío + `Apply` sobre fold).
- `src/SincoPresupuesto.Application/ConfiguracionesTenant/ConfiguracionTenantStreamId.cs` (patrón de stream-id bien-conocido: `Guid` constante, conjoined multi-tenancy de Marten discrimina por `tenant_id`).
- `src/SincoPresupuesto.Application/ConfiguracionesTenant/ConfiguracionTenantProjection.cs` (patrón de proyección inline `SingleStreamProjection`).
- `FOLLOWUPS.md` #20 (`TasaASnapshot` informativo en `MontoAsignadoARubro` — depende de este slice), #24 (multimoneda real en `AprobarPresupuesto` — consumidor directo de `TasasDeCambioVigentes`), #25 (`PresupuestoBaselineEnMonedaBase` — consumidor downstream).

---

## 1. Intención

Un administrador del tenant (o un servicio externo de FX) registra una tasa de cambio entre dos monedas para una fecha dada, con fuente opcional (texto libre — "BanRep", "manual", "exchangerate.host", etc.). El catálogo se materializa como un agregado event-sourced **singleton por tenant** (mismo patrón que `ConfiguracionTenant`): cada `RegistrarTasaDeCambio` apendea un evento al único stream de catálogo del tenant. Una proyección inline `TasasDeCambioVigentes` mantiene un diccionario por par `(MonedaDesde, MonedaHacia)` con la última tasa registrada — esta es la vista que `AprobarPresupuestoHandler` consultará en el slice 06b/futuro para popular `SnapshotTasas` (followup #24).

Este slice **solo cubre el registro** (catálogo). La integración con `AprobarPresupuesto` (consumir `TasasDeCambioVigentes` desde el handler para popular `SnapshotTasas` y levantar PRE-3 multimoneda de slice 05) es **fase aparte** — sigue cubierta por followup #24 y se materializará cuando el negocio pida aprobaciones multimoneda reales.

### Decisión de diseño del agregado (modeler, no se eleva al usuario)

Tres opciones evaluadas:

- **(A) Singleton `CatalogoDeTasas` por tenant**, stream-id bien-conocido (patrón `ConfiguracionTenant`). Cada `RegistrarTasaDeCambio` apendea un evento al stream único del tenant. La proyección `TasasDeCambioVigentes` mantiene un dict por `(MonedaDesde, MonedaHacia)` con la última tasa.
- (B) Stream por par `(MonedaDesde, MonedaHacia)`: cada par es su propio agregado. Stream-id derivado determinísticamente del par.
- (C) Stream por registro individual: cada registración crea un stream nuevo de un solo evento; la proyección agrega.

**Elegida: (A).** Replica el patrón ya validado en slice 02 (`ConfiguracionTenant`), mínima fricción cognitiva, integra naturalmente con conjoined multi-tenancy (Marten ya discrimina por `tenant_id`). Stream length no es preocupación en MVP — incluso con registros diarios para 5–10 pares, son ~3000 eventos/año, holgadamente dentro del rango cómodo de un único stream con snapshotting opcional. Si en producción crece más allá (p. ej. registros minutarios de 50 pares), evaluar reorganización via followup, no en este slice.

## 2. Comando

```csharp
public sealed record RegistrarTasaDeCambio(
    Moneda MonedaDesde,
    Moneda MonedaHacia,
    decimal Tasa,
    DateOnly Fecha,
    string? Fuente = null,
    string RegistradoPor = "sistema");
```

- `MonedaDesde` / `MonedaHacia`: VOs `Moneda` ya validados por construcción (slice 00 §2.2). El agregado **no** revalida ISO 4217.
- `Tasa`: `decimal` (no `Dinero`). Es una **ratio** (factor de conversión), no un monto. Coherente con la signatura `Dinero.En(Moneda destino, decimal factor)` del slice 00 §2.1 y con el tipo de los valores del `SnapshotTasas: IReadOnlyDictionary<Moneda, decimal>` del slice 05 §3.
- `Fecha`: `DateOnly`. Es la fecha **de vigencia** de la tasa (día calendario de mercado), no el timestamp de registro. El timestamp de registro va en el evento como `RegistradaEn: DateTimeOffset` separado.
- `Fuente`: opcional, texto libre. Whitespace / vacío se normaliza a `null` (preservar el opcional con su semántica clara — ver PRE-4).
- `RegistradoPor`: con default `"sistema"`. Vacío / whitespace / null → `"sistema"` (patrón slice 01 `CreadoPor`, slice 02 `ConfiguradoPor`, slice 04 `AsignadoPor`, slice 05 `AprobadoPor`).

Firma del caso de uso (siguiendo el patrón slice 02):

- **Stream vacío** (primer registro del tenant en el catálogo): `static TasaDeCambioRegistrada CatalogoDeTasas.Crear(RegistrarTasaDeCambio cmd, DateTimeOffset ahora)`.
- **Stream existente** (catálogo ya tiene registros): método de instancia `TasaDeCambioRegistrada Registrar(RegistrarTasaDeCambio cmd, DateTimeOffset ahora)` sobre el agregado reconstruido.

A diferencia de `ConfiguracionTenant.Ejecutar` que **siempre** lanza (porque la configuración del tenant es de una sola vez), aquí `Registrar` sí emite eventos en el camino feliz — el agregado puede recibir N comandos a lo largo del tiempo. Las precondiciones se evalúan idéntico en `Crear` y en `Registrar`.

## 3. Evento(s) emitido(s)

| Evento | Payload | Cuándo |
|---|---|---|
| `TasaDeCambioRegistrada` | `MonedaDesde: Moneda`, `MonedaHacia: Moneda`, `Tasa: decimal`, `Fecha: DateOnly`, `Fuente: string?`, `RegistradaEn: DateTimeOffset`, `RegistradaPor: string` | Al aceptar el comando con todas las precondiciones satisfechas (independientemente de si es el primer registro del catálogo o uno subsiguiente). |

Nota sobre `TasaDeCambioCorregida`: hotspots §2 menciona un evento separado para distinguir corrección de actualización. **No se modela en slice 06** — para MVP cualquier re-registración de la misma tupla `(MonedaDesde, MonedaHacia, Fecha)` con tasa distinta se emite también como `TasaDeCambioRegistrada` y la proyección "última gana". El refinamiento (`TasaDeCambioCorregida` con `Motivo` obligatorio para auditoría) queda como followup nuevo (#27, ver §13). Disparador: PO solicita auditoría de cambios con distinción correctiva.

## 4. Precondiciones

Todas las excepciones heredan de `SincoPresupuesto.Domain.SharedKernel.DominioException`. Los tests verifican **tipo + propiedades**, nunca mensajes.

- `PRE-1`: `MonedaDesde != MonedaHacia` — registrar tasa MISMA→MISMA es absurdo (la conversión es identidad por definición). Excepción: `MonedasIgualesEnTasaException(Moneda Moneda) : DominioException` (nueva — ver §12).
- `PRE-2`: `Tasa > 0m` — tasa cero o negativa no tiene sentido financiero (un factor de conversión es estrictamente positivo). Excepción: `TasaDeCambioInvalidaException(decimal TasaIntentada) : DominioException` (nueva — ver §12).
  - Decisión del modeler: **excepción nueva**, no reutilizar `FactorDeConversionInvalidoException` del slice 00. Razón: el contexto es distinto — slice 00 valida el factor en `Dinero.En(destino, factor)` (operación de conversión, error del caller que pasó dato corrupto); slice 06 valida la tasa al registrar (input del usuario / servicio externo de FX). Mantener excepciones separadas permite mapeos HTTP y mensajes de UX diferentes sin acoplar contextos.
- `PRE-3`: `cmd.Fecha <= ahora.Date` (fecha de la tasa **no** en el futuro). Excepción: `FechaDeTasaEnElFuturoException(DateOnly Fecha, DateOnly Hoy) : DominioException` (nueva — ver §12).
  - Decisión del modeler: **rechazar fecha futura**. Justificación: coherente con el modelo "snapshot histórico" de tasas (hotspots §2: "Catálogo de tasas… fuente: BanRep, manual, exchangerate.host"). Un providers que publica tasas con anticipación (pre-publicación) es un caso distinto que se modelaría con un comando dedicado (`PreRegistrarTasaDeCambio`) si surge — fuera del MVP. Ver §10 Q2.
- `PRE-4` (normalización, no fallo):
  - `cmd.RegistradoPor` nulo / vacío / whitespace → `"sistema"` en el evento emitido. No lanza. Patrón slices 01/02/04/05.
  - `cmd.Fuente` vacío / whitespace → `null` en el evento emitido (preserva la semántica del opcional). `null` entrante también queda `null`. Si llega un valor con whitespace alrededor (p. ej. `"  BanRep  "`), se aplica `Trim()` y se conserva si el resultado no está vacío.

Orden de evaluación (importa para tests deterministas):

1. PRE-1 (`MonedaDesde != MonedaHacia`).
2. PRE-2 (`Tasa > 0m`).
3. PRE-3 (`Fecha <= ahora.Date`).
4. PRE-4 (normalización antes de emitir).

## 5. Invariantes tocadas

- **`INV-CT-1` (nueva, propia del agregado `CatalogoDeTasas`)**: el catálogo permite re-registrar la misma tupla `(MonedaDesde, MonedaHacia, Fecha)` con tasa distinta — caso real: corrección de un dato ingresado mal. La proyección `TasasDeCambioVigentes` toma "la última registrada" como vigente. **Decisión del modeler:** no se modela `TasaDeCambioCorregida` separado en MVP (followup #27). Esto se cubre en escenario §6.3 (re-registración acumulativa) — ver Q1 en §10.
- **`INV-14`** (hotspots §2): "Un presupuesto solo puede aprobarse si existen tasas de cambio vigentes hacia `MonedaBase` para todas las monedas presentes en sus partidas." Este slice **alimenta** la proyección que `AprobarPresupuestoHandler` consultará para satisfacer INV-14 (followup #24). Sin escenario directo en slice 06 (es responsabilidad del slice consumidor).
- **`INV-13`** (hotspots §2): "Toda cantidad monetaria en un evento se almacena como `Dinero(valor, moneda)` — nunca `decimal` pelado." Cumplida vacuamente: el evento **no contiene** cantidades monetarias. La `Tasa` es un factor de conversión `decimal`, no un monto — coherente con el contrato de `Dinero.En(Moneda destino, decimal factor)` del slice 00.
- **`INV-NEW-CT-2` (nueva, declarativa)**: el evento `TasaDeCambioRegistrada` es **append-only** — una vez emitido, no se modifica ni se borra. Las correcciones se modelan como nuevos eventos (re-registración en MVP; `TasaDeCambioCorregida` en futuro followup #27).

Invariantes **tocadas pero no ejercitables en slice 06**:

- INV-14 / INV-15 — su ejercicio vive en el slice consumidor (followup #24), no aquí.

## 6. Escenarios Given / When / Then

Cada escenario se traduce a **un test** en la fase red. El catálogo siempre opera en el contexto de un tenant (Marten conjoined multi-tenancy), pero los tests unitarios del agregado no necesitan tocar `tenant_id` — operan sobre el stream del agregado directamente. La identidad del agregado es el `Guid` bien-conocido `CatalogoDeTasasStreamId.Value` (ver §12), idéntico patrón al slice 02.

### 6.1 Happy path — primer registro del catálogo (stream vacío)

**Given**
- Stream vacío (no hay registros previos en el catálogo de tasas del tenant).

**When**
- `RegistrarTasaDeCambio(MonedaDesde=Moneda.USD, MonedaHacia=Moneda.COP, Tasa=4170.50m, Fecha=DateOnly(2026,04,24), Fuente="BanRep", RegistradoPor="admin-alice")` con `ahora=T` (donde `T.Date == DateOnly(2026,04,24)`).

**Then**
- Emite un único `TasaDeCambioRegistrada` con:
  - `MonedaDesde = Moneda.USD`
  - `MonedaHacia = Moneda.COP`
  - `Tasa = 4170.50m`
  - `Fecha = DateOnly(2026,04,24)`
  - `Fuente = "BanRep"`
  - `RegistradaEn = T`
  - `RegistradaPor = "admin-alice"`

### 6.2 Happy path — segundo registro con par y fecha distintos (acumula en el stream)

**Given**
- Stream contiene `TasaDeCambioRegistrada(USD→COP, 4170.50m, 2026-04-24, "BanRep", T1, "admin-alice")`.

**When**
- `RegistrarTasaDeCambio(MonedaDesde=Moneda.EUR, MonedaHacia=Moneda.COP, Tasa=4520.75m, Fecha=DateOnly(2026,04,24), Fuente="BanRep", RegistradoPor="admin-alice")` con `ahora=T2 > T1`.

**Then**
- Emite `TasaDeCambioRegistrada` con `MonedaDesde=EUR`, `MonedaHacia=COP`, `Tasa=4520.75m`, `Fecha=DateOnly(2026,04,24)`, `Fuente="BanRep"`, `RegistradaEn=T2`, `RegistradaPor="admin-alice"`.
- El evento previo permanece intacto en el stream (append-only).

### 6.3 Happy path — re-registración del mismo par + fecha actualiza la "vigente" (INV-CT-1)

**Given**
- Stream contiene `TasaDeCambioRegistrada(USD→COP, 4170.50m, 2026-04-24, "BanRep", T1, "admin-alice")`.

**When**
- `RegistrarTasaDeCambio(MonedaDesde=Moneda.USD, MonedaHacia=Moneda.COP, Tasa=4180.00m, Fecha=DateOnly(2026,04,24), Fuente="manual-correccion", RegistradoPor="admin-alice")` con `ahora=T2 > T1`.

**Then**
- Emite `TasaDeCambioRegistrada` con `MonedaDesde=USD`, `MonedaHacia=COP`, `Tasa=4180.00m`, `Fecha=DateOnly(2026,04,24)`, `Fuente="manual-correccion"`, `RegistradaEn=T2`, `RegistradaPor="admin-alice"`.
- El stream contiene ahora **dos** eventos para la misma tupla `(USD, COP, 2026-04-24)`. La proyección `TasasDeCambioVigentes` (§8) tomará el segundo como vigente (last-write-wins por orden del stream).

_(Este escenario fija el contrato declarado en INV-CT-1: el dominio **acepta** la re-registración sin distinguir "actualización" de "corrección". El refinamiento `TasaDeCambioCorregida` queda diferido a followup #27.)_

### 6.4 Normalización `RegistradoPor` vacío / whitespace / null → `"sistema"`

**Given** Stream vacío.
**When** `RegistrarTasaDeCambio(USD→COP, 4170.50m, 2026-04-24, Fuente="BanRep", RegistradoPor=X)` con `X` en `{ "", "   ", null }` (theory con tres casos) y `ahora=T`.
**Then** Emite `TasaDeCambioRegistrada` con `RegistradaPor = "sistema"` y los demás campos del happy path §6.1.

### 6.5 Normalización `Fuente` vacío / whitespace → `null` (preserva semántica del opcional)

**Given** Stream vacío.
**When** `RegistrarTasaDeCambio(USD→COP, 4170.50m, 2026-04-24, Fuente=X, RegistradoPor="admin-alice")` con `X` en `{ "", "   ", null }` (theory con tres casos) y `ahora=T`.
**Then** Emite `TasaDeCambioRegistrada` con `Fuente = null` (no string vacío) y los demás campos del happy path §6.1.

_(Distinción importante: `Fuente="BanRep   "` → se preserva como `"BanRep"` (trim). Pero `Fuente="   "` → `null`. El test debe cubrir ambos.)_

### 6.6 Violación `PRE-1` — `MonedaDesde == MonedaHacia`

**Given** Stream vacío.
**When** `RegistrarTasaDeCambio(MonedaDesde=Moneda.USD, MonedaHacia=Moneda.USD, Tasa=1m, Fecha=DateOnly(2026,04,24), Fuente=null, RegistradoPor="admin-alice")` con `ahora=T`.
**Then** Lanza `MonedasIgualesEnTasaException` con `Moneda = Moneda.USD`.

### 6.7 Violación `PRE-2` — `Tasa <= 0` (theory: `0m`, `-1m`, `-0.0001m`)

**Given** Stream vacío.
**When** `RegistrarTasaDeCambio(USD→COP, Tasa=X, Fecha=DateOnly(2026,04,24), Fuente=null, RegistradoPor="admin-alice")` con `X` en `{ 0m, -1m, -0.0001m }` (theory con tres casos) y `ahora=T`.
**Then** Lanza `TasaDeCambioInvalidaException` con `TasaIntentada = X` (el valor concreto del caso del theory).

### 6.8 Violación `PRE-3` — `Fecha` en el futuro

**Given** Stream vacío. Sea `hoy = DateOnly(2026,04,24)` (derivado de `ahora.Date` con `ahora=T`).
**When** `RegistrarTasaDeCambio(USD→COP, 4170.50m, Fecha=DateOnly(2026,04,25), Fuente=null, RegistradoPor="admin-alice")` con `ahora=T` (donde `T.Date == hoy`).
**Then** Lanza `FechaDeTasaEnElFuturoException` con `Fecha = DateOnly(2026,04,25)` y `Hoy = hoy`.

### 6.9 Caso límite `PRE-3` — `Fecha == hoy` se acepta (no se rechaza)

**Given** Stream vacío. Sea `hoy = DateOnly(2026,04,24)`.
**When** `RegistrarTasaDeCambio(USD→COP, 4170.50m, Fecha=hoy, Fuente="BanRep", RegistradoPor="admin-alice")` con `ahora=T` (donde `T.Date == hoy`).
**Then** Emite `TasaDeCambioRegistrada` (sin lanzar). La condición es `Fecha <= ahora.Date`, **inclusiva** del día de hoy. Ejercita el límite estricto.

### 6.10 Violación `PRE-1` desde stream existente (camino de instancia)

**Given** Stream contiene `TasaDeCambioRegistrada(USD→COP, 4170.50m, 2026-04-24, "BanRep", T1, "admin-alice")`.
**When** `RegistrarTasaDeCambio(MonedaDesde=Moneda.EUR, MonedaHacia=Moneda.EUR, Tasa=1m, Fecha=DateOnly(2026,04,24), Fuente=null, RegistradoPor="admin-alice")` con `ahora=T2 > T1`.
**Then** Lanza `MonedasIgualesEnTasaException` con `Moneda = Moneda.EUR`.

_(Verifica que las precondiciones se aplican idéntico en el camino `Crear` (stream vacío) y en el camino `Registrar` (instancia con fold). Cubre el riesgo de duplicación divergente entre ambos métodos.)_

### 6.11 Fold — `CatalogoDeTasas` refleja el historial completo de registros

**Given** Eventos ordenados:
- `TasaDeCambioRegistrada(USD→COP, 4170.50m, 2026-04-23, "BanRep", T1, "admin-alice")`
- `TasaDeCambioRegistrada(EUR→COP, 4520.00m, 2026-04-23, "BanRep", T2, "admin-alice")`
- `TasaDeCambioRegistrada(USD→COP, 4175.00m, 2026-04-24, "BanRep", T3, "admin-alice")` (par USD→COP repetido en fecha distinta)

**When** Reconstruir el agregado aplicando los tres eventos (fold).

**Then**
- `agg.Id == CatalogoDeTasasStreamId.Value` (mismo patrón que slice 02).
- `agg.Registros` (lista expuesta por el agregado para verificación de fold) contiene **3** entradas en orden de aparición, cada una con los campos del evento correspondiente.
- El agregado **no** mantiene un dict "vigentes" — esa responsabilidad vive en la proyección `TasasDeCambioVigentes` (§8). El agregado solo expone el historial para que los tests de fold verifiquen consumo correcto de cada evento.

_(Decisión del modeler: el agregado expone una `IReadOnlyList<RegistroDeTasa>` con `(MonedaDesde, MonedaHacia, Tasa, Fecha, Fuente, RegistradaEn, RegistradaPor)` — un VO del agregado para consumo de tests. La proyección consume los eventos directamente, no el agregado, así que esta lista es puramente para test.)_

## 7. Idempotencia / retries

- **No idempotente por diseño del comando**: cada `RegistrarTasaDeCambio` representa un acto explícito de registración. Si el caller invoca dos veces el mismo comando (con la misma tupla `(MonedaDesde, MonedaHacia, Tasa, Fecha, Fuente)`), el agregado **emite dos eventos** — son dos registraciones, aunque sus contenidos sean idénticos. La proyección `TasasDeCambioVigentes` queda con la última (idéntica a la anterior — no observable como cambio de estado proyectado). Esto es coherente con el comportamiento declarado en INV-CT-1 / §6.3.
- **No hay protección anti-duplicado en el dominio**. Si la realidad del negocio exige distinguir "registro válido" de "duplicado accidental" (p. ej. doble click en UI, retry de webhook FX), se introduce un `IdempotencyKey` o se trata fuera del dominio (deduplication en el ingestor). **Decisión del modeler: no introducir `IdempotencyKey` en MVP.** Disparador para reabrir: bug real de duplicado en producción → followup nuevo.
- **Retries seguros**: como el comando solo emite eventos `TasaDeCambioRegistrada` y no toca otros agregados, un retry produce un evento extra sin corromper el catálogo. La proyección `TasasDeCambioVigentes` es resilient a duplicados (last-write-wins).

## 8. Impacto en proyecciones / read models

- **Nueva — `TasasDeCambioVigentes`** (proyección **inline** `SingleStreamProjection<TasasDeCambioVigentes>`).
  - Documento: `{ TenantId: string, Vigentes: IReadOnlyDictionary<ParMonedas, EntradaTasaVigente> }` donde:
    - `ParMonedas` es la clave compuesta `(MonedaDesde, MonedaHacia)`. Implementación concreta del wire-format la decide infra-wire (puede ser `string` con formato `"USD→COP"`, o `record struct` serializable). El contrato del read model es "una entrada por par".
    - `EntradaTasaVigente` es `{ Tasa: decimal, Fecha: DateOnly, Fuente: string?, RegistradaEn: DateTimeOffset, RegistradaPor: string }`.
  - Lógica de actualización (`Apply(TasaDeCambioRegistrada e)`): upsert sobre la clave `(e.MonedaDesde, e.MonedaHacia)` con la **última** entrada — `Vigentes[par] = new EntradaTasaVigente(...)`. Si la clave ya existía, se sobrescribe. Esto implementa el "last-write-wins por orden del stream" que cubre INV-CT-1.
  - **Inline**: el handler de `AprobarPresupuesto` (followup #24) la consultará en el contexto transaccional del comando — necesita consistencia inmediata, no eventual.
- **Sin impacto en proyecciones existentes** (`PresupuestoReadModel`, `EstructuraPresupuestal`, `SaldoPorRubro`, `PresupuestosPorPeriodo`, `ConfiguracionTenantActual`): el agregado `CatalogoDeTasas` es independiente del agregado `Presupuesto` y del agregado `ConfiguracionTenant`.
- **Followup #25** (`PresupuestoBaselineEnMonedaBase`) es consumidor downstream pero no toca este slice — depende de followup #24 que sí lo hará.

## 9. Impacto en endpoints HTTP

- **`POST /api/tenants/{tenantId}/catalogo-tasas/registros`** — registra una tasa de cambio.
  - Request body: `{ monedaDesde: "USD", monedaHacia: "COP", tasa: 4170.50, fecha: "2026-04-24", fuente?: "BanRep", registradoPor?: "admin-alice" }`.
  - **201 Created** — registro exitoso (consistente con patrón slices 01/02/03/04/05: append de evento ⇒ 201). Body: `{ monedaDesde, monedaHacia, tasa, fecha, fuente, registradaEn, registradaPor }`. Header `Location: GET /api/tenants/{tenantId}/catalogo-tasas/vigentes`.
  - **400 Bad Request** ante:
    - `MonedasIgualesEnTasaException` (PRE-1)
    - `TasaDeCambioInvalidaException` (PRE-2)
    - `FechaDeTasaEnElFuturoException` (PRE-3)
    - Construcción inválida de `Moneda` desde el JSON (`CodigoMonedaInvalidoException` — ya mapeada a 400 por slice 02)
  - **404 Not Found** — el `tenantId` no tiene `ConfiguracionTenantActual` (criterio coherente: para registrar tasas, el tenant debe estar configurado). Decisión del modeler: este chequeo vive en el handler (followup #8 / paralelo), **no** en el agregado `CatalogoDeTasas`. El agregado es independiente; el handler es quien aplica la regla cruzada. Si el handler aún no implementa este chequeo, el comando se acepta — el slice 06 no introduce este chequeo, queda como deuda en seguimiento del followup #8.

- **`GET /api/tenants/{tenantId}/catalogo-tasas/vigentes`** — lee `TasasDeCambioVigentes`.
  - **200 OK** con el documento. Body: `{ vigentes: [ { monedaDesde, monedaHacia, tasa, fecha, fuente, registradaEn, registradaPor }, ... ] }` (lista, no dict, para serialización JSON cómoda).
  - **404 Not Found** si el tenant no ha registrado ninguna tasa todavía (proyección no existe). Alternativa: 200 con `vigentes: []`. **Decisión del modeler: 200 con lista vacía** (más conveniente para clientes que iteran). El handler de `GET` crea la lista vacía si la proyección está vacía.

- Mapeo en `DomainExceptionHandler.Mapear`: añadir `MonedasIgualesEnTasaException → 400`, `TasaDeCambioInvalidaException → 400`, `FechaDeTasaEnElFuturoException → 400`. Refactor transversal incluido en el slice (mismo criterio que slices 02/03/04/05).

## 10. Preguntas abiertas

Cuatro preguntas evaluadas. **Cuatro** se resuelven directamente por el modeler. **Cero** se elevan al usuario — todas las decisiones tienen justificación en documentos previamente firmados (hotspots §2, slice 02, slice 05) o son evidentes con argumento técnico.

**Firma del usuario (2026-04-24):** spec aprobada tal como la dejó el modeler — 11 escenarios, patrón (A) singleton, 3 excepciones nuevas, slice 06 NO toca `AprobarPresupuesto`.

- [x] **Q1 — ¿Se permite registrar la misma tupla `(MonedaDesde, MonedaHacia, Fecha)` varias veces (la última gana en proyección)?** — **Resuelto: sí.** Justificación: coherente con MVP simple (evita modelar "corrección" como evento separado en este slice) y con el caso real de "ingresé mal la tasa, la corrijo". La distinción "actualización vs. corrección" se diferiré a `TasaDeCambioCorregida` (followup #27). Cubierto por escenario §6.3 e INV-CT-1.

- [x] **Q2 — ¿Se permiten tasas con fecha futura?** — **Resuelto: no, rechazar.** Justificación: coherente con "snapshot histórico" (hotspots §2). El caso "tasa anunciada para mañana" se trata como un comando distinto (`PreRegistrarTasaDeCambio`) si surge en el futuro. La regla `Fecha <= ahora.Date` (PRE-3) es inclusiva de hoy (§6.9 cubre el límite). Si el PO firma luego que pre-registración es necesaria, se modela en slice dedicado, no se relaja PRE-3 retroactivamente.

- [x] **Q3 — ¿La proyección `TasasDeCambioVigentes` indexa por `(desde, hacia)` o por `(desde, hacia, fecha)`?** — **Resuelto: por `(desde, hacia)` solamente** (la fecha viaja como atributo del valor, no como parte de la clave). Justificación: el caso de uso primario (INV-14 / followup #24) es "dame la tasa vigente USD→COP **ahora**" — el handler de aprobación necesita "la última conocida hacia `MonedaBase`". Si el negocio pide "dame la tasa USD→COP del día 2024-03-15 específicamente" (auditoría histórica), eso se sirve con una proyección distinta `TasasDeCambioHistoricas` indexada por `(desde, hacia, fecha)` — followup futuro si surge. Mantener el read model primario simple es más valioso que pre-anticipar.

- [x] **Q4 — ¿`AprobarPresupuesto` se modifica en este slice para consumir `TasasDeCambioVigentes`?** — **Resuelto: NO.** Slice 06 cubre **solo el catálogo**. La integración (popular `SnapshotTasas` desde la proyección, levantar PRE-3 multimoneda de slice 05) sigue cubierta por followup #24 y se hará cuando el negocio pida aprobaciones multimoneda reales. Razón: aislar "registrar tasas" de "consumir tasas" reduce el blast radius del slice y permite firmar el catálogo sin presión sobre la integración. Si el orquestador prefiere fusionarlo, abre slice 06b (variante de wire-up) — la spec actual no lo bloquea.

## 11. Checklist pre-firma

- [x] Todas las precondiciones mapean a un escenario Then (PRE-1 → §6.6 + §6.10; PRE-2 → §6.7; PRE-3 → §6.8 + §6.9 caso límite; PRE-4 → §6.4 + §6.5).
- [x] Todas las invariantes tocadas y ejercitables mapean a un escenario Then (INV-CT-1 → §6.3; INV-NEW-CT-2 declarativa, observada en §6.2 y §6.3 por inmutabilidad del stream).
- [x] El happy path está presente (§6.1 stream vacío, §6.2 acumulación, §6.3 re-registración).
- [x] Fold del evento documentado (§6.11).
- [x] Camino "stream vacío" (`Crear` factory) y "stream existente" (`Registrar` instancia) ambos cubiertos (§6.1 / §6.6 vs. §6.2 / §6.3 / §6.10).
- [x] Idempotencia decidida no en blanco (§7).
- [x] Impactos en SharedKernel (§12), proyecciones (§8), endpoints HTTP (§9) y followups (§13) documentados.
- [x] Preguntas abiertas (§10): todas resueltas por el modeler con justificación.
- [x] §6 contiene **11 escenarios** (6.1–6.11) — dentro del rango ~10–11 esperado.

## 12. Impacto en SharedKernel (refactor transversal incluido en el slice)

Este slice introduce excepciones nuevas, comando, evento, agregado nuevo, proyección nueva. Mantiene el patrón slices 02/03/04/05 de incluir el refactor transversal del `DomainExceptionHandler` dentro del propio slice.

### 12.1 Excepciones nuevas

Tres excepciones. Todas heredan de `DominioException`, viven cada una en su propio archivo bajo `SharedKernel/`, y exponen propiedades fuertemente tipadas para aserción estructural (consistente con INV-SK-4 / INV-SK-5 del slice 00).

1. **`MonedasIgualesEnTasaException(Moneda Moneda) : DominioException`**
   - Propiedad: `Moneda: Moneda`.
   - Uso: PRE-1. Lanza cuando `cmd.MonedaDesde == cmd.MonedaHacia`.

2. **`TasaDeCambioInvalidaException(decimal TasaIntentada) : DominioException`**
   - Propiedad: `TasaIntentada: decimal`.
   - Uso: PRE-2. Lanza cuando `cmd.Tasa <= 0m`.

3. **`FechaDeTasaEnElFuturoException(DateOnly Fecha, DateOnly Hoy) : DominioException`**
   - Propiedades: `Fecha: DateOnly`, `Hoy: DateOnly`.
   - Uso: PRE-3. Lanza cuando `cmd.Fecha > ahora.Date`.

Mapeo HTTP (ver §9):
- `MonedasIgualesEnTasaException → 400`
- `TasaDeCambioInvalidaException → 400`
- `FechaDeTasaEnElFuturoException → 400`

Se añaden al `switch` de `DomainExceptionHandler.Mapear` dentro del mismo slice.

### 12.2 Nuevo comando

Archivo nuevo: `src/SincoPresupuesto.Domain/CatalogoDeTasas/Commands/RegistrarTasaDeCambio.cs`. Firma en §2.

### 12.3 Nuevo evento

Archivo nuevo: `src/SincoPresupuesto.Domain/CatalogoDeTasas/Events/TasaDeCambioRegistrada.cs`.

```csharp
public sealed record TasaDeCambioRegistrada(
    Moneda MonedaDesde,
    Moneda MonedaHacia,
    decimal Tasa,
    DateOnly Fecha,
    string? Fuente,
    DateTimeOffset RegistradaEn,
    string RegistradaPor);
```

Nota de serialización: `DateOnly` y `Moneda` ya tienen contratos STJ funcionando (Moneda con `[JsonConstructor]` desde followup #23 cerrado; `DateOnly` con conversores estándar de System.Text.Json). El `green` debe verificar round-trip si surge fricción — no bloqueante para slice 06.

### 12.4 Nuevo agregado `CatalogoDeTasas`

Archivo nuevo: `src/SincoPresupuesto.Domain/CatalogoDeTasas/CatalogoDeTasas.cs`.

```csharp
public class CatalogoDeTasas
{
    public Guid Id { get; set; } // requerido por Marten; igual al stream-id bien-conocido.
    private readonly List<RegistroDeTasa> _registros = new();
    public IReadOnlyList<RegistroDeTasa> Registros => _registros;

    public static TasaDeCambioRegistrada Crear(RegistrarTasaDeCambio cmd, DateTimeOffset ahora);
    public TasaDeCambioRegistrada Registrar(RegistrarTasaDeCambio cmd, DateTimeOffset ahora);
    public void Apply(TasaDeCambioRegistrada e);
}

public sealed record RegistroDeTasa(
    Moneda MonedaDesde,
    Moneda MonedaHacia,
    decimal Tasa,
    DateOnly Fecha,
    string? Fuente,
    DateTimeOffset RegistradaEn,
    string RegistradaPor);
```

Lógica común a `Crear` y `Registrar` (extraída a un helper privado para no duplicar):

```
ArgumentNullException.ThrowIfNull(cmd);

if (cmd.MonedaDesde == cmd.MonedaHacia)
    throw new MonedasIgualesEnTasaException(cmd.MonedaDesde);

if (cmd.Tasa <= 0m)
    throw new TasaDeCambioInvalidaException(cmd.Tasa);

var hoy = DateOnly.FromDateTime(ahora.UtcDateTime);
if (cmd.Fecha > hoy)
    throw new FechaDeTasaEnElFuturoException(cmd.Fecha, hoy);

var fuenteNormalizada = string.IsNullOrWhiteSpace(cmd.Fuente)
    ? null
    : cmd.Fuente.Trim();

var registradoPorNormalizado = string.IsNullOrWhiteSpace(cmd.RegistradoPor)
    ? "sistema"
    : cmd.RegistradoPor.Trim();

return new TasaDeCambioRegistrada(
    MonedaDesde: cmd.MonedaDesde,
    MonedaHacia: cmd.MonedaHacia,
    Tasa: cmd.Tasa,
    Fecha: cmd.Fecha,
    Fuente: fuenteNormalizada,
    RegistradaEn: ahora,
    RegistradaPor: registradoPorNormalizado);
```

Nota sobre `DateOnly.FromDateTime(ahora.UtcDateTime)`: el `green` decide si convertir desde UTC o desde local — afecta el caso de borde "ahora" en horario de transición de día. El modeler recomienda **UTC** para ser consistente con multi-tenancy global (un tenant en Madrid y otro en Bogotá registran tasas con la misma referencia temporal). Si se requiere fecha-local-del-tenant, se inyecta la zona horaria desde el handler — fuera del scope del agregado.

`Apply(TasaDeCambioRegistrada e)`: agrega un `RegistroDeTasa` derivado del evento al `_registros`.

### 12.5 Stream id bien-conocido

Archivo nuevo: `src/SincoPresupuesto.Application/CatalogoDeTasas/CatalogoDeTasasStreamId.cs`.

```csharp
public static class CatalogoDeTasasStreamId
{
    // Patrón slice 02 (ConfiguracionTenantStreamId): Guid constante; conjoined multi-tenancy
    // discrimina por tenant_id, así que cada tenant obtiene su propio stream bajo este id fijo.
    public static readonly Guid Value = new("ca7a1060-a5a5-4a5a-a5a5-000000000001");
}
```

El Guid concreto lo decide infra-wire — el dominio solo necesita "un Guid bien-conocido". Sugerencia: alinear con la convención visual de slice 02 (`cf61f2d7-…000000000001` para configuración) usando un prefijo distinto para catálogo de tasas (p.ej. `ca7a1060-…000000000001` que evoca "catálogo tasas"). No es prescripción dura.

### 12.6 Nueva proyección inline `TasasDeCambioVigentes`

Archivo nuevo: `src/SincoPresupuesto.Application/CatalogoDeTasas/TasasDeCambioVigentesProjection.cs`. Estructura del read model y comportamiento descritos en §8. Patrón análogo a `ConfiguracionTenantProjection`.

### 12.7 Sin cambios de comportamiento en SharedKernel existente

- `Dinero`, `Moneda`, `DominioException`, `Requerir`, `EstadoPresupuesto`, excepciones previas, `ConfiguracionTenant`, `Presupuesto`: sin tocar.
- `DomainExceptionHandler.Mapear`: suma de tres casos al `switch` (las tres excepciones nuevas), sin tocar los existentes.

## 13. Follow-ups generados por este slice

Se proponen a `FOLLOWUPS.md` al firmar la spec. Los números son tentativos — el reviewer los confirma al cerrar.

- **Refinamiento de #20** (existente, `TasaASnapshot` en `MontoAsignadoARubro`): este slice 06 **habilita** parcialmente #20 al introducir el agregado/proyección que `MontoAsignadoARubro` necesitará consultar. El followup #20 sigue abierto y se refina así: "cuando exista la proyección `TasasDeCambioVigentes` (cierre de slice 06), populate `MontoAsignadoARubro.TasaASnapshot` con la tasa vigente de `Monto.Moneda → MonedaBase` al asignar." Disparador para reabrir #20: cierre de slice 06.

- **Refinamiento de #24** (existente, multimoneda real en `AprobarPresupuesto`): este slice 06 **habilita** la integración. El followup #24 ahora se refina a: "consumir `TasasDeCambioVigentes` desde `AprobarPresupuestoHandler` para popular `SnapshotTasas` y levantar PRE-3 multimoneda (slice 05)." Disparador: cierre de slice 06 + decisión del orquestador de abrir slice 06b (wire-up) o esperar.

- **#27 (nuevo)** — **`TasaDeCambioCorregida` como evento separado para auditoría de cambios**. Origen: slice-06 spec §3 + §5 (INV-CT-1) + §10 Q1. Hotspots §2 menciona el evento `TasaDeCambioCorregida { TasaAnteriorId, Fecha, MonedaDesde, MonedaHacia, TasaCorregida, Motivo }` para distinguir corrección de actualización. En MVP slice 06 se modela todo como `TasaDeCambioRegistrada` (last-write-wins). Cuando el PO solicite auditoría con motivo obligatorio (típicamente para cumplimiento contable o forensics): introducir el comando `CorregirTasaDeCambio(TasaAnteriorEventId, NuevaTasa, Motivo)` y el evento correspondiente. Disparador: PO solicita auditoría de cambios o aparece el primer caso de "necesito saber por qué cambió la tasa del 2024-03-15".

- **#28 (nuevo, condicional)** — **Proyección `TasasDeCambioHistoricas`** indexada por `(desde, hacia, fecha)`. Origen: slice-06 spec §10 Q3. La proyección primaria `TasasDeCambioVigentes` indexa solo por `(desde, hacia)` — útil para "dame la tasa actual". Si surge el caso de uso "dame la tasa de USD→COP del 2024-03-15 específicamente" (auditoría, reportes históricos, recálculo de baseline con tasa vintage), modelar proyección secundaria que conserve el detalle por fecha. Disparador: primera consulta histórica por fecha desde la UI o un reporte.

- **#29 (nuevo, condicional)** — **Slice 06b — wire-up `AprobarPresupuesto` con `TasasDeCambioVigentes`**. Origen: slice-06 spec §10 Q4. Variante del slice 06 que cubre la integración con `AprobarPresupuestoHandler`: consultar la proyección, popular `SnapshotTasas`, eliminar PRE-3 multimoneda de slice 05, lanzar `TasaDeCambioFaltanteException(Moneda monedaSinTasa)` si falta tasa para alguna moneda presente en partidas. Disparador: orquestador firma slice 06 y decide priorizar la integración antes de continuar con `Activar`/`Cerrar`. Alternativamente, se trata como follow-up de cierre del MVP multimoneda. Coincide funcionalmente con el refinamiento de #24 — al cerrar uno se cierra el otro.
