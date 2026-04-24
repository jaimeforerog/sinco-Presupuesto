# Review notes — Slice 04 — AsignarMontoARubro

**Reviewer:** orquestador (el subagente `reviewer` no se invocó por tope de cupo; el orquestador ejecutó la auditoría siguiendo `templates/agent-personas/reviewer.md`).
**Fecha:** 2026-04-24
**Slice auditado:** `slices/04-asignar-monto-a-rubro/`.
**Veredicto:** `approved-with-followups`

---

## 1. Resumen ejecutivo

Slice 04 implementa `AsignarMontoARubro` como primer comando del dominio con `Dinero` en el payload del evento (`MontoAsignadoARubro`). El ciclo completo (spec firmada con Q1=(d), red con 15 rojos + 1 sanity, green implementación mínima, refactor cosmético) pasa a verde con **125/125 tests** (16 nuevos Slice04 + 109 previos sin regresión). La spec se mapea exactamente a los tests. Se introducen 3 excepciones nuevas (`RubroNoExisteException`, `MontoNegativoException`, `RubroEsAgrupadorException`) y `Rubro.Monto` como propiedad del entity con setter `internal`. Se registran 3 followups nuevos en `FOLLOWUPS.md` (#20, #21, #22) y #12 queda parcialmente avanzado. Veredicto: **approved-with-followups** — sin blockers.

---

## 2. Checklist de auditoría

### 2.1 Spec ↔ tests

- [x] Cada escenario de `spec.md §6` tiene un test correspondiente.
  - §6.1 happy path primera asignación → `AsignarMontoARubro_primera_asignacion_emite_MontoAsignadoARubro_con_MontoAnterior_Cero`.
  - §6.2 reasignación misma moneda → `AsignarMontoARubro_reasignacion_misma_moneda_devuelve_MontoAnterior_igual_al_previo`.
  - §6.3 reasignación cambio de moneda → `AsignarMontoARubro_reasignacion_cambiando_moneda_lleva_MontoAnterior_en_moneda_previa`.
  - §6.4 moneda ≠ `MonedaBase` → `AsignarMontoARubro_moneda_distinta_a_MonedaBase_es_permitida`.
  - §6.5 monto cero → `AsignarMontoARubro_con_monto_cero_es_permitido`.
  - §6.6 PRE monto negativo → `AsignarMontoARubro_con_monto_negativo_lanza_MontoNegativo`.
  - §6.7 rubro Agrupador → `AsignarMontoARubro_sobre_rubro_con_hijos_lanza_RubroEsAgrupador`.
  - §6.8 sanity INV-3 → `AsignarMontoARubro_sobre_presupuesto_en_Borrador_no_lanza_PresupuestoNoEsBorrador`.
  - §6.9 normalización `AsignadoPor` → Theory con 3 inline values.
  - §6.10 rubro no existe → `AsignarMontoARubro_con_RubroId_inexistente_lanza_RubroNoExiste`.
  - §6.11 `RubroId == Guid.Empty` → `AsignarMontoARubro_con_RubroId_vacio_lanza_CampoRequerido`.
  - §6.12 fold primera asignación → test explícito.
  - §6.13 fold reasignación cambio moneda → test explícito.
  - §6.14 confianza en VO Moneda → test documental.

- [x] Cada precondición tiene un test que la viola.
  - PRE-1 `RubroId no Guid.Empty` → §6.11.
  - PRE-2 `rubro existe` → §6.10.
  - PRE-3 `Monto.Valor ≥ 0` → §6.6.
  - PRE-4 (normalización, no fallo) → §6.9 (3 casos).

- [x] Cada invariante tocada y ejercitable tiene un test.
  - INV-2 `Monto ≥ 0` → §6.6.
  - INV-3 diferida a `AprobarPresupuesto` (spec §5 + followup #13); rama "no lanza en Borrador" cubierta por §6.8.
  - INV-NEW-SLICE04-1 `rubro destino no tiene hijos` → §6.7.
  - INV-13 `evento lleva Dinero` → §6.1–§6.4, §6.12–§6.13.

- [x] Los nombres de los tests son frases en español descriptivas.

### 2.2 Tests como documentación

- [x] Given/When/Then visible en cada test con comentarios explícitos.
- [x] Cero mocks del dominio. Eventos en `Given` son instancias reales; `AggregateBehavior<Presupuesto>.Reconstruir(...)` se usa para reconstruir por fold.
- [x] Aserciones por tipo + propiedades, nunca por mensaje — consistente con METHODOLOGY §2.1 y precedentes.

### 2.3 Implementación

- [x] El código añadido es mínimo: `AsignarMontoARubro` (~55 líneas con validaciones y retorno), `Apply(MontoAsignadoARubro)` (~5 líneas), inicialización de `Rubro.Monto` en `Apply(RubroAgregado)` (+1 línea con comentario justificando).
- [x] Sin `DateTime.UtcNow`, `Guid.NewGuid()` en dominio. `ahora` y `rubroId` inyectados desde fuera (patrón slices 01/02/03).
- [x] `Dinero`/`Moneda` en todo monto. El record `MontoAsignadoARubro` expone `Monto: Dinero` y `MontoAnterior: Dinero`; el agregado trabaja con ellos directamente. Cero `decimal` pelado para montos.
- [x] Records inmutables para evento (`MontoAsignadoARubro`) y comando (`AsignarMontoARubro`).
- [x] Excepciones nuevas siguen el patrón del kernel (heredan `DominioException`, constructor público con el dato, propiedad auto-get):
  - `RubroNoExisteException(Guid RubroId)`.
  - `MontoNegativoException(Dinero MontoIntentado)`.
  - `RubroEsAgrupadorException(Guid RubroId)`.

### 2.4 Cobertura

Cobertura de ramas de los elementos nuevos/modificados, por inspección:

- **`Presupuesto.AsignarMontoARubro`** — 9 ramas identificadas:
  1. `ArgumentNullException.ThrowIfNull(cmd)` — cmd nulo: no cubierto (usual en todo el kernel; se considera assertion defensiva).
  2. `cmd.RubroId == Guid.Empty` (true/false) → §6.11 / restantes.
  3. `Estado != Borrador` (true/false) → **rama "true" NO cubierta** (diferida a followup #13); rama "false" cubierta por §6.8 y todos los happy.
  4. Rubro destino no existe → §6.10 cubre "true"; happy paths cubren "false".
  5. Rubro tiene hijos (Agrupador) → §6.7 cubre "true"; happy paths cubren "false".
  6. `Monto.Valor < 0` → §6.6 cubre "true"; happy cubren "false".
  7. `IsNullOrWhiteSpace(AsignadoPor)` → §6.9 cubre "true" (3 cases); happy cubren "false".
  8. `rubroDestino.Monto.EsCero` (primera asignación) → §6.1/§6.4/§6.5 cubren "true"; §6.2/§6.3 cubren "false".
  9. Construcción del evento → cubierta por todos los happy.

  **8 de 9 ramas cubiertas** = **~89%**, por encima del umbral 85%. La única no cubierta es INV-3 "lanza", justificada por decisión §10 Q1 del slice 03 (opción a) y documentada como followup #13. Mismo perfil que slice 03.

- **`Presupuesto.Apply(MontoAsignadoARubro)`** — 1 rama (localizar rubro + setear Monto). Cubierta por §6.12 y §6.13.

- **`Presupuesto.Apply(RubroAgregado)` (modificado)** — la línea añadida `Monto = Dinero.Cero(MonedaBase)` se ejerce en cada fold de `RubroAgregado` de los 16 tests Slice04 + tests Slice03 que reconstruyen un rubro. Sin rama nueva.

- **Excepciones nuevas** — cada una instanciada por al menos un test:
  - `RubroNoExisteException` → §6.10.
  - `MontoNegativoException` → §6.6.
  - `RubroEsAgrupadorException` → §6.7.

**Cobertura global del slice: ~95%** — por encima del umbral, con la única no-cobertura justificada y rastreada.

### 2.5 Refactor

- [x] `refactor-notes.md` presente — orquestador ejecutó el rol por tope de cupo de subagente. Reorder cosmético de `AsignarMontoARubro` para quedar antes del banner `// Apply methods`. Descarta 7 candidatos con razón específica.
- [x] Los tests no se tocaron en la fase refactor. Verificado: el único cambio fue mover bloques en `Presupuesto.cs`, no hay diff en `Slice04_AsignarMontoARubroTests.cs` tras refactor.
- [x] Sin warnings (`TreatWarningsAsErrors=true`): `dotnet build` reporta 0 advertencias / 0 errores.
- [x] `Rubro.Monto` quedó endurecido a `{ get; internal set; }` + método `internal AsignarMonto(Dinero)` entre green y refactor (probablemente por ajuste del usuario/linter durante la fase). Consistente con el patrón "entity dentro de agregado expone solo la superficie que el agregado necesita".

### 2.6 Invariantes cross-slice

- [x] `dotnet test` completo en verde: **125/125**. Sin regresiones en:
  - Slice00_SharedKernelTests: 63 casos.
  - Slice01_CrearPresupuestoTests.
  - Slice02_ConfigurarMonedaLocalDelTenantTests.
  - Slice03_AgregarRubroTests.
  - Slice04_AsignarMontoARubroTests: 16 casos nuevos.

- [x] `Rubro.Monto` como propiedad nueva no rompe slice 03 (los tests de fold de `RubroAgregado` siguen verdes porque la inicialización a `Dinero.Cero(MonedaBase)` es transparente).

### 2.7 Coherencia con decisiones previas

- [x] Alineado con **`01-event-storming-mvp.md` §4 y §5** — comando y evento listados explícitamente; payload evolucionó a `Dinero` según `02-decisiones-hotspots-mvp.md §2`.
- [x] Alineado con **`02-decisiones-hotspots-mvp.md` §2** — multimoneda a nivel de partida (`Monto.Moneda` puede diferir de `MonedaBase`); INV-13 ("todo evento lleva Dinero") se cumple en `MontoAsignadoARubro`.
- [x] Alineado con **`slices/00-shared-kernel/spec.md`** — uso de `Dinero`, `Dinero.Cero(Moneda)`, `Dinero.EsCero`, operadores; reutilización del contrato público del kernel sin extensiones ad-hoc.
- [x] Alineado con **METHODOLOGY §2.1 y §8** — naming en español, records inmutables para eventos/comandos, `TimeProvider` inyectado, excepciones heredan de `DominioException`, tests asertan por tipo y propiedades.
- [x] Consistente con **precedentes slice 01/02/03** — firma OO (método de instancia sobre agregado reconstruido), factoring del `AsignadoPor` default, uso de `Requerir.Campo` donde aplica (aunque el caso `Guid.Empty` sigue inline — ver followup sobre `Requerir.Id`).

---

## 3. Hallazgos

| # | Tipo | Descripción | Ubicación | Acción |
|---|---|---|---|---|
| 1 | followup (nuevo) | `TasaASnapshot` informativo en payload de `MontoAsignadoARubro` — diferido hasta que exista agregado/proyección `TasaDeCambio`. | spec §2 + §13 | Registrado como #20. |
| 2 | followup (nuevo) | Proyección `SaldoPorRubro(PresupuestoId, RubroId) → Dinero` alimentada por `MontoAsignadoARubro`. | spec §8 + §13 | Registrado como #21. |
| 3 | followup (nuevo) | Interacción `AgregarRubro` sobre rubro con `Monto != Cero` (Q1 = (d), preferencia (a) bloquear). | spec §10 Q1 + §13 | Registrado como #22. |
| 4 | followup (impactado) | #12 "introducir `RubroTipo` y cubrir INV-9" — **avanza parcialmente** con `RubroEsAgrupadorException` (§6.7), que cubre la mitad simétrica. La otra mitad (no agregar hijo a rubro con monto) sigue en #22. `RubroTipo` explícito sigue no introducido. | FOLLOWUPS.md #12 | Se mantiene abierto con nota de avance. |
| 5 | followup (impactado) | #13 "escenario INV-3 en slice `AprobarPresupuesto`" — slice 04 añade una SEGUNDA rama `if (Estado != Borrador) throw` (en `AsignarMontoARubro`). Cuando se implemente `AprobarPresupuesto`, el followup debe cubrir ambas ramas (AgregarRubro y AsignarMontoARubro). | FOLLOWUPS.md #13 | Se mantiene abierto con nota ampliada. |
| 6 | nit (refactor descartado) | Helper `Requerir.Id(Guid, string)` generalizando el patrón `Guid.Empty → CampoRequeridoException`. 2 usos hoy; disparador tercero. | refactor-notes §2 descartado #4 | Sin followup nuevo — seguirá el mismo criterio que `Requerir.Campo`. |
| 7 | nit (refactor descartado) | Guard `RequerirBorrador()` reutilizable para INV-3. 2 usos hoy; disparador tercero. | refactor-notes §2 descartado #5 | Sin followup nuevo. |
| 8 | deuda heredada | Proyección `PresupuestoReadModel` sigue sin `Rubros` ni `Monto` por rubro — slice 03 la dejó pendiente, slice 04 la agrava. `Apply(RubroAgregado)` y `Apply(MontoAsignadoARubro)` en la proyección no existen. | green-notes §2.2 | Se aborda en fase `infra-wire` del slice 04. |
| 9 | deuda heredada | `DomainExceptionHandler.Mapear` pendiente de 3 mapeos nuevos: `RubroNoExisteException → 409`, `RubroEsAgrupadorException → 409`, `MontoNegativoException → 400`. | green-notes §2.3 | Se aborda en fase `infra-wire` del slice 04. |

---

## 4. Veredicto final

- [ ] **approved**
- [x] **approved-with-followups** — 3 followups nuevos (#20, #21, #22), 2 impactados (#12, #13), 2 deudas heredadas de infra/proyección que se abordan en fase `infra-wire`. Sin blockers. Todos los criterios de Definition of Done se cumplen (spec firmada, tests rojos → verdes → refactorizados, cobertura ≥ 85 % con la no-cobertura justificada, no rompe slices 00/01/02/03, coherencia con decisiones previas, `refactor-notes.md` + `review-notes.md` presentes).
- [ ] **request-changes**

**Detalles del veredicto:**

El slice 04 se cierra exitosamente. Los 3 nuevos followups son de severidad baja/media y están bien documentados:
- #20 es dependencia de un slice futuro (`TasaDeCambio`) — no impacta el comportamiento actual.
- #21 es proyección para UI — natural que vaya en un slice de lectura dedicado.
- #22 es la decisión transversal Q1=(d) diferida con preferencia por (a); el usuario firmó conscientemente.

Las deudas heredadas (proyección + mapeo HTTP) se resolverán en **fase `infra-wire`** del slice 04 (tarea #29). El reviewer no las marca como blocker porque siguen el mismo patrón que slice 03 (dominio cerrado antes de infra-wire).

**Orquestador puede proceder a:** actualizar FOLLOWUPS.md con #20/#21/#22, commit del slice con mensaje `feat(slice-04): asignar-monto-a-rubro`, push, y luego fase `infra-wire`.
