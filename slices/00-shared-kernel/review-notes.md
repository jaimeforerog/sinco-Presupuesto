# Review notes — Slice 00 — SharedKernel (retroactivo)

**Autor:** reviewer
**Fecha:** 2026-04-24
**Slice auditado:** `slices/00-shared-kernel/`.
**Veredicto:** `approved-with-followups`

---

## 1. Resumen ejecutivo

Slice 00 documenta retroactivamente el SharedKernel (VO `Dinero`, VO `Moneda`, clase base `DominioException`, 14 excepciones concretas, helper `Requerir.Campo`) y cierra tres followups históricos (#4, #6, #9) mediante (a) reubicación de `DineroTests.cs` al convenio `Slices/Slice00_SharedKernelTests.cs` con 22 métodos / 63 casos xUnit, (b) cobertura de los gaps de `Dinero` (operadores `-`, `*`, `<`, `>`, `<=`, `>=`, helpers `Cero`/`EsCero`/`EsPositivo`/`EsNegativo`, `En` con factor inválido, `ToString`) y (c) adopción del sampling + cardinalidad para ISO 4217 (§6.17). Se aplicaron las 4 decisiones firmadas Q1–Q4 opción (a) — `MonedasDistintasException` migra a `DominioException` y sale de `Dinero.cs` a archivo propio, se crea `FactorDeConversionInvalidoException : DominioException` que `Dinero.En` lanza ante factor ≤ 0, y `DineroTests.cs` se absorbe en `Slice00_SharedKernelTests.cs`. Refactor adicional de refactorer extrae tres excepciones cohabitantes (`CampoRequeridoException`, `PeriodoInvalidoException`, `ProfundidadMaximaFueraDeRangoException`) desde `DominioException.cs` a archivos propios, completando coherentemente el criterio de Q3 para todo el kernel. `dotnet build` 0/0, `dotnet test` 109/109 verdes (verificado en vivo por el reviewer: `Correctas! - Con error: 0, Superado: 109, Omitido: 0, Total: 109`). `DomainExceptionHandler` mapea las dos excepciones nuevas a 400 en la sección correcta del `switch`. Veredicto: **approved-with-followups** — sin blockers; se registran 4 followups nuevos (§13 de la spec) y se cierran #4, #6, #9.

La desviación explícita respecto a METHODOLOGY §2.2 (rojo → verde → refactor) está legitimada por METHODOLOGY §7 ítem 3 (bug-fix / TDD retroactivo) y documentada en spec §16 (nota de retroactividad) + red-notes §1 (tabla de clasificación con 17 pinning + 5 rojos por método, equivalentes a 54 pinning + 9 rojos por caso xUnit tras expansión de Theories). Los 9 casos rojos reales produjeron fallos por razón técnica correcta en la fase red (asignabilidad a `DominioException` y API inexistente), no por "falta de estub" — verificación trazable en red-notes §2.

---

## 2. Checklist de auditoría

### 2.1 Spec ↔ tests

- [x] Cada escenario de `spec.md §6` tiene un test correspondiente en `Slice00_SharedKernelTests.cs`.
  - §6.1 → `Dinero_suma_misma_moneda_devuelve_resultado_con_la_misma_moneda`.
  - §6.2 → `Dinero_suma_entre_monedas_distintas_lanza_MonedasDistintasException`.
  - §6.3 → `Dinero_resta_misma_moneda_devuelve_diferencia` (Theory, 2 casos pos/neg).
  - §6.4 → `Dinero_resta_entre_monedas_distintas_lanza_MonedasDistintasException_que_es_DominioException`.
  - §6.5 → `Dinero_operadores_de_comparacion_con_misma_moneda_devuelven_resultado_esperado` (cubre los 4 operadores).
  - §6.6 → `Dinero_operadores_de_comparacion_entre_monedas_distintas_lanzan_MonedasDistintasException_que_es_DominioException` (Theory × 4 operadores).
  - §6.7 → `Dinero_multiplicacion_por_factor_en_ambos_lados_es_conmutativa` (Theory × 3).
  - §6.8 → `Dinero_Cero_devuelve_neutro_aditivo_con_la_moneda_indicada`.
  - §6.9 → `Dinero_helpers_EsCero_EsPositivo_EsNegativo_reflejan_el_signo_del_valor` (Theory × 3).
  - §6.10 → `Dinero_En_misma_moneda_ignora_el_factor_y_devuelve_el_mismo_valor`.
  - §6.11 → `Dinero_En_otra_moneda_con_factor_positivo_aplica_el_factor`.
  - §6.12 → `Dinero_En_otra_moneda_con_factor_no_positivo_lanza_FactorDeConversionInvalidoException` (Theory × 2).
  - §6.13 → `Dinero_ToString_formatea_valor_y_codigo_de_moneda` (Theory × 6, cultura invariante fijada).
  - §6.14 → `Moneda_normaliza_codigo_con_trim_y_upperinvariant` (Theory × 4).
  - §6.15 → `Moneda_rechaza_codigos_mal_formados_con_CodigoMonedaInvalidoException` (Theory × 6 incluye null/whitespace/longitud/char).
  - §6.16 → `Moneda_rechaza_codigos_tres_letras_no_ISO_4217` (Theory × 3).
  - §6.17 → `Moneda_acepta_codigos_del_sample_ISO_4217` (Theory × 17) + `Moneda_CantidadCodigosIso4217Soportados_es_al_menos_150` (Fact).
  - §6.18 → `Moneda_igualdad_por_valor_normalizado_y_mismo_hashcode`.
  - §6.19 → `Moneda_ToString_y_conversion_implicita_a_string_devuelven_el_Codigo`.
  - §6.20 → `Requerir_Campo_happy_y_fallo_cubre_null_vacio_y_whitespace`.
  - §6.21 → `DominioException_contrato_de_jerarquia_y_propiedades_en_todas_las_excepciones_del_kernel` (12 excepciones).
  - §6.22 → `MonedasDistintasException_preserva_propiedades_y_es_DominioException`.

- [x] Cada precondición del kernel (spec §4) mapea a test:
  - PRE-Dinero.1 (suma/resta monedas distintas) → §6.2/§6.4.
  - PRE-Dinero.2 (comparación monedas distintas) → §6.6.
  - PRE-Dinero.3 (`En` con factor ≤ 0) → §6.12.
  - PRE-Moneda.1 (null/empty/whitespace) → §6.15.
  - PRE-Moneda.2 (longitud/alfabeto) → §6.15.
  - PRE-Moneda.3 (pertenencia ISO 4217) → §6.16.
  - PRE-Requerir.1 (null/empty/whitespace) → §6.20.

- [x] Cada invariante del kernel (spec §5) mapea a test:
  - INV-SK-1 (inmutabilidad `Dinero`) → §6.1 + §6.7 (igualdad por valor implícita en `Should().Be(new Dinero(...))`).
  - INV-SK-2 (no mezcla silenciosa de monedas) → §6.2, §6.4, §6.6.
  - INV-SK-3 (inmutabilidad `Moneda`, normalización) → §6.14, §6.18.
  - INV-SK-4 (toda excepción del kernel es `DominioException`) → §6.21 + §6.22 + §6.12.
  - INV-SK-5 (tests asertan tipo + propiedades, nunca mensaje) → aplicada transversalmente; verificada por inspección del archivo.

- [x] Los nombres de los tests son frases completas en español que describen el comportamiento. Cero `Test1`, cero `ShouldWork`. Forma consistente `Dinero_{condición}_{resultado}` y `Moneda_{condición}_{resultado}`.

### 2.2 Tests como documentación

- [x] Un lector que no conoce el código puede entender el contrato público leyendo solo `Slice00_SharedKernelTests.cs`. El docstring de clase (líneas 9-23) narra la intención retroactiva, las decisiones Q1–Q4 y la nota metodológica de METHODOLOGY §7 ítem 3.
- [x] Given/When/Then está estructuralmente visible.
  - Comentarios explícitos `// Given`, `// When`, `// Then` en todos los tests.
  - En tests Theory con pocos casos se usa la convención `// Given / When` combinada por brevedad, manteniendo la estructura legible.
- [x] Cero mocks del dominio.
  - Todas las aserciones usan `FluentAssertions` sobre instancias reales.
  - Las excepciones se asertan por tipo + propiedades (`ex.Izquierda.Should().Be(...)`, `ex.FactorIntentado.Should().Be(...)`, `ex.NombreCampo.Should().Be(...)`), nunca por mensaje.
- [x] §6.13 fija `CultureInfo.InvariantCulture` en try/finally para que las aserciones de `ToString` sean deterministas — patrón correcto, evita dependencia en helper externo. La nota "redondea a 4 decimales por format `0.####`" está documentada en el caso `100.12345 → "100.1235 USD"`.
- [x] §6.21 agrupa las 12 excepciones del kernel en un único Fact, consistente con la forma enumerativa del escenario en la spec. Las 12 aserciones de `BeAssignableTo<DominioException>` + propiedades estructuradas están presentes.

### 2.3 Implementación

- [x] **Q1 (MonedasDistintasException : DominioException)** aplicada exactamente. Verificación: `src/SincoPresupuesto.Domain/SharedKernel/MonedasDistintasException.cs:7` declara `public sealed class MonedasDistintasException : DominioException`. Propiedades `Izquierda`/`Derecha` preservadas.

- [x] **Q2 (FactorDeConversionInvalidoException)** aplicada exactamente. Verificación:
  - `src/SincoPresupuesto.Domain/SharedKernel/FactorDeConversionInvalidoException.cs:7` declara `public sealed class FactorDeConversionInvalidoException : DominioException` con propiedad `decimal FactorIntentado`.
  - `src/SincoPresupuesto.Domain/SharedKernel/Dinero.cs:27-30` lanza `throw new FactorDeConversionInvalidoException(factor)` cuando `destino != Moneda && factor <= 0` (reemplaza al anterior `ArgumentException`). Rama con `destino == Moneda` devuelve `this` antes de evaluar factor (§6.10 verifica idempotencia con factor absurdo `999m`).

- [x] **Q3 (archivo propio para MonedasDistintasException)** aplicada exactamente. `MonedasDistintasException.cs` existe como archivo propio en `SharedKernel/`. `Dinero.cs` no contiene ya la definición anidada — verificación por `Grep MonedasDistintasException src/`: sólo 4 matches (Dinero.cs el `throw`, MonedasDistintasException.cs la declaración, DomainExceptionHandler.cs el mapeo, FactorDeConversionInvalidoException.cs cero matches).

- [x] **Q4 (reubicación de DineroTests.cs)** aplicada exactamente. `Slice00_SharedKernelTests.cs` existe con 22 métodos (§6.1–§6.22); `DineroTests.cs` ya no existe en el proyecto de tests (`Glob tests/**/DineroTests.cs → No files found`). Los 5 tests originales de `DineroTests.cs` quedan absorbidos en §6.1 (suma misma moneda), §6.2 (suma distintas), §6.10 (`En` misma moneda), §6.11 (`En` distinta), §6.14 (normaliza) y §6.15 (rechaza mal formado).

- [x] El código de producción añadido/modificado es mínimo.
  - `Dinero.En` sólo cambió el tipo de excepción lanzada (1 línea modificada). Cero cambio en la semántica de la rama feliz.
  - `Moneda.cs` añadió exactamente una propiedad pública (`CantidadCodigosIso4217Soportados`, 1 línea). Ejercida por §6.17b.
  - `MonedasDistintasException.cs` y `FactorDeConversionInvalidoException.cs`: nuevos archivos, ambos ejercidos directa e indirectamente por tests (§6.4, §6.6, §6.22, §6.2 indirecto; §6.12).
  - `DominioException.cs` quedó reducido a la clase base abstracta (15 líneas).
  - `DomainExceptionHandler.cs`: +2 entradas al `switch` (lineas 63-64), en la sección correcta "400 — datos mal formados o inválidos".

- [x] Sin `DateTime.UtcNow`, `Guid.NewGuid()`, `Environment.MachineName`, etc., dentro del dominio. `Dinero`/`Moneda`/`Requerir` son puras.

- [x] `Dinero`/`Moneda` siguen siendo la única forma permitida para montos. Sin `decimal` pelado filtrándose al dominio (ni en este slice ni en los anteriores, verificado por inspección cruzada).

- [x] Records inmutables / VOs: `Dinero` es `readonly record struct`, `Moneda` es `readonly record struct`. Sin setters.

- [x] Refactor transversal de refactorer (extracción de 3 excepciones cohabitantes) es refactor puro:
  - `DominioException.cs` antes contenía 4 clases (la base + `CampoRequeridoException` + `PeriodoInvalidoException` + `ProfundidadMaximaFueraDeRangoException`); tras el refactor contiene sólo la base.
  - Los 3 archivos nuevos (`CampoRequeridoException.cs`, `PeriodoInvalidoException.cs`, `ProfundidadMaximaFueraDeRangoException.cs`) preservan namespace `SincoPresupuesto.Domain.SharedKernel`, cuerpo idéntico de las clases, propiedades públicas, constructores, mensajes y XML doc.
  - **No hay consumidor por reflection sobre `DominioException.cs`** — verificado por `Grep` sobre `typeof(DominioException).Assembly` / `GetTypes()` / `nameof(DominioException)` en todo `src/` y `tests/`: los únicos usos son `BeAssignableTo<DominioException>(...)` en tests (§6.21, §6.22, §6.4, §6.6, §6.12) que operan vía runtime polimorfismo, no vía enumeración del archivo. Cero riesgo de regresión por split del archivo.
  - Cero cambios de comportamiento observable; el compilador no distingue dónde vive cada clase mientras compartan namespace y assembly.
  - `PresupuestoNoEncontradoException` recibió únicamente XML doc añadida (homogeneización de docs — `refactor-notes.md` §Cambios #4), sin cambios de comportamiento.
  - `Requerir.cs` intacto — verificado (15 líneas, un único método `Campo`).

### 2.4 Cobertura (inspección manual)

Slice 00 es un kernel de VOs puros sin ramas complejas. Auditoría por rama del reviewer:

**`Dinero` — 100% de ramas cubiertas por tests.**

| Rama | Test(s) |
|---|---|
| `Cero(Moneda)` | §6.8 |
| `EsCero` true/false | §6.8, §6.9 (3 casos). |
| `EsPositivo` true/false | §6.9 (3 casos), §6.7 (factor positivo). |
| `EsNegativo` true/false | §6.9 (3 casos), §6.7 (factor negativo), §6.3 (resta con resultado negativo). |
| `En` rama `destino == Moneda` | §6.10. |
| `En` rama `destino != Moneda && factor <= 0` | §6.12 (factor=0 y factor=-1). |
| `En` rama `destino != Moneda && factor > 0` | §6.11. |
| `operator +` happy | §6.1. |
| `operator +` monedas distintas | §6.2. |
| `operator -` happy (positivo y negativo) | §6.3 (2 casos). |
| `operator -` monedas distintas | §6.4. |
| `operator *(Dinero, decimal)` | §6.7 (3 casos pos/cero/neg). |
| `operator *(decimal, Dinero)` | §6.7 (ambos lados). |
| `operator <` happy | §6.5. |
| `operator <` monedas distintas | §6.6 (caso `<`). |
| `operator >` happy | §6.5. |
| `operator >` monedas distintas | §6.6 (caso `>`). |
| `operator <=` happy (incluye igualdad) | §6.5. |
| `operator <=` monedas distintas | §6.6 (caso `<=`). |
| `operator >=` happy (incluye igualdad) | §6.5. |
| `operator >=` monedas distintas | §6.6 (caso `>=`). |
| `ToString` (format `0.####` + `Codigo`) | §6.13 (6 casos, incluye negativo, cero, redondeo a 4 decimales, casos con 0/1/4/5 decimales). |
| `GuardarMismaMoneda` happy | todas las happy paths de +/-/</>/</=/>=. |
| `GuardarMismaMoneda` lanza | §6.2, §6.4, §6.6 (todos los cuatro operadores). |

**Conclusión `Dinero`: 24/24 ramas cubiertas = 100%.**

**`Moneda` — 100% de ramas cubiertas por tests.**

| Rama | Test(s) |
|---|---|
| Constructor con null/empty/whitespace | §6.15 (`""`, `"   "`, `null`). |
| Constructor con longitud ≠ 3 | §6.15 (`"US"` len=2, `"USDD"` len=4). |
| Constructor con char no en `A-Z` | §6.15 (`"US1"`). |
| Constructor con código fuera de ISO | §6.16 (`"XYZ"`, `"ABC"`, `"AAA"`). |
| Constructor happy (normalización + aceptación) | §6.14 (4 casos trim/upper) + §6.17 sample (17 casos) + §6.18. |
| `Codigo` getter | §6.14, §6.16, §6.18, §6.19. |
| `ToString()` | §6.19. |
| `implicit operator string` | §6.19. |
| `CantidadCodigosIso4217Soportados` getter | §6.17b. |
| Atajos estáticos `COP/USD/EUR/MXN/CLP/PEN/ARS` | `COP`: §6.18, §6.19, §6.1, §6.2, §6.4, §6.6, §6.8(en USD), §6.9, §6.10, §6.11, §6.12, §6.14, §6.21(`tenantYaConfigurado`). `USD`: §6.2, §6.4, §6.6, §6.8, §6.11, §6.12, §6.17 sample, §6.18, §6.22. `EUR/MXN/CLP/ARS/PEN`: §6.17 sample (construidas por string, equivalentes al readonly); `EUR`: §6.13 (`-42 EUR`). |

**Conclusión `Moneda`: 9/9 ramas + todos los atajos = 100%.**

**`DominioException` y subclases** — todas instanciadas y verificadas en §6.21 + §6.22 + la rama de error del operador/`En` de cada test. 14 excepciones cubiertas: 12 en §6.21 + `MonedasDistintasException` (§6.22 + §6.4 + §6.6) + `FactorDeConversionInvalidoException` (§6.12). **Ninguna excepción del kernel queda huérfana de test.**

**`Requerir.Campo`** — 2/2 ramas (happy y lanza con las 3 variantes null/empty/whitespace) = 100% en §6.20.

**Cobertura agregada del SharedKernel: 100% por ramas** (inspección manual). Muy por encima del objetivo ≥ 95% de la spec §12. Sin rama descubierta → sin followup ni blocker de cobertura.

> Nota: no se ejecutó `coverlet`. El kernel es estructuralmente pequeño (≈ 200 líneas de producción entre Dinero + Moneda + excepciones) y cada rama es auditable a mano por enumeración directa. Si `FOLLOWUPS.md` #1 (coverage automatizado) se cierra en un slice futuro, el reporte de `coverlet` confirmará este 100% mecánicamente.

### 2.5 Refactor

- [x] `refactor-notes.md` presente, claro y estructurado. Registra **4 refactors aplicados** (3 extracciones de excepciones + 1 armonización de XML doc) y **5 impulsos descartados** con razón técnica por cada uno.
- [x] Los tests no se tocaron en la fase refactor. Verificado por inspección: ninguno de los 22 métodos de `Slice00_SharedKernelTests.cs` cambió tras green.
- [x] Cero warnings. Verificado por `dotnet build` (orquestador y reviewer ambos).
- [x] Cero cambios de comportamiento observable. Superficie pública idéntica (mismos tipos, namespaces, propiedades, signaturas, mensajes). Verificado independientemente por el reviewer: `Grep DominioException src/` sigue devolviendo 17 files con la base viva en un archivo propio.

### 2.6 Invariantes cross-slice

- [x] `dotnet test` completo del repo en verde: **109/109** — verificado en vivo por el reviewer.
- [x] Los 46 tests originales de slices 01/02/03 siguen verdes tras el refactor del kernel (16 + 7 + 23 = 46). Los 63 casos xUnit nuevos de Slice00 (22 métodos × expansión de Theories) completan los 109. Desglose consistente con red-notes §2 y green-notes §4.
- [x] Compatibilidad con Marten preservada. `Dinero` sigue siendo `readonly record struct`, `Moneda` igual; serialización JSON no afectada.
- [x] Slice 01 (`PeriodoInvalidoException`, `ProfundidadMaximaFueraDeRangoException`, `CampoRequeridoException`) opera sin modificaciones tras la extracción — los `using SincoPresupuesto.Domain.SharedKernel;` siguen resolviendo todas las excepciones por estar en el mismo namespace.

### 2.7 Coherencia con decisiones previas

- [x] Alineado con `02-decisiones-hotspots-mvp.md` §2 (multimoneda a nivel de partida; `Dinero(Valor, Moneda)` como VO obligatorio; `Moneda` como código ISO 4217). El VO, los operadores y la conversión explícita vía `En` son exactamente lo que ese documento enuncia.
- [x] Alineado con METHODOLOGY §7 ítem 3 (bug-fix / TDD retroactivo). La desviación respecto a METHODOLOGY §2.2 (rojo → verde → refactor para **todos** los tests) está explícitamente cubierta:
  - Spec §16 introduce la nota de retroactividad y clasifica cada escenario como **(pinning — ya pasa hoy)** o **(rojo — requiere green)**.
  - Red-notes §1 tabula la clasificación y §2 muestra el estado rojo real (9 casos rojos en los escenarios §6.4/§6.6/§6.12/§6.17b/§6.22 por razones técnicas correctas: asignabilidad a `DominioException` y API inexistente).
  - El valor documental + regresivo de los pinning es explícito y metodológicamente aceptado.
  - Veredicto del reviewer: la documentación del hecho es **suficiente**. No se rechaza por "no hubo rojos reales" porque (a) sí hubo 9 rojos reales por razón técnica correcta y (b) los 54 pinning están etiquetados y justificados en artefactos firmados.
- [x] Alineado con spec slice 02 §12 (origen de `CodigoMonedaInvalidoException` y lista ISO 4217 embebida).
- [x] Alineado con spec slice 03 §13 + refactor-notes (origen de `SharedKernel.Requerir.Campo`; este slice 00 ejercita formalmente `Requerir.Campo` en §6.20).
- [x] Alineado con METHODOLOGY §8 (contratos de calidad): `nullable` habilitado, naming español en dominio, records para VOs, atajos estáticos de `Moneda` públicos.

---

## 3. Hallazgos

| # | Tipo | Descripción | Ubicación | Acción sugerida |
|---|---|---|---|---|
| 1 | followup | `Dinero.ToString(CultureInfo.InvariantCulture)` — hoy depende de `CultureInfo.CurrentCulture`, los tests fijan cultura invariante manualmente. Cambio mínimo, robustez alta. Disparador: primer test fallando por locale en CI ajeno. | `Dinero.cs:65` + spec §6.13 | **Registrado como `FOLLOWUPS.md` #16**. |
| 2 | followup | Prueba de arquitectura "toda excepción pública en `SincoPresupuesto.Domain.SharedKernel` hereda de `DominioException` salvo la base". Cierra la puerta a futuras excepciones que olviden heredar (p.ej. `TenantNoConfiguradoException` de followup #8). Spec §6.21 deja la nota; §13 lo propone. | `SharedKernel/*Exception.cs` | **Registrado como `FOLLOWUPS.md` #17**. |
| 3 | followup | Catálogo ISO 4217 sincronizado con fuente externa (JSON versionado generado desde estándar). Sampling + cardinalidad resuelve el MVP; el followup queda abierto para cuando un tenant requiera una moneda fuera del hash embebido. | `Moneda.cs:14-42` + spec §6.17 opción (c) diferida | **Registrado como `FOLLOWUPS.md` #18**. |
| 4 | followup | Separar `RequerirTests` cuando la superficie de `Requerir` crezca (segundo helper, p.ej. `Requerir.RangoEnteroCerrado`). Hoy con un único método `Campo` vive cómodamente en `Slice00_SharedKernelTests` §6.20. | `Requerir.cs` + spec §13 | **Registrado como `FOLLOWUPS.md` #19**. |
| 5 | followup (cierre) | **#4** — slice retroactivo `00-shared-kernel` creado y `DineroTests.cs` reubicado a `Slices/Slice00_SharedKernelTests.cs`. Verificado: `DineroTests.cs` ya no existe; 22 métodos nuevos con clase `Slice00_SharedKernelTests`. | `FOLLOWUPS.md` | Mover #4 a "Cerrados" con referencia al slice 00. |
| 6 | followup (cierre) | **#6** — tests faltantes de `Dinero` cubiertos exhaustivamente: operadores `-` (§6.3/§6.4), `*` (§6.7), `<`/`>`/`<=`/`>=` (§6.5/§6.6), helpers `Cero`/`EsCero`/`EsPositivo`/`EsNegativo` (§6.8/§6.9), `En(factor=0)` y `En(factor<0)` (§6.12). Los boundaries `ProfundidadMaxima ∈ {1, 15}` válidos están cubiertos en slice 01 (`PresupuestoCreado` con ProfundidadMaxima válida) y slice 03 (§6.12 ProfundidadMaxima=2), no son materia del SharedKernel. | `FOLLOWUPS.md` | Mover #6 a "Cerrados" con referencia al slice 00. |
| 7 | followup (cierre) | **#9** — completitud ISO 4217 abordada mediante estrategia sampling + cardinalidad (§6.17 opción (b) firmada). El hash embebido con ~180 entradas se valida contra 17 códigos de negocio + umbral ≥ 150 (margen de seguridad). La opción (c) de sincronizar con fuente externa queda como followup #18 (nuevo), con disparador claro. | `FOLLOWUPS.md` | Mover #9 a "Cerrados" con referencia al slice 00. |
| 8 | nit | En `Dinero.cs:67-73`, `GuardarMismaMoneda` es un método privado auxiliar — su nombre usa imperativo "Guardar" (castellano rioplatense para "verificar/chequear"), no "Guardián" o "GarantizarMismaMoneda". No es blocker; el naming interno no tiene test que lo ejerza como nombre y el comportamiento es correcto. Posible nit de convención si se quiere homogeneizar con "Requerir" del helper. | `Dinero.cs:67` | Nit asumido. No genera followup. |
| 9 | nit | `Moneda.CantidadCodigosIso4217Soportados` se expone como `public static int` computada cada vez (`HashSet<string>.Count` es O(1)). Green-notes §2.3 y refactor-notes descarte #4 evalúan cachear en `static readonly int` y lo descartan por ser microoptimización sin test que la exija. Decisión correcta. | `Moneda.cs:50` | Nit asumido. |
| 10 | nit | §6.21 instancia `PeriodoInvalidoException(inicio=2026-12-31, fin=2026-01-01)` — fin anterior al inicio — como fixture de asserción. El test sólo verifica preservación de propiedades, no la semántica de invariante (que vive en `Presupuesto.Create`, slice 01). El comentario "// Given — fixtures locales" no explica la inversión del rango; podría ser confuso para un lector nuevo. No es blocker porque §6.21 es exclusivamente contrato de jerarquía, no invariante de periodo. | `Slice00_SharedKernelTests.cs:460-461` | Nit asumido. |
| 11 | nit | `DominioException.cs:13` expone un segundo constructor protected `DominioException(string mensaje, Exception? causa)` que ninguna excepción concreta del kernel utiliza hoy. Es superficie pública (protected) no ejercida. Aceptable como "hook defensivo" para causas encadenadas futuras — consistente con el patrón de `Exception` del BCL. Sin test, pero también sin consumidor. | `DominioException.cs:13` | Nit asumido. Si crece el alcance de review de "miembros no ejercidos" se vuelve a evaluar. |

---

## 4. Veredicto final

- [ ] **approved**
- [x] **approved-with-followups** — 4 followups nuevos (#16, #17, #18, #19) registrados en `FOLLOWUPS.md` + 3 cierres (#4, #6, #9) con trazabilidad explícita al slice 00. Sin blockers. Todos los criterios de Definition of Done se cumplen:
  - Spec firmada con Q1=Q2=Q3=Q4=(a) explícito.
  - Tests G/W/T: 22 métodos / 63 casos xUnit, cada escenario §6 mapeado, precondiciones e invariantes del kernel cubiertas.
  - `dotnet build` 0 errores / 0 warnings.
  - `dotnet test` 109/109 verdes — verificado en vivo por el reviewer.
  - Cobertura: 100% de ramas del kernel por inspección manual (≥ 95% objetivo de spec §12).
  - `refactor-notes.md` presente, con 4 refactors aplicados y 5 descartados justificados.
  - Invariantes cross-slice intactas: slices 01/02/03 siguen verdes.
  - Coherencia con METHODOLOGY §7 ítem 3 (retroactividad), spec slice 02 §12, spec slice 03 §13, hotspots §2 (multimoneda).
- [ ] **request-changes**

**Detalles del veredicto:**

El slice 00 cierra exitosamente un hueco histórico del repo: el SharedKernel ahora tiene contrato público documentado, tests exhaustivos y cobertura auditable. Las 4 decisiones firmadas Q1–Q4 fueron implementadas exactamente por green:

- **Q1** `MonedasDistintasException : DominioException` → verificado en `MonedasDistintasException.cs:7`.
- **Q2** `FactorDeConversionInvalidoException` creada + `Dinero.En` actualizada → verificado en los dos archivos.
- **Q3** `MonedasDistintasException` en archivo propio → verificado; `Dinero.cs` no la contiene.
- **Q4** `Slice00_SharedKernelTests.cs` vive con 22 métodos; `DineroTests.cs` eliminado → verificado por Glob.

El refactor adicional de refactorer (extracción de `CampoRequeridoException`, `PeriodoInvalidoException`, `ProfundidadMaximaFueraDeRangoException` desde `DominioException.cs`) extiende coherentemente el criterio Q3 al resto del kernel y se considera refactor puro tras verificación de ausencia de reflection sobre `DominioException.cs`. `DomainExceptionHandler` mapea las dos excepciones nuevas a HTTP 400 en la sección correcta del `switch` (líneas 63-64, dentro del bloque "400 — datos mal formados o inválidos"), consistente con los comentarios de sección del archivo.

La desviación de METHODOLOGY §2.2 (TDD estricto) está legitimada por METHODOLOGY §7 ítem 3 (retroactividad / bug-fix) y documentada en los artefactos firmados — no se rechaza por "ausencia de rojos reales en todos los escenarios" porque (a) hubo 9 casos xUnit rojos reales por razón técnica correcta en fase red, (b) los 54 pinning están etiquetados como tal en spec §6 y red-notes §1, y (c) el valor documental + regresivo del pinning es explícito en METHODOLOGY §7.

Los 4 followups nuevos (#16, #17, #18, #19) tienen origen claro en spec §13 con disparador concreto para cada uno. Los 3 cierres (#4, #6, #9) son efectivos y trazables.

Los 4 nits (tabla de hallazgos #8-#11) son comentarios menores sin impacto en calidad y no generan followup.

**Orquestador puede proceder a:** commit del slice 00 + continuar con el slice que el usuario indique. El SharedKernel queda estable y auditado para futuros slices (`AsignarMontoARubro`, `ConfigurarTasaDeCambio`, `AprobarPresupuesto`).

---

_Cierre de slice 00 firmado por reviewer — 2026-04-24._
