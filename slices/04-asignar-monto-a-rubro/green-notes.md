# Green notes — Slice 04 — AsignarMontoARubro

**Implementador:** green
**Fecha:** 2026-04-24
**Spec consumida:** `slices/04-asignar-monto-a-rubro/spec.md` (firmada 2026-04-24, Q1=(d)).
**Red-notes consumidas:** `slices/04-asignar-monto-a-rubro/red-notes.md`.
**Estado:** 15 rojos Slice04 → verdes; test de sanidad §6.8 sigue verde. Total Slice04 16/16. Suite completa **125/125 verdes**, 0 warnings, 0 errors.

---

## 1. Archivos modificados

### Modificados

1. **`src/SincoPresupuesto.Domain/Presupuestos/Presupuesto.cs`**
   - **`Apply(RubroAgregado e)`** — añadida la inicialización `Monto = Dinero.Cero(MonedaBase)` al crear el `Rubro`. Necesario para que el entity no quede en `default(Dinero)` (cuya `Moneda` interna tiene `Codigo = null` y violaría INV-SK-3 del VO `Moneda`). Spec §12.2 decisión elegida (alternativa B). Sin esta inicialización, la comparación `rubro.Monto.EsCero` usada por `AsignarMontoARubro` para detectar "primera asignación" operaría sobre un `Dinero` con `Moneda` inválida.
   - **`AsignarMontoARubro(cmd, ahora)`** — reemplazado el stub `NotImplementedException` por la implementación real. Orden de validación (coincidente con brief y red-notes §7):
     1. `ArgumentNullException.ThrowIfNull(cmd)`.
     2. **PRE-1**: `cmd.RubroId == Guid.Empty` → `CampoRequeridoException("RubroId")`.
     3. **INV-3 declarada**: `Estado != Borrador` → `PresupuestoNoEsBorradorException(Estado)` (rama que no se ejercita en este slice; followup #13 cubre la violación cuando exista `AprobarPresupuesto`).
     4. **PRE-2**: `_rubros.FirstOrDefault(r => r.Id == cmd.RubroId)`; si no existe → `RubroNoExisteException(cmd.RubroId)`.
     5. **INV-NEW-SLICE04-1**: si `_rubros.Any(r => r.PadreId == cmd.RubroId)` → `RubroEsAgrupadorException(cmd.RubroId)`.
     6. **PRE-3 / INV-2**: `cmd.Monto.Valor < 0m` → `MontoNegativoException(cmd.Monto)`.
     7. **PRE-4**: `string.IsNullOrWhiteSpace(cmd.AsignadoPor) ? "sistema" : cmd.AsignadoPor`.
     8. **`MontoAnterior`**: `rubroDestino.Monto.EsCero ? Dinero.Cero(cmd.Monto.Moneda) : rubroDestino.Monto`.
     9. Devuelve `new MontoAsignadoARubro(Id, cmd.RubroId, cmd.Monto, montoAnterior, ahora, asignadoPor)`.
   - **`Apply(MontoAsignadoARubro e)`** — reemplazado el stub. Implementación mínima: `_rubros.First(r => r.Id == e.RubroId).Monto = e.Monto;`. Se usa `.First(...)` (no `.FirstOrDefault(...)`) porque la ausencia del rubro durante el fold sería un bug de integridad del stream: el método de comando garantiza que el rubro existe antes de emitir. Criterio idéntico al usado en el fold de `RubroAgregado` para resolver el padre (slice 03 green-notes §3.5).

### No tocados (ya estaban correctos tras red)

- `src/SincoPresupuesto.Domain/Presupuestos/Rubro.cs` — red dejó `Monto { get; set; }` con set público. **Decisión deliberada**: no se endurece a `private set` en este slice — ver §3.1.
- `src/SincoPresupuesto.Domain/Presupuestos/Commands/AsignarMontoARubro.cs` — record correcto.
- `src/SincoPresupuesto.Domain/Presupuestos/Events/MontoAsignadoARubro.cs` — record correcto.
- `src/SincoPresupuesto.Domain/SharedKernel/RubroNoExisteException.cs`, `MontoNegativoException.cs`, `RubroEsAgrupadorException.cs` — las tres siguen el patrón de las excepciones previas (heredan de `DominioException`, un constructor, propiedad pública tipada). Sin cambios.
- Tests de Slice04 — intactos.
- Otros slices — intactos.
- `PresupuestoProjection` / `PresupuestoReadModel` — no tocados. Ningún test del slice ejercita la proyección; igual criterio que slice 03 green-notes §3.3 (disciplina "no agregues código sin test").
- `DomainExceptionHandler.Mapear` — no tocado. No hay tests HTTP en este proyecto de dominio; spec §9 y §12.1 describen el mapeo pero el refactor transversal del handler pertenece a un slice de wire/API.

---

## 2. Impulsos de refactor descartados (candidatos para `refactorer`)

### 2.1 `Rubro.Monto { get; set; }` público

El brief sugería evaluar endurecer a `private set`. No lo hice porque el fold `Apply(MontoAsignadoARubro)` muta el `Monto` desde `Presupuesto` (otra clase) y `private set` rompería esa ruta. `init` rompería también el fold. Opciones para refactorer:

- Mover la mutación a un método `internal`/`private` en `Rubro` (p.ej. `rubro.ConMonto(e.Monto)` devolviendo una nueva instancia con `with`), pero `Rubro` es `class` (no record), así que `with` no aplica directamente.
- Convertir `Rubro` a `record` y usar `rubro with { Monto = e.Monto }` en el fold, reemplazando el elemento en `_rubros` por índice. Esto alinea con la inmutabilidad del resto de entity fields (`init`) pero introduce mutación de colección indexada.
- Introducir un método `internal void MutarMonto(Dinero)` en `Rubro` con visibilidad limitada al ensamblado. Expresa intención sin exponer el setter al mundo.

**Candidato firme** — la decisión afecta el modelo de mutación del agregado. Anotado para refactorer.

### 2.2 `PresupuestoProjection` / `PresupuestoReadModel` siguen sin soporte de `Rubros` ni `Monto`

Spec §8 describe los cambios necesarios; slice 03 green-notes §3.3 los dejó como deuda. Este slice los agrava al introducir `Monto` pero sigue sin tocarlos (mismo criterio). **Candidato para slice de infra/proyección dedicado**.

### 2.3 `DomainExceptionHandler.Mapear` pendiente de tres nuevos casos

Spec §12.1 especifica mapeos: `RubroNoExisteException → 409`, `RubroEsAgrupadorException → 409`, `MontoNegativoException → 400`. Sin tests HTTP en el proyecto de dominio, no se tocó. **Candidato para slice de wire/API** (consistente con slice 02 §12 y slice 03 §12 que dejaron el mapeo en el slice que introduce la excepción cuando hay wire disponible — aquí no lo hay).

### 2.4 Helper `RequireCampo(Guid, string)` / `RequireCampo(string, string)`

Slice 03 green-notes §3.2 ya marcó followup #10 como disparado (siete usos de `IsNullOrWhiteSpace → CampoRequeridoException`). Este slice añade un octavo uso del patrón `Guid.Empty → CampoRequeridoException("RubroId")` y un noveno uso del patrón `IsNullOrWhiteSpace → "sistema"` (normalización, no throw). **Refactor transversal — pertenece a refactorer**, no a este slice.

### 2.5 Orden de validación entre PRE-2 y PRE-3

Elegí: primero PRE-2 (existe rubro) y validación "es Agrupador", luego PRE-3 (Monto ≥ 0). Alternativa: validar PRE-3 primero porque es "barato" (sin acceder a `_rubros`). Rechazado porque:
- El brief fija este orden explícitamente.
- Los tests ya asumen este orden implícitamente (cada uno ejercita un camino único, sin mezclar violaciones).
- La semántica del dominio privilegia "existe el destino" antes de "el dato es bueno" — si no hay destino, el contenido del dato es irrelevante.

No hay test que combine violaciones simultáneas, así que refactorer podría reordenar sin romper nada. Anotado por si en el futuro un test mixto fuerza otro orden.

### 2.6 `EstadoPresupuesto` comparación contra `Borrador`

Se comparte idéntico bloque entre `AgregarRubro` y `AsignarMontoARubro` (spec §5, INV-3). **Candidato a extracción** (p.ej. `RequerirBorrador()` privado) cuando aparezcan más comandos con la misma guard. Hoy sólo son dos — no se extrae por disciplina green.

---

## 3. Decisiones deliberadas (código más simple que debería ser)

### 3.1 Inicialización de `Rubro.Monto` en `Apply(RubroAgregado)` (spec §12.2 alternativa B)

Alternativa A era `Dinero?` nullable — fuerza al resto del código a distinguir `null` vs `Cero`. Alternativa B (elegida por la spec) inicializa a `Dinero.Cero(MonedaBase)`. La consecuencia es que la detección de "primera asignación" se reduce a `rubroDestino.Monto.EsCero`, un proxy operativo que:

- **Acepta un caso borderline**: si una asignación explícita `Monto = Cero(cmd.Monto.Moneda)` pone el rubro en `.EsCero == true` y luego llega otra asignación, el `MontoAnterior` de la segunda se alinea a la moneda **del segundo comando** en vez de mantener la moneda del cero explícito previo. Este comportamiento está **contemplado en spec §12.2** ("modulo la Moneda del Cero — que se alinea a la moneda del comando, comportamiento sobre el que el usuario no puede notar diferencia visible en proyecciones"). No hay test que discrimine entre "primera asignación desde cero inicial" y "primera asignación tras reset explícito a cero en la misma moneda base" — ambos rinden el mismo evento observable.
- **No requiere flag de estado adicional**: el VO `Dinero` ya expone `.EsCero` como parte de su API pública (`Dinero.cs`).

Decisión documentada aquí por si un futuro slice ejercita el matiz.

### 3.2 `Rubros` mutados por `Monto = e.Monto` dentro de `Apply(MontoAsignadoARubro)` — mutación directa, no reemplazo por índice

Spec §12.5 sugiere "reemplaza en `_rubros` por una copia con `Monto = e.Monto` (las demás propiedades preservadas). Patrón: inmutable por `init`, mutación del array por index". **No lo hice así** porque red dejó `Monto { get; set; }` público en `Rubro`, y la mutación directa es estrictamente más simple que reconstruir + reemplazar. La decisión de endurecer `Monto` a `init` (y por tanto forzar reemplazo) es de refactorer (§2.1).

Impacto operativo: la referencia al `Rubro` en `_rubros` es estable; quien guarde una referencia externa y luego haga fold vería el `Monto` actualizado. En el dominio no hay tal uso: `IReadOnlyList<Rubro> Rubros` se expone como vista; los tests reconstruyen cada vez. No hay regresión.

### 3.3 `_rubros.Any(r => r.PadreId == cmd.RubroId)` — O(n) por llamada

Idem slice 03 green-notes §4.3: lookup lineal aceptable en MVP. Candidato a índice por `PadreId` si escala.

### 3.4 `.First(...)` en `Apply(MontoAsignadoARubro)`

Idem slice 03 green-notes §3.5: confío en el invariante del stream (el rubro existe cuando se emitió el evento → existe al aplicarlo). Si falta, `InvalidOperationException` es preferible a silenciar el error. Defensiva extra es tarea de refactorer.

### 3.5 INV-3 declarada pero no ejercitada en su rama "lanza"

Idem slice 03 green-notes §2: la rama `if (Estado != Borrador) throw` existe por exigencia declarativa de la spec (§5) pese a que este slice no la ejercita en violación. Followup #13 la cubre cuando exista `AprobarPresupuesto`. Test de sanidad §6.8 ejercita la rama "no lanza en Borrador".

---

## 4. Verificación

### 4.1 `dotnet build`

```
Compilación correcta.
    0 Advertencia(s)
    0 Errores
```

### 4.2 `dotnet test --filter "FullyQualifiedName~Slice04"`

```
Correctas! - Con error: 0, Superado: 16, Omitido: 0, Total: 16
```

Los 16 casos xUnit (14 métodos, §6.11 Theory con 3 cases) pasan — incluye el test de sanidad §6.8 que ya pasaba en rojo intencional.

### 4.3 `dotnet test` (suite completa)

```
Correctas! - Con error: 0, Superado: 125, Omitido: 0, Total: 125
```

**125/125 verdes.** Sin regresiones en Slices 00, 01, 02 y 03 (109/109 previos) ni en los tests de SharedKernel.

---

## 5. Hand-off a refactorer

Lista priorizada (suma a la de slice 03):

1. **`Rubro.Monto` — endurecer encapsulamiento** (§2.1). Opciones: convertir `Rubro` a `record` + mutación `with`; o introducir método `internal` de mutación controlada. Decisión del refactorer según el modelo de mutación preferido para entities de agregado.
2. **Proyección `PresupuestoProjection` / `PresupuestoReadModel`** (§2.2). Debe ganar `Rubros` + `Monto` por rubro — deuda heredada de slice 03 que este slice agrava.
3. **`DomainExceptionHandler.Mapear`** (§2.3). Añadir los tres nuevos casos al switch. Tarea de slice wire/API cuando haya tests HTTP.
4. **Followup #10 — helper `RequireCampo`** (§2.4). Ahora con 9 usos del patrón en dominio (era 7 tras slice 03).
5. **Guard de estado `RequerirBorrador()` reutilizable** (§2.6). Cuando haya ≥ 3 comandos que lo usen (hoy 2).
6. **INV-3 rama "lanza"** — sigue pendiente; cubierta por followup #13 en `AprobarPresupuesto`.
