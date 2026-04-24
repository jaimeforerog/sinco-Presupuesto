# Review notes — Slice {N} — {NombreComando}

**Autor:** reviewer
**Fecha:** YYYY-MM-DD
**Slice auditado:** `slices/{N}-{slug}/`.
**Veredicto:** `approved` | `approved-with-followups` | `request-changes`

---

## 1. Resumen ejecutivo

Dos o tres frases con el estado del slice y el veredicto. Si es `request-changes`, decir explícitamente a quién se devuelve (red / green / refactorer) y por qué.

## 2. Checklist de auditoría

### 2.1 Spec ↔ tests

- [ ] Cada escenario de `spec.md §6` tiene un test correspondiente.
- [ ] Cada precondición tiene un test que la viola.
- [ ] Cada invariante tocada tiene un test que la viola.
- [ ] Los nombres de los tests son frases completas que describen el comportamiento (no `Test1`, no `ShouldWork`).

### 2.2 Tests como documentación

- [ ] Un lector que no conoce el código puede entender el comportamiento leyendo solo los tests.
- [ ] Given/When/Then está claro visualmente en cada test (comentarios o estructura).
- [ ] Sin mocks del dominio.

### 2.3 Implementación

- [ ] El código de producción añadido es mínimo (no hay métodos/propiedades no ejercidos por los tests).
- [ ] No hay `DateTime.UtcNow`, `Guid.NewGuid()` u otras fuentes de no-determinismo dentro del dominio.
- [ ] Los eventos son `record` inmutables.
- [ ] `Dinero`/`Moneda` se usan para montos; nunca `decimal` pelado.

### 2.4 Cobertura

- [ ] Cobertura de ramas del agregado ≥ **85 %**. Actual: **XX %**.
- [ ] Ramas descubiertas están justificadas en `refactor-notes.md` o anotadas como deuda.

### 2.5 Refactor

- [ ] `refactor-notes.md` presente y claro, aunque sea "sin cambios".
- [ ] Los tests no se tocaron en la fase refactor (salvo renombrar).
- [ ] Sin warnings de compilación.

### 2.6 Invariantes cross-slice

- [ ] El slice no rompe invariantes de slices previos (verificación: `dotnet test` completo en verde, no solo el filtro del slice).

### 2.7 Coherencia con decisiones previas

- [ ] Alineado con `01-event-storming-mvp.md`.
- [ ] Alineado con `02-decisiones-hotspots-mvp.md`.
- [ ] Alineado con memoria del proyecto (multimoneda, stack).

## 3. Hallazgos

Numerar cada hallazgo, clasificar en:

- **blocker**: obliga a `request-changes`.
- **followup**: permite `approved-with-followups`, se mueve a `FOLLOWUPS.md`.
- **nit**: comentario menor, no bloquea ni genera followup.

| # | Tipo | Descripción | Ubicación | Acción sugerida |
|---|---|---|---|---|
| 1 | blocker / followup / nit | … | archivo:línea | … |

## 4. Veredicto final

- [ ] **approved** — sin hallazgos, o solo nits asumidos.
- [ ] **approved-with-followups** — followups registrados en `FOLLOWUPS.md`.
- [ ] **request-changes** — se devuelve a **{red | green | refactorer}** con los blockers detallados.

---

_Cuando el veredicto es `approved` o `approved-with-followups`, el orquestador puede proceder al commit del slice y a la fase de infra-wire._
