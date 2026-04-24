# Refactor notes — Slice 03 — AgregarRubro

**Autor:** refactorer
**Fecha:** 2026-04-24
**Estado de entrada:** green cerrado, 58/58 verdes, 0 warnings.
**Estado de salida:** 58/58 verdes, 0 warnings.

---

## Cambios aplicados

| # | Tipo | Archivo | Descripción | Tests antes | Tests después |
|---|---|---|---|---|---|
| 1 | feature (helper) | `src/SincoPresupuesto.Domain/SharedKernel/Requerir.cs` (nuevo) | Introduce helper estático `Requerir.Campo(string? valor, string nombreCampo)` que centraliza el patrón `if IsNullOrWhiteSpace(valor) throw new CampoRequeridoException(nombre)`. Devuelve el valor para permitir chain con `.Trim()`. Cierra followup #10 (disparador "tercer uso" sobradamente superado: 6 ocurrencias tras slice 03). | 58 pass | 58 pass |
| 2 | DRY / inline → helper | `src/SincoPresupuesto.Domain/Presupuestos/Presupuesto.cs` — método `Create` | Las tres comprobaciones `IsNullOrWhiteSpace` de `TenantId`, `Codigo`, `Nombre` → `Requerir.Campo(…, nameof(…))`. Sin cambio de comportamiento: el helper lanza exactamente la misma excepción con el mismo `NombreCampo`. | 58 pass | 58 pass |
| 3 | DRY / inline → helper | `src/SincoPresupuesto.Domain/Presupuestos/Presupuesto.cs` — método `AgregarRubro` | Las dos comprobaciones `IsNullOrWhiteSpace` de `cmd.Codigo` y `cmd.Nombre` → `Requerir.Campo(…, "…")`. El chequeo `rubroId == Guid.Empty` **no** se refactoriza (tipo `Guid`, no `string`; una sobrecarga para `Guid` sería abstracción especulativa con un solo uso). | 58 pass | 58 pass |
| 4 | DRY / inline → helper | `src/SincoPresupuesto.Domain/ConfiguracionesTenant/ConfiguracionTenant.cs` — método `Create` | La comprobación `IsNullOrWhiteSpace(cmd.TenantId)` → `Requerir.Campo(cmd.TenantId, "TenantId")`. Cero cambios a la lógica de normalización (`Trim`) posterior. | 58 pass | 58 pass |
| 5 | extract method | `src/SincoPresupuesto.Domain/Presupuestos/Presupuesto.cs` — método `AgregarRubro` | Extrae la rama `if padre null/not null` que selecciona entre INV-F (hijo extiende al padre) e INV-10 (formato canónico) a un método privado `ValidarFormatoDelCodigo(Rubro? padre, string codigo)`. Reduce 20 líneas a una sola llamada en el flujo principal, manteniendo la linealidad de lectura. La decisión de invariante (INV-F vs. INV-10) queda encapsulada con su XML doc. | 58 pass | 58 pass |

## Refactors descartados

| # | Sugerido por | Motivo para no aplicar |
|---|---|---|
| 1 | green-notes §3.3 — proyección `PresupuestoReadModel.Rubros` | No es un refactor (cero cambio de comportamiento). Es **feature nueva**: spec §8 pide que la proyección gane una colección de `Rubros`, pero ningún test de dominio lo ejerce. Ámbito correcto: fase `infra-wire` posterior al review, o slice dedicado de proyección/integración. Registrado como deuda consciente en green-notes §3.3. |
| 2 | green-notes §2 — INV-3 rama "lanza" | No es un refactor de remoción. La spec §5 exige la rama presente y el followup #13 cubre el test retroactivo cuando exista `AprobarPresupuesto`. Remover la rama ahora rompería el aislamiento por slice (se tendría que reintroducir luego). Se mantiene el código actual con su comentario explicativo. |
| 3 | green-notes §3.4 — `Rubro` como `record` vs. `class` | Candidato "ligero". Motivo de descarte: consistencia con `Presupuesto` (también `class sealed`) y ausencia de lógica de value-equality que un `record` agregaría. Cambio estético sin beneficio observable, violaría el principio "refactor solo cuando hay DRY real o claridad que mejora". Registrado como gusto del equipo si se quiere homogeneizar más adelante. |
| 4 | green-notes §3.5 — `.First(padre)` en `Apply(RubroAgregado)` | Green justifica la elección (`First` en lugar de `FirstOrDefault`) como contrato con el invariante del stream: si el padre no está, es un bug de integridad y debe explotar. Un helper `NivelDe(padreId)` defensivo no añade valor sin un caso concreto de corrupción documentado. Sin cambio. |
| 5 | green-notes §4.1 — INV-F con manipulación de strings vs. regex | La implementación char-a-char es más barata que compilar/cachear una regex por padre, y la green-notes documenta la decisión con cuatro condiciones nombradas explícitas. La extracción a `ValidarFormatoDelCodigo` (refactor #5 arriba) mejora la lectura sin cambiar el mecanismo. Cambiar a regex introduciría una micro-asignación por invocación sin ganancia de claridad. Sin cambio. |
| 6 | green-notes §4.2 — INV-10 redundante para hijos | Mantener INV-10 sólo para raíces es correcto por DRY (INV-F implica INV-10 para hijos por construcción). Validar INV-10 siempre sería código muerto cubierto en dos lugares. Sin cambio. |
| 7 | green-notes §4.3 — unicidad O(n) en `_rubros.Any(...)` | Preocupación de escalabilidad (HashSet auxiliar). Para MVP con decenas de rubros por presupuesto es negligible, y un HashSet duplicaría estado con riesgo de desync con `_rubros`. Sin test que ejerza el tamaño, la optimización es especulativa. Sin cambio. |
| 8 | green-notes §3.1 — orden de validación / helper `ValidarCodigoDelHijo` | Aplicado en forma reducida: refactor #5 (tabla de arriba) extrae `ValidarFormatoDelCodigo(padre, codigo)` que abarca ambas ramas (con/sin padre). El nombre sugerido por green (`ValidarCodigoDelHijo`) se descarta porque el método también cubre la rama sin padre (INV-10); `ValidarFormatoDelCodigo` es más preciso. El reordenamiento de INV-10 antes de las reglas con padre **no** se aplica (rompería el contrato del test §6.10 `"011.01"` que exige precedencia de `CodigoHijoNoExtiendeAlPadreException`). |
| 9 | seguimiento FOLLOWUPS #5 — firma uniforme `Decidir(dados, cmd, ...)` | Recomendación del modeler (spec §13): mantener la forma OO actual y cerrar #5 como "no aplicable". Es decisión del reviewer final, no del refactorer; el refactor de este slice no debe forzarla. Sin cambio en este slice. |

## Cero cambios de comportamiento observable

- Ninguna firma pública cambió. `Presupuesto.Create`, `Presupuesto.AgregarRubro`, `ConfiguracionTenant.Create` siguen con su misma signatura, mismos parámetros, mismo tipo de retorno.
- Los tipos y propiedades de excepción lanzadas son idénticos. `Requerir.Campo` lanza exactamente `CampoRequeridoException(nombreCampo)` — el helper es una pura extracción del cuerpo del `if/throw` original.
- Los métodos `Apply(...)` no se tocaron.
- Ningún test se modificó.
- Orden de validaciones preservado en todos los sitios: `Requerir.Campo` conserva el orden `TenantId → Codigo → Nombre` en `Create`, y `Codigo → Nombre → RubroId → Estado → padre → formato → nivel → unicidad` en `AgregarRubro`.

## Impacto en SharedKernel

Archivo nuevo: `src/SincoPresupuesto.Domain/SharedKernel/Requerir.cs` con una única función estática `Campo`. No hereda de nada, no rompe la jerarquía `DominioException`. Las excepciones lanzadas (vía `CampoRequeridoException`) quedan donde ya estaban.

## Acciones en FOLLOWUPS.md

- **#10 cerrado**: movido de "Abiertos" a "Cerrados" con nota "cerrado en slice 03 refactor — helper `SharedKernel.Requerir.Campo` introducido (ver `slices/03-agregar-rubro/refactor-notes.md`)". Desglose explícito de las 6 sustituciones (3 en `Presupuesto.Create`, 2 en `Presupuesto.AgregarRubro`, 1 en `ConfiguracionTenant.Create`) y justificación de por qué el chequeo `rubroId == Guid.Empty` queda inline (tipo `Guid`, no `string`).

## Verificación

```bash
dotnet build                → 0 errors, 0 warnings.
dotnet test (suite completa) → 58/58 verdes (Slice01: 16, Slice02: 7, Slice03: 23, DineroTests: 12).
```

Cada uno de los 5 refactors aplicados se verificó de forma incremental con `dotnet test` antes de pasar al siguiente. Ningún intermedio rompió verde.

## Resumen ejecutivo

- **5 refactors aplicados**: un helper nuevo (`Requerir.Campo`) + cuatro sustituciones DRY en 3 archivos + una extracción de método privado en `AgregarRubro`.
- **9 impulsos evaluados y descartados** con razón técnica (spec, green-notes, followups).
- **Cero cambios de comportamiento observable**; cero warnings; 58/58 verdes.
- **Followup #10 cerrado**; trigger "tercer uso" superado con creces (7 usos totales del patrón, 6 refactorizables — el séptimo, `Guid.Empty`, queda inline).
