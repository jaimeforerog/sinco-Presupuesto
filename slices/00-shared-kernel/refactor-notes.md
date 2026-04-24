# Refactor notes — Slice 00 — SharedKernel

**Autor:** refactorer
**Fecha:** 2026-04-24
**Estado de entrada:** green cerrado, 109/109 verdes, 0 warnings.
**Estado de salida:** 109/109 verdes, 0 warnings.

---

## Contexto

Slice retroactivo sobre el SharedKernel. Green cerró los cuatro cambios de §12 de la spec (Q1–Q4 aceptadas) y dejó cinco impulsos de refactor en `green-notes.md` §2 a evaluar. Además, la pregunta Q3 (mover `MonedasDistintasException` a su propio archivo) extiende implícitamente el criterio "un archivo por excepción" a todo el kernel: el único rincón que aún incumplía esa regla era `DominioException.cs`, que cohabitaba con tres subclases concretas.

## Cambios aplicados

| # | Tipo | Archivo(s) | Descripción | Tests antes | Tests después |
|---|---|---|---|---|---|
| 1 | extract file | `src/SincoPresupuesto.Domain/SharedKernel/CampoRequeridoException.cs` (nuevo) + `DominioException.cs` (reducido) | Extracción de `CampoRequeridoException` a archivo propio. Sin cambios en el cuerpo de la clase (XML doc, propiedades, constructor idénticos). Motivo: consistencia con el resto del kernel — todas las demás excepciones viven en archivo propio; tras Q3 este era el último rincón incoherente. | 109 pass | 109 pass |
| 2 | extract file | `src/SincoPresupuesto.Domain/SharedKernel/PeriodoInvalidoException.cs` (nuevo) + `DominioException.cs` (reducido) | Extracción de `PeriodoInvalidoException` a archivo propio. Cuerpo idéntico al original. | 109 pass | 109 pass |
| 3 | extract file | `src/SincoPresupuesto.Domain/SharedKernel/ProfundidadMaximaFueraDeRangoException.cs` (nuevo) + `DominioException.cs` (reducido) | Extracción de `ProfundidadMaximaFueraDeRangoException` a archivo propio. Cuerpo idéntico al original. Deja `DominioException.cs` con **solo** la clase base abstracta — punto de entrada único para "la jerarquía de excepciones del dominio". | 109 pass | 109 pass |
| 4 | doc | `src/SincoPresupuesto.Domain/SharedKernel/PresupuestoNoEncontradoException.cs` | Añadida docstring XML `<summary>` (el archivo era la única excepción del kernel sin summary). Sin cambio de comportamiento. Homogeneiza el estilo de documentación en las 14 excepciones. | 109 pass | 109 pass |

## Refactors descartados

| # | Sugerido por | Motivo para no aplicar |
|---|---|---|
| 1 | green-notes §2.2 — `Dinero.ToString(CultureInfo.InvariantCulture)` | Ya registrado como followup #1 de spec slice-00 §13 (pendiente de disparador: primer CI fallando por locale). Los tests fijan cultura manualmente; el comportamiento observable no cambia. El cambio sí sería un cambio de API (sobrecarga sin parámetro → con cultura implícita), y la instrucción del orquestador lo marca explícitamente como followup futuro, no refactor inmediato. |
| 2 | green-notes §2.3 — helper `Requerir` generalizado (ej. `RangoEnteroCerrado`) | Un único uso potencial (en `Presupuesto.Create` con `ProfundidadMaxima`). Extraer ahora sería abstracción especulativa — regla DRY exige "≥ 3 ocurrencias" (precedente: slice 03 cerró `Requerir.Campo` solo cuando había 6 usos). Followup implícito: cuando aparezca un segundo caso. |
| 3 | green-notes §2.1 — homogeneizar estilo de constructor entre excepciones | **Evaluado y ya uniforme.** Las 14 excepciones del kernel (`CampoRequeridoException`, `PeriodoInvalidoException`, `ProfundidadMaximaFueraDeRangoException`, `CodigoMonedaInvalidoException`, `TenantYaConfiguradoException`, `PresupuestoNoEsBorradorException`, `CodigoRubroInvalidoException`, `CodigoRubroDuplicadoException`, `CodigoHijoNoExtiendeAlPadreException`, `RubroPadreNoExisteException`, `ProfundidadExcedidaException`, `PresupuestoNoEncontradoException`, `MonedasDistintasException`, `FactorDeConversionInvalidoException`) usan **exactamente el mismo patrón**: XML doc → `public sealed class X : DominioException` → propiedades públicas → constructor explícito con `: base(interpolación)` + asignaciones en el cuerpo. Green ya aplicó este patrón a `MonedasDistintasException` al moverlo (green-notes §3.1). No hay outlier que alinear. |
| 4 | green-notes §2.4 — cache de `CantidadCodigosIso4217Soportados` en `static readonly int` | Microoptimización sin test que la exija. `HashSet<string>.Count` es O(1) — la propiedad computada cada vez tiene costo constante trivial. Introducir un `static readonly int` duplicaría estado (hash + contador) con riesgo mínimo pero real de desync si alguien modifica el hash. Sin beneficio medible. Descartado. |
| 5 | green-notes §2.5 — i18n / localización de mensajes | Fuera de scope. INV-SK-5 garantiza que los tests nunca asertan contra el mensaje, así que los strings duros en español no bloquean nada. Cuando entre i18n será un slice dedicado. |

## Cero cambios de comportamiento observable

- La superficie pública del SharedKernel es idéntica: mismos tipos, mismos namespaces (`SincoPresupuesto.Domain.SharedKernel`), mismas propiedades, mismas signaturas de constructor, mismos mensajes de excepción, misma base `DominioException`.
- Ningún test se tocó.
- Ningún slice consumidor (01, 02, 03) requiere cambios: los `using SincoPresupuesto.Domain.SharedKernel;` siguen resolviendo las tres excepciones extraídas sin modificaciones.
- La eliminación del `DominioException.cs` "grueso" es un movimiento de texto a nivel de archivos — el compilador no distingue entre una clase en un archivo u otro siempre que compartan namespace.

## Impacto en SharedKernel

Antes:

```
SharedKernel/
├── DominioException.cs          ← base + 3 excepciones (incoherente)
├── MonedasDistintasException.cs
├── CodigoMonedaInvalidoException.cs
├── … (10 excepciones más en archivos propios)
├── Dinero.cs, Moneda.cs, Requerir.cs
```

Después:

```
SharedKernel/
├── DominioException.cs          ← solo la base abstracta
├── CampoRequeridoException.cs   ← nuevo
├── PeriodoInvalidoException.cs  ← nuevo
├── ProfundidadMaximaFueraDeRangoException.cs  ← nuevo
├── MonedasDistintasException.cs
├── CodigoMonedaInvalidoException.cs
├── … (resto sin cambios)
├── Dinero.cs, Moneda.cs, Requerir.cs
```

Total de archivos en `SharedKernel/`: 15 (antes 12). Cada excepción vive en su archivo; `DominioException.cs` solo contiene la base abstracta.

## Acciones en FOLLOWUPS.md

**Ninguna nueva.** Los refactors aplicados (#1–#4) son movimientos internos sin deuda residual. Los refactors descartados (#1, #2, #4, #5) o están ya registrados como followups de la spec §13 (caso de `Dinero.ToString(CultureInfo)`) o no han alcanzado su disparador (helper `Requerir` generalizado, i18n, cache).

## Verificación

```bash
dotnet build       → Compilación correcta. 0 Advertencia(s), 0 Errores.
dotnet test        → Correctas! - Con error: 0, Superado: 109, Omitido: 0, Total: 109.
```

Cada uno de los cuatro refactors aplicados se verificó incremental antes de pasar al siguiente:

1. Extracción de `CampoRequeridoException` + `PeriodoInvalidoException` + `ProfundidadMaximaFueraDeRangoException` (tres archivos nuevos + un `DominioException.cs` reducido a la base): 109/109.
2. Docstring XML en `PresupuestoNoEncontradoException`: 109/109.

## Resumen ejecutivo

- **4 refactors aplicados**: 3 extracciones de excepciones cohabitantes a archivos propios + 1 armonización de XML doc.
- **5 impulsos de green evaluados y descartados** con razón técnica (followup futuro, abstracción especulativa, estilo ya uniforme, microoptimización, o fuera de scope).
- **Cero cambios de comportamiento**, cero warnings, **109/109 verdes**.
- **Followup #4 (closing path)**: al nivel del slice queda documentado que Q3 se extendió coherentemente al resto del kernel — no hay ya ninguna excepción cohabitando en un archivo compartido.
