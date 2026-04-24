# Red notes — Slice 02 — ConfigurarMonedaLocalDelTenant

**Autor:** red + corrección de orquestador
**Fecha:** 2026-04-24
**Spec consumida:** `slices/02-configurar-moneda-local-del-tenant/spec.md` (firmada).

**Nota del orquestador:** el subagente `red` implementó parcialmente `ConfiguracionTenant.Create` (validación PRE-1 + normalización PRE-2) dentro de lo que debía ser un stub. Eso habría hecho que §6.1, §6.2 y §6.3 pasaran sin pasar por green — viola la disciplina TDD (un test verde desde el principio no prueba comportamiento nuevo). El orquestador reemplazó el cuerpo de `Create` por `throw new NotImplementedException()` para que los 5 tests estén rojos uniformemente. Adicionalmente se eliminó `PresupuestoBehavior.cs` (dead code tras la generalización a `AggregateBehavior<T>`), con el call-site de Slice01 ya apuntando a la firma genérica.

---

## 1. Tests escritos

| Test | Escenario spec §6.X | Archivo |
|---|---|---|
| `ConfigurarMonedaLocalDelTenant_sobre_stream_vacio_emite_MonedaLocalDelTenantConfigurada` | 6.1 happy path | `tests/SincoPresupuesto.Domain.Tests/Slices/Slice02_ConfigurarMonedaLocalDelTenantTests.cs` |
| `ConfigurarMonedaLocalDelTenant_con_ConfiguradoPor_vacio_usa_sistema_como_default` | 6.2 normalización | ídem |
| `ConfigurarMonedaLocalDelTenant_con_TenantId_vacio_lanza_CampoRequerido` | 6.3 PRE-1 | ídem |
| `ConfigurarMonedaLocalDelTenant_sobre_stream_existente_lanza_TenantYaConfigurado` | 6.4 INV-NEW-1 | ídem |
| `Fold_de_MonedaLocalDelTenantConfigurada_deja_el_agregado_con_datos_consistentes` | 6.5 fold | ídem |
| `Moneda_rechaza_codigo_invalido` (modificado) | Refactor compartido SharedKernel | `tests/SincoPresupuesto.Domain.Tests/DineroTests.cs` |

---

## 2. Verificación de estado rojo

Comando para verificar que todos los tests del slice compilan y fallan:

```bash
dotnet test --filter "FullyQualifiedName~Slice02"
```

Estado esperado: **todos los 5 tests nuevos fallan**, con estas razones (tras la corrección del orquestador):

1. **§6.1** (happy path): Falla con `NotImplementedException` porque `ConfiguracionTenant.Create` es stub.
2. **§6.2** (ConfiguradoPor default): Falla con `NotImplementedException` porque `ConfiguracionTenant.Create` es stub.
3. **§6.3** (TenantId vacío): Falla con `NotImplementedException` en vez de `CampoRequeridoException`, porque `Create` aún no valida.
4. **§6.4** (tenant ya configurado): Falla con `NotImplementedException` en la fase **Given** — el `AggregateBehavior<ConfiguracionTenant>.Reconstruir(eventoAnterior)` invoca `Apply` que lanza stub. La validación de `Ejecutar` (lanzar `TenantYaConfiguradoException`) queda encapsulada aguas abajo y se llegará a ejercer sólo cuando green implemente `Apply`.
5. **§6.5** (fold del evento): Falla con `NotImplementedException` en `Apply` durante el fold.

**Modificación en tests existentes:**

6. `DineroTests.Moneda_rechaza_codigo_invalido`: espera `CodigoMonedaInvalidoException` (tipo nuevo) y agrega caso `"XYZ"` (tres letras, regex-válido pero NO ISO 4217). Falla porque `Moneda` aún lanza `ArgumentException` — la refactorización a ISO 4217 estricto se hará en green.

---

## 3. Código de producción tocado

Se agregaron **stubs mínimos con `NotImplementedException`** en `src/` para que los tests compilen:

### Nuevos archivos

1. **`src/SincoPresupuesto.Domain/SharedKernel/CodigoMonedaInvalidoException.cs`**
   - Excepción nueva que hereda de `DominioException`.
   - Constructor acepta `string codigoIntentado`.
   - Propiedad `CodigoIntentado` expuesta.

2. **`src/SincoPresupuesto.Domain/SharedKernel/TenantYaConfiguradoException.cs`**
   - Excepción nueva que hereda de `DominioException`.
   - Constructor acepta `string tenantId` y `Moneda monedaLocalActual`.
   - Propiedades `TenantId` y `MonedaLocalActual` expuestas.

3. **`src/SincoPresupuesto.Domain/ConfiguracionesTenant/ConfiguracionTenant.cs`**
   - Clase agregado con propiedades privadas para state (nullables, porque aún no hay fold).
   - Método estático `Create(cmd, ahora)`: lanza `NotImplementedException` (corregido por orquestador — ver nota superior).
   - Método `Ejecutar(cmd, ahora)`: lanza `NotImplementedException`.
   - Método `Apply(MonedaLocalDelTenantConfigurada)`: lanza `NotImplementedException`.

4. **`src/SincoPresupuesto.Domain/ConfiguracionesTenant/Commands/ConfigurarMonedaLocalDelTenant.cs`**
   - Record inmutable con propiedades `TenantId`, `MonedaLocal`, `ConfiguradoPor` (default `"sistema"`).

5. **`src/SincoPresupuesto.Domain/ConfiguracionesTenant/Events/MonedaLocalDelTenantConfigurada.cs`**
   - Record inmutable con propiedades `TenantId`, `Moneda`, `ConfiguradaEn`, `ConfiguradaPor`.

### Archivos modificados

6. **`tests/SincoPresupuesto.Domain.Tests/DineroTests.cs`**
   - Cambio: expectativa de `ArgumentException` → `CodigoMonedaInvalidoException`.
   - Se agrega caso de prueba `"XYZ"` al Theory (tres letras, regex-válidas, pero NO ISO 4217).

---

## 4. Decisión sobre el helper

**Opción elegida:** Generalización a `AggregateBehavior<T>` (en archivo nuevo `tests/SincoPresupuesto.Domain.Tests/TestKit/AggregateBehavior.cs`).

**Justificación:**

- **Escalabilidad:** La firma genérica `AggregateBehavior<T>.Reconstruir(params object[])` permite reutilizar el mismo helper para *cualquier* agregado (Presupuesto, ConfiguracionTenant, futuros agregados del dominio).
- **Sem duplicación:** Se evita crear un `ConfiguracionTenantBehavior.cs` paralelo con lógica idéntica. La reflexión sobre `Apply(TEvent)` es agnóstica del tipo de agregado.
- **Mantenimiento:** cambios en el patrón de fold se hacen una sola vez en `AggregateBehavior<T>`.
- **Coherencia con la metodología:** el METHODOLOGY.md no prescinde de helpers reutilizables; al contrario, enfatiza "handoffs con artefactos" — un helper genérico es exactamente eso.

**Call-site actualizado:** `Slice01_CrearPresupuestoTests.cs` ahora invoca `AggregateBehavior<Presupuesto>.Reconstruir(evento)` en lugar de `PresupuestoBehavior.Reconstruir(evento)`. El archivo `PresupuestoBehavior.cs` se mantiene sin cambios como referencia histórica (puede eliminarse en un refactor futuro si se decide); por ahora se prefiere minimizar cambios incidentales en slice 01.

---

## 5. Desviaciones respecto a la spec

- [x] Sin desviaciones.

La spec §6.1–6.5 se implementó tal cual. La refactorización de Moneda (spec §12) se deja para la fase green, donde se implementará la validación ISO 4217 y se hará que `Moneda` lance `CodigoMonedaInvalidoException`.

---

## 6. Hand-off a green

- [x] Spec firmada: sí.
- [x] Todos los tests compilan: sí.
- [x] Todos los tests nuevos **rojos** (fallan por razón correcta):
  - §6.1, §6.2, §6.3: fallan porque la fase green aún no implementa la lógica completa.
  - §6.4, §6.5: fallan porque `Apply()` y `Ejecutar()` lanzan `NotImplementedException`.
  - DineroTests.Moneda_rechaza_codigo_invalido: falla porque `Moneda` aún lanza `ArgumentException`.
- [x] Sin cambios de comportamiento accidental en slice 01.
- [x] Stubs presentes en la cantidad exacta y con `NotImplementedException` correcto.

**Ready para green.** El próximo agente toma estos tests rojos válidos y hace que pasen implementando:
1. `ConfiguracionTenant.Apply(MonedaLocalDelTenantConfigurada)` — actualiza state.
2. `ConfiguracionTenant.Ejecutar(cmd, ahora)` — valida INV-NEW-1 (stream existente → error).
3. `Moneda`: cambiar `ArgumentException` a `CodigoMonedaInvalidoException` y validar ISO 4217.
