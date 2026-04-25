# Review notes — Slice 06 — RegistrarTasaDeCambio

**Reviewer:** orquestador (siguiendo `templates/agent-personas/reviewer.md`).
**Fecha:** 2026-04-24
**Veredicto:** `approved-with-followups`

---

## 1. Resumen ejecutivo

Slice 06 introduce el agregado **`CatalogoDeTasas`** (singleton por tenant, patrón análogo a `ConfiguracionTenant` del slice 02) que materializa el catálogo de tasas FX. 17 casos xUnit verdes, sin regresiones (159/159 dominio + 24/24 integración = 183/183). El slice queda enfocado en la **registración**: la integración con `AprobarPresupuesto` para popular `SnapshotTasas` y levantar la PRE-3 multimoneda no soportada permanece como **followup #24** (slice 06b candidato), tal como decidió el modeler.

---

## 2. Checklist de auditoría

### 2.1 Spec ↔ tests

- [x] Cada escenario §6 mapea a un test:
  - §6.1 happy stream vacío.
  - §6.2 acumulación.
  - §6.3 last-write-wins (re-registración misma tupla).
  - §6.4 normalización `RegistradoPor` (Theory 3 cases).
  - §6.5 normalización `Fuente` (Theory 3 cases).
  - §6.6 PRE-1 stream vacío.
  - §6.7 PRE-2 tasa inválida (Theory 3 cases).
  - §6.8 PRE-3 fecha futura.
  - §6.9 caso límite Fecha == hoy.
  - §6.10 PRE-1 stream existente.
  - §6.11 fold con historial.
- [x] Cada precondición tiene test que la viola: PRE-1 (§6.6 + §6.10), PRE-2 (§6.7), PRE-3 (§6.8).
- [x] PRE-4 normalización tiene tests dedicados (§6.4 + §6.5).
- [x] INV-CT-1 last-write-wins ejercida (§6.3 — registrar misma tupla con tasa distinta NO lanza).

### 2.2 Tests como documentación

- [x] Given/When/Then visible en cada test.
- [x] Cero mocks del dominio.
- [x] Aserciones por tipo + propiedades.

### 2.3 Implementación

- [x] Código mínimo: agregado `CatalogoDeTasas` (~80 líneas), record `RegistroDeTasa` (vista de fold), 3 excepciones nuevas + record comando + record evento.
- [x] Sin `DateTime.UtcNow`, `Guid.NewGuid()` en dominio. `ahora` inyectado.
- [x] **`Tasa` es `decimal` (no `Dinero`)**. Correcto: tasa es ratio, no monto.
- [x] **`Fecha` es `DateOnly`**. Correcto: fecha calendario, no timestamp.
- [x] Records inmutables: `RegistrarTasaDeCambio`, `TasaDeCambioRegistrada`, `RegistroDeTasa`.
- [x] Excepciones nuevas siguen el patrón del kernel:
  - `MonedasIgualesEnTasaException(Moneda Moneda)`.
  - `TasaDeCambioInvalidaException(decimal TasaIntentada)`.
  - `FechaDeTasaEnElFuturoException(DateOnly Fecha, DateOnly Hoy)`.

### 2.4 Cobertura

Por inspección de `CatalogoDeTasas.ValidarYConstruir`:

1. `ArgumentNullException.ThrowIfNull(cmd)` — defensivo, mismo patrón que el resto.
2. `cmd.MonedaDesde == cmd.MonedaHacia` (true/false) → §6.6 + §6.10 / happy paths.
3. `cmd.Tasa <= 0m` (true/false) → §6.7 / happy paths.
4. `cmd.Fecha > hoy` (true/false) → §6.8 / §6.9 + happy paths.
5. `IsNullOrWhiteSpace(cmd.Fuente)` (true/false) → §6.5 / §6.1 (Fuente="BanRep").
6. `IsNullOrWhiteSpace(cmd.RegistradoPor)` (true/false) → §6.4 / §6.1.

`Crear` vs `Ejecutar` — ambos delegan al helper. `Crear` cubierto por §6.1 + violations sobre stream vacío. `Ejecutar` cubierto por §6.2/§6.3/§6.10 + acumulación.

`Apply` — sin ramas, mutación lineal. Cubierto por todos los §6.X que reconstruyen el agregado.

**Cobertura del slice: 100% de ramas** (excepto `ArgumentNullException.ThrowIfNull` defensivo).

### 2.5 Refactor

- [x] `refactor-notes.md` presente — cero cambios (green ya extrajo `ValidarYConstruir`); 5 candidatos descartados con justificación.
- [x] Tests no se tocaron en refactor.
- [x] Cero warnings (`TreatWarningsAsErrors=true`).

### 2.6 Invariantes cross-slice

- [x] `dotnet test` completo: 159/159 dominio + 24/24 integración. **Sin regresiones**.

### 2.7 Coherencia con decisiones previas

- [x] Alineado con **`02-decisiones-hotspots-mvp.md` §2**: catálogo de tasas como agregado/proyección separada, alimentado manualmente; eventos `TasaDeCambioRegistrada` + futuro `TasaDeCambioCorregida`.
- [x] Alineado con **`slices/02-configurar-moneda-local-del-tenant/spec.md`**: patrón singleton por tenant con stream-id bien-conocido.
- [x] Alineado con **`slices/05-aprobar-presupuesto/spec.md` §13** y **followup #24**: el slice 06 NO toca `AprobarPresupuesto`. La integración (popular `SnapshotTasas` + levantar PRE-3) queda en #24 refinado.
- [x] Q evaluadas internamente por modeler: re-registración (resuelta sí, INV-CT-1), fecha futura (resuelta no), indexación de proyección (resuelta solo por par), integración con AprobarPresupuesto (diferida). Todas con justificación trazable.

---

## 3. Hallazgos

| # | Tipo | Descripción | Acción |
|---|---|---|---|
| 1 | followup (refinado) | #24 — habilitar multimoneda real en `AprobarPresupuesto`: el catálogo ya existe; falta consumirlo desde el handler. | Mantener abierto, refinar texto en FOLLOWUPS.md. |
| 2 | followup (nuevo) | **#27** — `TasaDeCambioCorregida` como evento separado para distinguir "corrección" de "actualización". Hoy MVP usa last-write-wins de `TasaDeCambioRegistrada`. Disparador: PO solicita auditoría de cambios. | Abrir #27. |
| 3 | followup (nuevo) | **#28** — proyección `TasasDeCambioHistoricas` (lista completa por par) además de `TasasDeCambioVigentes` (solo última). Disparador: UI solicita ver historial / auditoría. | Abrir #28. |
| 4 | followup (nuevo) | **#29** — slice 06b candidato: integración `AprobarPresupuesto` ↔ `TasasDeCambioVigentes`. Cuando se aborde, populará `SnapshotTasas` y levantará la PRE-3 multimoneda no soportada del slice 05. Cierra #24 efectivamente. | Abrir #29. |
| 5 | deuda heredada | Proyección `TasasDeCambioVigentes` + endpoints HTTP (`POST` + `GET`) + 3 mapeos en `DomainExceptionHandler`. | Abordar en `infra-wire` del slice 06. |

---

## 4. Veredicto final

- [ ] **approved**
- [x] **approved-with-followups** — 1 followup refinado (#24), 3 nuevos (#27, #28, #29), 1 deuda heredada para infra-wire. Sin blockers.
- [ ] **request-changes**

**Detalles:**

Slice 06 cumple su scope de manera limpia: catálogo de tasas como agregado nuevo, sin tocar `AprobarPresupuesto`. La decisión de mantener el slice 06 enfocado y abrir #29 (slice 06b candidato) para la integración real es metodológicamente sana — preserva el ciclo TDD acotado por slice y permite firmar 06 sin acumular alcance.

**Orquestador puede proceder a:** actualizar FOLLOWUPS.md con #24/#27/#28/#29, commit `feat(slice-06): tasa-de-cambio-registrada`, push, luego fase `infra-wire`.
