# Red notes — Slice 00 — SharedKernel (retroactivo)

**Autor:** red
**Fecha:** 2026-04-24
**Spec consumida:** `slices/00-shared-kernel/spec.md` (firmada 2026-04-24, commit `b6148c8`). Q1=(a), Q2=(a), Q3=(a), Q4=(a).

---

## 1. Tests escritos

Archivo: `tests/SincoPresupuesto.Domain.Tests/Slices/Slice00_SharedKernelTests.cs` (**nuevo**).
Archivo borrado: `tests/SincoPresupuesto.Domain.Tests/DineroTests.cs` (**absorbido** por Q4).

22 métodos (uno por escenario §6.X), agrupados en cuatro secciones con separadores de comentario estilo slice 02.

| # | Test (método) | Escenario spec §6.X | Tipo xUnit | Casos | Clasificación |
|---|---|---|---|---|---|
| 1 | `Dinero_suma_misma_moneda_devuelve_resultado_con_la_misma_moneda` | 6.1 | Fact | 1 | pinning |
| 2 | `Dinero_suma_entre_monedas_distintas_lanza_MonedasDistintasException` | 6.2 | Fact | 1 | pinning |
| 3 | `Dinero_resta_misma_moneda_devuelve_diferencia` | 6.3 | Theory | 2 (+/- result) | pinning |
| 4 | `Dinero_resta_entre_monedas_distintas_lanza_MonedasDistintasException_que_es_DominioException` | 6.4 | Fact | 1 | **rojo (Q1)** |
| 5 | `Dinero_operadores_de_comparacion_con_misma_moneda_devuelven_resultado_esperado` | 6.5 | Fact | 1 (4 operadores) | pinning |
| 6 | `Dinero_operadores_de_comparacion_entre_monedas_distintas_lanzan_MonedasDistintasException_que_es_DominioException` | 6.6 | Theory (MemberData) | 4 (`<`, `>`, `<=`, `>=`) | **rojo (Q1)** |
| 7 | `Dinero_multiplicacion_por_factor_en_ambos_lados_es_conmutativa` | 6.7 | Theory | 3 (pos/cero/neg) | pinning |
| 8 | `Dinero_Cero_devuelve_neutro_aditivo_con_la_moneda_indicada` | 6.8 | Fact | 1 | pinning |
| 9 | `Dinero_helpers_EsCero_EsPositivo_EsNegativo_reflejan_el_signo_del_valor` | 6.9 | Theory | 3 (0, +ε, −ε) | pinning |
| 10 | `Dinero_En_misma_moneda_ignora_el_factor_y_devuelve_el_mismo_valor` | 6.10 | Fact | 1 | pinning |
| 11 | `Dinero_En_otra_moneda_con_factor_positivo_aplica_el_factor` | 6.11 | Fact | 1 | pinning |
| 12 | `Dinero_En_otra_moneda_con_factor_no_positivo_lanza_FactorDeConversionInvalidoException` | 6.12 | Theory | 2 (0, −1) | **rojo (Q2)** |
| 13 | `Dinero_ToString_formatea_valor_y_codigo_de_moneda` | 6.13 | Theory | 6 | pinning |
| 14 | `Moneda_normaliza_codigo_con_trim_y_upperinvariant` | 6.14 | Theory | 4 | pinning |
| 15 | `Moneda_rechaza_codigos_mal_formados_con_CodigoMonedaInvalidoException` | 6.15 | Theory | 6 (incluye null + whitespace) | pinning |
| 16 | `Moneda_rechaza_codigos_tres_letras_no_ISO_4217` | 6.16 | Theory | 3 | pinning |
| 17a | `Moneda_acepta_codigos_del_sample_ISO_4217` | 6.17 sample | Theory | 17 | pinning |
| 17b | `Moneda_CantidadCodigosIso4217Soportados_es_al_menos_150` | 6.17 cardinalidad | Fact | 1 | **rojo (§6.17 / §12)** |
| 18 | `Moneda_igualdad_por_valor_normalizado_y_mismo_hashcode` | 6.18 | Fact | 1 | pinning |
| 19 | `Moneda_ToString_y_conversion_implicita_a_string_devuelven_el_Codigo` | 6.19 | Fact | 1 | pinning |
| 20 | `Requerir_Campo_happy_y_fallo_cubre_null_vacio_y_whitespace` | 6.20 | Fact | 1 (5 casos consolidados) | pinning |
| 21 | `DominioException_contrato_de_jerarquia_y_propiedades_en_todas_las_excepciones_del_kernel` | 6.21 | Fact | 1 (12 excepciones) | pinning |
| 22 | `MonedasDistintasException_preserva_propiedades_y_es_DominioException` | 6.22 | Fact | 1 | **rojo (Q1)** |

**Conteo:**
- Métodos de test: **22** (uno por escenario §6.X, incluyendo 17a/17b para cubrir los dos asertos independientes de §6.17).
- Casos xUnit tras expansión de `Theory`: **63** (visible en `dotnet test`).
- Clasificación por método: **17 pinning** + **5 rojos** (§6.4, §6.6, §6.12, §6.17b, §6.22). En casos xUnit expandidos: **54 pinning** (verdes hoy) + **9 rojos** (4 de §6.6 × operadores + 2 de §6.12 × factores + 1 de §6.4 + 1 de §6.17b + 1 de §6.22).

**Nota metodológica (METHODOLOGY §7.3, spec §16 / retroactividad).** Los tests pinning pasan de inmediato sobre el código actual — excepción explícita a "red siempre falla primero" aplicable a slices retroactivos. El valor que aportan es documental (congelan el contrato público que §6 enumera) y de regresión (cualquier cambio futuro en `Dinero`/`Moneda`/`Requerir` que rompa una rama existente se detectará).

## 2. Verificación de estado rojo

Comando usado:

```bash
dotnet test tests/SincoPresupuesto.Domain.Tests/SincoPresupuesto.Domain.Tests.csproj \
  --filter "FullyQualifiedName~Slice00" --nologo
```

Resultado observado:

```
Con error! - Con error: 9, Superado: 54, Omitido: 0, Total: 63
```

Detalle por test rojo (razón técnica del fallo):

| Test rojo (caso xUnit) | §6.X | Razón del fallo |
|---|---|---|
| `Dinero_resta_entre_monedas_distintas_lanza_MonedasDistintasException_que_es_DominioException` | 6.4 | `BeAssignableTo<DominioException>` falla: hoy `MonedasDistintasException` hereda de `InvalidOperationException` (Q1 — green cambiará la base). |
| `Dinero_operadores_…_MonedasDistintasException_que_es_DominioException("<")` | 6.6 | idem Q1. |
| `Dinero_operadores_…_MonedasDistintasException_que_es_DominioException(">")` | 6.6 | idem Q1. |
| `Dinero_operadores_…_MonedasDistintasException_que_es_DominioException("<=")` | 6.6 | idem Q1. |
| `Dinero_operadores_…_MonedasDistintasException_que_es_DominioException(">=")` | 6.6 | idem Q1. |
| `Dinero_En_otra_moneda_con_factor_no_positivo_lanza_FactorDeConversionInvalidoException(0)` | 6.12 | `NotImplementedException("Esperando green (§12): Dinero.En con factor <= 0 debe lanzar FactorDeConversionInvalidoException …")`. La excepción `FactorDeConversionInvalidoException` aún no existe en `src/` (Q2). El test dispara el stub del helper `FallaPendienteDeGreen`. |
| `Dinero_En_otra_moneda_con_factor_no_positivo_lanza_FactorDeConversionInvalidoException(-1)` | 6.12 | idem Q2. |
| `Moneda_CantidadCodigosIso4217Soportados_es_al_menos_150` | 6.17b | `NotImplementedException("Esperando green (§12): Exponer `public static int Moneda.CantidadCodigosIso4217Soportados` …")`. La propiedad no existe aún; el test dispara el stub `FallaPendienteDeGreen`. |
| `MonedasDistintasException_preserva_propiedades_y_es_DominioException` | 6.22 | `BeAssignableTo<DominioException>` falla: hoy `MonedasDistintasException` hereda de `InvalidOperationException` (Q1). |

**9 rojos — todos por razón correcta** (aserción de dominio no cumplida o stub `FallaPendienteDeGreen` disparado por API aún inexistente). Ningún rojo por error de compilación.

**Regresión en otros slices.** Comando:

```bash
dotnet test tests/SincoPresupuesto.Domain.Tests/SincoPresupuesto.Domain.Tests.csproj \
  --filter "FullyQualifiedName!~Slice00" --nologo
# salida: Con error: 0, Superado: 46, Omitido: 0, Total: 46
```

Desglose: Slice01=16, Slice02=7, Slice03=23, total **46 verdes, 0 rojos** en tests no-Slice00. El conteo baja de los 58 anteriores a 46 porque los **12 casos xUnit de `DineroTests.cs`** (6 métodos × expansión de Theory) se **absorben** en `Slice00_SharedKernelTests.cs` por decisión Q4 — no son regresiones, son movimientos.

Build limpio: `dotnet build` → `0 Advertencia(s), 0 Errores`.

## 3. Código de producción tocado

- [x] **Sin cambios en `src/`** — ni en `Dinero.cs`, ni en `Moneda.cs`, ni en ninguna excepción del kernel. No se añadieron stubs en `src/`. Toda la "barrera" para los tests rojos que dependen de APIs inexistentes (`Moneda.CantidadCodigosIso4217Soportados`, `FactorDeConversionInvalidoException`) se implementó **dentro del propio archivo de tests** mediante el helper privado `FallaPendienteDeGreen(string detalle)` que lanza `NotImplementedException` con un mensaje indicando qué debe hacer green (alternativa aceptada por el briefing cuando crear stubs en `src/` forzaría API pública que green aún no debe formalizar).
- [x] Cada test rojo que depende de API futura incluye un comentario `TODO(green): …` con el cuerpo esperado una vez la API exista, listo para que green lo descomente.
- [x] Ningún stub en `src/` devuelve un valor que enmascare el rojo.

### Archivos de tests tocados

1. **Nuevo:** `tests/SincoPresupuesto.Domain.Tests/Slices/Slice00_SharedKernelTests.cs` — 22 métodos (§6.1–§6.22), namespace `SincoPresupuesto.Domain.Tests.Slices`, clase `Slice00_SharedKernelTests`. Incluye `using System.Globalization` para fijar cultura invariante en §6.13 (followup #1 de spec §13).
2. **Borrado:** `tests/SincoPresupuesto.Domain.Tests/DineroTests.cs` — contenido absorbido en §6.1, §6.2, §6.10, §6.11, §6.14, §6.15 del archivo nuevo (Q4).

## 4. Desviaciones respecto a la spec

- [x] Sin desviaciones de §6.

Notas menores de ejecución (no alteran la intención):

- **§6.17** se implementa como **dos métodos separados** (`Moneda_acepta_codigos_del_sample_ISO_4217` + `Moneda_CantidadCodigosIso4217Soportados_es_al_menos_150`) en vez de uno, para que el rojo de cardinalidad no oculte el verde del sampling y para que el runner de xUnit reporte cada parte con diagnóstico independiente. La spec describe §6.17 con dos asertos, así que esto es fiel al escenario.
- **§6.20** (`Requerir.Campo`) consolida los cinco casos (happy + 2 whitespace-no-trim + 3 fallos) en un único `Fact` porque la spec los plantea como un bloque conjunto ("happy path y fallo"). Un `Theory` hubiera duplicado la estructura por entrada.
- **§6.21** se implementa como un único `Fact` que instancia las doce excepciones del kernel y verifica asignabilidad + propiedades. Dado que el escenario enumera las doce excepciones en un solo bloque de Given, se respeta esa agrupación.
- **§6.13** fija cultura invariante vía `try/finally` con `CultureInfo.CurrentCulture` (sin `using` helper externo, para no arrastrar dependencia nueva). Equivalente funcional a lo propuesto en la spec.
- **§6.6** usa `[MemberData]` con tuplas `(nombre, Action<Dinero,Dinero>)` para que los cuatro operadores salgan como casos independientes en el reporte (mejor diagnóstico que `[InlineData]` con una cadena-operador + switch interno).

## 5. Hand-off a green

- [x] Spec firmada: sí (2026-04-24, Q1=Q2=Q3=Q4=(a)).
- [x] Todos los tests compilan: sí (`dotnet build` → 0 warnings / 0 errores).
- [x] 9 casos xUnit rojos por razón correcta; 54 verdes por pinning retroactivo.
- [x] Sin regresión en Slice01/02/03 (46/46 verdes).
- [x] `DineroTests.cs` borrado; contenido absorbido (Q4).
- [x] Sin cambios en `src/` — green aplica §12 completo.

**Green debe aplicar §12 de la spec**, específicamente:

1. `MonedasDistintasException : DominioException` (Q1). Al migrar base, descomentar nada en tests — ya están escritos con `BeAssignableTo<DominioException>` activo.
2. Mover `MonedasDistintasException` a `src/SincoPresupuesto.Domain/SharedKernel/MonedasDistintasException.cs` (Q3). Cero cambios en tests.
3. Crear `src/SincoPresupuesto.Domain/SharedKernel/FactorDeConversionInvalidoException.cs` (Q2) con `public decimal FactorIntentado { get; }` y base `DominioException`. Luego, en el test `Dinero_En_otra_moneda_con_factor_no_positivo_lanza_FactorDeConversionInvalidoException`, descomentar el bloque `TODO(green)` y eliminar la llamada a `FallaPendienteDeGreen`.
4. Modificar `Dinero.En` para lanzar `FactorDeConversionInvalidoException(factor)` en lugar de `ArgumentException` cuando `destino != Moneda && factor <= 0`.
5. Exponer en `Moneda.cs`: `public static int CantidadCodigosIso4217Soportados => CodigosIso4217Validos.Count;` (§6.17). Luego descomentar el bloque `TODO(green)` del test `Moneda_CantidadCodigosIso4217Soportados_es_al_menos_150`.
6. Actualizar `src/SincoPresupuesto.Api/ExceptionHandlers/DomainExceptionHandler.cs` añadiendo al `switch`: `MonedasDistintasException → 400` y `FactorDeConversionInvalidoException → 400`.

Al finalizar green, los 63 casos xUnit de Slice00 deben ser verdes sin que ningún test del archivo tenga que reescribirse — solo descomentar los dos bloques `TODO(green)` y borrar las dos llamadas a `FallaPendienteDeGreen`.
