# Green notes — Slice 00 — SharedKernel (retroactivo)

**Implementador:** green
**Fecha:** 2026-04-24
**Spec consumida:** `slices/00-shared-kernel/spec.md` (firmada 2026-04-24, Q1=Q2=Q3=Q4=(a)).
**Red-notes consumidas:** `slices/00-shared-kernel/red-notes.md` (9 casos xUnit rojos + 54 pinning verdes).

Estado de partida: 100 verdes + 9 rojos sobre 109 (`dotnet test`). Estado de salida: **109/109 verdes**. Build: 0 errores, 0 warnings.

---

## 1. Archivos modificados / creados

### Creados

1. **`src/SincoPresupuesto.Domain/SharedKernel/MonedasDistintasException.cs`** (Q1 + Q3).
   - Excepción migrada desde su ubicación anidada al final de `Dinero.cs`.
   - Base cambiada de `InvalidOperationException` a `DominioException` (Q1 aceptada).
   - Mensaje y propiedades (`Izquierda`, `Derecha`) preservadas idénticos al original — cero cambios de comportamiento fuera del tipo base.
   - Refactorizada de constructor primario a constructor explícito para que el patrón coincida con las demás excepciones del kernel (`CampoRequeridoException`, `CodigoMonedaInvalidoException`, etc.). Decisión consciente para mantener consistencia visual — ver §3.

2. **`src/SincoPresupuesto.Domain/SharedKernel/FactorDeConversionInvalidoException.cs`** (Q2).
   - Nueva excepción `FactorDeConversionInvalidoException : DominioException` con propiedad `decimal FactorIntentado`.
   - Sustituye al `throw new ArgumentException(...)` que vivía en `Dinero.En`.
   - Mapeada a HTTP 400 en `DomainExceptionHandler` (spec §9).

### Modificados

3. **`src/SincoPresupuesto.Domain/SharedKernel/Dinero.cs`**.
   - Eliminada la definición anidada de `MonedasDistintasException` (Q3; ahora vive en archivo propio).
   - Sustituido `throw new ArgumentException("El factor de conversión debe ser mayor que cero.", nameof(factor))` por `throw new FactorDeConversionInvalidoException(factor)` dentro de `En(Moneda destino, decimal factor)`.
   - Sin cambios en ninguna otra rama (happy paths de `+`, `-`, `*`, `<`, `>`, `<=`, `>=`, `En` con factor válido, `ToString`).

4. **`src/SincoPresupuesto.Domain/SharedKernel/Moneda.cs`**.
   - Añadida `public static int CantidadCodigosIso4217Soportados => CodigosIso4217Validos.Count;` junto a la propiedad `Codigo` (spec §6.17 + §12).
   - Documentación XML explica la intención: defender la cardinalidad del hash embebido contra podas accidentales.
   - Sin cambios en el constructor ni en la normalización.

5. **`src/SincoPresupuesto.Api/ExceptionHandlers/DomainExceptionHandler.cs`**.
   - Añadidas dos entradas al `switch` de `Mapear`, en la sección "400 — datos mal formados o inválidos":
     - `MonedasDistintasException => (400, "Operación entre monedas distintas")` (Q1 → mapeo HTTP, spec §9).
     - `FactorDeConversionInvalidoException => (400, "Factor de conversión inválido")` (Q2 → mapeo HTTP, spec §9).
   - Sin reordenamiento ni cambio de las entradas existentes.

### Modificado (tests, solo descomentaciones permitidas por la instrucción del orquestador)

6. **`tests/SincoPresupuesto.Domain.Tests/Slices/Slice00_SharedKernelTests.cs`**.
   - §6.12 `Dinero_En_otra_moneda_con_factor_no_positivo_lanza_FactorDeConversionInvalidoException`: eliminada la llamada a `FallaPendienteDeGreen(...)` y descomentadas las aserciones `TODO(green)` que asertan `Throw<FactorDeConversionInvalidoException>` con `FactorIntentado` + `BeAssignableTo<DominioException>`.
   - §6.17b `Moneda_CantidadCodigosIso4217Soportados_es_al_menos_150`: eliminada `FallaPendienteDeGreen(...)` y reemplazada por `Moneda.CantidadCodigosIso4217Soportados.Should().BeGreaterThanOrEqualTo(150)` (API de FluentAssertions vigente — `BeGreaterOrEqualTo` estaba comentado en el stub pero la versión del proyecto usa `BeGreaterThanOrEqualTo`).
   - Helper `FallaPendienteDeGreen` eliminado del archivo por quedar sin uso (evita código muerto). Recomendado por la instrucción del orquestador.
   - Doc comment de clase actualizada: "los tests rojos aún no pueden pasar" → "tras green §12 los tests rojos originales ya pasan"; eliminadas referencias al helper desaparecido.
   - **No se tocó ninguna otra sección de tests** (ningún assert nuevo, ninguna reordenación, ningún cambio de casos de `Theory`).

### No tocados deliberadamente

- El resto del SharedKernel (`DominioException.cs`, `Requerir.cs`, las diez excepciones concretas restantes) — intactos.
- Slices 01/02/03 y su código de producción — intactos.
- `DineroTests.cs` ya estaba borrado por red (Q4).

---

## 2. Impulsos de refactor no implementados (candidatos para refactorer)

### 2.1 Normalización de constructores de excepción del kernel

Al crear `MonedasDistintasException.cs` opté por constructor explícito con inicialización de propiedades en el cuerpo (patrón de `CampoRequeridoException`, `CodigoMonedaInvalidoException`, `CodigoRubroInvalidoException`, `CodigoRubroDuplicadoException`, `RubroPadreNoExisteException`, `PresupuestoNoEncontradoException`, `PresupuestoNoEsBorradorException`). Observaciones para refactorer:

- `CampoRequeridoException`, `PeriodoInvalidoException`, `ProfundidadMaximaFueraDeRangoException` viven **todas juntas en `DominioException.cs`** en lugar de un archivo por excepción. Inconsistente con el resto (un archivo por excepción para las demás). Candidato firme: extraer cada una a su propio archivo, coherente con Q3 aplicada a `MonedasDistintasException`.
- `CodigoHijoNoExtiendeAlPadreException`, `ProfundidadExcedidaException`, `TenantYaConfiguradoException` — verificar que el patrón de construcción (constructor primario vs. explícito) sea consistente en todas. Si no, homogeneizar a uno solo estilo.

Disparador: Q3 firmada extiende el criterio "un archivo por excepción" implícitamente a todo el kernel; las tres excepciones compartiendo `DominioException.cs` son la siguiente anomalía natural.

### 2.2 Mensaje de `MonedasDistintasException` y `FactorDeConversionInvalidoException` — i18n candidate

Los mensajes de las nuevas excepciones son strings duros en español:

```
"Operación aritmética no permitida entre monedas distintas: {izquierda} vs {derecha}. Convierte explícitamente con Dinero.En(destino, factor)."
"El factor de conversión '{factorIntentado}' es inválido. Debe ser mayor que cero."
```

Los tests nunca asertan el mensaje (INV-SK-5 lo prohíbe), así que son libres de cambiar. Si llegara un followup de i18n/localización, este es uno de los puntos. Fuera de scope del slice.

### 2.3 `Moneda.CantidadCodigosIso4217Soportados` como propiedad vs. método

Expuesto como `public static int` computada cada vez (`CodigosIso4217Validos.Count`). `HashSet<string>.Count` es O(1), pero si alguna vez se cambia a una estructura enumerable el acceso podría degradarse. Alternativa: cachear en un `static readonly int` junto a la declaración del hash. No lo hago: microoptimización sin test que la exija.

Candidato ligero para refactorer.

### 2.4 Formalizar `ToString(CultureInfo.InvariantCulture)` en `Dinero`

Ya registrado como followup #1 en spec §13. No lo abordo aquí (no es parte de §12). El test §6.13 ya fija cultura invariante manualmente. Disparador: otro CI con locale distinto, o spec futuro de serialización.

### 2.5 Migrar `CampoRequeridoException("RubroId")` en `Presupuesto.AgregarRubro` para Guid

Fuera de scope slice 00. Registrado en `slices/03-agregar-rubro/green-notes.md` §3.2. Aún sin ejecutar.

---

## 3. Decisiones deliberadas

### 3.1 Constructor explícito en `MonedasDistintasException` (no primario)

Razón: el patrón establecido en el SharedKernel para excepciones con propiedades es constructor explícito con asignaciones en el cuerpo (ver `CampoRequeridoException`, `CodigoMonedaInvalidoException`, etc.). Hoy `DominioException` expone `protected DominioException(string mensaje) : base(mensaje) { }` — un constructor primario en la subclase obliga a pasar un `base(...)` con interpolación, funcional pero estilísticamente distinto.

El código original de `MonedasDistintasException` (con constructor primario heredando de `InvalidOperationException`) era funcional pero el resto del kernel no lo usa. Mantener el estilo dominante reduce ruido cognitivo.

Cambio de comportamiento: **ninguno**. Propiedades públicas, igualdad, mensaje y tipo idéntico al esperado por los tests.

### 3.2 Eliminar el helper `FallaPendienteDeGreen` en lugar de mantenerlo

La instrucción del orquestador marcó como "recomendación" eliminarlo para evitar código muerto. Lo elimino. Si un futuro slice retroactivo lo necesita, es trivial reintroducirlo — tres líneas.

### 3.3 `BeGreaterThanOrEqualTo` en lugar de `BeGreaterOrEqualTo`

El stub `TODO(green)` de red-notes mencionaba `BeGreaterOrEqualTo` (API más antigua de FluentAssertions). La versión actual del proyecto (FluentAssertions ≥ 6.x) expone `BeGreaterThanOrEqualTo`. Reemplacé el nombre al descomentar. Cambio de API pública de la librería, no de la semántica del test.

### 3.4 Mapeo HTTP 400 para ambas excepciones nuevas

Spec §9 recomienda 400 para `MonedasDistintasException` (error de forma del caller) y 400 para `FactorDeConversionInvalidoException` (dato mal formado). Apliqué exactamente esa recomendación, sin analizar alternativas (409 por conflicto lógico, 422 por semántica no procesable, etc.). Si el reviewer discrepa, refactorer puede moverlos.

---

## 4. Verificación

Build:

```
dotnet build
  Compilación correcta.
      0 Advertencia(s)
      0 Errores
```

Tests Slice00:

```
dotnet test --filter "FullyQualifiedName~Slice00" --nologo
  Correctas! - Con error: 0, Superado: 63, Omitido: 0, Total: 63
```

Tests suite completa:

```
dotnet test --nologo
  Correctas! - Con error: 0, Superado: 109, Omitido: 0, Total: 109
```

- 100 pinning que ya estaban verdes → siguen verdes (sin regresión).
- 9 rojos → verdes:
  - 1 en §6.4 (resta entre monedas distintas, Q1).
  - 4 en §6.6 (operadores de comparación entre monedas distintas, Q1).
  - 2 en §6.12 (`En(factor=0)` y `En(factor<0)`, Q2).
  - 1 en §6.17b (cardinalidad ISO 4217).
  - 1 en §6.22 (jerarquía de `MonedasDistintasException`, Q1).

**Cobertura funcional lograda**:

- Q1: `MonedasDistintasException : DominioException` ✓ + mapeo 400 ✓.
- Q2: `FactorDeConversionInvalidoException(decimal FactorIntentado) : DominioException` ✓ + `Dinero.En` actualizado ✓ + mapeo 400 ✓.
- Q3: excepción movida a `SharedKernel/MonedasDistintasException.cs` ✓; definición anidada en `Dinero.cs` eliminada ✓.
- §6.17b: `Moneda.CantidadCodigosIso4217Soportados` expuesta ✓ (valor actual: cardinalidad del hash embebido — margen holgado sobre el umbral 150).
- Q4: ya ejecutada en fase red (archivo `DineroTests.cs` absorbido en `Slice00_SharedKernelTests.cs`).

**Sin regresiones en Slice01/02/03** (46/46 verdes conservados).

---

## 5. Handoff a refactorer

Lista priorizada (spec §12 cerrada, candidatos fuera-de-slice):

1. **Extraer las tres excepciones cohabitantes de `DominioException.cs`** (`CampoRequeridoException`, `PeriodoInvalidoException`, `ProfundidadMaximaFueraDeRangoException`) a archivos propios. Mismo criterio de Q3.
2. **Followup #1 de spec §13** — formalizar `Dinero.ToString(CultureInfo.InvariantCulture)`.
3. **Followup #10 disparado** (heredado de slice 03 §3.2) — extraer helper `RequireCampo` generalizado.
4. **Microoptimizar** `Moneda.CantidadCodigosIso4217Soportados` cacheando en `static readonly int` si alguien mide degradación.
5. **Normalizar mensajes** si i18n entra en scope.

Ninguno de los cinco puntos bloquea la salida del slice 00; todos son refactors puros que no cambian semántica.
