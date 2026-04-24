# Refactor notes — Slice 01 — CrearPresupuesto

**Autor:** refactorer (retroactivo + fixes post-review)
**Fecha:** 2026-04-24 (segunda pasada)

**Nota de retroactividad:** el código de este slice se escribió sin pasar por un ciclo explícito red→green. La primera pasada de review identificó debilidades en las aserciones de los tests (acopladas a mensajes de excepción, no a tipos). Esta segunda pasada aplica los fixes acordados.

---

## Cambios aplicados (pasada 2 — post-review)

| # | Tipo | Archivo(s) | Descripción |
|---|---|---|---|
| 1 | feature | `src/SincoPresupuesto.Domain/SharedKernel/DominioException.cs` (nuevo) | Jerarquía de excepciones de dominio: `DominioException` (abstract base) + `CampoRequeridoException(NombreCampo)` + `PeriodoInvalidoException(PeriodoInicio, PeriodoFin)` + `ProfundidadMaximaFueraDeRangoException(Valor, MinimoInclusivo, MaximoInclusivo)`. |
| 2 | refactor | `src/SincoPresupuesto.Domain/Presupuestos/Presupuesto.cs` | `Create` deja de lanzar `ArgumentException`/`ArgumentOutOfRangeException` y lanza las nuevas excepciones de dominio. Sin cambio de comportamiento observable más allá del tipo de excepción. |
| 3 | test | `tests/.../Slices/Slice01_CrearPresupuestoTests.cs` | Asserts cambian de `WithMessage("*...")` a `Throw<Tipo>().Which.Propiedad`. Happy path pierde `BeOfType<PresupuestoCreado>()` redundante. Agregado `using SincoPresupuesto.Domain.Tests.TestKit`. §6.4 asserta `NombreCampo = "Codigo"`, §6.5 `= "Nombre"`, §6.6 PeriodoInicio/Fin, §6.7 Valor/Min/Max. |
| 4 | test | `tests/.../DineroTests.cs` | Añadidos `// Given / When / Then` explícitos en cada test. Agregado caso "USD"→"USD" al theory de normalización. `Suma_entre_monedas_distintas` ahora asserta `Izquierda`/`Derecha` de la excepción. Renombrado para reflejar el tipo de excepción en el nombre. |
| 5 | spec | `slices/01-crear-presupuesto/spec.md` | §4 y §6.3-6.7 actualizados con los nuevos tipos de excepción y sus propiedades observables. |

## Cambios aplicados (pasada 1 — scaffold)

| # | Tipo | Archivo | Descripción |
|---|---|---|---|
| 1 | fix | `src/SincoPresupuesto.Api/Program.cs` | Corregido namespace del enum de tenancy con alias `MartenTenancyStyle`. |

## Refactors descartados

| # | Sugerido por | Motivo para no aplicar |
|---|---|---|
| 1 | green-notes §2.1 (primary constructor de `MonedasDistintasException`) | Estilo C# 12 idiomático; mantener. |
| 2 | green-notes §2.2 (helper de validación `AssertRequerido`) | Tras introducir `CampoRequeridoException`, los tres checks iguales en `Presupuesto.Create` podrían volverse un helper `RequireCampo(cmd.TenantId, nameof(cmd.TenantId))`. Descartado por ahora: tres usos es el borderline; si slice 02+ agrega más campos requeridos, se extrae. |
| 3 | green-notes §2.3 (helper de trim) | Dos usos no justifican abstracción. |
| 4 | green-notes §2.4 (atajos de `Moneda`) | Útiles en tests y aceptables en código. |
| 5 | green-notes §2.5 (`Apply` público vs internal) | Marten funciona con `public`; Followup. |
| 6 | review §4 (cambiar a firma `Decidir(events, cmd) → IReadOnlyList<object>`) | Descartado **por ahora**: slice 01 puede seguir con `Create` como factory especializada porque el "Given vacío" es un caso real. Para slice 02 (`AgregarRubro`) se evalúa introducir `Decidir` como forma canónica a nivel de agregado. |

## Cero cambios de comportamiento observable

- El agregado expone la misma superficie; lo único que cambia es el **tipo** de excepción lanzado en los caminos de error.
- Ningún test válido del pasado (happy path o fold) se rompe con los cambios.
- Warnings esperados: cero tras la compilación local.

---

## Deuda técnica registrada como follow-up

- FOLLOWUPS.md #1 (cobertura automática)
- FOLLOWUPS.md #2 (unicidad por tenant/código/periodo)
- FOLLOWUPS.md #3 (ConfiguracionTenant como prerequisito)
- FOLLOWUPS.md #4 (nuevo — slice retroactivo `00-shared-kernel` para adoptar DineroTests)
- FOLLOWUPS.md #5 (nuevo — evaluar firma `Decidir` uniforme en slice 02)
- FOLLOWUPS.md #6 (nuevo — ampliar tests de Dinero: operadores `-`, `*`, comparadores, helpers `Cero`/`EsCero`/`EsPositivo`/`EsNegativo`, y `En` con factor ≤ 0)
