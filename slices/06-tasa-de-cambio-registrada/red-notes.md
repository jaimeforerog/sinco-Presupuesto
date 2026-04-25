# Red notes — Slice 06 — RegistrarTasaDeCambio

**Autor:** red
**Fecha:** 2026-04-24
**Spec consumida:** `slices/06-tasa-de-cambio-registrada/spec.md` (firmada 2026-04-24, 11 escenarios).

---

## 1. Tests escritos

Once tests Given/When/Then en xUnit + FluentAssertions, uno por escenario de la spec §6. `Theory` se usa donde la spec lo pide explícitamente (§6.4, §6.5, §6.7); el resto son `Fact`. La cuenta total xUnit tras expansión de los theories es **17 casos** (1+1+1+3+3+1+3+1+1+1+1).

| Test | Escenario spec §6.X | Casos xUnit |
|---|---|---|
| `RegistrarTasaDeCambio_sobre_stream_vacio_emite_TasaDeCambioRegistrada` | 6.1 happy path stream vacío | 1 |
| `RegistrarTasaDeCambio_sobre_stream_existente_con_par_distinto_emite_segundo_evento` | 6.2 acumulación | 1 |
| `RegistrarTasaDeCambio_re_registra_mismo_par_y_fecha_emite_segundo_evento` | 6.3 last-write-wins (INV-CT-1) | 1 |
| `RegistrarTasaDeCambio_con_RegistradoPor_vacio_o_whitespace_usa_sistema_como_default` | 6.4 normalización RegistradoPor | 3 (`""`, `"   "`, `"\t"`) |
| `RegistrarTasaDeCambio_con_Fuente_null_o_whitespace_emite_evento_con_Fuente_null` | 6.5 normalización Fuente | 3 (`null`, `""`, `"   "`) |
| `RegistrarTasaDeCambio_con_monedas_iguales_sobre_stream_vacio_lanza_MonedasIgualesEnTasa` | 6.6 PRE-1 stream vacío | 1 |
| `RegistrarTasaDeCambio_con_tasa_no_positiva_lanza_TasaDeCambioInvalida` | 6.7 PRE-2 | 3 (`0m`, `-1m`, `-0.0001m`) |
| `RegistrarTasaDeCambio_con_fecha_futura_lanza_FechaDeTasaEnElFuturo` | 6.8 PRE-3 fecha futura | 1 |
| `RegistrarTasaDeCambio_con_fecha_igual_a_hoy_emite_evento` | 6.9 caso límite Fecha == hoy | 1 |
| `RegistrarTasaDeCambio_con_monedas_iguales_sobre_stream_existente_lanza_MonedasIgualesEnTasa` | 6.10 PRE-1 stream existente | 1 |
| `Fold_de_TasaDeCambioRegistrada_deja_el_agregado_con_historial_en_orden` | 6.11 fold con historial | 1 |

Archivo: `tests/SincoPresupuesto.Domain.Tests/Slices/Slice06_RegistrarTasaDeCambioTests.cs`.

---

## 2. Verificación de estado rojo

Comando exacto:

```bash
dotnet test --filter "FullyQualifiedName~Slice06" --nologo
```

Salida resumida:

```
Con error! - Con error: 17, Superado: 0, Omitido: 0, Total: 17, Duración: 516 ms
```

Razón de fallo de cada test (todos fallan por `NotImplementedException`, no por defectos de compilación):

- **§6.1, §6.4, §6.5, §6.6, §6.7, §6.8, §6.9** (7 tests, 13 casos xUnit): fallan en `CatalogoDeTasas.Crear(...)` que es stub `throw new NotImplementedException()`. Para los tests de excepción esperada (§6.6, §6.7, §6.8), FluentAssertions reporta "Expected `<ExcepcionEsperada>` to be thrown, but found `<NotImplementedException>`" — exactamente el rojo válido del red.
- **§6.2, §6.3, §6.10** (3 tests): fallan en la fase **Given** porque `AggregateBehavior<CatalogoDeTasas>.Reconstruir(eventoPrevio)` invoca `Apply(TasaDeCambioRegistrada)` que es stub. La validación de `Ejecutar` queda encapsulada aguas abajo y se ejercerá una vez green implemente `Apply`.
- **§6.11** (fold): falla en `Apply(TasaDeCambioRegistrada)` durante el fold del primer evento.

Ningún test falla por incompatibilidad de tipos / ausencia de método / namespace incorrecto. La build solución completa pasa con **0 errores, 0 warnings**:

```
Compilación correcta.
    0 Advertencia(s)
    0 Errores
```

Comprobación de no-regresión sobre el resto del dominio:

```bash
dotnet test tests/SincoPresupuesto.Domain.Tests/SincoPresupuesto.Domain.Tests.csproj --filter "FullyQualifiedName!~Slice06" --nologo
```

Salida:

```
Correctas! - Con error: 0, Superado: 142, Omitido: 0, Total: 142, Duración: 55 ms
```

---

## 3. Código de producción tocado

Stubs mínimos en `src/` para que los tests compilen. **Ningún cuerpo de método del agregado contiene lógica** — todos lanzan `NotImplementedException()` para garantizar que el rojo se observa por la razón correcta y que green tendrá trabajo real.

### Archivos nuevos

- `src/SincoPresupuesto.Domain/SharedKernel/MonedasIgualesEnTasaException.cs` — `(Moneda Moneda) : DominioException`. Mensaje y propiedad concretos (sin stub) — se construye con datos del comando, los tests verifican `.Moneda`.
- `src/SincoPresupuesto.Domain/SharedKernel/TasaDeCambioInvalidaException.cs` — `(decimal TasaIntentada) : DominioException`. Idem.
- `src/SincoPresupuesto.Domain/SharedKernel/FechaDeTasaEnElFuturoException.cs` — `(DateOnly Fecha, DateOnly Hoy) : DominioException`. Idem.
- `src/SincoPresupuesto.Domain/CatalogosDeTasas/Commands/RegistrarTasaDeCambio.cs` — record con la firma exacta de spec §2.
- `src/SincoPresupuesto.Domain/CatalogosDeTasas/Events/TasaDeCambioRegistrada.cs` — record con el payload exacto de spec §3 / §12.3.
- `src/SincoPresupuesto.Domain/CatalogosDeTasas/CatalogoDeTasas.cs` — agregado `public class` (no sealed, replica `ConfiguracionTenant`). Contiene:
  - `Id : Guid` con `set` público (Marten lo requiere — patrón slice 02).
  - `_registros: List<RegistroDeTasa>` privada + `Registros: IReadOnlyList<RegistroDeTasa>` pública (vista del fold para tests).
  - `static Crear(cmd, ahora)` → stub `NotImplementedException`.
  - `Ejecutar(cmd, ahora)` → stub `NotImplementedException`.
  - `Apply(TasaDeCambioRegistrada)` → stub `NotImplementedException`.
  - `record RegistroDeTasa(...)` (compañera del agregado, en el mismo archivo según spec §12.4).

### Sin tocar

- Cero cambios en agregados/proyecciones/tests existentes (Presupuesto, ConfiguracionTenant, slices 00–05). El SharedKernel solo recibe **adiciones** (tres archivos nuevos); las clases existentes no se modifican.
- No se introduce `CatalogoDeTasasStreamId` (spec §12.5) — vive en el proyecto `Application`, fuera del scope del dominio. El red no lo necesita: los tests unitarios operan sobre el agregado directamente. Se deja para infra-wire / green si lo pide.
- No se introduce la proyección `TasasDeCambioVigentesProjection` (spec §12.6) — vive en `Application`, fuera del scope del red.
- No se toca `DomainExceptionHandler.Mapear` (spec §9 / §12.7) — vive en infraestructura, fuera del scope del red.

---

## 4. Decisiones del red sobre la spec

### 4.1 Nombre del método de instancia: `Ejecutar` (no `Registrar`)

La spec §2 (líneas 56–57) y §6.10 mencionan `Registrar` como método de instancia. La instrucción del orquestador y la coherencia con slice 02 (que usa `Ejecutar`) indican que el nombre canónico del agregado es `Ejecutar`. Decisión: usar `Ejecutar` en código y tests, en línea con el patrón del agregado singleton de slice 02. La spec no firma una distinción semántica entre los dos nombres — la clave es que sea método de instancia que actúa sobre el fold.

Si el reviewer prefiere `Registrar` por enfasis sobre el verbo de dominio, el rename es trivial (refactor mecánico) y no afecta los rojos. Documentado aquí, no se eleva al modeler.

### 4.2 Tipo del parámetro decimal en `[InlineData]` (§6.7)

xUnit `[InlineData]` no acepta literales `decimal`. Patrón estándar: declarar el parámetro como `double` y castear a `decimal` en el cuerpo del test. El test asierta `.TasaIntentada.Should().Be(tasa)` donde `tasa = (decimal)tasaDouble`. Los valores `0`, `-1`, `-0.0001` redondean exactamente bajo este cast. Sin pérdida de precisión observable para el rojo.

### 4.3 No se asierta `agg.Id == CatalogoDeTasasStreamId.Value` en §6.11

La spec §6.11 sugiere asertar `agg.Id == CatalogoDeTasasStreamId.Value`. Pero `CatalogoDeTasasStreamId` vive en `Application` (spec §12.5), fuera del proyecto de dominio. El test unitario del agregado no debe depender de `Application`. Adicionalmente, el patrón del slice 02 tampoco asierta `agg.Id` en su test de fold (`Fold_de_MonedaLocalDelTenantConfigurada_deja_el_agregado_con_datos_consistentes`) — `Id` queda en `Guid.Empty` y Marten lo asigna al rehidratar (ver `ConfiguracionTenant.Id` doc-comment). Decisión: omitir la aserción de `agg.Id` en §6.11 — coherente con slice 02, sin acoplar el dominio a un constante de Application.

### 4.4 Nombre de la propiedad expuesta para fold: `Registros` (no `Tasas`)

La instrucción del orquestador menciona "agg.Tasas" con una pista "(nombre exacto a criterio del modeler — confirma con spec §12)". Spec §12.4 firma `IReadOnlyList<RegistroDeTasa> Registros`. Decisión: usar `Registros`. El record acompañante del agregado se llama `RegistroDeTasa` (también según spec §12.4).

---

## 5. Desviaciones respecto a la spec

- [x] Sin desviaciones de comportamiento. Las decisiones de §4 son aclaraciones de naming sobre puntos donde la spec ofrecía dos términos coexistentes (`Registrar` vs `Ejecutar`) o sugerencias no firmes (`agg.Id` en §6.11). Ninguna afecta los Then de los escenarios.

---

## 6. Hand-off a green

- Spec firmada: sí.
- Todos los tests compilan: sí (build solución 0E/0W).
- Todos los tests del slice rojos por la razón correcta: sí (17/17 fallan por `NotImplementedException` aguas abajo, sin defectos de compilación).
- Sin regresiones en el resto del dominio: sí (142/142 domain tests verdes con filtro `!~Slice06`).
- Stubs presentes en la cantidad exacta y con `NotImplementedException`: sí (`Crear`, `Ejecutar`, `Apply` en `CatalogoDeTasas`).

**Ready para green.** El próximo agente toma estos 11 tests rojos y hace que pasen implementando:

1. `CatalogoDeTasas.Crear(cmd, ahora)`: validar PRE-1/PRE-2/PRE-3 + normalizar Fuente / RegistradoPor + emitir `TasaDeCambioRegistrada`. Lógica común a extraer (spec §12.4).
2. `CatalogoDeTasas.Ejecutar(cmd, ahora)`: idéntica validación, no lanza por "ya configurado" — emite el evento.
3. `CatalogoDeTasas.Apply(TasaDeCambioRegistrada)`: agrega un `RegistroDeTasa` al `_registros`.

Tareas paralelas (fuera del scope del rojo, abrir si surgen):
- `CatalogoDeTasasStreamId` y `TasasDeCambioVigentesProjection` en `Application` — infra-wire.
- Mapeo HTTP de las tres excepciones nuevas en `DomainExceptionHandler` — infra-wire.
- Endpoints HTTP `POST .../catalogo-tasas/registros` y `GET .../catalogo-tasas/vigentes` — infra-wire.
