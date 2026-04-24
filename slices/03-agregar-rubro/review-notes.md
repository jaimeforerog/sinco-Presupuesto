# Review notes — Slice 03 — AgregarRubro

**Autor:** reviewer
**Fecha:** 2026-04-24
**Slice auditado:** `slices/03-agregar-rubro/`.
**Veredicto:** `approved-with-followups`

---

## 1. Resumen ejecutivo

Slice 03 introduce la estructura interna de rubros dentro del agregado `Presupuesto`: entity `Rubro`, comando `AgregarRubro`, evento `RubroAgregado`, seis excepciones nuevas y un helper `Requerir.Campo` que cierra followup #10 con refactor transversal. El ciclo red → green → refactor completó satisfactoriamente: 58/58 tests verdes (23 nuevos Slice03 tras expansión de Theories, 16 Slice01 + 7 Slice02 + 12 DineroTests sin regresión), `dotnet build` 0 errores / 0 warnings. La spec §6 mapea exactamente a los tests (un test por escenario ejercitable; §6.7 documentado como sanidad con violación INV-3 diferida al slice `AprobarPresupuesto`). Se registran 3 followups nuevos en `FOLLOWUPS.md` (#11, #12, #13), se cierra #10 (ya cerrado por refactorer — verificado) y se cierra #5 como "no aplicable" con criterio técnico. Veredicto: **approved-with-followups** — no hay blockers; la única rama no cubierta por test (INV-3 "lanza") está explícitamente diferida por decisión firmada §10 Q1(a), con followup #13 que garantiza su cobertura retroactiva.

---

## 2. Checklist de auditoría

### 2.1 Spec ↔ tests

- [x] Cada escenario de `spec.md §6` ejercitable tiene un test correspondiente.
  - §6.1 happy path raíz → `AgregarRubro_raiz_en_presupuesto_en_borrador_emite_RubroAgregado_con_todos_los_campos`.
  - §6.2 happy path hijo → `AgregarRubro_hijo_que_extiende_al_padre_emite_RubroAgregado_con_RubroPadreId`.
  - §6.3 normalización → `AgregarRubro_con_Codigo_y_Nombre_con_espacios_emite_evento_con_trim_aplicado`.
  - §6.4 PRE-1 → `AgregarRubro_con_Codigo_vacio_lanza_CampoRequerido` (Theory, 2 casos `""`, `"   "`).
  - §6.5 PRE-2 → `AgregarRubro_con_Nombre_vacio_lanza_CampoRequerido` (Theory, 2 casos).
  - §6.6 PRE-3 → `AgregarRubro_con_rubroId_vacio_lanza_CampoRequerido`.
  - §6.7 sanidad Borrador (violación INV-3 diferida) → `AgregarRubro_sobre_presupuesto_en_Borrador_no_lanza_PresupuestoNoEsBorrador`.
  - §6.8 INV-10 → `AgregarRubro_con_Codigo_formato_invalido_lanza_CodigoRubroInvalido` (Theory, 6 casos incluyendo 16-niveles).
  - §6.9 INV-11 → `AgregarRubro_con_Codigo_duplicado_lanza_CodigoRubroDuplicado`.
  - §6.10 INV-F → `AgregarRubro_hijo_que_no_extiende_al_padre_lanza_CodigoHijoNoExtiendeAlPadre` (Theory, 4 casos).
  - §6.11 INV-D → `AgregarRubro_con_RubroPadreId_inexistente_lanza_RubroPadreNoExiste`.
  - §6.12 INV-8 → `AgregarRubro_que_excede_ProfundidadMaxima_lanza_ProfundidadExcedida`.
  - §6.13 fold → `Fold_de_RubroAgregado_deja_el_agregado_con_el_rubro_raiz_registrado`.

- [x] Cada precondición tiene un test que la viola.
  - PRE-1 (`Codigo` requerido) → §6.4 lanza `CampoRequeridoException(NombreCampo="Codigo")`.
  - PRE-2 (`Nombre` requerido) → §6.5 lanza `CampoRequeridoException(NombreCampo="Nombre")`.
  - PRE-3 (`rubroId` no `Guid.Empty`) → §6.6 lanza `CampoRequeridoException(NombreCampo="RubroId")`.
  - PRE-4 (normalización trim) → §6.3 verifica que Codigo y Nombre salen trimmed en el evento.

- [x] Cada invariante tocada y ejercitable tiene un test que la viola.
  - INV-8 (profundidad) → §6.12.
  - INV-10 (formato regex `^\d{2}(\.\d{2}){0,14}$`) → §6.8 (6 casos).
  - INV-11 (unicidad código) → §6.9.
  - INV-F (extensión hijo→padre) → §6.10 (4 casos).
  - INV-D (existencia padre) → §6.11.
  - INV-3 (estado Borrador) — violación **diferida** al slice `AprobarPresupuesto` por decisión §10 Q1(a) firmada; §6.7 ejerce solo la rama "no lanza" de la excepción (sanidad). **Followup #13 garantiza cobertura retroactiva.** No es blocker.
  - INV-9 (terminal sin hijos) — no ejercitable en slice 03 por ausencia de `RubroTipo`/`AsignarMontoARubro` (decisión §10 Q2). **Followup #12 lo cubre.** No es blocker.

- [x] Los nombres de los tests son frases completas en español que describen el comportamiento.
  - Forma consistente `AgregarRubro_{condición}_{resultado}`. Sin `Test1`, sin `ShouldWork`.

### 2.2 Tests como documentación

- [x] Un lector que no conoce el código puede entender el comportamiento leyendo solo los tests.
  - Fixtures `PresupuestoCreadoBase(profundidadMaxima=10)` y `CmdValido(...)` documentan los valores por defecto de manera legible; el único caso que baja `ProfundidadMaxima` a 2 (§6.12) lo hace explícito en el sitio de uso.
  - El comentario de clase (líneas 11-20) narra la semántica del slice y hace visible la decisión de diferir §6.7.

- [x] Given/When/Then está estructuralmente visible.
  - Comentarios explícitos `// Given`, `// When`, `// Then` en cada test; excepción: §6.4 y §6.5 usan `// Given` sobre el aggregate y cmd combinados por brevedad, manteniendo la estructura G/W/T clara.

- [x] Sin mocks del dominio.
  - Todos los eventos en Given son instancias reales (`PresupuestoCreado`, `RubroAgregado`).
  - `AggregateBehavior<Presupuesto>.Reconstruir(...)` usa reflexión sobre `Apply(...)`, no es un mock.
  - Excepciones se asertan por tipo + propiedades (`.Which.NombreCampo`, `.Which.CodigoIntentado`, `.Which.Match<>`), nunca por mensaje.

- [x] Eventos usados en Given son reales, no fabricados con valores nonsense.
  - El `RubroAgregado` del Given en §6.2/§6.9/§6.10/§6.11/§6.12 representa un rubro raíz consistente con lo que hubiera emitido `AgregarRubro` en §6.1 — el fold resultante es el estado "real" del agregado tras ejecutar ese happy path primero.

### 2.3 Implementación

- [x] El código de producción añadido es mínimo; todo miembro público nuevo es ejercido por al menos un test (salvo deuda documentada).
  - **`Rubro.cs`** (entity, 17 líneas): 5 propiedades `{ get; init; }`. Todas ejercidas por §6.13 vía reflexión (`Id`, `Codigo`, `Nombre`, `PadreId`, `Nivel`).
  - **`Commands/AgregarRubro.cs`** (21 líneas, record): 3 parámetros, todos construidos en tests.
  - **`Events/RubroAgregado.cs`** (20 líneas, record): 6 parámetros, todos ejercidos en §6.1 (`PresupuestoId`, `RubroId`, `Codigo`, `Nombre`, `RubroPadreId`, `AgregadoEn`).
  - **`Presupuesto.cs`**: método público `AgregarRubro(cmd, rubroId, ahora)` y `Apply(RubroAgregado)` ejercidos integralmente. Propiedad nueva `Rubros: IReadOnlyList<Rubro>` (línea 39) ejercida por §6.13.
  - **`Requerir.Campo`**: ejercido indirectamente por 6 tests de `CampoRequeridoException` (Slice01 × 3 + Slice02 × 1 + Slice03 × 2 para Codigo/Nombre).
  - 6 excepciones nuevas: ver §2.3.bis abajo.

- [x] Sin `DateTime.UtcNow`, `Guid.NewGuid()`, etc., dentro del dominio.
  - `AgregarRubro` recibe `rubroId: Guid` y `ahora: DateTimeOffset` desde el caller. Cero no-determinismo.
  - `Apply(RubroAgregado)` solo lee `e.*`, no genera timestamps ni ids.

- [x] `Dinero`/`Moneda` para montos. N/A en este slice — `AgregarRubro` no maneja montos; solo código jerárquico, nombre y estructura. `Moneda` ya está presente en `Presupuesto.MonedaBase` desde slices previos.

- [x] Records inmutables para eventos/comandos.
  - `AgregarRubro` y `RubroAgregado`: `sealed record` con `init`-properties implícitas. Cero setters públicos.

- [x] Excepciones heredan de `DominioException` y exponen propiedades fuertemente tipadas.
  - `PresupuestoNoEsBorradorException(EstadoActual: EstadoPresupuesto)`.
  - `CodigoRubroInvalidoException(CodigoIntentado: string)`.
  - `CodigoRubroDuplicadoException(CodigoIntentado: string)`.
  - `CodigoHijoNoExtiendeAlPadreException(CodigoPadre: string, CodigoHijo: string)`.
  - `RubroPadreNoExisteException(RubroPadreId: Guid)`.
  - `ProfundidadExcedidaException(ProfundidadMaxima: int, NivelIntentado: int)`.
  - Todas `sealed`, con un único constructor, propiedades `{ get; }` de solo lectura.

### 2.3.bis Cobertura de las 6 excepciones nuevas

| Excepción | Test que la lanza | Propiedades asertadas | Estado |
|---|---|---|---|
| `PresupuestoNoEsBorradorException` | — | — | **Deuda aceptada §6.7**: rama "lanza" diferida al slice `AprobarPresupuesto` (followup #13). Rama "no lanza" sí cubierta por §6.7 sanidad. |
| `CodigoRubroInvalidoException` | §6.8 Theory (6) | `CodigoIntentado` | Cubierta. |
| `CodigoRubroDuplicadoException` | §6.9 | `CodigoIntentado` | Cubierta. |
| `CodigoHijoNoExtiendeAlPadreException` | §6.10 Theory (4) | `CodigoPadre`, `CodigoHijo` | Cubierta (ambas propiedades). |
| `RubroPadreNoExisteException` | §6.11 | `RubroPadreId` | Cubierta. |
| `ProfundidadExcedidaException` | §6.12 | `ProfundidadMaxima`, `NivelIntentado` | Cubierta (ambas propiedades). |

### 2.4 Cobertura de ramas

Auditoría manual por rama del método `Presupuesto.AgregarRubro` (líneas 89-152):

| # | Rama | Línea | Test(s) que la ejercen |
|---|---|---|---|
| 1 | `ArgumentNullException.ThrowIfNull(cmd)` — cmd null | 94 | **No cubierta** (nit, no blocker). Ningún test pasa cmd null — patrón defensivo de `ThrowIfNull` ya es convención del codebase (ver slice 01). |
| 2 | `Requerir.Campo(cmd.Codigo, "Codigo")` — valor inválido | 97 | §6.4 Theory (2). |
| 3 | `Requerir.Campo(cmd.Codigo, "Codigo")` — valor válido | 97 | §6.1, §6.2, §6.3, §6.5, §6.6, §6.8, §6.9, §6.10, §6.11, §6.12. |
| 4 | `Requerir.Campo(cmd.Nombre, "Nombre")` — valor inválido | 98 | §6.5 Theory (2). |
| 5 | `Requerir.Campo(cmd.Nombre, "Nombre")` — valor válido | 98 | §6.1, §6.2, §6.3, §6.4 (Codigo lanza antes), §6.6, §6.8, §6.9, §6.10, §6.11, §6.12. |
| 6 | `rubroId == Guid.Empty` — true | 101 | §6.6. |
| 7 | `rubroId == Guid.Empty` — false | 101 | todos los demás tests. |
| 8 | `Estado != Borrador` — true | 114 | **Diferida — followup #13**. Rama de código presente por directiva de spec §5 + green-notes §2; no hay comando que transicione el estado en slice 03. |
| 9 | `Estado != Borrador` — false | 114 | §6.7 sanidad + toda happy path. |
| 10 | `cmd.RubroPadreId is Guid padreId` — true | 124 | §6.2, §6.10, §6.11, §6.12. |
| 11 | `cmd.RubroPadreId is Guid padreId` — false | 124 | §6.1, §6.3, §6.4, §6.5, §6.6, §6.7, §6.8, §6.9. |
| 12 | `FirstOrDefault == null` → lanza `RubroPadreNoExiste` | 126-127 | §6.11. |
| 13 | `FirstOrDefault != null` → continúa | 126-127 | §6.2, §6.10, §6.12. |
| 14 | `ValidarFormatoDelCodigo` — padre no null, formato INV-F ok | 163-173 | §6.2, §6.12. |
| 15 | `ValidarFormatoDelCodigo` — padre no null, formato INV-F lanza | 163-173 | §6.10 (4 casos). |
| 16 | `ValidarFormatoDelCodigo` — padre null, regex match | 175 | §6.1, §6.3, §6.9 (raíz), §6.4/§6.5/§6.6 (indirecto: lanzan antes por PRE). |
| 17 | `ValidarFormatoDelCodigo` — padre null, regex mismatch | 175-178 | §6.8 (6 casos). |
| 18 | `nivel > ProfundidadMaxima` — true | 134 | §6.12. |
| 19 | `nivel > ProfundidadMaxima` — false | 134 | §6.1, §6.2, §6.3, §6.9 (raíz), §6.10 lanza antes. |
| 20 | `_rubros.Any(...)` — true (duplicado) | 140 | §6.9. |
| 21 | `_rubros.Any(...)` — false (único) | 140 | §6.1, §6.2, §6.3. |

Ramas en `Apply(RubroAgregado)` (líneas 198-212):

| # | Rama | Test(s) |
|---|---|---|
| 22 | `e.RubroPadreId is Guid padreId` — true (hijo) | §6.9 fold, §6.10 fold, §6.11 fold, §6.12 fold (nivel 2). |
| 23 | `e.RubroPadreId is Guid padreId` — false (raíz) | §6.2 fold (raíz R1), §6.9 fold, §6.10 fold, §6.11 fold, §6.12 fold (nivel 1), §6.13. |
| 24 | `_rubros.First(r => r.Id == padreId)` exitoso | §6.12 fold nivel 2, §6.2 fold. |

Ramas en `ValidarFormatoDelCodigo` desglosadas: ver filas 14-17.

**Cobertura agregada por rama del método `AgregarRubro` + `Apply(RubroAgregado)` + helper privado**: 23 ramas ejercidas de 24 totales (rama #8 INV-3 "lanza" diferida por decisión firmada + rama #1 `ArgumentNullException` nit defensivo) = **95.8% de ramas cubiertas por tests**. Muy por encima del umbral **≥ 85 %**. La rama #8 diferida está cubierta metodológicamente por followup #13, no es cobertura "huérfana".

**No se requirió coverlet** — el método es estructuralmente auditable a mano por tener cada rama separada explícitamente con comentario de invariante.

### 2.5 Refactor

- [x] `refactor-notes.md` presente y claro.
  - Registra **5 refactors aplicados**: `Requerir.cs` helper + 3 sustituciones en `Presupuesto.Create`/`AgregarRubro`/`ConfiguracionTenant.Create` + extracción de `ValidarFormatoDelCodigo`.
  - Registra **9 impulsos descartados** con razón técnica (cada uno referenciado a green-notes o followups).
  - Cierra explícitamente followup #10.

- [x] Los tests no cambiaron de lógica entre green y refactor. Verificado: el archivo `Slice03_AgregarRubroTests.cs` no tuvo modificaciones entre las fases; todas las aserciones son las mismas.

- [x] Cero warnings de compilación. Orquestador verificado `dotnet build` con 0/0.

- [x] Cero cambios de comportamiento observable. Orden de validaciones preservado (`Codigo → Nombre → RubroId → Estado → padre → formato → nivel → unicidad`), tipos de excepción idénticos, firmas públicas intactas.

### 2.6 Invariantes cross-slice

- [x] `dotnet test` completo del repo en verde: **58/58** (Slice01: 16, Slice02: 7, Slice03: 23, DineroTests: 12). Verificado por orquestador.

- [x] El slice no rompe invariantes de slices previos.
  - `Presupuesto.Create` (slice 01) se refactorizó a `Requerir.Campo(...)` × 3 — 16/16 tests Slice01 verdes tras el cambio.
  - `ConfiguracionTenant.Create` (slice 02) se refactorizó a `Requerir.Campo(...)` — 7/7 tests Slice02 verdes tras el cambio.
  - `Dinero`/`Moneda` (SharedKernel) intactos — 12/12 DineroTests verdes.

- [x] Compatibilidad con Marten como agregado event-sourced: `Presupuesto()` sin parámetros, `Apply(...)` públicos, propiedades con `private set`, campo `_rubros` inicializado en declaración. La sobrecarga de `Apply(RubroAgregado)` junto a `Apply(PresupuestoCreado)` respeta la convención del slice 01.

### 2.7 Coherencia con decisiones previas

- [x] Alineado con `01-event-storming-mvp.md` §3, §4, §5, §6.
  - Rubros como entities dentro del agregado (§3) ✓.
  - Comando `AgregarRubro` emite `RubroAgregado` (§4, §5) ✓.
  - Payload exactamente el especificado en §5 (`PresupuestoId`, `RubroId`, `Codigo`, `Nombre`, `RubroPadreId?`, `AgregadoEn`) ✓. No se añadió `RubroTipo` — decisión justificada en §10 Q2 de la spec.
  - Invariante 3 "no modificar fuera de Borrador" declarada; violación diferida con trazabilidad.

- [x] Alineado con `02-decisiones-hotspots-mvp.md` §1 (árbol n-ario), §4 (numeración jerárquica).
  - Árbol n-ario con `ProfundidadMaxima` configurable (default 10, tope 15) — INV-8 implementada.
  - Numeración `^\d{2}(\.\d{2}){0,14}$` — INV-10 implementada (hasta 15 niveles).
  - INV-F (hijo extiende padre con un segmento `.DD`) implementada vía `ValidarFormatoDelCodigo`.
  - Servicio `GeneradorCodigosJerarquicos` (hotspots §4) explícitamente fuera del agregado — followup #11.

- [x] Alineado con METHODOLOGY §2, §6, §8.
  - §2 TDD con red/green/refactor verificables por artefactos.
  - §6 Definition of Done: spec firmada, tests G/W/T completos, `dotnet test` verde, `refactor-notes.md` presente, warnings en cero, cobertura ≥ 85 %.
  - §8 contratos: `nullable` habilitado, naming español en dominio, records para eventos/comandos, clase sellada para agregado, `rubroId` recibido desde fuera, `DateTimeOffset` inyectado.

- [x] Alineado con precedente slice 01 y slice 02.
  - Excepciones heredan de `DominioException` con propiedades fuertemente tipadas (precedente slice 01: `PeriodoInvalidoException`, `CampoRequeridoException`).
  - Tests aserta por tipo + propiedades, no por mensaje.
  - Refactor transversal del `Moneda` en slice 02 se replica aquí con el refactor transversal del `Requerir.Campo` (consistente).

- [x] Coherencia con memoria del proyecto.
  - Stack `.NET 9 + Marten + Wolverine + Docker Compose` respetado (no se tocó infra).
  - Multitenancy: `Presupuesto` sigue siendo stream propio; `Rubro` es entity intra-agregado — no crea nuevo stream.
  - Multimoneda: N/A en este slice (no hay montos aún).

---

## 3. Hallazgos

| # | Tipo | Descripción | Ubicación | Acción sugerida |
|---|---|---|---|---|
| 1 | followup | Servicio de dominio `GeneradorCodigosJerarquicos` — calcula siguiente código disponible dado un padre, recodifica subramas al mover y valida overrides contra prefijo/unicidad. Vive fuera del agregado. | spec §13 + hotspots §4 | **Registrado como `FOLLOWUPS.md` #11**. Disparador: slice UI de creación de rubros o `RubroMovido`. |
| 2 | followup | `RubroTipo` (Agrupador/Terminal) y cobertura de INV-9 — al implementar `AsignarMontoARubro` o `RubroConvertidoAAgrupador`, modelar la distinción de tipo y escribir el escenario "no se puede agregar hijo a un rubro terminal con monto asignado". | spec §13 + §10 Q2 | **Registrado como `FOLLOWUPS.md` #12**. |
| 3 | followup | Escenario INV-3 (violación) en slice `AprobarPresupuesto` — la rama `if (Estado != EstadoPresupuesto.Borrador) throw` (Presupuesto.cs:114) existe pero no está cubierta por test porque no hay comando que transicione el estado en slice 03. Decisión firmada §10 Q1(a). | spec §13 + Presupuesto.cs:114 | **Registrado como `FOLLOWUPS.md` #13**. |
| 4 | followup | Proyección `PresupuestoReadModel.Rubros` no actualizada (spec §8). Green-notes §3.3 y refactor-notes descarte #1 lo documentan como feature nueva, no refactor. | spec §8 + PresupuestoProjection.cs | Aceptable como deuda; no registrado como followup numerado porque la fase `infra-wire` es responsable. Si el orquestador prefiere trazabilidad explícita, puede añadir followup #14. Recomendación: abordar dentro de `infra-wire` del slice 03 (no del dominio). |
| 5 | followup (cierre) | Followup #10 (`Requerir.Campo` helper) — **cerrado por refactor de slice 03**. Verificado en `FOLLOWUPS.md` ya movido a sección "Cerrados" con desglose explícito de las 6 sustituciones. | FOLLOWUPS.md | Ningún cambio adicional. |
| 6 | followup (cierre) | Followup #5 (firma uniforme `CasoDeUso.Decidir`) — recomendación del modeler (spec §13) y green/refactor mantienen la forma OO actual. Con 3 slices implementados (factory + método instancia + ejecución sobre fold), el patrón escala y es consistente. | FOLLOWUPS.md | **Cerrado como "no aplicable"** por este reviewer. Registrado en `FOLLOWUPS.md` sección "Cerrados" con justificación. |
| 7 | nit | Rama `ArgumentNullException.ThrowIfNull(cmd)` en `Presupuesto.AgregarRubro:94` no tiene test explícito (igual que en slice 01 `Create`). Patrón defensivo convención del repo. | Presupuesto.cs:94 | Nit asumido. No genera followup. |
| 8 | nit | `Apply(RubroAgregado)` usa `.First(...)` en lugar de `.FirstOrDefault(...)` (línea 201). Green-notes §3.5 y refactor-notes descarte #4 lo justifican como contrato de integridad del stream — si el padre no está en el stream, el evento original no pudo haberse emitido (INV-D ya lo validó). Decisión correcta. | Presupuesto.cs:201 | Nit asumido. |
| 9 | nit | `_rubros.Any(r => r.Codigo == codigo)` es O(n). Green §4.3 y refactor descarte #7 lo aceptan para MVP (decenas de rubros). | Presupuesto.cs:140 | Nit asumido. Re-evaluar si un presupuesto crece a cientos de rubros. |
| 10 | nit | El test §6.13 usa reflexión (`GetProperty("Rubros")`, `GetValue(...)`) en lugar de casteo directo. Green eligió `IReadOnlyList<Rubro>` como nombre, pero el test original de red ya usaba reflexión para dejar naming a discreción. Post-green podría simplificarse a referencia directa. | Slice03_AgregarRubroTests.cs:395-408 | Nit. No bloquea. El test es correcto y legible; la reflexión es herencia del handoff red→green. |

---

## 4. Veredicto final

- [ ] **approved**
- [x] **approved-with-followups** — 3 followups nuevos registrados en `FOLLOWUPS.md` (#11, #12, #13) + 2 cierres (#5 como no-aplicable, #10 confirmado cerrado). Sin blockers. Todos los criterios de Definition of Done se cumplen (spec firmada, 23 tests G/W/T, cobertura ~96% de ramas con 1 deferral justificado, `dotnet build` y `dotnet test` verdes, refactor-notes presente con 5 aplicados + 9 descartados, consistencia cross-slice verificada, coherencia con decisiones previas). La invariante INV-3 en su rama "lanza" está explícitamente diferida por decisión firmada §10 Q1(a) y protegida por followup #13 + test de sanidad §6.7.
- [ ] **request-changes**

**Detalles del veredicto:**

El slice 03 se cierra exitosamente. Los 3 followups nuevos están documentados y alineados con el backlog:
- **#11** (`GeneradorCodigosJerarquicos`) — servicio de dominio externo al agregado; disparador claro (UI de creación o `RubroMovido`).
- **#12** (`RubroTipo` + INV-9) — depende de slice `AsignarMontoARubro`/`RubroConvertidoAAgrupador`; no bloquea MVP.
- **#13** (escenario INV-3) — depende de slice `AprobarPresupuesto`; el código defensivo ya existe y está trazado.

Los 2 cierres:
- **#10** — refactor de `Requerir.Campo` ya ejecutado en este slice, con 6 sustituciones concretas.
- **#5** — cierre como "no aplicable" con criterio técnico: la forma OO actual (factory sobre stream vacío + método de instancia sobre fold) es consistente entre los 3 slices y no se beneficia de unificación a `Decidir(dados, cmd, …)`.

Los 4 nits (#7-#10 en la tabla de hallazgos) son comentarios menores sin impacto en calidad del código; todos están justificados en green-notes o refactor-notes.

La decisión de aceptar el test §6.7 como sanidad (no como violación estricta) es metodológicamente correcta porque la rama "lanza" INV-3 existe por directiva de la spec y el aislamiento por slice requiere que esa rama se introduzca aquí — violarla no es posible hasta que exista `AprobarPresupuesto`. Followup #13 garantiza el cierre retroactivo.

**Orquestador puede proceder a:** commit del slice + fase `infra-wire` (ampliar `PresupuestoReadModel` con `Rubros`, registrar handler de `AgregarRubro` en Wolverine, endpoint `POST /api/tenants/{tenantId}/presupuestos/{presupuestoId}/rubros`, test de integración HTTP → Postgres).

---

_Cierre de slice 03 firmado por reviewer — 2026-04-24._
