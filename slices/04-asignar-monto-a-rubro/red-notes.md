# Red notes — Slice 04 — AsignarMontoARubro

**Autor:** red
**Fecha:** 2026-04-24
**Spec consumida:** `slices/04-asignar-monto-a-rubro/spec.md` (firmada 2026-04-24, Q1=(d), commit `b6148c8`).

---

## 1. Tests escritos

Archivo: `tests/SincoPresupuesto.Domain.Tests/Slices/Slice04_AsignarMontoARubroTests.cs`.

Un test por escenario de la spec §6 (14 escenarios). §6.11 se expande con `[Theory]` sobre `{"", "   ", "\t"}`. §6.8 es test de sanidad (mismo patrón que slice 03 §6.7) — ver §5 de este documento. Total: **14 métodos / 16 casos xUnit** tras expansión de Theory.

| # | Test | Escenario spec §6 | Tipo | Casos |
|---|---|---|---|---|
| 1 | `AsignarMontoARubro_primera_asignacion_a_rubro_terminal_emite_MontoAsignadoARubro_con_MontoAnterior_cero` | 6.1 happy path primera asignación (COP = MonedaBase) | Fact | 1 |
| 2 | `AsignarMontoARubro_reasignacion_misma_moneda_emite_evento_con_MontoAnterior_igual_al_monto_previo` | 6.2 reasignación misma moneda | Fact | 1 |
| 3 | `AsignarMontoARubro_reasignacion_cambiando_moneda_emite_evento_con_MontoAnterior_en_moneda_previa` | 6.3 reasignación COP→USD | Fact | 1 |
| 4 | `AsignarMontoARubro_primera_asignacion_en_moneda_distinta_a_MonedaBase_se_permite` | 6.4 primera asignación en USD con MonedaBase=COP | Fact | 1 |
| 5 | `AsignarMontoARubro_con_monto_cero_y_AsignadoPor_vacio_emite_evento_con_AsignadoPor_sistema` | 6.5 monto cero + normalización | Fact | 1 |
| 6 | `AsignarMontoARubro_con_monto_negativo_lanza_MontoNegativoException` | 6.6 INV-2 | Fact | 1 |
| 7 | `AsignarMontoARubro_sobre_rubro_con_hijos_lanza_RubroEsAgrupadorException` | 6.7 INV-NEW-SLICE04-1 | Fact | 1 |
| 8 | `AsignarMontoARubro_sobre_presupuesto_en_Borrador_no_lanza_PresupuestoNoEsBorrador` | 6.8 sanidad INV-3 (violación diferida a AprobarPresupuesto, followup #13) | Fact | 1 |
| 9 | `AsignarMontoARubro_con_RubroId_vacio_lanza_CampoRequerido` | 6.9 PRE-1 | Fact | 1 |
| 10 | `AsignarMontoARubro_con_RubroId_inexistente_lanza_RubroNoExiste` | 6.10 PRE-2 | Fact | 1 |
| 11 | `AsignarMontoARubro_con_AsignadoPor_vacio_o_whitespace_normaliza_a_sistema` | 6.11 PRE-4 normalización | Theory | 3 (`""`, `"   "`, `"\t"`) |
| 12 | `Fold_de_MontoAsignadoARubro_primera_asignacion_deja_el_rubro_con_el_Monto_asignado` | 6.12 fold primera asignación | Fact | 1 |
| 13 | `Fold_de_MontoAsignadoARubro_reasignacion_cambiando_moneda_deja_el_rubro_con_la_moneda_nueva` | 6.13 fold cambio moneda | Fact | 1 |
| 14 | `AsignarMontoARubro_con_Moneda_construida_desde_string_ISO_4217_valida_emite_evento_sin_revalidar` | 6.14 confianza en VO Moneda | Fact | 1 |

Expansión por Theory: **16 casos xUnit totales** (14 métodos distintos).

## 2. Verificación de estado rojo

Comando usado:

```bash
dotnet test tests/SincoPresupuesto.Domain.Tests/SincoPresupuesto.Domain.Tests.csproj \
  --filter "FullyQualifiedName~Slice04" --nologo --no-build
```

Resultado:

```
Con error! - Con error: 15, Superado: 1, Omitido: 0, Total: 16
```

**15 casos fallan con la razón correcta** (ninguno por "no compila"). **1 caso pasa intencionalmente** (test de sanidad §6.8 — ver §5).

Razón de fallo por test:

| # | Test | Razón esperada de fallo |
|---|---|---|
| 1 | `AsignarMontoARubro_primera_asignacion_…_MontoAnterior_cero` | `NotImplementedException` en `Presupuesto.AsignarMontoARubro` (stub). |
| 2 | `AsignarMontoARubro_reasignacion_misma_moneda_…` | `NotImplementedException` en `Presupuesto.Apply(MontoAsignadoARubro)` durante el fold del Given (asignación previa). |
| 3 | `AsignarMontoARubro_reasignacion_cambiando_moneda_…` | `NotImplementedException` en `Presupuesto.Apply(MontoAsignadoARubro)` durante el fold del Given. |
| 4 | `AsignarMontoARubro_primera_asignacion_en_moneda_distinta_a_MonedaBase_…` | `NotImplementedException` en `Presupuesto.AsignarMontoARubro` (stub). |
| 5 | `AsignarMontoARubro_con_monto_cero_y_AsignadoPor_vacio_…` | `NotImplementedException` en `Presupuesto.AsignarMontoARubro` (stub). |
| 6 | `AsignarMontoARubro_con_monto_negativo_…` | Se esperaba `MontoNegativoException(MontoIntentado=Dinero(-1, COP))` — se lanzó `NotImplementedException`. |
| 7 | `AsignarMontoARubro_sobre_rubro_con_hijos_…_RubroEsAgrupador` | Se esperaba `RubroEsAgrupadorException(RubroId=R1)` — se lanzó `NotImplementedException`. |
| 8 | `AsignarMontoARubro_sobre_presupuesto_en_Borrador_no_lanza_…` | **Pasa.** Asserta `NotThrow<PresupuestoNoEsBorradorException>`; el stub lanza `NotImplementedException` (tipo distinto) y la aserción se cumple. Red-protección de regresión futura — ver §5. |
| 9 | `AsignarMontoARubro_con_RubroId_vacio_lanza_CampoRequerido` | Se esperaba `CampoRequeridoException(NombreCampo="RubroId")` — se lanzó `NotImplementedException`. |
| 10 | `AsignarMontoARubro_con_RubroId_inexistente_lanza_RubroNoExiste` | Se esperaba `RubroNoExisteException(RubroId=…)` — se lanzó `NotImplementedException`. |
| 11 | `AsignarMontoARubro_con_AsignadoPor_vacio_o_whitespace_normaliza_a_sistema` (x3) | `NotImplementedException` en `Presupuesto.AsignarMontoARubro` (stub). |
| 12 | `Fold_…_primera_asignacion_deja_el_rubro_con_el_Monto_asignado` | `TargetInvocationException` envolviendo `NotImplementedException` en `Presupuesto.Apply(MontoAsignadoARubro)`. |
| 13 | `Fold_…_reasignacion_cambiando_moneda_…` | `TargetInvocationException` envolviendo `NotImplementedException` en `Presupuesto.Apply(MontoAsignadoARubro)`. |
| 14 | `AsignarMontoARubro_con_Moneda_construida_desde_string_…` | `NotImplementedException` en `Presupuesto.AsignarMontoARubro` (stub). |

Ningún test falla por compilación — la solución entera compila con **0 advertencias / 0 errores** (`dotnet build` exitoso).

## 3. Regresión en otros slices

Comando usado:

```bash
dotnet test tests/SincoPresupuesto.Domain.Tests/SincoPresupuesto.Domain.Tests.csproj \
  --filter "FullyQualifiedName!~Slice04" --nologo --no-build
# salida: Correctas! - Con error: 0, Superado: 109, Omitido: 0, Total: 109
```

**109/109 casos verdes sin Slice04** — no hay regresión en slices 00, 01, 02 ni 03. El stub añadido a `Rubro` (propiedad `Monto`) y los dos métodos stub en `Presupuesto` (`AsignarMontoARubro`, `Apply(MontoAsignadoARubro)`) no alteran ningún camino cubierto por tests previos.

## 4. Código de producción tocado

Se agregaron **stubs mínimos** en `src/`. Todos los cuerpos con lógica lanzan `NotImplementedException` — no hay validación ni normalización introducida aquí.

### Archivos nuevos

1. **`src/SincoPresupuesto.Domain/Presupuestos/Commands/AsignarMontoARubro.cs`**
   - `record AsignarMontoARubro(Guid RubroId, Dinero Monto, string AsignadoPor = "sistema")` alineado con spec §2.

2. **`src/SincoPresupuesto.Domain/Presupuestos/Events/MontoAsignadoARubro.cs`**
   - `record MontoAsignadoARubro(Guid PresupuestoId, Guid RubroId, Dinero Monto, Dinero MontoAnterior, DateTimeOffset AsignadoEn, string AsignadoPor)` alineado con spec §3 y §12.3.

3. **`src/SincoPresupuesto.Domain/SharedKernel/RubroNoExisteException.cs`** — propiedad `RubroId: Guid` (PRE-2, spec §12.1).

4. **`src/SincoPresupuesto.Domain/SharedKernel/MontoNegativoException.cs`** — propiedad `MontoIntentado: Dinero` (INV-2, spec §12.1).

5. **`src/SincoPresupuesto.Domain/SharedKernel/RubroEsAgrupadorException.cs`** — propiedad `RubroId: Guid` (INV-NEW-SLICE04-1, spec §12.1).

Las tres excepciones heredan de `DominioException`, tienen un único constructor público que guarda la propiedad, y siguen el patrón de `RubroPadreNoExisteException` / `CodigoRubroInvalidoException` de slice 03.

### Archivos modificados

6. **`src/SincoPresupuesto.Domain/Presupuestos/Rubro.cs`**
   - Se añadió `public Dinero Monto { get; set; }`. Sin valor inicial: queda en `default(Dinero)` tras `Apply(RubroAgregado)` bajo el stub. Green inicializará a `Dinero.Cero(MonedaBase)` en el fold y mutará en `Apply(MontoAsignadoARubro)`.
   - Desviación respecto al brief: el brief sugería `private set`; aquí se usó `set` público. Justificación — la entity `Rubro` se construye en `Presupuesto.Apply(RubroAgregado)` con object initializer (`new Rubro { Id = …, Codigo = …, … }`), y la mutación posterior en `Apply(MontoAsignadoARubro)` (verde) necesita escribir `Monto` desde fuera del object initializer. `init` bloquearía la mutación y `private set` bloquearía el initializer externo. El acceso público es transitorio — green puede endurecerlo moviendo la construcción a un constructor interno si lo prefiere. Anotado en §6 Desviaciones.

7. **`src/SincoPresupuesto.Domain/Presupuestos/Presupuesto.cs`**
   - Se añadió `public MontoAsignadoARubro AsignarMontoARubro(Commands.AsignarMontoARubro cmd, DateTimeOffset ahora) => throw new NotImplementedException();`.
   - Se añadió `public void Apply(MontoAsignadoARubro e) => throw new NotImplementedException();`.
   - `Create`, `AgregarRubro`, `Apply(PresupuestoCreado)` y `Apply(RubroAgregado)` existentes **no se tocaron**.

Ningún stub contiene lógica de dominio. Green es responsable de introducir cada pieza en respuesta a los tests rojos.

## 5. Decisión sobre el test de sanidad §6.8

El spec §6.8 indica que el escenario de violación de INV-3 (estado ≠ Borrador) se **difiere** al slice `AprobarPresupuesto` (followup #13) y que aquí sólo se cubre el camino "estado Borrador → no lanza" vía el test de sanidad. Implementado como `act.Should().NotThrow<PresupuestoNoEsBorradorException>()` en el test #8.

Hoy pasa porque el stub lanza `NotImplementedException` (tipo distinto de `PresupuestoNoEsBorradorException`) — no es un "rojo estricto". Justificación idéntica a la de slice 03 §5 (ver `slices/03-agregar-rubro/red-notes.md`):

- El comportamiento que protege es **negativo** (no lanzar INV-3 en Borrador); no existe un "código mínimo para pasarlo" que green pueda añadir, porque green no introducirá una rama que bloquee Borrador.
- Sirve como **red-protección de regresión futura**: cuando el slice `AprobarPresupuesto` introduzca la validación INV-3 en `AsignarMontoARubro`, este test evita que la validación sea demasiado agresiva y bloquee Borrador por error.
- El nombre conserva referencia explícita a INV-3 para trazabilidad con followup #13.
- Alternativa descartada: invertir a `Should().NotThrow()` (sin tipo). Rechazada porque haría el test fallar hoy con `NotImplementedException` (razón técnica del stub, no semántica del dominio) y dejaría de discriminar un fallo de INV-3 genuino.

Documentado aquí para que el reviewer lo audite en la pasada final del slice.

## 6. Desviaciones respecto a la spec

- [x] **Sin desviaciones del catálogo §6 de escenarios** — los 14 escenarios de la spec están cubiertos 1:1 (13 Fact + 1 Theory con 3 casos = 16 casos xUnit).
- [x] **Acceso de `Rubro.Monto`**: se usó `{ get; set; }` público en vez de `private set` (brief §2 opción (a)). Motivo: la entity `Rubro` se construye desde `Presupuesto.Apply(RubroAgregado)` con object initializer externo, por lo que `private set` rompería esa ruta. `init` rompería el fold de `Apply(MontoAsignadoARubro)`. El acceso público es aceptable para la fase red y green puede endurecerlo (p.ej. moviendo la construcción a un constructor interno de `Rubro` con visibilidad controlada). Sin impacto sobre los asertos de los tests.
- [x] **§6.11 Theory cases**: el spec enumera `{ "", "   ", null }` pero el record `AsignarMontoARubro` declara `AsignadoPor` como `string` (no nullable) con default `"sistema"`. Pasar `null!` explícito produciría un warning CS8625 y no refleja un caso real posible vía la API pública. Se sustituyó `null` por `"\t"` (tab), que sigue la intención de "whitespace no espacio simple" y cubre la ruta completa de normalización de `string.IsNullOrWhiteSpace`. Documentado en el propio test.
- [x] **§6.14 consolidación vs. §6.1 (ofrecida por el brief)**: se mantuvo como test separado. Motivo: el spec §6.14 lo enumera como escenario distinto (la carga es declarativa: "el dominio no revalida ISO 4217"), usa `new Moneda("EUR")` construida desde string (no el atajo `Moneda.COP`) y asserta sobre `Monto.Moneda.Codigo` explícitamente. El costo marginal es un Fact adicional; el beneficio es documentación ejecutable de la decisión §10 del spec.

## 7. Hand-off a green

- [x] Spec firmada: sí (2026-04-24, Q1=(d)).
- [x] Todos los tests compilan: sí (`dotnet build` con 0 errores y 0 warnings).
- [x] 15 casos fallan por razón correcta (`NotImplementedException` desde stubs de `AsignarMontoARubro` o `Apply(MontoAsignadoARubro)`, o aserción de excepción de dominio no lanzada).
- [x] 1 caso (§6.8 sanidad) pasa intencionalmente — ver §5.
- [x] Sin regresión en slices 00, 01, 02 y 03 (109/109 verdes al filtrar `!~Slice04`).
- [x] Stubs en `src/` con `NotImplementedException` — **cero lógica** añadida.

**Ready para green.** El próximo agente toma estos tests rojos y los hace verdes implementando:

1. `Presupuesto.AsignarMontoARubro(cmd, ahora)` — validación y emisión del evento con:
   - PRE-1 (`CampoRequeridoException` con `NombreCampo="RubroId"` cuando `cmd.RubroId == Guid.Empty`),
   - PRE-2 (`RubroNoExisteException(cmd.RubroId)` cuando el rubro no está en `_rubros`),
   - PRE-3 / INV-2 (`MontoNegativoException(cmd.Monto)` cuando `cmd.Monto.Valor < 0m`),
   - PRE-4 (normalización `cmd.AsignadoPor` vacío/whitespace → `"sistema"`),
   - INV-3 declarada (la rama `if (Estado != Borrador) throw new PresupuestoNoEsBorradorException(Estado);` existe pero no se ejercita en este slice — followup #13),
   - INV-NEW-SLICE04-1 (`RubroEsAgrupadorException(cmd.RubroId)` cuando `_rubros.Any(r => r.PadreId == cmd.RubroId)`),
   - cálculo de `MontoAnterior` según spec §3 y §12.2: `rubroDestino.Monto.EsCero ? Dinero.Cero(cmd.Monto.Moneda) : rubroDestino.Monto`.

2. `Presupuesto.Apply(MontoAsignadoARubro e)` — localizar el rubro con `e.RubroId` en `_rubros` y mutar su `Monto` a `e.Monto` (spec §12.5).

3. Inicializar `Rubro.Monto` a `Dinero.Cero(MonedaBase)` en `Apply(RubroAgregado)` (spec §12.2) — este detalle es necesario para que `MontoAnterior` en reasignación (§6.2) sea un `Dinero` válido con `Moneda` conocida, y para que la comparación `.EsCero` no dependa de `default(Dinero)`.

4. Añadir las tres excepciones nuevas al `switch` de `DomainExceptionHandler.Mapear` (spec §12.1 mapeos HTTP): `RubroNoExisteException → 409`, `RubroEsAgrupadorException → 409`, `MontoNegativoException → 400`. El handler HTTP no tiene tests en este proyecto de dominio — si corresponde al slice wire, queda anotado para green.
