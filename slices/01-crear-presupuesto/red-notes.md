# Red notes — Slice 01 — CrearPresupuesto

**Autor:** red (retroactivo)
**Fecha:** 2026-04-24
**Spec consumida:** `slices/01-crear-presupuesto/spec.md` (firmado 2026-04-24).

**Nota de retroactividad:** este slice se implementó antes de acordar la metodología. Los tests en forma Given/When/Then se escribieron **después** del código de producción (red post-hoc) para alinear el slice a la metodología antes de seguir con slice 02. El ciclo TDD estricto aplica a partir del slice 02.

---

## 1. Tests escritos

Archivo: `tests/SincoPresupuesto.Domain.Tests/Slices/Slice01_CrearPresupuestoTests.cs`.

| Test | Escenario spec §6 | Tipo |
|---|---|---|
| `CrearPresupuesto_sobre_stream_vacio_emite_PresupuestoCreado_con_todos_los_campos` | 6.1 happy path | Fact |
| `CrearPresupuesto_con_CreadoPor_vacio_usa_sistema_como_default` | 6.2 | Theory (2 casos) |
| `CrearPresupuesto_con_TenantId_vacio_lanza_ArgumentException` | 6.3 PRE-1 | Theory (2 casos) |
| `CrearPresupuesto_con_Codigo_vacio_lanza_ArgumentException` | 6.4 PRE-2 | Theory (2 casos) |
| `CrearPresupuesto_con_Nombre_vacio_lanza_ArgumentException` | 6.5 PRE-3 | Theory (2 casos) |
| `CrearPresupuesto_con_PeriodoFin_anterior_a_PeriodoInicio_lanza_ArgumentException` | 6.6 PRE-4 | Fact |
| `CrearPresupuesto_con_ProfundidadMaxima_fuera_de_rango_lanza_ArgumentOutOfRangeException` | 6.7 PRE-5 | Theory (4 casos) |
| `CrearPresupuesto_con_Codigo_y_Nombre_con_espacios_emite_evento_con_trim_aplicado` | 6.8 | Fact |
| `Fold_de_PresupuestoCreado_deja_el_agregado_en_Borrador_con_MonedaBase_fijada` | — complementario (INV-7) | Fact |

Archivo eliminado: `tests/SincoPresupuesto.Domain.Tests/PresupuestoTests.cs` (superseded por el slice).

Helper agregado: `tests/SincoPresupuesto.Domain.Tests/TestKit/PresupuestoBehavior.cs` con `Reconstruir(params object[] historial)` para fold de eventos previos en tests futuros (Given != vacío).

## 2. Verificación de estado rojo

**Retroactivo — no aplica.** Los tests se escribieron contra código de producción ya existente; todos pasan al momento de crearlos. Esto rompe la regla de TDD estricto "un test rojo antes de cualquier código".

**Mitigación:** en la siguiente sesión se correrá cobertura de ramas y el reviewer auditará que los tests **sí ejercen** el código — si un test verde no ejerce su rama, es equivalente a no haberlo escrito.

A partir del slice 02 se exige la secuencia ortodoxa: spec firmada → red (tests fallan) → green → refactor → review.

## 3. Código de producción tocado

- [x] Sin cambios en `src/`. El código del agregado `Presupuesto`, el comando, el evento y los value objects ya existían del scaffold inicial.
- [ ] Agregadas firmas/stubs mínimas — no aplica.

## 4. Desviaciones respecto a la spec

- **§6.2 (CreadoPor vacío → "sistema")**: la implementación ya maneja este caso vía `string.IsNullOrWhiteSpace(cmd.CreadoPor) ? "sistema" : cmd.CreadoPor`. Alineado con la spec.
- **§6.8 (normalización de espacios)**: la implementación ya aplica `.Trim()` a `Codigo` y `Nombre`. Alineado.
- Sin otras desviaciones.

## 5. Hand-off a green

- Spec firmada: **sí**.
- Todos los tests en estado conocido: **sí**, todos pasan (retroactivo).
- Sin cambios de comportamiento accidentales: **sí**, el código no se tocó.
