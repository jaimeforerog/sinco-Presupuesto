# Slice 00 — SharedKernel (Dinero, Moneda, excepciones de dominio)

**Autor:** domain-modeler (retroactivo)
**Fecha:** 2026-04-24
**Estado:** firmado
**Agregado afectado:** _ninguno_ — este slice documenta el **SharedKernel** (tipos de soporte transversal). No hay comando de negocio, no hay eventos.
**Decisiones previas relevantes:**
- `02-decisiones-hotspots-mvp.md` §2 (multimoneda a nivel de partida; `Dinero(Valor, Moneda)` como VO obligatorio; `Moneda` como código ISO 4217; snapshot de tasas al aprobar).
- `METHODOLOGY.md` §7.3 (bug-fix / slice retroactivo = TDD al revés: test que justifica la rama existente → impl ya verde → refactor si aplica), §8 (convenciones de código y naming).
- `slices/01-crear-presupuesto/spec.md` (precedente de slice retroactivo sobre código ya existente).
- `slices/02-configurar-moneda-local-del-tenant/spec.md` §12 (origen de `CodigoMonedaInvalidoException` y lista ISO 4217 embebida en `Moneda`).
- `slices/03-agregar-rubro/spec.md` §13 + refactor-notes (origen de `SharedKernel.Requerir.Campo`).
- `FOLLOWUPS.md` #4 (este slice), #6 (gaps de tests de `Dinero`), #9 (completitud ISO 4217).
- `src/SincoPresupuesto.Api/ExceptionHandlers/DomainExceptionHandler.cs` (mapeo HTTP actual; referenciado en §10 Q1/Q2).

**Nota de retroactividad:** El código del SharedKernel existe desde el scaffold inicial. La finalidad de este slice es (a) **documentar** el contrato público como spec de referencia, (b) **cerrar gaps** de tests (followup #6, #9) y reubicar `DineroTests.cs` al convenio `Slices/Slice00_SharedKernelTests.cs` (followup #4), y (c) **cerrar inconsistencias** menores detectadas al auditar el kernel (ver §10 Q1–Q3, §12). Los escenarios de §6 se marcan explícitamente como **(pinning — ya pasa hoy)** o **(rojo — requiere fase green)** para que `red` sepa cuáles tests nuevos deberán fallar antes de que `green` los haga pasar.

---

## 1. Intención

El SharedKernel agrupa los tipos transversales usados por todos los agregados y casos de uso del dominio:

- **`Dinero`** — VO inmutable `(Valor: decimal, Moneda: Moneda)`. Toda cantidad monetaria en el dominio lo usa. Sus operadores aritméticos rechazan combinar monedas distintas; la conversión es explícita vía `En(destino, factor)`.
- **`Moneda`** — VO inmutable que encapsula un código ISO 4217 validado contra una lista embebida. Construible desde `string` con normalización (trim + upper-invariant).
- **`DominioException`** — clase base abstracta de la que heredan **todas** las excepciones de dominio. Los tests asertan por **tipo + propiedades**, nunca por mensaje.
- **Excepciones concretas** del kernel: `CampoRequeridoException`, `PeriodoInvalidoException`, `ProfundidadMaximaFueraDeRangoException`, `CodigoMonedaInvalidoException`, `TenantYaConfiguradoException`, `PresupuestoNoEsBorradorException`, `CodigoRubroInvalidoException`, `CodigoRubroDuplicadoException`, `CodigoHijoNoExtiendeAlPadreException`, `RubroPadreNoExisteException`, `ProfundidadExcedidaException`, `PresupuestoNoEncontradoException`.
- **`MonedasDistintasException`** — hoy hereda de `InvalidOperationException` y vive anidada en `Dinero.cs` (ver §10 Q1 y Q3).
- **`Requerir.Campo(valor, nombreCampo)`** — helper estático que lanza `CampoRequeridoException` ante nulo/vacío/whitespace y devuelve el valor válido para encadenar.

Este slice no introduce ningún comando ni emite eventos. La unidad de trabajo son los **VOs y helpers**, por lo que §2 y §3 se reinterpretan: §2 describe el **contrato público** del kernel (tipos + operaciones), §3 queda como "no aplica".

## 2. Contrato público del SharedKernel

> Plantilla adaptada: en un slice de comando `§2` lista el record comando; aquí se enumeran los tipos y operaciones cuyo comportamiento cubre §6.

### 2.1 `Dinero`

```csharp
public readonly record struct Dinero(decimal Valor, Moneda Moneda)
{
    // Constructores / fábricas
    public static Dinero Cero(Moneda moneda);                       // → new(0m, moneda)

    // Helpers de predicado
    public bool EsCero    { get; }                                  // Valor == 0m
    public bool EsPositivo{ get; }                                  // Valor > 0m
    public bool EsNegativo{ get; }                                  // Valor < 0m

    // Conversión a otra moneda (caller responsable del factor)
    public Dinero En(Moneda destino, decimal factor);

    // Aritmética (misma moneda obligatoria)
    public static Dinero operator +(Dinero a, Dinero b);
    public static Dinero operator -(Dinero a, Dinero b);
    public static Dinero operator *(Dinero a, decimal factor);
    public static Dinero operator *(decimal factor, Dinero a);

    // Comparación (misma moneda obligatoria)
    public static bool   operator <(Dinero a, Dinero b);
    public static bool   operator >(Dinero a, Dinero b);
    public static bool   operator <=(Dinero a, Dinero b);
    public static bool   operator >=(Dinero a, Dinero b);

    // Formato
    public override string ToString();                              // "{Valor:0.####} {Moneda.Codigo}"
}
```

### 2.2 `Moneda`

```csharp
public readonly record struct Moneda
{
    public Moneda(string codigo);                                   // normaliza (trim + upper) y valida ISO 4217
    public string Codigo { get; }
    public override string ToString();                              // == Codigo
    public static implicit operator string(Moneda m);               // m.Codigo

    // Atajos para las monedas más usadas del mercado objetivo
    public static readonly Moneda COP, USD, EUR, MXN, CLP, PEN, ARS;
}
```

### 2.3 `DominioException` y descendientes

```csharp
public abstract class DominioException : Exception { /* mensajes solo informativos */ }

// Subclases concretas (cada una inmutable, propiedades públicas con los datos del contexto):
public sealed class CampoRequeridoException(string NombreCampo) : DominioException;
public sealed class PeriodoInvalidoException(DateOnly PeriodoInicio, DateOnly PeriodoFin) : DominioException;
public sealed class ProfundidadMaximaFueraDeRangoException(int Valor, int MinimoInclusivo, int MaximoInclusivo) : DominioException;
public sealed class CodigoMonedaInvalidoException(string CodigoIntentado) : DominioException;
public sealed class TenantYaConfiguradoException(string TenantId, Moneda MonedaLocalActual) : DominioException;
public sealed class PresupuestoNoEsBorradorException(EstadoPresupuesto EstadoActual) : DominioException;
public sealed class CodigoRubroInvalidoException(string CodigoIntentado) : DominioException;
public sealed class CodigoRubroDuplicadoException(string CodigoIntentado) : DominioException;
public sealed class CodigoHijoNoExtiendeAlPadreException(string CodigoPadre, string CodigoHijo) : DominioException;
public sealed class RubroPadreNoExisteException(Guid RubroPadreId) : DominioException;
public sealed class ProfundidadExcedidaException(int ProfundidadMaxima, int NivelIntentado) : DominioException;
public sealed class PresupuestoNoEncontradoException(Guid PresupuestoId) : DominioException;
```

### 2.4 `MonedasDistintasException` (estado actual)

```csharp
// Hoy: hereda de InvalidOperationException, vive dentro de Dinero.cs.
public sealed class MonedasDistintasException(Moneda Izquierda, Moneda Derecha) : InvalidOperationException;
```

Ver §10 Q1 y Q3 + §12 para la propuesta de migración.

### 2.5 `Requerir`

```csharp
public static class Requerir
{
    // Lanza CampoRequeridoException si null/empty/whitespace; devuelve el valor para encadenar.
    public static string Campo(string? valor, string nombreCampo);
}
```

## 3. Evento(s) emitido(s)

**No aplica.** El SharedKernel no emite eventos; es infraestructura de dominio. Los eventos concretos pertenecen a los slices de comandos que usan estos tipos (01, 02, 03, …).

## 4. Precondiciones

Un SharedKernel no ejecuta comandos, así que "precondiciones" se reinterpretan como **precondiciones de construcción o de operación** de los VOs:

- `PRE-Dinero.1`: en `+` y `-`, ambos operandos comparten `Moneda` — de lo contrario `MonedasDistintasException`.
- `PRE-Dinero.2`: en `<`, `>`, `<=`, `>=`, ambos operandos comparten `Moneda` — de lo contrario `MonedasDistintasException`.
- `PRE-Dinero.3`: en `En(destino, factor)`, si `destino != Moneda`, entonces `factor > 0m` — de lo contrario **hoy** `ArgumentException`; propuesta en §12 migrarlo a `FactorDeConversionInvalidoException : DominioException` (ver §10 Q2).
- `PRE-Moneda.1`: el `codigo` recibido por el constructor no es null/empty/whitespace — de lo contrario `CodigoMonedaInvalidoException`.
- `PRE-Moneda.2`: tras normalizar (`Trim().ToUpperInvariant()`), el código tiene longitud 3 y solo letras A–Z — de lo contrario `CodigoMonedaInvalidoException`.
- `PRE-Moneda.3`: el código normalizado pertenece a la lista ISO 4217 embebida (`CodigosIso4217Validos`) — de lo contrario `CodigoMonedaInvalidoException`.
- `PRE-Requerir.1`: `Requerir.Campo(valor, nombreCampo)` con `valor` null/empty/whitespace lanza `CampoRequeridoException` con `NombreCampo = nombreCampo`.

## 5. Invariantes del SharedKernel

> Reinterpretación: en un slice de comando `§5` lista invariantes del agregado. Aquí son invariantes **de los VOs**.

- `INV-SK-1`: `Dinero` es inmutable (record struct readonly). Dos `Dinero` con mismo `Valor` y misma `Moneda` son iguales por valor.
- `INV-SK-2`: Las operaciones aritméticas y comparativas de `Dinero` **nunca** mezclan monedas en silencio: o lanzan `MonedasDistintasException`, o exigen conversión explícita vía `En`.
- `INV-SK-3`: `Moneda` es inmutable. Dos `Moneda` con mismo `Codigo` (tras normalización) son iguales por valor. `Codigo` siempre está en mayúsculas ASCII de 3 letras y pertenece a ISO 4217 vigente.
- `INV-SK-4`: Toda violación de regla del kernel que el dominio deba traducir a un error de negocio hereda de `DominioException`. (Hoy incumple `MonedasDistintasException` y el `throw new ArgumentException` del `En` — ver §10 y §12.)
- `INV-SK-5`: Los mensajes de las excepciones son **informativos, no contractuales** — los tests verifican tipo y propiedades estructuradas (nombre del campo, códigos, monedas, etc.), nunca strings.

## 6. Escenarios Given / When / Then

> Cada escenario se traduce a **un test** en la fase red. La etiqueta al final de cada encabezado indica el estado esperado:
>
> - **(pinning — ya pasa hoy)** → el test DEBE fallar en la fase red **únicamente porque aún no existe**; en cuanto `red` lo escriba sobre el código actual pasará sin cambios en `src/`. La fase green para este test es un no-op.
> - **(rojo — requiere green)** → el test ejercita una rama que el código actual no implementa o implementa de forma incompatible (ver §12). Requiere cambio en `src/` para pasar.

### 6.1 `Dinero` — suma de misma moneda (happy) — (pinning — ya pasa hoy)

**Given** `a = Dinero(100, COP)`, `b = Dinero(50, COP)`.
**When** `a + b`.
**Then** resultado es `Dinero(150, COP)`.

_(Ya cubierto por `DineroTests.Suma_de_misma_moneda_funciona`; se traslada al nuevo archivo sin cambios.)_

### 6.2 `Dinero` — suma entre monedas distintas lanza `MonedasDistintasException` — (pinning — ya pasa hoy)

**Given** `a = Dinero(100, COP)`, `b = Dinero(10, USD)`.
**When** `a + b`.
**Then** lanza `MonedasDistintasException` con `Izquierda = COP`, `Derecha = USD`.

_(Ya cubierto por `DineroTests.Suma_entre_monedas_distintas_lanza_MonedasDistintasException`; se traslada.)_

### 6.3 `Dinero` — resta de misma moneda (happy + resultado negativo) — (rojo — requiere green si se aplica Q1)

**Given** `a = Dinero(100, COP)`, `b = Dinero(30, COP)`.
**When** `a - b`.
**Then** resultado es `Dinero(70, COP)`.

**Y** para el caso límite: `Given c = Dinero(30, COP), d = Dinero(100, COP); When c - d; Then resultado es Dinero(-70, COP) y resultado.EsNegativo == true`. (Un `Dinero` negativo es semánticamente válido — representa saldo a favor / ajuste.)

_Etiqueta: **pinning** si Q1 se rechaza; **rojo** si Q1 se acepta (porque la excepción del escenario 6.5 migrará de base y el test del cuerpo positivo seguirá pasando, pero se agrupa con 6.5)._

### 6.4 `Dinero` — resta entre monedas distintas — ver Q1

**Given** `a = Dinero(100, COP)`, `b = Dinero(10, USD)`.
**When** `a - b`.
**Then** lanza `MonedasDistintasException` con `Izquierda = COP`, `Derecha = USD`.

- Si Q1 se **acepta** (opción a en §10): la excepción hereda ahora de `DominioException`. El test adicional `act.Should().Throw<DominioException>()` debe pasar → **(rojo — requiere green)**.
- Si Q1 se **rechaza**: solo se asserta el tipo concreto `MonedasDistintasException` y sus propiedades → **(pinning — ya pasa hoy)**.

### 6.5 `Dinero` — operadores de comparación happy (misma moneda) — (pinning — ya pasa hoy)

Cuatro escenarios agrupados (uno por operador):

**Given** `a = Dinero(100, COP)`, `b = Dinero(50, COP)`, `c = Dinero(100, COP)`.
**When** / **Then**:
- `a > b` → `true`; `b > a` → `false`; `a > c` → `false`.
- `b < a` → `true`; `a < b` → `false`; `a < c` → `false`.
- `a >= c` → `true`; `a >= b` → `true`; `b >= a` → `false`.
- `a <= c` → `true`; `b <= a` → `true`; `a <= b` → `false`.

_(Caso límite de igualdad cubierto explícitamente: `a >= c` y `a <= c` con `a == c`.)_

### 6.6 `Dinero` — operadores de comparación entre monedas distintas lanzan — ver Q1

**Given** `a = Dinero(100, COP)`, `b = Dinero(10, USD)`.
**When** cada uno de `a < b`, `a > b`, `a <= b`, `a >= b`.
**Then** cada uno lanza `MonedasDistintasException` con `Izquierda = COP`, `Derecha = USD`.

- Q1 aceptada → adicional `Should().Throw<DominioException>()` → **(rojo — requiere green)**.
- Q1 rechazada → solo tipo concreto → **(pinning — ya pasa hoy)**.

### 6.7 `Dinero` — multiplicación por factor (ambos lados) — (pinning — ya pasa hoy)

**Given** `a = Dinero(100, COP)`.
**When** / **Then**:
- `a * 2m` → `Dinero(200, COP)`.
- `2m * a` → `Dinero(200, COP)` (simetría del operador).
- `a * 0m` → `Dinero(0, COP)` con `.EsCero == true`.
- `a * -1m` → `Dinero(-100, COP)` con `.EsNegativo == true` (factor negativo permitido: representa "contra-asiento").

### 6.8 `Dinero.Cero(Moneda)` devuelve neutro aditivo — (pinning — ya pasa hoy)

**Given** — nada.
**When** `Dinero.Cero(Moneda.USD)`.
**Then** resultado es `new Dinero(0m, Moneda.USD)` y `.EsCero == true`, `.EsPositivo == false`, `.EsNegativo == false`.

### 6.9 Helpers `EsCero` / `EsPositivo` / `EsNegativo` — (pinning — ya pasa hoy)

**Given** `cero = Dinero(0, COP)`, `pos = Dinero(0.0001m, COP)`, `neg = Dinero(-0.0001m, COP)`.
**When** / **Then**:
- `cero.EsCero == true`, `cero.EsPositivo == false`, `cero.EsNegativo == false`.
- `pos.EsCero == false`, `pos.EsPositivo == true`, `pos.EsNegativo == false`.
- `neg.EsCero == false`, `neg.EsPositivo == false`, `neg.EsNegativo == true`.

_(Los casos límite `0.0001m` ejercitan que "es positivo" no usa umbral — basta con `> 0m`.)_

### 6.10 `Dinero.En` a misma moneda es idempotente — (pinning — ya pasa hoy)

**Given** `a = Dinero(100, COP)`.
**When** `a.En(Moneda.COP, 999m)` (factor arbitrario, incluso absurdo).
**Then** resultado es `a` — el factor se ignora cuando `destino == Moneda`.

_(Ya cubierto por `DineroTests.En_a_la_misma_moneda_no_aplica_factor`; se traslada.)_

### 6.11 `Dinero.En` a otra moneda con factor > 0 — (pinning — ya pasa hoy)

**Given** `a = Dinero(10, USD)`.
**When** `a.En(Moneda.COP, 4200m)`.
**Then** resultado es `Dinero(42_000, COP)`.

_(Ya cubierto por `DineroTests.En_aplica_factor_a_la_moneda_destino`; se traslada.)_

### 6.12 `Dinero.En` con factor inválido (= 0 y < 0) — ver Q2

**Given** `a = Dinero(10, USD)`.
**When** / **Then**:
- `a.En(Moneda.COP, 0m)` → lanza excepción de factor inválido.
- `a.En(Moneda.COP, -1m)` → lanza excepción de factor inválido.

- Si Q2 se **acepta** (opción a en §10): la excepción es `FactorDeConversionInvalidoException : DominioException` con propiedad `FactorIntentado` igual al recibido → **(rojo — requiere green)**.
- Si Q2 se **rechaza**: la excepción sigue siendo `ArgumentException` y el test assert solo `Should().Throw<ArgumentException>()` → **(rojo — requiere green, pero solo crea el test; no cambia src/)**.

### 6.13 `Dinero.ToString()` formato `{Valor:0.####} {Codigo}` — (pinning — ya pasa hoy)

**Given** / **When** / **Then** (tabla de casos):

| Entrada | `ToString()` esperado |
|---|---|
| `Dinero(100m, COP)` | `"100 COP"` |
| `Dinero(100.5m, COP)` | `"100.5 COP"` |
| `Dinero(100.1234m, USD)` | `"100.1234 USD"` |
| `Dinero(100.12345m, USD)` | `"100.1235 USD"` _(redondea a 4 decimales por el format `0.####`)_ |
| `Dinero(-42m, EUR)` | `"-42 EUR"` |
| `Dinero(0m, COP)` | `"0 COP"` |

_Nota para `red`: el format `0.####` usa el separador decimal de `CultureInfo.CurrentCulture`. El test DEBE fijar cultura invariante (`using var _ = new CultureSwitch("en-US")` o equivalente) para que las aserciones sean deterministas. Si la política del proyecto es "dominio siempre invariant", esta observación va a §13 como followup para formalizar `ToString(CultureInfo.InvariantCulture)` en `Dinero`._

### 6.14 `Moneda` — normalización (trim + upper) — (pinning — ya pasa hoy)

**Given** cada una de las entradas `"USD"`, `"usd"`, `"  cop  "`, `" eur "`.
**When** `new Moneda(entrada)`.
**Then** `Codigo` es la versión normalizada (`"USD"`, `"USD"`, `"COP"`, `"EUR"`).

_(Ya cubierto por `DineroTests.Moneda_normaliza_codigo` — se traslada y se amplían casos de borde.)_

### 6.15 `Moneda` — rechaza códigos mal formados — (pinning — ya pasa hoy)

**Given** cada una de `"US"`, `"USDD"`, `"US1"`, `""`, `"   "`, `null`.
**When** `new Moneda(entrada)`.
**Then** lanza `CodigoMonedaInvalidoException` con `CodigoIntentado` reflejando la entrada normalizada o vacía.

_(Amplía `DineroTests.Moneda_rechaza_codigo_invalido` con whitespace y null explícito.)_

### 6.16 `Moneda` — rechaza códigos bien formados pero no ISO 4217 — (pinning — ya pasa hoy)

**Given** `"XYZ"`, `"ABC"`, `"AAA"` (letras A–Z de longitud 3, pero no en ISO).
**When** `new Moneda(entrada)`.
**Then** lanza `CodigoMonedaInvalidoException`.

### 6.17 `Moneda` — cobertura ISO 4217 sampling + cardinalidad — (pinning — ya pasa hoy)

> **Decisión del modeler (followup #9):** se adopta la **opción (b)** descrita en el briefing — **sample + cardinalidad**. Razones:
> - (a) "theory con ~180 inputs" es redundante: la misma aserción se duplica. Ruido en reportes.
> - (c) "lista externa desde ISO oficial" es correcta a largo plazo pero **fuera de alcance del MVP** (no hay proceso de sync con el estándar).
> - (b) da seguridad de que los códigos de negocio del mercado objetivo están y que la lista no se redujo accidentalmente a un set trivial.

**Given** — nada.
**When** `new Moneda(codigo)` para cada `codigo` en el sample: `{ "COP", "USD", "EUR", "MXN", "CLP", "ARS", "PEN", "BRL", "GBP", "JPY", "CAD", "CHF", "AUD", "ZAR", "INR", "CNY", "KRW" }`.
**Then** la construcción tiene éxito y `Codigo` coincide con la entrada.

**Y** cardinalidad: `Moneda.CantidadCodigosIso4217Soportados >= 150` (el hash actual tiene ~180 entradas; el umbral de 150 da margen de seguridad ante podas futuras legítimas).

> **Cambio requerido en `src/` (§12):** exponer `public static int CantidadCodigosIso4217Soportados => CodigosIso4217Validos.Count;` en `Moneda`. Sin esto el test solo puede validar sample, no cardinalidad. Alternativa equivalente: exponer `public static IReadOnlySet<string> CodigosIso4217Soportados` (inmutable wrapper del hash). El modeler recomienda la **propiedad de cardinalidad** (no la colección), para no comprometerse con un contrato de enumeración completa que invite a tests frágiles.

Etiqueta: el sample es **(pinning — ya pasa hoy)**; la parte de cardinalidad es **(rojo — requiere green)** porque la propiedad `CantidadCodigosIso4217Soportados` aún no existe.

### 6.18 `Moneda` — igualdad por valor — (pinning — ya pasa hoy)

**Given** `a = new Moneda("usd")`, `b = new Moneda("USD")`, `c = Moneda.USD`.
**When** comparar.
**Then** `a == b`, `a == c`, `a.GetHashCode() == b.GetHashCode()`. (Valida `INV-SK-3`.)

### 6.19 `Moneda` — `ToString()` y conversión implícita a `string` — (pinning — ya pasa hoy)

**Given** `m = Moneda.COP`.
**When** / **Then**:
- `m.ToString() == "COP"`.
- `string s = m;` (conversión implícita) → `s == "COP"`.
- Interpolación: `$"{m}"` == `"COP"`.

### 6.20 `Requerir.Campo` — happy path y fallo — (pinning — ya pasa hoy)

**Given** / **When** / **Then**:
- `Requerir.Campo("acme", "TenantId")` → devuelve `"acme"`.
- `Requerir.Campo("  acme  ", "TenantId")` → devuelve `"  acme  "` (sin trim — el helper solo valida presencia, no normaliza; la normalización es responsabilidad del caller si aplica).
- `Requerir.Campo(null, "TenantId")` → lanza `CampoRequeridoException` con `NombreCampo = "TenantId"`.
- `Requerir.Campo("", "Nombre")` → lanza `CampoRequeridoException` con `NombreCampo = "Nombre"`.
- `Requerir.Campo("   ", "Codigo")` → lanza `CampoRequeridoException` con `NombreCampo = "Codigo"`.

### 6.21 `DominioException` — contrato de jerarquía — (pinning — ya pasa hoy)

**Given** — nada.
**When** instanciar (vía su excepción concreta) cada subclase del kernel: `CampoRequeridoException("x")`, `PeriodoInvalidoException(inicio, fin)`, `ProfundidadMaximaFueraDeRangoException(0, 1, 15)`, `CodigoMonedaInvalidoException("XYZ")`, `TenantYaConfiguradoException("acme", Moneda.COP)`, `PresupuestoNoEsBorradorException(EstadoPresupuesto.Aprobado)`, `CodigoRubroInvalidoException("1.x"))`, `CodigoRubroDuplicadoException("1.01")`, `CodigoHijoNoExtiendeAlPadreException("1.01", "1.01.99.99")`, `RubroPadreNoExisteException(Guid.NewGuid())`, `ProfundidadExcedidaException(10, 11)`, `PresupuestoNoEncontradoException(Guid.NewGuid())`.
**Then** cada instancia:
- es asignable a `DominioException` (`ex.Should().BeAssignableTo<DominioException>()`).
- preserva sus propiedades estructuradas (el valor que recibió el constructor está disponible vía getter público — se valida en el escenario).

_Nota: este escenario **congela el contrato** de "toda excepción del kernel es `DominioException`". Si alguien crea una excepción nueva que olvida heredar de la base, el test no lo detecta automáticamente — §13 propone un followup para una prueba de arquitectura (NetArchTest o reflection) que verifique el invariante a nivel de assembly._

### 6.22 `MonedasDistintasException` hereda de `DominioException` — ver Q1

**Given** — nada.
**When** instanciar `new MonedasDistintasException(Moneda.COP, Moneda.USD)`.
**Then**:
- `ex.Izquierda == COP`, `ex.Derecha == USD`.
- **Si Q1 aceptada:** `ex.Should().BeAssignableTo<DominioException>()` → **(rojo — requiere green)**.
- **Si Q1 rechazada:** `ex.Should().BeAssignableTo<InvalidOperationException>()` + comentario en el test justificando por qué NO es `DominioException` → **(pinning — ya pasa hoy)**.

## 7. Idempotencia / retries

**No aplica.** Un SharedKernel no es una unidad de ejecución con efectos laterales; sus operaciones son funciones puras sobre VOs inmutables. Invocar `new Moneda("USD")` o `dinero + dinero` cualquier número de veces produce el mismo resultado.

Lo único con estado latente es la lista `CodigosIso4217Validos` (static readonly), cuyo ciclo de vida es el del proceso y no requiere tratamiento de reintentos.

## 8. Impacto en proyecciones / read models

**Ninguno.** El SharedKernel no alimenta proyecciones. Sí es **consumido** por todas las proyecciones que persisten `Dinero` / `Moneda` (serialización JSON via Marten). Ese contrato de serialización está fuera del scope de este slice — los slices consumidores (01, 02, 03, …) ya lo ejercitan implícitamente en sus tests de integración (followup #14).

## 9. Impacto en endpoints HTTP

**Ninguno directo.** El SharedKernel no expone endpoints. Sí afecta el mapeo de excepciones en `DomainExceptionHandler`:

- **Si Q1 se acepta** (ver §10 y §12): `MonedasDistintasException` pasa a heredar de `DominioException` y el handler la mapeará. Propuesta del modeler: **400 Bad Request** (el caller intentó operar monedas incompatibles — es un error de forma, no un conflicto lógico-estado). Se añade al `switch` de `DomainExceptionHandler.Mapear`.
- **Si Q2 se acepta**: `FactorDeConversionInvalidoException` → **400 Bad Request** (dato mal formado). Se añade al `switch`.
- Si Q1/Q2 se rechazan: sin impacto en el handler (esas excepciones siguen siendo `InvalidOperationException` / `ArgumentException` → 500 genérico, aceptado como "error de programación del caller").

Actualizar `DomainExceptionHandler` es un cambio menor que este slice puede incluir, o diferirse a infra-wire post-review. Decisión: se incluye en §12 como parte del refactor transversal si Q1/Q2 se aceptan.

## 10. Preguntas abiertas

Cuatro decisiones requieren firma del usuario antes de que `red` y `green` procedan. El modeler las lista con opciones, consecuencias y recomendación.

**Firma del usuario (2026-04-24): Q1=(a), Q2=(a), Q3=(a), Q4=(a). Todas las recomendaciones del modeler aceptadas.**

### Q1 — ¿`MonedasDistintasException` debe heredar de `DominioException`?

**Contexto.** Hoy hereda de `InvalidOperationException` y el `DomainExceptionHandler` no la mapea: si se propaga a un endpoint HTTP (p.ej. desde un handler que opera con `Dinero` sin cuidado) el cliente recibe **500 Internal Server Error**. El resto del kernel trata toda violación de regla de dominio como `DominioException`. Inconsistencia documentada en `INV-SK-4`.

**Opciones.**
- **(a) Migrar a `DominioException`.** Cambio no-breaking a nivel de source: los tests existentes (`DineroTests.Suma_entre_monedas_distintas_lanza_MonedasDistintasException`) usan el tipo concreto, no el base; el código de producción solo lanza la excepción, nunca la catchea. Impacto: añadir mapeo 400 en `DomainExceptionHandler`.
- **(b) Dejar como está.** Semántica: "usar dinero en monedas distintas sin convertir explícitamente es un error de programación del caller, no una violación de regla de negocio expuesta al usuario final." El 500 es legítimo porque el endpoint no debería llegar a esa línea si el caller respeta el contrato.

**Recomendación del modeler: (a).** Coherencia con el resto del kernel. Un `AsignarMontoARubro(rubro, Dinero)` que recibe dinero en moneda distinta a la del presupuesto llegará a este operador; el usuario merece un 400 con mensaje claro, no un 500. El argumento de "error de programación" es válido para handlers internos del propio dominio, pero `Dinero` se usa directamente desde payloads HTTP en slices futuros (`AsignarMontoARubro`, `AprobarPresupuesto`).

### Q2 — ¿`En(factor <= 0)` debe lanzar una `DominioException`?

**Contexto.** Hoy lanza `ArgumentException("factor > 0", nameof(factor))`. Misma inconsistencia que Q1 pero con un matiz: `En` recibe el `factor` desde el `SnapshotTasas` del evento `PresupuestoAprobado` o del catálogo `TasaDeCambio`. Un factor ≤ 0 en producción implica **dato corrupto en una proyección**, no "un usuario escribió mal en un form".

**Opciones.**
- **(a) Migrar a `FactorDeConversionInvalidoException(decimal FactorIntentado) : DominioException`.** Se añade al kernel como excepción nueva. Mapeo HTTP: 400 (dato mal formado). Encaja con `INV-SK-4`.
- **(b) Dejar como `ArgumentException`.** Semántica: "un factor inválido es precondición del programador que llama `En`; si el catálogo `TasaDeCambio` está corrupto, eso es un bug de la proyección, no del dominio." Se propagaría como 500.

**Recomendación del modeler: (a).** La proyección `TasaDeCambio` puede contener datos sucios; el dominio no debería confiar ciegamente. Si el factor llega inválido, `FactorDeConversionInvalidoException` con la propiedad `FactorIntentado` permite al handler de `AprobarPresupuesto` (slice futuro) decidir qué hacer: abortar la aprobación con 400 informando "no hay tasa válida para X moneda" (semántica actual de INV-15 del hotspot §2).

### Q3 — ¿`MonedasDistintasException` debe moverse a su archivo propio?

**Contexto.** Hoy vive anidada al final de `Dinero.cs`. El patrón del resto del SharedKernel es **un archivo por excepción** (ver `CodigoMonedaInvalidoException.cs`, `RubroPadreNoExisteException.cs`, etc.).

**Opciones.**
- **(a) Mover a `SharedKernel/MonedasDistintasException.cs`.** Cero cambio de comportamiento. Mejora navegabilidad y consistencia.
- **(b) Dejar en `Dinero.cs`.** Argumento: está tan pegada a `Dinero` que vivir ahí "documenta la relación". Contraargumento: ninguna otra excepción del kernel se trata así; si Q1 se acepta, la incoherencia estructural sube.

**Recomendación del modeler: (a).** Es refactor puro (mover definición), parte natural de este slice retroactivo.

### Q4 — ¿Reubicar `DineroTests.cs` a `Slices/Slice00_SharedKernelTests.cs`?

**Contexto.** Followup #4. Los demás tests viven en `tests/SincoPresupuesto.Domain.Tests/Slices/Slice{N}_{Nombre}Tests.cs`. `DineroTests.cs` quedó en la raíz del proyecto de tests por ser pre-metodología.

**Opciones.**
- **(a) Mover + renombrar a `Slices/Slice00_SharedKernelTests.cs`**, manteniendo el contenido idéntico; agregar los nuevos escenarios de §6 en el mismo archivo. Clase `Slice00_SharedKernelTests`.
- **(b) Dejarlo donde está**, aceptando heterogeneidad.

**Recomendación del modeler: (a).** Parte de cerrar el followup #4. El movimiento lo hace `red` al introducir los tests nuevos (mueve el archivo existente, ajusta namespace/clase, agrega los escenarios faltantes). Es refactor de tests (no cambia comportamiento), por tanto excepción explícita a "los tests no se tocan en refactor" de METHODOLOGY §3.4 — queda cubierta como parte de la fase **red** del slice 00 según METHODOLOGY §7.3 (slices retroactivos).

---

## 11. Checklist pre-firma

- [x] Todas las precondiciones (§4) mapean a un escenario Then (Dinero.1→6.2/6.4/6.6; Dinero.2→6.6; Dinero.3→6.12; Moneda.1→6.15; Moneda.2→6.15; Moneda.3→6.16; Requerir.1→6.20).
- [x] Todas las invariantes del kernel (§5) mapean a escenario(s): INV-SK-1→6.1/6.7; INV-SK-2→6.2/6.4/6.6; INV-SK-3→6.14/6.18; INV-SK-4→6.22 (pende de Q1) + 6.12 (pende de Q2); INV-SK-5→enfoque de aserciones en todos los tests de excepción (tipo + propiedades, nunca mensaje).
- [x] El "happy path" está presente — reinterpretado como: cada VO tiene al menos un escenario de construcción/uso exitoso (6.1, 6.7, 6.8, 6.14, 6.18, 6.20).
- [x] Preguntas abiertas (§10) listan opciones, consecuencias y recomendación. Ninguna queda sin propuesta.
- [x] Cada escenario de §6 está etiquetado como **(pinning — ya pasa hoy)** o **(rojo — requiere green)**, cumpliendo el requisito del briefing.
- [x] §6 excede el umbral de **~15 escenarios** del briefing: contiene **22 escenarios** (6.1–6.22), varios con múltiples Then.

## 12. Impacto en SharedKernel (refactor transversal condicional)

Los cambios a aplicar dependen del veredicto del usuario sobre Q1/Q2/Q3/Q4. La tabla resume:

| Cambio | Gatillo | Archivo(s) | Tipo |
|---|---|---|---|
| `MonedasDistintasException : DominioException` (cambiar base) | Q1 = (a) | `Dinero.cs` o nuevo archivo (ver siguiente fila) | Cambio de jerarquía de excepción |
| Mover `MonedasDistintasException` a archivo propio `SharedKernel/MonedasDistintasException.cs` | Q3 = (a) | nuevo archivo + borrar definición anidada en `Dinero.cs` | Refactor puro |
| Nueva `FactorDeConversionInvalidoException(decimal FactorIntentado) : DominioException` en `SharedKernel/FactorDeConversionInvalidoException.cs` | Q2 = (a) | nuevo archivo | Nueva excepción |
| `Dinero.En` lanza `FactorDeConversionInvalidoException` en lugar de `ArgumentException` cuando `destino != Moneda && factor <= 0` | Q2 = (a) | `Dinero.cs` | Cambio de excepción |
| Actualizar `DomainExceptionHandler.Mapear` para mapear `MonedasDistintasException → 400` y `FactorDeConversionInvalidoException → 400` | Q1 = (a) y/o Q2 = (a) | `src/SincoPresupuesto.Api/ExceptionHandlers/DomainExceptionHandler.cs` | Suma de casos al `switch` |
| Exponer `public static int CantidadCodigosIso4217Soportados => CodigosIso4217Validos.Count;` | §6.17 (siempre, independiente de Q) | `Moneda.cs` | API pública mínima adicional |
| Mover `DineroTests.cs` → `Slices/Slice00_SharedKernelTests.cs` y ampliarlo con los escenarios nuevos de §6 | Q4 = (a) | `tests/SincoPresupuesto.Domain.Tests/Slices/Slice00_SharedKernelTests.cs` + borrar el original | Reubicación + extensión |

**Sin cambio de comportamiento** en ninguna rama no-excepción:
- Valores de `Dinero.Cero`, `EsCero`, `EsPositivo`, `EsNegativo`, operadores `+`, `-`, `*`, `<`, `>`, `<=`, `>=`, `En` (cuando factor > 0), `ToString`: idénticos antes y después.
- `Moneda` sigue normalizando idéntico y validando contra la misma lista.
- `Requerir.Campo`: sin cambio.

**Cobertura esperada post-slice:** el SharedKernel queda con coverage ≥ 95% de líneas y ramas (objetivo realista dado que son VOs puros sin ramas complejas). Se reporta en `review-notes.md` al cerrar el slice.

## 13. Follow-ups generados por este slice

Se propondrán a `FOLLOWUPS.md` al firmar la spec. Los numeros son tentativos — el reviewer los asigna al cerrar.

- **#{siguiente}** — `Dinero.ToString` con cultura explícita. Origen: slice-00, spec §6.13. Propuesta: cambiar `ToString()` a `ToString(CultureInfo.InvariantCulture)` para que el format `0.####` sea determinista en cualquier host. Hoy depende de `CultureInfo.CurrentCulture`; los tests tienen que fijarla manualmente. Cambio mínimo, alto valor de robustez. Disparador: primer test fallando por cultura en CI de otro locale.
- **#{siguiente}** — prueba de arquitectura: "toda excepción pública en `SincoPresupuesto.Domain.SharedKernel` hereda de `DominioException` salvo la base misma". Origen: slice-00, spec §6.21 nota. Propuesta: test con NetArchTest o reflection de `typeof(DominioException).Assembly`. Cierra la puerta a futuras excepciones que olviden la regla. Disparador: se toca al crear una nueva excepción del kernel (p.ej. `TenantNoConfiguradoException` de followup #8).
- **#{siguiente}** — catálogo ISO 4217 sincronizado con fuente externa. Origen: slice-00, spec §6.17 nota (opción (c) diferida). Propuesta: generar la lista desde un JSON embebido actualizable (p.ej. `iso-4217-currencies.json` versionado). Cuándo: cuando un tenant pida operar con una moneda que no esté en el hash embebido.
- **#{siguiente}** — test `SharedKernel/RequerirTests` separado si la superficie de `Requerir` crece. Hoy con un solo método `Campo` vive cómodamente en `Slice00_SharedKernelTests`. Disparador: cuando se agregue un segundo helper (p.ej. `Requerir.RangoEnteroCerrado(int valor, int min, int max, string nombreCampo)` que hoy vive como if-throw inline en `Presupuesto.Create`).
- **#6** (existente) — este slice lo **cierra** al implementar los escenarios 6.3, 6.4, 6.5, 6.6, 6.7, 6.8, 6.9, 6.12, 6.13.
- **#9** (existente) — este slice lo **cierra** al adoptar la opción (b) de sampling + cardinalidad en §6.17.
- **#4** (existente) — este slice lo **cierra** al reubicar el archivo de tests (§10 Q4).
