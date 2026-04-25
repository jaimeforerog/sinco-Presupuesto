# Red notes — Slice 05 — AprobarPresupuesto

**Autor:** red
**Fecha:** 2026-04-24
**Spec consumida:** `slices/05-aprobar-presupuesto/spec.md` (firmada 2026-04-24, Q2=(a) — no validar `PeriodoFin >= ahora.Date`).

---

## 1. Tests escritos

Archivo: `tests/SincoPresupuesto.Domain.Tests/Slices/Slice05_AprobarPresupuestoTests.cs`.

Un test por escenario de la spec §6 (12 escenarios). §6.3 se expande con `[Theory]` sobre `{"", "   ", "\t"}` (el caso `null` se sustituye por `"\t"` por la misma razón documentada en slice 04 §6 de las desviaciones — el record declara `AprobadoPor` como `string` no nullable). Total: **12 métodos / 14 casos xUnit** tras expansión.

| # | Test | Escenario spec §6 | Tipo | Casos |
|---|---|---|---|---|
| 1 | `AprobarPresupuesto_con_un_terminal_en_MonedaBase_emite_PresupuestoAprobado_con_payload_completo` | 6.1 happy simple | Fact | 1 |
| 2 | `AprobarPresupuesto_con_arbol_Agrupador_y_dos_terminales_suma_solo_los_terminales` | 6.2 happy árbol | Fact | 1 |
| 3 | `AprobarPresupuesto_con_AprobadoPor_vacio_o_whitespace_normaliza_a_sistema` | 6.3 normalización PRE-4 | Theory | 3 (`""`, `"   "`, `"\t"`) |
| 4 | `AprobarPresupuesto_sin_rubros_lanza_PresupuestoSinMontosException` | 6.4 PRE-2 sin rubros | Fact | 1 |
| 5 | `AprobarPresupuesto_con_todos_los_terminales_en_cero_lanza_PresupuestoSinMontosException` | 6.5 PRE-2 todos cero | Fact | 1 |
| 6 | `AprobarPresupuesto_con_terminales_en_moneda_distinta_a_MonedaBase_lanza_AprobacionConMultimonedaNoSoportada` | 6.6 PRE-3 multimoneda | Fact | 1 |
| 7 | `AprobarPresupuesto_sobre_presupuesto_ya_aprobado_lanza_PresupuestoNoEsBorrador` | 6.7 INV-3 ya aprobado | Fact | 1 |
| 8 | `AgregarRubro_sobre_presupuesto_aprobado_lanza_PresupuestoNoEsBorrador` | 6.8 INV-3 retroactivo (a) — cierra followup #13 | Fact | 1 |
| 9 | `AsignarMontoARubro_sobre_presupuesto_aprobado_lanza_PresupuestoNoEsBorrador` | 6.9 INV-3 retroactivo (b) — cierra followup #13 | Fact | 1 |
| 10 | `Fold_de_PresupuestoAprobado_deja_el_agregado_en_Aprobado_con_baseline_completo` | 6.10 fold | Fact | 1 |
| 11 | `AprobarPresupuesto_con_un_terminal_en_cero_y_otro_con_monto_calcula_total_solo_con_los_positivos` | 6.11 ignora terminales en cero | Fact | 1 |
| 12 | `AprobarPresupuesto_con_un_Agrupador_y_un_terminal_hijo_solo_suma_el_terminal` | 6.12 Agrupadores no aportan (defensivo) | Fact | 1 |

Expansión por Theory: **14 casos xUnit totales** (12 métodos distintos).

## 2. Verificación de estado rojo

Comando usado:

```bash
dotnet test tests/SincoPresupuesto.Domain.Tests/SincoPresupuesto.Domain.Tests.csproj \
  --filter "FullyQualifiedName~Slice05" --nologo --no-build
```

Resultado:

```
Con error! - Con error: 14, Superado: 0, Omitido: 0, Total: 14
```

**14/14 casos fallan con la razón correcta** (ninguno por "no compila").

Razón de fallo por test:

| # | Test | Razón esperada de fallo |
|---|---|---|
| 1 | `AprobarPresupuesto_con_un_terminal_en_MonedaBase_…_payload_completo` | `NotImplementedException` en `Presupuesto.AprobarPresupuesto` (stub). |
| 2 | `AprobarPresupuesto_con_arbol_Agrupador_y_dos_terminales_…` | `NotImplementedException` en `Presupuesto.AprobarPresupuesto` (stub). |
| 3 | `AprobarPresupuesto_con_AprobadoPor_vacio_o_whitespace_…` (x3) | `NotImplementedException` en `Presupuesto.AprobarPresupuesto` (stub). |
| 4 | `AprobarPresupuesto_sin_rubros_…_PresupuestoSinMontosException` | Se esperaba `PresupuestoSinMontosException(PresupuestoId=…)` — se lanzó `NotImplementedException`. |
| 5 | `AprobarPresupuesto_con_todos_los_terminales_en_cero_…` | Se esperaba `PresupuestoSinMontosException(PresupuestoId=…)` — se lanzó `NotImplementedException`. |
| 6 | `AprobarPresupuesto_con_terminales_en_moneda_distinta_…` | Se esperaba `AprobacionConMultimonedaNoSoportadaException(…, [R1, R3], COP)` — se lanzó `NotImplementedException`. |
| 7 | `AprobarPresupuesto_sobre_presupuesto_ya_aprobado_…` | `TargetInvocationException` envolviendo `NotImplementedException` en `Presupuesto.Apply(PresupuestoAprobado)` durante el fold del Given (la aprobación previa). |
| 8 | `AgregarRubro_sobre_presupuesto_aprobado_…` | `TargetInvocationException` envolviendo `NotImplementedException` en `Presupuesto.Apply(PresupuestoAprobado)` durante el fold del Given. |
| 9 | `AsignarMontoARubro_sobre_presupuesto_aprobado_…` | `TargetInvocationException` envolviendo `NotImplementedException` en `Presupuesto.Apply(PresupuestoAprobado)` durante el fold del Given. |
| 10 | `Fold_de_PresupuestoAprobado_…_baseline_completo` | `TargetInvocationException` envolviendo `NotImplementedException` en `Presupuesto.Apply(PresupuestoAprobado)`. |
| 11 | `AprobarPresupuesto_con_un_terminal_en_cero_y_otro_con_monto_…` | `NotImplementedException` en `Presupuesto.AprobarPresupuesto` (stub). |
| 12 | `AprobarPresupuesto_con_un_Agrupador_y_un_terminal_hijo_…` | `NotImplementedException` en `Presupuesto.AprobarPresupuesto` (stub). |

Los tests de ramas que requieren un `PresupuestoAprobado` previo en el historial (6.7/6.8/6.9/6.10) fallan en la fase Given porque `Apply(PresupuestoAprobado)` también es stub. Esto sigue el mismo patrón que slice 04: cualquier test cuya razón de fallo termine en `NotImplementedException` desde el stub correspondiente cuenta como rojo válido (no compila ≠ fallo). Cuando green implemente el `Apply`, el rojo se traslada al método de comando — y la solución verde cubre ambas cosas a la vez.

Ningún test falla por compilación — la solución entera compila con **0 advertencias / 0 errores**.

## 3. Regresión en otros slices

Comando usado:

```bash
dotnet test tests/SincoPresupuesto.Domain.Tests/SincoPresupuesto.Domain.Tests.csproj \
  --filter "FullyQualifiedName!~Slice05" --nologo --no-build
# salida: Correctas! - Con error: 0, Superado: 128, Omitido: 0, Total: 128
```

**128/128 casos verdes sin Slice05** — no hay regresión en slices 00, 01, 02, 03 ni 04. Las propiedades nuevas en `Presupuesto` (`MontoTotal`, `SnapshotTasas`, `AprobadoEn`, `AprobadoPor`) y los stubs (`AprobarPresupuesto`, `Apply(PresupuestoAprobado)`) no alteran ningún camino cubierto por tests previos: solo se accede a las propiedades en los tests del propio slice 05, y los stubs solo se invocan cuando un test del slice los dispara.

## 4. Código de producción tocado

Se agregaron **stubs mínimos** en `src/`. Todos los cuerpos con lógica lanzan `NotImplementedException` — no hay validación ni normalización introducida aquí.

### Archivos nuevos

1. **`src/SincoPresupuesto.Domain/Presupuestos/Commands/AprobarPresupuesto.cs`**
   - `record AprobarPresupuesto(string AprobadoPor = "sistema")` alineado con spec §2.

2. **`src/SincoPresupuesto.Domain/Presupuestos/Events/PresupuestoAprobado.cs`**
   - `record PresupuestoAprobado(Guid PresupuestoId, Dinero MontoTotal, IReadOnlyDictionary<Moneda, decimal> SnapshotTasas, DateTimeOffset AprobadoEn, string AprobadoPor)` alineado con spec §3 y §12.3.

3. **`src/SincoPresupuesto.Domain/SharedKernel/PresupuestoSinMontosException.cs`** — propiedad `PresupuestoId: Guid`. Hereda de `DominioException` (PRE-2, spec §12.1).

4. **`src/SincoPresupuesto.Domain/SharedKernel/AprobacionConMultimonedaNoSoportadaException.cs`** — propiedades `PresupuestoId: Guid`, `RubrosConMonedaDistinta: IReadOnlyList<Guid>`, `MonedaBase: Moneda`. Hereda de `DominioException` (PRE-3, spec §12.1).

### Archivos modificados

5. **`src/SincoPresupuesto.Domain/Presupuestos/Presupuesto.cs`**
   - Se añadieron las propiedades nullable según spec §12.5:
     - `public Dinero MontoTotal { get; private set; }` (default `default(Dinero)` antes del fold).
     - `public IReadOnlyDictionary<Moneda, decimal> SnapshotTasas { get; private set; } = new Dictionary<Moneda, decimal>();` (inicializado a vacío para evitar NRE).
     - `public DateTimeOffset? AprobadoEn { get; private set; }` (null antes del fold).
     - `public string? AprobadoPor { get; private set; }` (null antes del fold).
   - Se añadió `public PresupuestoAprobado AprobarPresupuesto(Commands.AprobarPresupuesto cmd, DateTimeOffset ahora) => throw new NotImplementedException();`.
   - Se añadió `public void Apply(PresupuestoAprobado e) => throw new NotImplementedException();`.
   - El resto (`Crear`, `AgregarRubro`, `AsignarMontoARubro`, `Apply(PresupuestoCreado)`, `Apply(RubroAgregado)`, `Apply(MontoAsignadoARubro)`) **no se tocó**.

Ningún stub contiene lógica de dominio. Green es responsable de introducir cada pieza en respuesta a los tests rojos.

## 5. Decisión sobre §6.12 (escenario defensivo)

La spec §6.12 describe un caso "boundary" (Agrupadores no aportan a `MontoTotal`) y aclara entre paréntesis que el flujo actual ya garantiza que un Agrupador queda con `Monto.EsCero` (slice 04 §6.7 prohíbe asignarle). El brief preguntaba si la spec lo declara como "test no necesario, comportamiento defensivo documentado".

Lectura de la spec §6.12: **el escenario está enumerado dentro del catálogo §6 con Given/When/Then explícitos** — no aparece marcado como omitido. Su valor reside en fijar el contrato "siempre ignorar Agrupadores en la suma" para futuros flujos donde la asignación a Agrupadores cambie. La spec lo lista como escenario activo, así que se incluye como Fact.

Implementación: el test arma R1 Agrupador (con hijo R2 terminal con monto > 0) y aserta `MontoTotal = Dinero(400_000, COP)`. Hoy fallará igual que el resto por `NotImplementedException`; cuando green implemente la lógica con la distinción operacional `_rubros.Any(r => r.PadreId == nodo.Id)` (spec §10 Q2 resuelta y §12.4), el test pasará verde. Mismo precedente operacional que slice 04 §6.7.

## 6. Desviaciones respecto a la spec

- [x] **Sin desviaciones del catálogo §6** — los 12 escenarios de la spec están cubiertos 1:1 (11 Fact + 1 Theory con 3 casos = 14 casos xUnit).
- [x] **§6.3 Theory cases**: el spec enumera `{"", "   ", null}` pero el record `AprobarPresupuesto` declara `AprobadoPor` como `string` (no nullable) con default `"sistema"`. Pasar `null!` explícito produciría warning CS8625 y no representa un caso real vía la API pública. Se sustituyó `null` por `"\t"` (tab) — sigue cubriendo la rama completa de `string.IsNullOrWhiteSpace`. Documentado en el propio test. Mismo patrón usado en slice 04 §6.11 (red-notes §6).
- [x] **§6.12 incluido como test activo** — ver §5 de este documento (no se redujo a 11 tests; la spec lo lista en el catálogo §6).
- [x] **Excepciones nuevas con propiedades fuertemente tipadas** — `PresupuestoSinMontosException(Guid)` y `AprobacionConMultimonedaNoSoportadaException(Guid, IReadOnlyList<Guid>, Moneda)` siguen estrictamente el patrón slice 04 (cada propiedad en el constructor; `DominioException` como base; sin `IDictionary` ni colecciones modificables expuestas).

## 7. Hand-off a green

- [x] Spec firmada: sí (2026-04-24, Q2=(a)).
- [x] Todos los tests compilan: sí (`dotnet build` con 0 errores y 0 warnings).
- [x] 14/14 casos fallan por razón correcta (`NotImplementedException` desde stubs de `AprobarPresupuesto` o `Apply(PresupuestoAprobado)`, o aserción de excepción de dominio no lanzada).
- [x] Sin regresión en slices 00/01/02/03/04 (128/128 verdes al filtrar `!~Slice05`).
- [x] Stubs en `src/` con `NotImplementedException` — **cero lógica** añadida.

**Ready para green.** El próximo agente toma estos tests rojos y los hace verdes implementando, según spec §12.4 y §12.6:

1. `Presupuesto.AprobarPresupuesto(cmd, ahora)` — validación y emisión del evento, en orden (spec §4):
   - PRE-1: `if (Estado != Borrador) throw new PresupuestoNoEsBorradorException(Estado);` (cierra retroactivamente la rama declarada en slices 03/04 — la suma de los §6.7/§6.8/§6.9 ejerce esa rama desde tres puntos distintos).
   - PRE-2: identificar terminales (`!_rubros.Any(otro => otro.PadreId == r.Id)`), filtrar los con `Monto.EsPositivo`. Si la lista resulta vacía → `PresupuestoSinMontosException(Id)`.
   - PRE-3: de los terminales con monto > 0, recoger los con `Monto.Moneda != MonedaBase`. Si la lista no está vacía → `AprobacionConMultimonedaNoSoportadaException(Id, listaDeIds, MonedaBase)` con la lista en orden de aparición en `_rubros`.
   - PRE-4: `var aprobadoPor = string.IsNullOrWhiteSpace(cmd.AprobadoPor) ? "sistema" : cmd.AprobadoPor;`.
   - Cómputo: `var montoTotal = terminalesConMontoPositivo.Aggregate(Dinero.Cero(MonedaBase), (acc, r) => acc + r.Monto);`.
   - Emite: `new PresupuestoAprobado(Id, montoTotal, new Dictionary<Moneda, decimal>(), ahora, aprobadoPor)`.

2. `Presupuesto.Apply(PresupuestoAprobado e)` — fold según spec §12.6: setea `Estado = Aprobado`, `MontoTotal = e.MontoTotal`, `SnapshotTasas = e.SnapshotTasas`, `AprobadoEn = e.AprobadoEn`, `AprobadoPor = e.AprobadoPor`.

3. **Mapeo HTTP** (refactor transversal slice — fuera del proyecto de dominio puro pero anotado por la spec §9 / §12.1): añadir al `switch` de `DomainExceptionHandler.Mapear`:
   - `PresupuestoSinMontosException → 400`
   - `AprobacionConMultimonedaNoSoportadaException → 400`
   - `PresupuestoNoEsBorradorException → 409` (ya existe desde slice 03 §12).
