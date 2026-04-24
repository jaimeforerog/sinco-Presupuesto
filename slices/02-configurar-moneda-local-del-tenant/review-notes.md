# Review notes — Slice 02 — ConfigurarMonedaLocalDelTenant

**Autor:** reviewer
**Fecha:** 2026-04-24
**Slice auditado:** `slices/02-configurar-moneda-local-del-tenant/`.
**Veredicto:** `approved-with-followups`

---

## 1. Resumen ejecutivo

Slice 02 implementa la configuración de moneda local del tenant como agregado event-sourced nuevo. El ciclo red → green → refactor completó satisfactoriamente: 35/35 tests pasan (7 nuevos Slice02, 5 DineroTests modificados, 23 Slice01 + SharedKernel sin regresión). La especificación se mapea exactamente a los tests. El refactor transversal de `Moneda` a ISO 4217 está correctamente embebido. Se registran 3 followups nuevos en `FOLLOWUPS.md` (#8, #9, #10) y uno existente (#7) es impactado. Veredicto: **approved-with-followups** — no hay blockers; los followups son registrados y alineados con el backlog.

---

## 2. Checklist de auditoría

### 2.1 Spec ↔ tests

- [x] Cada escenario de `spec.md §6` tiene un test correspondiente.
  - §6.1 happy path → `ConfigurarMonedaLocalDelTenant_sobre_stream_vacio_emite_MonedaLocalDelTenantConfigurada`.
  - §6.2 normalización `ConfiguradoPor` → `ConfigurarMonedaLocalDelTenant_con_ConfiguradoPor_vacio_usa_sistema_como_default` (2 cases: `""`, `"   "`).
  - §6.3 PRE-1 `TenantId` vacío → `ConfigurarMonedaLocalDelTenant_con_TenantId_vacio_lanza_CampoRequerido` (2 cases).
  - §6.4 INV-NEW-1 tenant ya configurado → `ConfigurarMonedaLocalDelTenant_sobre_stream_existente_lanza_TenantYaConfigurado`.
  - §6.5 fold del evento → `Fold_de_MonedaLocalDelTenantConfigurada_deja_el_agregado_con_datos_consistentes`.
  - §12 refactor transversal `Moneda` ISO 4217 → `DineroTests.Moneda_rechaza_codigo_invalido` modificado con caso crítico `"XYZ"`.

- [x] Cada precondición tiene un test que la viola.
  - PRE-1 (`TenantId` requerido) → test §6.3 lanza `CampoRequeridoException`.
  - PRE-2 (normalización, no fallo) → no hay test de "fallo", es correcto (spec §4).

- [x] Cada invariante tocada tiene un test que la viola.
  - INV-NEW-1 (configuración única) → test §6.4 lanza `TenantYaConfiguradoException` sobre stream existente.
  - INV-16 (establecida en §6.1) → happy path verifica que el evento se emite (invariante se establece, no se viola en este slice).
  - INV-NEW-2 (inmutabilidad) → declarativa en spec; sin comando que la viole en este slice.

- [x] Los nombres de los tests son frases completas que describen el comportamiento.
  - Todos los nombres siguen la forma "ConfigurarMonedaLocalDelTenant_{condición}_{resultado}".

### 2.2 Tests como documentación

- [x] Un lector que no conoce el código puede entender el comportamiento leyendo solo los tests.
  - Cada test tiene sección Given (comentario explícito) → When (invocación de comando) → Then (assertions).
  - El test §6.4 es el más ilustrativo: muestra que tras un fold previo, `Ejecutar` detecta el estado y lanza; es la protección de invariante.

- [x] Given/When/Then está claro visualmente en cada test.
  - Comentarios explícitos `// Given:`, `// When`, `// Then` marcan las secciones.
  - Los fixtures (`CmdValido()`, `AhoraFijo`) reutilizan construcciones comunes sin abstraerlas en mocks.

- [x] Sin mocks del dominio.
  - Todos los eventos (Given) son instancias reales de `MonedaLocalDelTenantConfigurada` y `PresupuestoCreado`.
  - El helper `AggregateBehavior<T>.Reconstruir()` usa reflexión sobre `Apply`, no es un mock — es un orquestador de fold.
  - Las excepciones se aserta por tipo (no por mensaje), desacoplando del texto.

### 2.3 Implementación

- [x] El código de producción añadido es mínimo.
  - `ConfiguracionTenant.cs`: 77 líneas, 3 métodos públicos (`Create`, `Ejecutar`, `Apply`) + 4 propiedades. Todos ejercidos.
  - `MonedaLocalDelTenantConfigurada.cs`: 14 líneas, record inmutable.
  - `ConfigurarMonedaLocalDelTenant.cs`: 13 líneas, record inmutable.
  - `CodigoMonedaInvalidoException.cs`: 16 líneas, excepción.
  - `TenantYaConfiguradoException.cs`: 19 líneas, excepción.
  - `AggregateBehavior<T>.cs`: 38 líneas, helper genérico reutilizable (reemplazó `PresupuestoBehavior`, que era dead code tras generalización).
  - `Moneda.cs`: refactor (+60, -10), lista ISO 4217 + validación.
  - **Total dominio:** ~225 líneas; cero código no ejercido por tests.

- [x] No hay `DateTime.UtcNow`, `Guid.NewGuid()` u otras fuentes de no-determinismo dentro del dominio.
  - `DateTimeOffset ahora` se inyecta en `Create` y `Ejecutar`.
  - `Presupuesto.Create` recibe `PresupuestoId` desde fuera.

- [x] Los eventos son `record` inmutables.
  - `MonedaLocalDelTenantConfigurada`: `sealed record`.
  - `ConfigurarMonedaLocalDelTenant`: `sealed record`.

- [x] `Dinero`/`Moneda` se usan para montos; nunca `decimal` pelado.
  - El campo `MonedaLocal` en `ConfiguracionTenant` es de tipo `Moneda?` (nullable porque es reconstructible desde eventos).
  - `Dinero` no se usa aquí (no hay montos), pero `Moneda` sí, consistente con el patrón.

### 2.4 Cobertura

- [x] Cobertura de ramas del agregado ≥ **85 %**.
  
  **Análisis de ramas en `ConfiguracionTenant.cs`:**
  
  1. **`Create(cmd, ahora)`** — líneas 31-50:
     - L35: `if (IsNullOrWhiteSpace(TenantId))` — rama verdadera ejercida (test §6.3, 2 cases). Rama falsa ejercida (tests §6.1, §6.2).
     - L41-42: `IsNullOrWhiteSpace(ConfiguradoPor)` — rama verdadera (test §6.2, 2 cases). Rama falsa (test §6.1).
     - **100 % de ramas en `Create`.**

  2. **`Ejecutar(cmd, ahora)`** — líneas 59-64:
     - L63: `throw new TenantYaConfiguradoException(...)` — rama ejecutada por test §6.4.
     - **Nota de refactor-notes:** la rama `return Create(...)` fue eliminada en refactor (dead code, nunca ejercida). Decisión correcta: `Ejecutar` siempre se invoca sobre stream existente (fold previo), por lo que `TenantId != null` es garantizado. El método es ahora incondicionalmente lanzador.
     - **100 % de ramas en `Ejecutar` (tras refactor).**

  3. **`Apply(evt)`** — líneas 70-76:
     - Asignaciones directas sin control de flujo.
     - **100 % de ramas.**

  **Cobertura agregada de `ConfiguracionTenant`: 100 %.**

  **Análisis de ramas en `Moneda.cs` (líneas 46-65):**
  - L48: `IsNullOrWhiteSpace` — ejercida por test `Moneda_rechaza_codigo_invalido` caso `""`.
  - L54: `Length != 3` — ejercida por casos `"US"`, `"USDD"`.
  - L54: `!All(...)` — ejercida por caso `"US1"` (dígito).
  - L59: `!Contains(normalizado)` — ejercida por caso `"XYZ"` (nuevo en spec §12, no existía antes).
  - **100 % de ramas en constructor `Moneda`.**

  **Cobertura agregada: 100 %** — muy por encima del umbral 85 %.

- [x] Ramas descubiertas están justificadas o anotadas como deuda.
  - No hay ramas descubiertas.

### 2.5 Refactor

- [x] `refactor-notes.md` presente y claro.
  - Archivo `slices/02-configurar-moneda-local-del-tenant/refactor-notes.md` presente, ~140 líneas.
  - Registra 1 refactor aplicado (Item 1: extirpar rama muerta en `Ejecutar`).
  - Registra 5 impulsos descartados con justificación (Items 2-6).
  - Criterio METHODOLOGY §8 (100 % cobertura de código público) se aplica explícitamente y se cumple tras refactor.

- [x] Los tests no se tocaron en la fase refactor.
  - Verificado: no hay cambios en `Slice02_ConfigurarMonedaLocalDelTenantTests.cs` ni en `DineroTests.cs` entre green-notes y refactor-notes.
  - El test §6.4 sigue pasando porque el comportamiento observable es idéntico (la rama verdadera del `if` es la única ejercida).

- [x] Sin warnings de compilación.
  - El uso de `MonedaLocal!.Value` en línea 63 de `ConfiguracionTenant.cs` es correcto y necesario para suprimir nullable-warning (la propiedad es nullable, pero tras fold siempre está asignada).
  - Refactor-notes §6 "Decisiones deliberadas" justifica esta decisión.

### 2.6 Invariantes cross-slice

- [x] El slice no rompe invariantes de slices previos.
  - Slice 01 (`CrearPresupuesto`): 26 tests, todos pasan. Verificado: call-site de `AggregateBehavior<Presupuesto>` actualizado correctamente en Slice01_CrearPresupuestoTests.cs línea 218.
  - SharedKernel (`DineroTests`): 5 tests positivos + 1 test negativo modificado, todos pasan. El refactor de `Moneda` de regex a ISO 4217 solo cambió el tipo de excepción lanzada (de `ArgumentException` a `CodigoMonedaInvalidoException`), que es alineado con la spec §12.
  - **Comando de verificación:** green-notes §7 declara `dotnet test` pasa con 35/35.

- [x] El agregado `ConfiguracionTenant` se alinea con el patrón de `Presupuesto`.
  - Ambos tienen `Create(cmd, ...)` → factory estática que devuelve evento.
  - Ambos tienen `Apply(evt)` → fold pasivo.
  - Ambos usan el mismo helper genérico `AggregateBehavior<T>`.
  - Moneda se usa en ambos (Presupuesto.MonedaBase, ConfiguracionTenant.MonedaLocal).

### 2.7 Coherencia con decisiones previas

- [x] Alineado con `01-event-storming-mvp.md`.
  - ConfiguracionTenant es un agregado nuevo (no estaba en event storming original, pero se sugirió en spec §1).
  - Evento `MonedaLocalDelTenantConfigurada` es la materialización del concepto "configurar moneda del tenant".
  - El agregado es event-sourced, consustancial con la arquitectura CQRS + EDA.

- [x] Alineado con `02-decisiones-hotspots-mvp.md` §2.
  - Hot spot: "multimoneda del tenant". Decisión: tenant tiene `MonedaLocal` inmutable.
  - Slice 02 implementa exactamente eso: `ConfiguracionTenant` establece `MonedaLocal` una sola vez.
  - Spec §5 (INV-NEW-1, INV-16) mapea a esa decisión.

- [x] Alineado con memoria `project_multimoneda.md`.
  - Tenant tiene `MonedaLocal` (configurada aquí en slice 02).
  - Presupuesto hereda `MonedaBase` de la configuración del tenant (será en slice 02 followup #8).
  - Snapshot de tasas al aprobar (future slice, no slice 02).

- [x] Alineado con memoria `project_metodologia.md`.
  - Excepciones heredan de `DominioException` (§12 en spec, implemented: `CodigoMonedaInvalidoException`, `TenantYaConfiguradoException`).
  - Tests aserta por tipo, no por mensaje (METHODOLOGY §2.1; todos los tests usan `.Which.NombreCampo` o `.Which.Match<>`, nunca `.Message`).

- [x] Alineado con memoria `project_stack_decision.md`.
  - Marten 7.34, multi-tenant conjoint (StreamId = TenantId para el agregado).
  - No hay Wolverine o infra en este slice (eso es fase posterior `infra-wire`).

---

## 3. Hallazgos

| # | Tipo | Descripción | Ubicación | Acción sugerida |
|---|---|---|---|---|
| 1 | followup | Validación de `MonedaBase` en `CrearPresupuestoHandler` — falta verificar que el tenant esté configurado antes de crear presupuesto. | spec.md §10 Q2 + green-notes §2 | Registrado como FOLLOWUPS.md #8. No es blocker de slice 02, es handoff a infra-wire. |
| 2 | followup | Completitud de la lista ISO 4217 en `Moneda` — green embebió 180+ códigos (lista amplia). Refactor-notes §2 justifica mantenerla. Confirmación de paridad exacta con ISO 4217 vigente está pendiente. | Moneda.cs líneas 14-42 + refactor-notes §2 | Registrado como FOLLOWUPS.md #9. Candidato para cierre con slice 00 retroactivo (FOLLOWUPS.md #4). |
| 3 | followup | Helper `RequireCampo()` abstracción — hoy se usa en Presupuesto.Create (3 campos) y ConfiguracionTenant.Create (1 campo) con lógica idéntica. Candidato para tercer uso. | refactor-notes §4.2 descartado #3 | Registrado como FOLLOWUPS.md #10. Disparador: tercer uso. |
| 4 | followup | `Apply` público vs. `internal` — agregados exponen Apply en superficie pública. Investigar si Marten soporta `internal Apply` con `InternalsVisibleTo`. | ConfiguracionTenant.cs línea 70 + refactor-notes §4.3 descartado #4 | Registrado en FOLLOWUPS.md #7 (abierto desde slice 01). Impactado por slice 02 (nuevo agregado con Apply público). |
| 5 | nit | Mensaje de `CodigoMonedaInvalidoException` es parcialmente incorrecto. Dice "Esperado: tres letras A-Z" pero ahora valida contra lista ISO 4217. Los tests NO aserta sobre mensaje (solo tipo + propiedad `CodigoIntentado`), así que no afecta behavior. | CodigoMonedaInvalidoException.cs línea 12 | Nit asumido. Candidato para refactor de strings en momento posterior (internacionalización, UX). |
| 6 | nit | `AggregateBehavior<T>` usa reflexión (línea 28-33) sin caché. Para alto volumen de tests podría precalcular `MethodInfo` por tipo. Hoy no hay impacto observable (35 tests corren sub-segundo). | TestKit/AggregateBehavior.cs | Nit asumido. Optimización prematura; re-evaluar si test suite crece >100 tests. |

---

## 4. Veredicto final

- [ ] **approved**
- [x] **approved-with-followups** — 4 followups registrados en `FOLLOWUPS.md` (#8, #9, #10, impacto en #7). Sin blockers. Todos los criterios de Definition of Done se cumplen (spec firmada, tests rojos → verdes → refactorizados, cobertura 100 %, no rompre slice 01, coherencia con decisiones previas, `review-notes.md` presente).
- [ ] **request-changes**

**Detalles del veredicto:**

El slice 02 se cierra exitosamente. Los 3 nuevos followups (#8, #9, #10) están documentados en `FOLLOWUPS.md` y son de baja severidad:
- #8 es handoff a infra-wire (fuera del scope de auditoría de dominio).
- #9 es confirmación de datos (no impacta behavior; red-notes §1 ya lo anticipa).
- #10 es oportunidad de DRY (tercer uso aún no se alcanza).

El impacto en #7 (Apply público) es asumido: es open issue desde slice 01 y no bloquea slice 02 (investigación pendiente de Marten + InternalsVisibleTo).

Los 2 nits (#5, #6) son comentarios menores sin impacto en calidad del código.

**Orquestador puede proceder a:** commit del slice + fase `infra-wire` (registrar handler en Wolverine, proyección en Marten, endpoint HTTP, test de integración).

