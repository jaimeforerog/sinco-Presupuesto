# Review notes — Slice 01 — CrearPresupuesto

**Autor:** reviewer (retroactivo, dos pasadas)
**Fecha:** 2026-04-24 (revisión 2)
**Slice auditado:** `slices/01-crear-presupuesto/`.
**Veredicto:** `approved-with-followups`.

**Nota de retroactividad:** este slice no siguió el ciclo TDD estricto. La spec, los tests en forma Given/When/Then y las notas de red/green/refactor se produjeron **después** del código. En consecuencia, partes estándar de la auditoría (estado rojo verificable, disciplina "mínimo código para pasar rojo") no se pueden confirmar. El veredicto refleja que el resultado final es correcto, alineado con la spec, y sirve como referencia — pero queda explícito que el slice 01 **no** es ejemplo de TDD ortodoxo.

**Segunda pasada:** tras review inicial, se detectaron asserts frágiles (acoplados a mensajes de excepción), cobertura redundante y falta de Given/When/Then visible en `DineroTests`. Los fixes se aplicaron en la segunda pasada (ver `refactor-notes.md` §"Cambios aplicados — pasada 2"). El veredicto se mantiene `approved-with-followups` con los hallazgos actualizados.

---

## 1. Resumen ejecutivo

El slice implementa `CrearPresupuesto → PresupuestoCreado` con el agregado `Presupuesto`, los value objects `Dinero` y `Moneda`, y una proyección inline `PresupuestoReadModel`. Los ocho escenarios de `spec.md §6` tienen tests Given/When/Then correspondientes. La implementación respeta invariantes, usa `Dinero`/`Moneda`, inyecta `TimeProvider`, y se apoya en records inmutables. Hay tres follow-ups menores que no bloquean el cierre.

## 2. Checklist de auditoría

### 2.1 Spec ↔ tests

- [x] Cada escenario de `spec.md §6.1–6.8` tiene un test en `Slice01_CrearPresupuestoTests.cs`.
- [x] PRE-1..PRE-5 tienen test de violación (§6.3–§6.7).
- [x] INV-7 (MonedaBase fijada tras creación) cubierta por test complementario `Fold_de_PresupuestoCreado_deja_el_agregado_en_Borrador_con_MonedaBase_fijada`.
- [x] Nombres de tests son frases descriptivas en español.

### 2.2 Tests como documentación

- [x] Given/When/Then visibles en cada test.
- [x] Cero mocks del dominio.
- [x] Valores en `Given` y `When` son realistas (un presupuesto de obra en COP con periodo fiscal anual).

### 2.3 Implementación

- [x] Miembros públicos nuevos están ejercidos por tests (verificación manual; cobertura automática pendiente — ver §3 follow-up 1).
- [x] Sin `DateTime.UtcNow` en dominio (se recibe por parámetro).
- [x] Sin `Guid.NewGuid()` en dominio (se recibe por parámetro; solo en handler).
- [x] `Dinero`/`Moneda` usados correctamente. `decimal` solo dentro de `Dinero.Valor`.
- [x] Records inmutables para eventos y comandos. Sin setters públicos.

### 2.4 Cobertura

- [ ] **Follow-up 1**: cobertura de ramas del agregado `Presupuesto` **no reportada automáticamente** (no hay pipeline ni `coverlet` configurado). Agregar en follow-up dedicado a observabilidad del proceso.
- Verificación manual: el único método con ramas (`Create`) tiene ramas ejercidas por los tests §6.3–§6.8.

### 2.5 Refactor

- [x] `refactor-notes.md` presente con cero cambios y justificación por cada refactor descartado.
- [x] Tests no cambiaron entre green y refactor (aplicable trivialmente: no hubo green ni refactor en su momento).
- [x] Esperado cero warnings — pendiente confirmar con `dotnet build` local del usuario.

### 2.6 Invariantes cross-slice

- No aplica: este es el primer slice.

### 2.7 Coherencia con decisiones previas

- [x] Alineado con `01-event-storming-mvp.md` (comando, evento, invariantes base).
- [x] Alineado con `02-decisiones-hotspots-mvp.md` §2 (multimoneda con `MonedaBase` inmutable).
- [x] Alineado con memoria `project_multimoneda.md`.
- [x] Alineado con memoria `project_stack_decision.md`.
- [x] Alineado con memoria `project_metodologia.md` (con la salvedad retroactiva explícita).

## 3. Hallazgos

### Pasada 2 — resueltos en esta pasada

| # | Estado | Descripción | Resolución |
|---|---|---|---|
| P2-1 | resuelto | Asserts acoplados al texto del mensaje de excepción (`WithMessage("*TenantId*")`). | Introducida jerarquía `DominioException`; tests verifican tipo + propiedades. |
| P2-2 | resuelto | `evento.Should().BeOfType<PresupuestoCreado>()` redundante en happy path. | Eliminada. |
| P2-3 | resuelto | `DineroTests` sin Given/When/Then visible. | Añadidos comentarios explícitos y `using` de TestKit. |
| P2-4 | resuelto | `Suma_entre_monedas_distintas_lanza` no verificaba `Izquierda`/`Derecha`. | Asserta ambas propiedades. |
| P2-5 | resuelto | Test de normalización de `Moneda` no cubría el caso identidad. | Añadido `"USD" → "USD"` al theory. |

### Pasada 1 — followups originales (siguen vigentes)

| # | Tipo | Descripción | Ubicación | Acción sugerida |
|---|---|---|---|---|
| 1 | followup | Cobertura de ramas no reportada automáticamente. | pipeline | Agregar `coverlet.collector` a `SincoPresupuesto.Domain.Tests.csproj` y un step `dotnet test --collect:"XPlat Code Coverage"` al pipeline CI. |
| 2 | followup | Unicidad `(TenantId, Codigo, Periodo)` diferida (spec §4 PRE-6). | dominio + proyección | Slice dedicado `PresupuestoCodigoIndex` con `UniqueIndex` compuesto de Marten. |
| 3 | followup | `ConfiguracionTenant` es prerequisito de `CrearPresupuesto`. | dominio + handler | Slice `ConfiguracionTenant.CrearTenant` + validación cruzada. |
| 4 | nit | Sin dotnet SDK en el sandbox, ni `dotnet build` ni `dotnet test` se corrieron. | proceso | Usuario debe ejecutarlos localmente antes del commit. Si falla, el slice vuelve a review. |

### Pasada 2 — followups nuevos (diferidos)

| # | Tipo | Descripción | Ubicación | Acción sugerida |
|---|---|---|---|---|
| 5 | followup | `DineroTests.cs` vive en raíz de tests, no en un slice. | tests | Slice retroactivo `00-shared-kernel` con spec + notas, mover tests a `Slices/Slice00_SharedKernelTests.cs`. |
| 6 | followup | Slice 01 usa `Presupuesto.Create → PresupuestoCreado`; slices futuros pueden querer firma uniforme `CasoDeUso.Decidir(dados, cmd, ...) → IReadOnlyList<object>`. | Application / Domain | Evaluar en slice 02 y aplicar retroactivamente si el patrón gana tracción. |
| 7 | followup | Operadores `-`, `*`, `<`, `>`, `<=`, `>=` y helpers `Cero`/`EsCero`/`EsPositivo`/`EsNegativo` de `Dinero` sin tests. Método `En` sin tests para `factor ≤ 0`. | tests SharedKernel | Cerrar al adoptar slice `00-shared-kernel` (FOLLOWUPS #5). |

## 4. Veredicto final

- [ ] **approved** — no, hay follow-ups explícitos.
- [x] **approved-with-followups** — follow-ups 1, 2, 3 van a `FOLLOWUPS.md`. Nit 4 queda como pendiente operativo.
- [ ] **request-changes** — no aplica.

---

El orquestador puede proceder al commit del slice como `feat(slice-01): CrearPresupuesto` una vez el usuario confirme que `dotnet build` y `dotnet test` están en verde en local. Los follow-ups se atienden en slices posteriores según prioridad del Product Owner.
