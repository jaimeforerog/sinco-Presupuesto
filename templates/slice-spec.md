# Slice {N} — {NombreComando}

**Autor:** domain-modeler
**Fecha:** YYYY-MM-DD
**Estado:** draft | firmado
**Agregado afectado:** {Presupuesto | Rubro | ConfiguracionTenant | TasaDeCambio | …}
**Decisiones previas relevantes:** links a event-storming, hot-spots, ADRs aplicables.

---

## 1. Intención

Una o dos frases que describan qué necesita lograr el usuario con este comando.

> _Ejemplo: "El responsable de presupuesto necesita incorporar un rubro nuevo al árbol del presupuesto en borrador, con un código único dentro del presupuesto y opcionalmente bajo un rubro padre existente."_

## 2. Comando

```csharp
public sealed record {NombreComando}(
    // payload tipado; usar Dinero para montos, Moneda para códigos, DateOnly para fechas calendario.
);
```

## 3. Evento(s) emitido(s)

| Evento | Payload | Cuándo |
|---|---|---|
| `{NombreEvento}` | {campos} | Al {condición}. |

## 4. Precondiciones

Condiciones que deben cumplirse **antes** de ejecutar el comando. Si no se cumplen, el comando falla con excepción de dominio específica.

- `{PRE-1}`: {condición} — excepción: `{Tipo}`.
- `{PRE-2}`: …

## 5. Invariantes tocadas

Invariantes del agregado que este comando debe preservar (referenciar por código `INV-X` si ya existe en el event-storming o hot-spots).

- `INV-B`: un terminal no puede tener hijos.
- …

## 6. Escenarios Given / When / Then

Cada escenario se convierte en **un test** en la fase red. Todos los escenarios de esta lista deben terminar con un test correspondiente.

### 6.1 Happy path

**Given**
- {estado inicial, expresado como lista de eventos previos}

**When**
- {comando}

**Then**
- emite `{Evento}` con `{campos esperados}`.

### 6.2 Violación de precondición `{PRE-1}`

**Given** …
**When** …
**Then** lanza `{Tipo}` con mensaje "…".

### 6.3 Violación de invariante `{INV-X}`

**Given** …
**When** …
**Then** lanza `{Tipo}`.

_(Añadir tantos escenarios como precondiciones + invariantes el comando pueda violar.)_

## 7. Idempotencia / retries

¿Qué pasa si el comando se reintenta? ¿Es naturalmente idempotente? ¿Requiere `IdempotencyKey`? Decidir y dejar explícito.

## 8. Impacto en proyecciones / read models

- `{ReadModelX}`: añadir/actualizar campos `…`.
- Si no impacta ninguna proyección: anotarlo explícitamente.

## 9. Impacto en endpoints HTTP

- Método + ruta propuesta: `{POST /…}`.
- DTO de request / response.
- Código HTTP esperado en happy path y en cada error de dominio.

## 10. Preguntas abiertas

Lista de dudas que el domain-modeler no resolvió y requieren decisión del usuario antes de pasar a red.

- [ ] ¿…?

## 11. Checklist pre-firma

- [ ] Todas las precondiciones mapean a un escenario Then.
- [ ] Todas las invariantes tocadas mapean a un escenario Then.
- [ ] El happy path está presente.
- [ ] Preguntas abiertas están todas respondidas o marcadas como asunción con justificación.
