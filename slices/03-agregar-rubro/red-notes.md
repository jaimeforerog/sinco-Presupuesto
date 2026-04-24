# Red notes — Slice 03 — AgregarRubro

**Autor:** red
**Fecha:** 2026-04-24
**Spec consumida:** `slices/03-agregar-rubro/spec.md` (firmada 2026-04-24, commit `b6148c8`).

---

## 1. Tests escritos

Archivo: `tests/SincoPresupuesto.Domain.Tests/Slices/Slice03_AgregarRubroTests.cs`.

Un test por escenario ejercitable de la spec §6 (12 escenarios) + un test de sanidad para el camino Borrador (§6.7 diferido).

| # | Test | Escenario spec §6 | Tipo | Casos |
|---|---|---|---|---|
| 1 | `AgregarRubro_raiz_en_presupuesto_en_borrador_emite_RubroAgregado_con_todos_los_campos` | 6.1 happy path raíz | Fact | 1 |
| 2 | `AgregarRubro_hijo_que_extiende_al_padre_emite_RubroAgregado_con_RubroPadreId` | 6.2 happy path hijo | Fact | 1 |
| 3 | `AgregarRubro_con_Codigo_y_Nombre_con_espacios_emite_evento_con_trim_aplicado` | 6.3 normalización | Fact | 1 |
| 4 | `AgregarRubro_con_Codigo_vacio_lanza_CampoRequerido` | 6.4 PRE-1 | Theory | 2 (`""`, `"   "`) |
| 5 | `AgregarRubro_con_Nombre_vacio_lanza_CampoRequerido` | 6.5 PRE-2 | Theory | 2 (`""`, `"   "`) |
| 6 | `AgregarRubro_con_rubroId_vacio_lanza_CampoRequerido` | 6.6 PRE-3 | Fact | 1 |
| 7 | `AgregarRubro_sobre_presupuesto_en_Borrador_no_lanza_PresupuestoNoEsBorrador` | 6.7 INV-3 (sanidad Borrador — violación diferida) | Fact | 1 |
| 8 | `AgregarRubro_con_Codigo_formato_invalido_lanza_CodigoRubroInvalido` | 6.8 INV-10 | Theory | 6 (`"1"`, `"1.1"`, `"01.1"`, `"01-01"`, `"a1"`, 16-niveles) |
| 9 | `AgregarRubro_con_Codigo_duplicado_lanza_CodigoRubroDuplicado` | 6.9 INV-11 | Fact | 1 |
| 10 | `AgregarRubro_hijo_que_no_extiende_al_padre_lanza_CodigoHijoNoExtiendeAlPadre` | 6.10 INV-F | Theory | 4 (`"02.01"`, `"01"`, `"01.01.01"`, `"011.01"`) |
| 11 | `AgregarRubro_con_RubroPadreId_inexistente_lanza_RubroPadreNoExiste` | 6.11 INV-D | Fact | 1 |
| 12 | `AgregarRubro_que_excede_ProfundidadMaxima_lanza_ProfundidadExcedida` | 6.12 INV-8 | Fact | 1 |
| 13 | `Fold_de_RubroAgregado_deja_el_agregado_con_el_rubro_raiz_registrado` | 6.13 fold | Fact | 1 |

Expansión por Theory: **23 casos xUnit totales** (13 métodos distintos).

**Escenario §6.7 (violación INV-3) no se escribe**: está diferido al slice `AprobarPresupuesto` por decisión §10 Q1 opción (a) firmada por el usuario. En su lugar, el test #7 verifica que el camino "Borrador → AgregarRubro" no lanza `PresupuestoNoEsBorradorException`, con referencia explícita a INV-3 en su nombre para facilitar el split cuando llegue el slice de aprobación (followup #13).

## 2. Verificación de estado rojo

Comando usado:

```bash
dotnet test tests/SincoPresupuesto.Domain.Tests/SincoPresupuesto.Domain.Tests.csproj \
  --filter "FullyQualifiedName~Slice03" --nologo
```

Resultado observado tras la corrección de orquestador/stubs:

```
Con error! - Con error: 22, Superado: 1, Omitido: 0, Total: 23
```

**22 casos fallan con la razón correcta** (ninguno por "no compila"). **1 caso pasa intencionalmente** (el test de sanidad — ver fila §6.7 abajo y nota en §4 de este documento).

Razón de fallo por test:

| # | Test | Razón esperada de fallo |
|---|---|---|
| 1 | `AgregarRubro_raiz_…_con_todos_los_campos` | `NotImplementedException` en `Presupuesto.AgregarRubro` (stub). |
| 2 | `AgregarRubro_hijo_…_con_RubroPadreId` | `NotImplementedException` en `Presupuesto.Apply(RubroAgregado)` durante el fold del Given. |
| 3 | `AgregarRubro_con_Codigo_y_Nombre_con_espacios_…_trim` | `NotImplementedException` en `Presupuesto.AgregarRubro`. |
| 4 | `AgregarRubro_con_Codigo_vacio_lanza_CampoRequerido` (x2) | Se esperaba `CampoRequeridoException(NombreCampo="Codigo")` — se lanzó `NotImplementedException`. |
| 5 | `AgregarRubro_con_Nombre_vacio_lanza_CampoRequerido` (x2) | Se esperaba `CampoRequeridoException(NombreCampo="Nombre")` — se lanzó `NotImplementedException`. |
| 6 | `AgregarRubro_con_rubroId_vacio_lanza_CampoRequerido` | Se esperaba `CampoRequeridoException(NombreCampo="RubroId")` — se lanzó `NotImplementedException`. |
| 7 | `AgregarRubro_sobre_presupuesto_en_Borrador_no_lanza_PresupuestoNoEsBorrador` | **Pasa.** El test asserta `NotThrow<PresupuestoNoEsBorradorException>` y el stub lanza `NotImplementedException` (tipo distinto), por lo que el aserto se cumple accidentalmente. En green el test se mantendrá verde cuando la validación INV-3 correcta (no bloquear Borrador) quede implementada. Es una red-protección de comportamiento, no un red-test estricto. Ver §4 para la justificación metodológica. |
| 8 | `AgregarRubro_con_Codigo_formato_invalido_lanza_CodigoRubroInvalido` (x6) | Se esperaba `CodigoRubroInvalidoException` — se lanzó `NotImplementedException`. |
| 9 | `AgregarRubro_con_Codigo_duplicado_lanza_CodigoRubroDuplicado` | `NotImplementedException` en `Presupuesto.Apply(RubroAgregado)` durante el fold del Given (antes de llegar al aserto). |
| 10 | `AgregarRubro_hijo_que_no_extiende_al_padre_…` (x4) | `NotImplementedException` en `Presupuesto.Apply(RubroAgregado)` durante el fold del Given. |
| 11 | `AgregarRubro_con_RubroPadreId_inexistente_…` | `NotImplementedException` en `Presupuesto.Apply(RubroAgregado)` durante el fold del Given. |
| 12 | `AgregarRubro_que_excede_ProfundidadMaxima_…` | `NotImplementedException` en `Presupuesto.Apply(RubroAgregado)` durante el fold del Given. |
| 13 | `Fold_de_RubroAgregado_deja_el_agregado_con_el_rubro_raiz_registrado` | `TargetInvocationException` envolviendo `NotImplementedException` en `Presupuesto.Apply(RubroAgregado)`. |

Ningún test falla por compilación — la solución entera compila con **0 advertencias / 0 errores** (`dotnet build` exitoso).

## 3. Regresión en otros slices

Comando usado para verificar que los slices anteriores siguen verdes:

```bash
dotnet test tests/SincoPresupuesto.Domain.Tests/SincoPresupuesto.Domain.Tests.csproj --nologo
# salida: Con error: 22, Superado: 36, Omitido: 0, Total: 58
```

Desglose:

- **Slice01**: `dotnet test --filter "FullyQualifiedName~Slice01"` → 16/16 verdes (Fact+Theory expandidos).
- **Slice02**: `dotnet test --filter "FullyQualifiedName~Slice02"` → 7/7 verdes.
- **DineroTests**: `dotnet test --filter "FullyQualifiedName~Dinero"` → 12/12 verdes (Theory expandidos).
- **Slice03**: 22 fallan (razón correcta) + 1 pasa (sanidad §6.7). Ver §2.

Total 36 verdes no-Slice03 + 22 rojos + 1 sanidad verde = 58 casos. **Sin regresiones.**

> Nota: el brief mencionaba "Slice01 (26), Slice02 (7), DineroTests (6)" como referencia. Los conteos reales de la rama son 16 / 7 / 12 tras expansión de Theory — el criterio de "no regresión" (todos los pre-existentes verdes) se cumple; el conteo numérico del brief está desactualizado.

## 4. Código de producción tocado

Se agregaron **stubs mínimos** en `src/`. Todos los cuerpos lanzan `NotImplementedException` — no hay lógica que haga pasar un test prematuramente. Se aprendió de la corrección del orquestador en slice 02 (subagente implementó lógica en un stub y se tuvo que revertir).

### Archivos nuevos

1. **`src/SincoPresupuesto.Domain/Presupuestos/Commands/AgregarRubro.cs`**
   - `record AgregarRubro(string Codigo, string Nombre, Guid? RubroPadreId = null)`.

2. **`src/SincoPresupuesto.Domain/Presupuestos/Events/RubroAgregado.cs`**
   - `record RubroAgregado(Guid PresupuestoId, Guid RubroId, string Codigo, string Nombre, Guid? RubroPadreId, DateTimeOffset AgregadoEn)`.
   - Payload alineado al event-storming §5.

3. **`src/SincoPresupuesto.Domain/SharedKernel/PresupuestoNoEsBorradorException.cs`** — propiedad `EstadoActual: EstadoPresupuesto` (INV-3).

4. **`src/SincoPresupuesto.Domain/SharedKernel/CodigoRubroInvalidoException.cs`** — propiedad `CodigoIntentado: string` (INV-10).

5. **`src/SincoPresupuesto.Domain/SharedKernel/CodigoRubroDuplicadoException.cs`** — propiedad `CodigoIntentado: string` (INV-11).

6. **`src/SincoPresupuesto.Domain/SharedKernel/CodigoHijoNoExtiendeAlPadreException.cs`** — propiedades `CodigoPadre: string`, `CodigoHijo: string` (INV-F).

7. **`src/SincoPresupuesto.Domain/SharedKernel/RubroPadreNoExisteException.cs`** — propiedad `RubroPadreId: Guid` (INV-D).

8. **`src/SincoPresupuesto.Domain/SharedKernel/ProfundidadExcedidaException.cs`** — propiedades `ProfundidadMaxima: int`, `NivelIntentado: int` (INV-8).

Las seis excepciones heredan de `DominioException`, tienen un único constructor que guarda las propiedades en `get;` públicos de solo lectura (sin setters), y **no** contienen lógica además del mensaje base formateado. Siguen el patrón existente de slice 01 (`PeriodoInvalidoException`, `ProfundidadMaximaFueraDeRangoException`, `CampoRequeridoException`).

### Archivos modificados

9. **`src/SincoPresupuesto.Domain/Presupuestos/Presupuesto.cs`**
   - Se añadió método de instancia `AgregarRubro(Commands.AgregarRubro cmd, Guid rubroId, DateTimeOffset ahora) => throw new NotImplementedException();`.
   - Se añadió `Apply(RubroAgregado e) => throw new NotImplementedException();` para que el fold en tests que requieren un `RubroAgregado` previo ejercite el stub y falle uniformemente.
   - El método `Create(...)` y `Apply(PresupuestoCreado)` existentes **no se tocaron**.

Ningún stub contiene validación, normalización o lógica de dominio. Green es responsable de introducir cada pieza en respuesta a los tests rojos.

## 5. Decisión sobre el test de sanidad §6.7

El spec §6.7 indica que el escenario de violación de INV-3 (estado ≠ Borrador) se **difiere** al slice `AprobarPresupuesto`, pero **manda** a este slice un test de cobertura mínima verificando que el camino "estado Borrador" no lanza. Ese test es el #7 (`AgregarRubro_sobre_presupuesto_en_Borrador_no_lanza_PresupuestoNoEsBorrador`).

Implementado como `act.Should().NotThrow<PresupuestoNoEsBorradorException>()`. Hoy pasa porque el stub lanza `NotImplementedException` (tipo distinto) — es decir, no es un "rojo estricto". Justificación:

- El comportamiento que protege es **negativo** (no lanzar INV-3 en Borrador); no existe un "código mínimo para pasarlo" que green pueda añadir, porque green nunca introducirá una rama que bloquee Borrador. El test es una **red-protección de regresión futura**: cuando llegue el slice `AprobarPresupuesto` y se introduzca la validación INV-3 en `AgregarRubro`, este test evita que la validación sea demasiado agresiva y bloquee Borrador por error.
- Por eso el test mantiene su referencia explícita a INV-3 en el nombre y queda trazable al followup #13.
- Alternativa descartada: invertir a `Should().NotThrow()` (sin tipo específico). Rechazada porque haría el test fallar hoy con `NotImplementedException` — razón técnica del stub, no semántica del dominio — y no discrimina un fallo de INV-3 genuino de uno colateral.

Documentado aquí para que el reviewer lo audite en la pasada final del slice.

## 6. Desviaciones respecto a la spec

- [x] Sin desviaciones de la spec §6 (12 escenarios ejercitables + 1 sanidad).

El test #13 (fold §6.13) asume por reflexión que el agregado expone una propiedad pública `Rubros : IEnumerable<Rubro>` con elementos que exponen `Id`, `Codigo`, `Nombre`, `PadreId`, `Nivel`. La spec §6.13 indica textualmente "La colección interna de rubros (nombre exacto a criterio de green)"; la prueba deja el naming a discreción de green pero fija los miembros públicos por contrato del test. Si green prefiere otro naming, ajusta la invocación reflexiva en un solo lugar del test (no hay duplicación).

## 7. Hand-off a green

- [x] Spec firmada: sí (2026-04-24).
- [x] Todos los tests compilan: sí (`dotnet build` con 0 errores y 0 warnings).
- [x] 22 casos fallan por razón correcta (`NotImplementedException` desde stubs de `AgregarRubro` o `Apply(RubroAgregado)`, o aserción de excepción de dominio no lanzada).
- [x] 1 caso (§6.7 sanidad) pasa intencionalmente — ver §5.
- [x] Sin regresión en slices 01 y 02 (todos verdes).
- [x] Stubs en `src/` con `NotImplementedException` — **cero lógica**.

**Ready para green.** El próximo agente toma estos tests rojos y los hace verdes implementando:

1. `Presupuesto.AgregarRubro(cmd, rubroId, ahora)` — validación y emisión del evento con: PRE-1/PRE-2/PRE-3 (`CampoRequeridoException` con nombres `Codigo`, `Nombre`, `RubroId`), normalización de espacios, INV-10 (regex `^\d{2}(\.\d{2}){0,14}$`), INV-11 (unicidad intra-presupuesto), INV-F (extensión hijo→padre con exactamente un segmento `\.\d{2}`), INV-D (existencia del padre), INV-8 (nivel ≤ ProfundidadMaxima).
2. `Presupuesto.Apply(RubroAgregado e)` — agregar el rubro a la colección interna derivando `Nivel` y `PadreId`. La colección debe exponerse públicamente (lectura) para el test #13.
3. Definición de la entity `Rubro` interna al agregado con miembros `Id`, `Codigo`, `Nombre`, `PadreId?`, `Nivel`.

Green **no** debe introducir `PresupuestoNoEsBorradorException` en el camino activo (el escenario de violación INV-3 llega con el slice `AprobarPresupuesto` — followup #13). La excepción existe en SharedKernel pero no se lanza aún desde `AgregarRubro`.
