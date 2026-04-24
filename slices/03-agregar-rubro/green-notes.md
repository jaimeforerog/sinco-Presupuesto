# Green notes — Slice 03 — AgregarRubro

**Implementador:** green
**Fecha:** 2026-04-24
**Estado:** implementación completa — 22 tests rojos → verdes, 1 sanidad sigue verde. Total 23/23 Slice03 y 58/58 suite completa.

---

## 1. Archivos creados / modificados

### Creados

1. **`src/SincoPresupuesto.Domain/Presupuestos/Rubro.cs`**
   - Entity interna del agregado `Presupuesto`.
   - Propiedades `{ get; init; }`: `Id (Guid)`, `Codigo (string)`, `Nombre (string)`, `PadreId (Guid?)`, `Nivel (int)`.
   - Clase inmutable: sólo se materializa desde el fold de `RubroAgregado`. Sin métodos.

### Modificados

2. **`src/SincoPresupuesto.Domain/Presupuestos/Presupuesto.cs`**
   - Añadidos:
     - Regex `CodigoRubroRegex` (static readonly, Compiled + CultureInvariant) con el patrón `^\d{2}(\.\d{2}){0,14}$` — INV-10.
     - Campo privado `List<Rubro> _rubros` + propiedad pública `IReadOnlyList<Rubro> Rubros => _rubros` para el test §6.13.
   - Implementación real de `AgregarRubro(cmd, rubroId, ahora)`:
     - PRE-1/PRE-2/PRE-3 con `CampoRequeridoException("Codigo"|"Nombre"|"RubroId")`.
     - PRE-4: `Trim` sobre `Codigo` y `Nombre`.
     - INV-3: si `Estado != Borrador` lanza `PresupuestoNoEsBorradorException(Estado)`. En slice 03 la rama "lanza" no tiene test porque no existe un comando que saque el presupuesto de Borrador — deuda cubierta por followup #13 (slice `AprobarPresupuesto`). La rama "no lanza" la ejercita el test de sanidad §6.7.
     - INV-D: si `cmd.RubroPadreId` no es null, busca por `Id` en `_rubros`; si no está, lanza `RubroPadreNoExisteException(padreId)`.
     - INV-F: sólo cuando hay padre. Exige `codigo.Length == padre.Codigo.Length + 3`, `codigo.StartsWith(padre.Codigo + ".")`, y que los últimos dos caracteres sean dígitos. Lanza `CodigoHijoNoExtiendeAlPadreException(padre.Codigo, codigo)`.
     - INV-10: sólo cuando **no** hay padre. Valida el regex; si falla lanza `CodigoRubroInvalidoException(codigo)`. Para rubros con padre, INV-F ya impone un formato más estricto que implica INV-10 — redundar con el regex habría agregado código sin cobertura.
     - INV-8: nivel = 1 para raíz o `padre.Nivel + 1` para hijo. Si excede `ProfundidadMaxima`, lanza `ProfundidadExcedidaException(ProfundidadMaxima, nivel)`.
     - INV-11: comprobación de unicidad de `Codigo` dentro de `_rubros`; si duplica lanza `CodigoRubroDuplicadoException(codigo)`.
     - Retorna `RubroAgregado(Id, rubroId, codigo, nombre, cmd.RubroPadreId, ahora)`.
   - Implementación real de `Apply(RubroAgregado e)`:
     - Calcula `Nivel`: si `e.RubroPadreId == null` → 1, si no → `_rubros.First(padre).Nivel + 1`.
     - Añade un `Rubro { Id, Codigo, Nombre, PadreId, Nivel }` a `_rubros`.
   - `Create` y `Apply(PresupuestoCreado)` **no se tocaron** (salvo por el añadido pasivo del campo `_rubros` que se inicializa en declaración).

### No creados / no tocados

- `src/SincoPresupuesto.Domain/Presupuestos/Commands/AgregarRubro.cs` — intacto (stub de red era correcto).
- `src/SincoPresupuesto.Domain/Presupuestos/Events/RubroAgregado.cs` — intacto.
- `src/SincoPresupuesto.Domain/SharedKernel/*.cs` (seis excepciones nuevas) — intactas: red ya las había introducido con constructores correctos y propiedades públicas tipadas.
- Tests: intactos.
- `src/SincoPresupuesto.Application/Presupuestos/PresupuestoProjection.cs`: sin cambios. La spec §8 requiere que `PresupuestoReadModel` gane una colección de rubros, pero ningún test de este slice ejercita la proyección. **Candidato para slice de infra/proyección** — ver §3.

---

## 2. Decisión sobre la rama INV-3 "lanza"

La spec §5 declara la invariante INV-3 y la spec §10 Q1 opción (a) **posterga** el escenario de violación al slice `AprobarPresupuesto`. La excepción `PresupuestoNoEsBorradorException` se introduce en este slice pero:

- La rama **"no lanza"** (estado Borrador) queda cubierta por el test de sanidad §6.7 (#7 en red-notes).
- La rama **"lanza"** (estado ≠ Borrador) no tiene test porque no existe comando que transicione el estado en slice 03.

**Código añadido de todas formas** — `if (Estado != EstadoPresupuesto.Borrador) throw …` — porque la spec lo exige declarativamente y el followup #13 garantiza la cobertura retroactiva cuando exista `AprobarPresupuesto`. Esto es una excepción consciente al principio green "no agregues código que ningún test ejerza": la rama positiva sí está protegida (test §6.7), y la negativa queda programada. Sin esta rama el slice `AprobarPresupuesto` tendría que modificar `AgregarRubro` retroactivamente, violando el aislamiento por slice.

**Deuda cubierta por followup #13.**

---

## 3. Impulsos de refactor no implementados

### 3.1 Orden de validación y "cálculo de nivel antes o después de INV-F"

La implementación evalúa **primero** INV-D (padre existe) → luego INV-F (extensión del código) cuando hay padre, o INV-10 (formato) cuando no. INV-8 (profundidad) y INV-11 (duplicado) se chequean al final.

Alternativa: reordenar para que INV-10 se valide siempre antes que cualquier otra regla específica del padre. Rechazado porque el test §6.10 (`"011.01"`) exige que `CodigoHijoNoExtiendeAlPadreException` prevalezca sobre `CodigoRubroInvalidoException` cuando hay padre — es semánticamente más específica y más accionable para el caller.

**Candidato para refactorer**: revisar si el orden merece un helper `ValidarCodigoDelHijo(padre, codigo)` que encapsule la decisión INV-F/INV-10. Hoy son dos ramas simples en `AgregarRubro`; extraer encapsularía la lógica y facilitaría el futuro slice `RubroMovido`.

### 3.2 `RequireCampo` — followup #10

Identificado en `Presupuesto.AgregarRubro` tres chequeos idénticos en estructura:

```csharp
if (string.IsNullOrWhiteSpace(cmd.Codigo)) throw new CampoRequeridoException("Codigo");
if (string.IsNullOrWhiteSpace(cmd.Nombre)) throw new CampoRequeridoException("Nombre");
// + rubroId == Guid.Empty → CampoRequeridoException("RubroId")   (patrón similar, otra forma)
```

Sumado a los cuatro usos existentes en `Presupuesto.Create` y `ConfiguracionTenant.Create`, hoy tenemos **siete usos** del patrón `IsNullOrWhiteSpace → throw CampoRequeridoException`. Followup #10 marca el disparador como "tercer uso" — está sobradamente disparado.

No extraigo el helper en este slice porque es refactor transversal (toca tres archivos de dominio). **Candidato firme para refactorer** con justificación: followup #10 disparado + tres slices lo usan.

Nota: el chequeo de `rubroId == Guid.Empty` lanza `CampoRequeridoException("RubroId")` — mismo tipo de excepción aunque el dato sea `Guid`, no `string`. Un helper `RequireCampo` podría sobrecargarse para `Guid` (`if == Guid.Empty`) o mantenerse sólo para strings y dejar el `Guid` inline. Decisión del refactorer.

### 3.3 Proyección `PresupuestoProjection` no actualizada

La spec §8 indica que `PresupuestoReadModel` debe ganar una lista de rubros y que `PresupuestoProjection.Apply(RubroAgregado)` debe mantenerla. Ningún test de este slice cubre la capa de proyección, por lo que no añadí código nuevo allí (disciplina "no agregues código sin test"). Si otro slice de capa de proyección lo requiere, se aborda como followup.

**Candidato para refactorer / nuevo followup**: añadir `Rubros` a `PresupuestoReadModel` y el manejador a `PresupuestoProjection`, con tests de infra/proyección dedicados.

### 3.4 Entity `Rubro` como record vs. class

Elegí clase con `{ get; init; }` (inmutable) para el entity `Rubro`. Un `record` class habría sido igual de válido y posiblemente más idiomático — su value-equality no molesta porque dentro del agregado se comparan por `Id` explícitamente. Preferí `class` porque:
- Consistencia con `Presupuesto` (también clase sellada).
- La semántica de value-equality de records no aplica: dos `Rubro` distintos con mismo contenido son el "mismo" rubro, pero nada en el código los compara así.

**Candidato ligero para refactorer** si el equipo prefiere records para entities inmutables.

### 3.5 `_rubros.First(padre).Nivel + 1` en `Apply(RubroAgregado)`

El fold asume que cuando llega un `RubroAgregado` con `RubroPadreId` no-null, el padre ya fue aplicado antes. Es cierto por orden cronológico de los eventos — si el padre no existiera, el comando original habría lanzado INV-D y el evento nunca se habría emitido. Por eso uso `.First(...)` en lugar de `.FirstOrDefault(...)`: si el padre falta durante el fold, es un bug de integridad del stream y preferimos que explote con `InvalidOperationException` antes que silenciar el error con `Nivel = 1` incorrecto.

**Decisión deliberada de simplicidad**: confiar en el invariante del stream. Un refactorer podría introducir un método `NivelDe(padreId)` más explícito si se requiere defensiva extra.

---

## 4. Decisiones deliberadas de "código más simple que debería ser"

### 4.1 INV-F chequeada por manipulación de strings, no regex

Implementación actual:

```csharp
var esperadoLen = padre.Codigo.Length + 3;
var prefijo = padre.Codigo + ".";
if (codigo.Length != esperadoLen
    || !codigo.StartsWith(prefijo, StringComparison.Ordinal)
    || !char.IsDigit(codigo[esperadoLen - 2])
    || !char.IsDigit(codigo[esperadoLen - 1]))
{
    throw new CodigoHijoNoExtiendeAlPadreException(padre.Codigo, codigo);
}
```

Alternativa: regex `^{Regex.Escape(padre.Codigo)}\.\d{2}$`. Rechazada porque:
- Compilar una regex por invocación es más caro que el chequeo char-a-char actual.
- Cachear un regex por padre complica la lógica.
- El chequeo actual es auto-explicativo (cuatro condiciones nombradas).

Refactorer puede proponer regex si mide mejor legibilidad.

### 4.2 INV-10 sólo se ejercita en la rama "sin padre"

El patrón `^\d{2}(\.\d{2}){0,14}$` garantiza la forma global del código. Si hay padre y INV-F pasa, el código es `padre.Codigo + ".DD"` por construcción; y como el padre ya pasó INV-10 en su propia creación, el código hijo es también canónico. Por eso INV-10 está en el `else` de la rama sin padre — evita una verificación redundante y mantiene cada invariante en una sola responsabilidad.

Refactorer puede discutir si conviene validar INV-10 siempre (defensivo) o sólo cuando corresponde (DRY). Hoy elegí DRY.

### 4.3 `_rubros.Any(r => r.Codigo == codigo)` — O(n)

Lookup lineal por cada `AgregarRubro`. Con N rubros por presupuesto y M operaciones, es O(N·M). Para MVP con decenas de rubros por presupuesto es negligible; si se escalara a miles podría necesitar un índice auxiliar (`HashSet<string>` por código). No se introduce porque ningún test lo requiere.

**Candidato para refactorer** si el tamaño del presupuesto crece.

---

## 5. Verificación final

```
dotnet build                                              → 0 errors, 0 warnings.
dotnet test --filter "FullyQualifiedName~Slice03"         → 23/23 verdes (22 nuevos + 1 sanidad).
dotnet test                                                → 58/58 verdes.
```

Sin regresiones en Slice01, Slice02 o DineroTests. La suite completa pasó de 36/58 verdes (22 rojos + 1 sanidad verde colateral) a 58/58 verdes.

---

## 6. Handoff a refactorer

Lista priorizada:

1. **Followup #10 disparado** — extraer helper `RequireCampo(string, string)` en `SharedKernel` (siete usos identificados: 3 en `Presupuesto.Create`, 2 en `Presupuesto.AgregarRubro`, 1 en `ConfiguracionTenant.Create`, +1 variante para Guid en `AgregarRubro`).
2. **Proyección `PresupuestoProjection.Apply(RubroAgregado)`** — añadir soporte de `Rubros` en `PresupuestoReadModel`. Requiere spec o followup explícito; puede escribirse como slice de infra.
3. **Orden de validaciones y helpers** — evaluar si `ValidarCodigoDelHijo(padre, codigo)` escala mejor cuando llegue el slice `RubroMovido` (followup #11).
4. **INV-3 rama "lanza"** — dejar explícito en el slice `AprobarPresupuesto` el test de violación (followup #13).
