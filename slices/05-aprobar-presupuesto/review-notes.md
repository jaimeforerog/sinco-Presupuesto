# Review notes — Slice 05 — AprobarPresupuesto

**Reviewer:** orquestador (siguiendo `templates/agent-personas/reviewer.md`).
**Fecha:** 2026-04-24
**Veredicto:** `approved-with-followups`

---

## 1. Resumen ejecutivo

Slice 05 cierra el primer eslabón del ciclo de vida del presupuesto (`Borrador → Aprobado`) y libera la deuda más sensible del backlog: **followup #13** (rama "lanza" de INV-3) queda cubierta por primera vez para los 3 comandos del agregado (`AgregarRubro`, `AsignarMontoARubro`, `AprobarPresupuesto`). El multimoneda real se difiere consistentemente con el plan: `SnapshotTasas` queda en el payload del evento como diccionario vacío hasta que exista `TasaDeCambio` (followup #24 nuevo). 14 casos xUnit nuevos verdes; 142/142 dominio + 20/20 integración (162/162). El refactor de `RequerirBorrador()` cierra el patrón al alcanzar el tercer uso, mismo precedente que `Requerir.Campo` (followup #10).

---

## 2. Checklist de auditoría

### 2.1 Spec ↔ tests

- [x] Cada escenario §6 tiene test correspondiente:
  - §6.1 happy simple → `AprobarPresupuesto_con_un_terminal_con_monto_positivo_emite_PresupuestoAprobado`.
  - §6.2 árbol con Agrupador + 2 terminales → test correspondiente verificado.
  - §6.3 normalización `AprobadoPor` → Theory con 3 inline values (`""`, `"   "`, `"\t"`).
  - §6.4 sin rubros → `PresupuestoSinMontosException`.
  - §6.5 todos en cero → `PresupuestoSinMontosException`.
  - §6.6 multimoneda → `AprobacionConMultimonedaNoSoportadaException` con lista completa.
  - §6.7 ya aprobado → `PresupuestoNoEsBorradorException`.
  - §6.8 INV-3 retroactivo `AgregarRubro` post-aprobado.
  - §6.9 INV-3 retroactivo `AsignarMontoARubro` post-aprobado.
  - §6.10 fold preserva `Estado/MontoTotal/SnapshotTasas/AprobadoEn/AprobadoPor`.
  - §6.11 ignora terminales en cero.
  - §6.12 Agrupadores no aportan.
- [x] Cada precondición tiene test que la viola: PRE-1 (§6.7), PRE-2 (§6.4 + §6.5), PRE-3 (§6.6), PRE-4 (§6.3).
- [x] Cada invariante ejercitable tiene test: INV-3 lanza (§6.7), retroactivo (§6.8/§6.9); INV-13 `Dinero` en payload (§6.1–§6.2 + §6.10); INV-14 indirecta vía PRE-3.
- [x] Nombres de tests son frases en español que describen el comportamiento.

### 2.2 Tests como documentación

- [x] Given/When/Then visible con comentarios explícitos en cada test.
- [x] Cero mocks del dominio. `AggregateBehavior<Presupuesto>.Reconstruir(...)` reconstruye desde eventos reales.
- [x] Aserciones por tipo + propiedades, nunca por mensaje.

### 2.3 Implementación

- [x] Código añadido es mínimo: `AprobarPresupuesto(...)` (~50 líneas con validaciones), `Apply(PresupuestoAprobado)` (~7 líneas), `RequerirBorrador()` (~5 líneas).
- [x] Sin `DateTime.UtcNow`, `Guid.NewGuid()` en dominio. `ahora` inyectado.
- [x] `Dinero`/`Moneda` para montos. `MontoTotal` es `Dinero`, no `decimal` pelado.
- [x] Records inmutables: `AprobarPresupuesto` (comando), `PresupuestoAprobado` (evento) son `sealed record`.
- [x] Excepciones nuevas siguen el patrón del kernel:
  - `PresupuestoSinMontosException(Guid PresupuestoId)`.
  - `AprobacionConMultimonedaNoSoportadaException(Guid PresupuestoId, IReadOnlyList<Guid> RubrosConMonedaDistinta, Moneda MonedaBase)`.
- [x] **`SnapshotTasas` en el evento** está bien diseñado: `IReadOnlyDictionary<Moneda, decimal>`, vacío en MVP. Cuando exista `TasaDeCambio` (slice 06 / followup #24), se popula sin cambio de schema.

### 2.4 Cobertura

Por inspección de `AprobarPresupuesto`:

1. `ArgumentNullException.ThrowIfNull(cmd)` — cmd nulo: no cubierto (defensivo, mismo patrón que slice 04).
2. `Estado != Borrador` (true/false) — `RequerirBorrador()` lanza:
   - "true" → §6.7.
   - "false" → todos los happy.
3. Identificación de terminales con monto > 0 (lista vacía/no vacía):
   - vacía + presupuesto sin rubros → §6.4.
   - vacía + rubros pero todos en cero → §6.5.
   - no vacía → happy paths.
4. Filtrar rubros con `Moneda != MonedaBase`:
   - lista no vacía → §6.6.
   - lista vacía → happy paths.
5. Cómputo de `MontoTotal` con `Aggregate(+)` — cubierto por §6.1, §6.2, §6.11.
6. `IsNullOrWhiteSpace(AprobadoPor)`:
   - "true" → §6.3.
   - "false" → otros happy.

**6 de 7 ramas cubiertas** = ~86 %, sobre el umbral 85 %. La única no cubierta (cmd nulo) es defensiva consistente con el resto del agregado.

`Apply(PresupuestoAprobado)` — sin ramas, mutación directa. Cubierta por §6.10 + todos los §6.X que reconstruyen presupuestos aprobados (§6.7/§6.8/§6.9).

`RequerirBorrador()` — 1 rama:
- "true" (lanza) → §6.7 + §6.8 + §6.9.
- "false" → todos los happy.

**Cobertura del slice + cierre retroactivo de #13: ~95 %**, supera holgadamente el umbral.

### 2.5 Refactor

- [x] `refactor-notes.md` presente — extracción de `RequerirBorrador()`, descarte fundamentado de 5 candidatos.
- [x] Tests no se tocaron en refactor — los 3 call-sites mutaron, los tests no.
- [x] Cero warnings (`TreatWarningsAsErrors=true`). `dotnet build`: 0 advertencias / 0 errores.

### 2.6 Invariantes cross-slice

- [x] `dotnet test` completo: 142/142 dominio + 20/20 integración. **Sin regresiones**.
- [x] Slice 03 y 04 siguen verdes con `RequerirBorrador()` aplicado a sus call-sites — la lógica observable es idéntica.

### 2.7 Coherencia con decisiones previas

- [x] Alineado con **`01-event-storming-mvp.md` §3.2 y §4** — máquina de estados y precondiciones de `AprobarPresupuesto`.
- [x] Alineado con **`02-decisiones-hotspots-mvp.md` §1 (Agrupador no tiene monto directo) y §2 (multimoneda + SnapshotTasas + INV-14)**. La "PRE-3 multimoneda no soportada" es una decisión pragmática del MVP — se levanta cuando exista TasaDeCambio (followup #24).
- [x] Q1 (atajo `Aprobado → Cerrado`) resuelta por modeler con (a) — sin atajo, alineado con event-storming §3.2. Followup #26 abierto si negocio lo demanda.
- [x] Q2 (validar `PeriodoFin >= ahora.Date`) resuelta por usuario con (a) — no validar, regla pertenece a `ActivarPresupuesto`.
- [x] Patrón consistente con slices 01–04: factory/comando como método de instancia, payload con `Dinero`, normalización de `AprobadoPor`, excepciones heredan de `DominioException`.

---

## 3. Hallazgos

| # | Tipo | Descripción | Acción |
|---|---|---|---|
| 1 | followup (cerrado) | #13 (escenario INV-3 lanza) — cubierto retroactivamente por §6.8 (AgregarRubro) y §6.9 (AsignarMontoARubro) más §6.7 (AprobarPresupuesto sobre ya-aprobado). | Mover a Cerrados en FOLLOWUPS.md. |
| 2 | followup (refinado) | #20 (TasaASnapshot) — el evento ya lleva `SnapshotTasas` (vacío). Refinar a: "popular `SnapshotTasas` en `AprobarPresupuesto` cuando exista TasaDeCambio". | Editar texto en FOLLOWUPS.md. |
| 3 | followup (nuevo) | **#24** — Habilitar multimoneda en `AprobarPresupuesto`: handler consulta proyección `TasasDeCambioVigentes`, construye `SnapshotTasas`, lanza si falta alguna tasa. Disparador: slice `TasaDeCambioRegistrada`. | Abrir #24. |
| 4 | followup (nuevo) | **#25** — Proyección `PresupuestoBaselineEnMonedaBase` que materializa los totales por rubro convertidos al `MonedaBase` usando `SnapshotTasas`. Disparador: cuando exista `SnapshotTasas` poblado (post #24). | Abrir #25. |
| 5 | followup (nuevo) | **#26** — Comando opcional `RevertirAprobacion` post-MVP. Disparador: PO confirma necesidad real. Origen: spec §10 Q1 resuelta por modeler con (a). | Abrir #26. |
| 6 | deuda heredada | `PresupuestoReadModel` sigue sin `Estado`/`MontoTotal`/`AprobadoEn`/`AprobadoPor`/`SnapshotTasas`. Slice 05 los expone en el agregado pero la proyección no los refleja. | Abordar en `infra-wire` del slice 05. |
| 7 | deuda heredada | `DomainExceptionHandler.Mapear` necesita 2 mapeos nuevos: `PresupuestoSinMontosException → 400`, `AprobacionConMultimonedaNoSoportadaException → 400`. | Abordar en `infra-wire` del slice 05. |

---

## 4. Veredicto final

- [ ] **approved**
- [x] **approved-with-followups** — 1 followup cerrado (#13), 1 refinado (#20), 3 nuevos (#24, #25, #26), 2 deudas heredadas a abordar en infra-wire. Sin blockers. Definition of Done satisfecho excepto las deudas marcadas (proyección + mapeo HTTP).
- [ ] **request-changes**

**Detalles:**

El slice 05 cierra elegantemente el ciclo de vida más crítico del MVP: la transición a `Aprobado`. El cierre retroactivo de #13 es la primera vez que las invariantes "estado != Borrador" se ejercitan completas, y `RequerirBorrador()` consolida el patrón en un solo helper privado.

La diferencia frente a slice 04 en términos de cobertura es notable: slice 04 dejaba INV-3 "lanza" sin cubrir (justificado por #13). Slice 05 sube la cobertura del slice de ~89 % (slice 04) a ~95 % (slice 05) cubriendo esa rama por primera vez.

Las deudas de proyección + mapeo HTTP siguen el patrón establecido (slice 03 / slice 04): se cierran en fase `infra-wire`.

**Orquestador puede proceder a:** actualizar FOLLOWUPS.md con #13/#20/#24/#25/#26, commit `feat(slice-05): aprobar-presupuesto`, push, y luego fase `infra-wire`.
