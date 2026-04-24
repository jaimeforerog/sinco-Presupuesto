# Slice 01 — CrearPresupuesto

**Autor:** domain-modeler (retroactivo)
**Fecha:** 2026-04-24
**Estado:** firmado
**Agregado afectado:** Presupuesto
**Decisiones previas relevantes:**
- `01-event-storming-mvp.md` §3, §4, §5 (comando, evento, invariantes base).
- `02-decisiones-hotspots-mvp.md` §1, §2, §4, §5 (árbol, multimoneda, numeración, unicidad).
- Memoria `project_multimoneda.md` (MonedaBase inmutable tras creación).
- Memoria `project_stack_decision.md` (Marten + Wolverine + .NET 9).

**Nota de retroactividad:** este slice se implementó antes de acordar la metodología. Esta spec se redacta a posteriori como referencia; los tests y la implementación se alinean a este contrato.

---

## 1. Intención

El responsable de presupuesto de un tenant (empresa) necesita crear un presupuesto nuevo en estado Borrador, con su código de negocio, periodo fiscal, moneda base y la profundidad máxima del árbol de rubros. A partir de ese momento existe un stream event-sourced para ese presupuesto que podrá recibir estructura de rubros, asignaciones, y eventualmente aprobación/activación.

## 2. Comando

```csharp
public sealed record CrearPresupuesto(
    string TenantId,
    string Codigo,
    string Nombre,
    DateOnly PeriodoInicio,
    DateOnly PeriodoFin,
    Moneda MonedaBase,
    int ProfundidadMaxima = 10,
    string CreadoPor = "sistema");
```

## 3. Evento(s) emitido(s)

| Evento | Payload | Cuándo |
|---|---|---|
| `PresupuestoCreado` | `PresupuestoId`, `TenantId`, `Codigo`, `Nombre`, `PeriodoInicio`, `PeriodoFin`, `MonedaBase`, `ProfundidadMaxima`, `CreadoEn`, `CreadoPor` | Al aceptar el comando sobre un stream vacío. |

## 4. Precondiciones

Todas las excepciones heredan de `SincoPresupuesto.Domain.SharedKernel.DominioException`. Los tests verifican el **tipo** y sus propiedades, no el mensaje.

- `PRE-1`: `TenantId` no es nulo ni vacío — excepción: `CampoRequeridoException` con `NombreCampo = "TenantId"`.
- `PRE-2`: `Codigo` no es nulo ni vacío — excepción: `CampoRequeridoException` con `NombreCampo = "Codigo"`.
- `PRE-3`: `Nombre` no es nulo ni vacío — excepción: `CampoRequeridoException` con `NombreCampo = "Nombre"`.
- `PRE-4`: `PeriodoFin >= PeriodoInicio` — excepción: `PeriodoInvalidoException` con `PeriodoInicio` y `PeriodoFin`.
- `PRE-5`: `1 ≤ ProfundidadMaxima ≤ 15` — excepción: `ProfundidadMaximaFueraDeRangoException` con `Valor`, `MinimoInclusivo`, `MaximoInclusivo`.
- `PRE-6` (de infra, no del agregado): no existe otro presupuesto con `(TenantId, Codigo, PeriodoFiscal)` iguales. Se valida en la proyección inline `PresupuestoCodigoIndex` con `UniqueIndex` compuesto. **Fuera del alcance de este slice** — se diferirá a un slice posterior dedicado a esa proyección.

## 5. Invariantes tocadas

- `INV-7` (reformulada por multimoneda): `MonedaBase` del presupuesto es inmutable tras creación. Este slice **establece** la `MonedaBase` por primera vez; los slices futuros que toquen el presupuesto deben respetarla.
- `INV-0` (nueva, de este slice): el comando solo es válido sobre un stream vacío. Reintentar sobre un stream con al menos un evento lanza violación de concurrencia (Marten lo detecta por versión de stream).

## 6. Escenarios Given / When / Then

### 6.1 Happy path

**Given**
- Stream vacío (no hay eventos previos).

**When**
- `CrearPresupuesto(TenantId="acme", Codigo="OBRA-2026-01", Nombre="Torre Norte", PeriodoInicio=2026-01-01, PeriodoFin=2026-12-31, MonedaBase=COP, ProfundidadMaxima=10, CreadoPor="alice")` con `presupuestoId` y `ahora` dados desde fuera.

**Then**
- Emite un único `PresupuestoCreado` con exactamente los mismos campos del comando, más `PresupuestoId = presupuestoId` y `CreadoEn = ahora`.

### 6.2 `CreadoPor` vacío → default "sistema"

**Given**
- Stream vacío.

**When**
- Comando válido con `CreadoPor = ""`.

**Then**
- Emite `PresupuestoCreado` con `CreadoPor = "sistema"`.

### 6.3 Violación de `PRE-1` — TenantId vacío

**Given** Stream vacío.
**When** Comando con `TenantId = ""` o `"   "`.
**Then** Lanza `CampoRequeridoException` con `NombreCampo = "TenantId"`.

### 6.4 Violación de `PRE-2` — Codigo vacío

**Given** Stream vacío.
**When** Comando con `Codigo = ""` o `"   "`.
**Then** Lanza `CampoRequeridoException` con `NombreCampo = "Codigo"`.

### 6.5 Violación de `PRE-3` — Nombre vacío

**Given** Stream vacío.
**When** Comando con `Nombre = ""` o `"   "`.
**Then** Lanza `CampoRequeridoException` con `NombreCampo = "Nombre"`.

### 6.6 Violación de `PRE-4` — periodo invertido

**Given** Stream vacío.
**When** Comando con `PeriodoInicio = 2026-12-31` y `PeriodoFin = 2026-01-01`.
**Then** Lanza `PeriodoInvalidoException` con `PeriodoInicio` y `PeriodoFin` reflejando los valores del comando.

### 6.7 Violación de `PRE-5` — profundidad fuera de rango

**Given** Stream vacío.
**When** Comando con `ProfundidadMaxima ∈ {-1, 0, 16, 99}`.
**Then** Lanza `ProfundidadMaximaFueraDeRangoException` con `Valor` = el recibido, `MinimoInclusivo = 1`, `MaximoInclusivo = 15`.

### 6.8 Normalización de espacios en `Codigo` y `Nombre`

**Given** Stream vacío.
**When** Comando con `Codigo = "  OBRA-2026-01  "`, `Nombre = "  Torre Norte  "`.
**Then** Emite `PresupuestoCreado` con valores **trim** aplicado (`Codigo = "OBRA-2026-01"`, `Nombre = "Torre Norte"`).

## 7. Idempotencia / retries

- **No idempotente por diseño del comando**: cada `CrearPresupuesto` quiere producir un presupuesto **nuevo**. Reintentos deben evitarse en el caller.
- **Protección contra reintento**: Marten rechaza un `StartStream` sobre un stream existente con `ExistingStreamIdCollisionException`. Eso garantiza que un reintento con el mismo `presupuestoId` falle limpio.
- **IdempotencyKey**: no se introduce en este slice. Si el caller necesita idempotencia fuerte (p.ej. webhook reintentado), abrirá un slice `CrearPresupuestoIdempotente` con clave explícita.

## 8. Impacto en proyecciones / read models

- `PresupuestoReadModel` (nuevo): documento plano con todos los campos del presupuesto para lectura. Proyección inline `PresupuestoProjection : SingleStreamProjection<PresupuestoReadModel>`.
- `PresupuestoCodigoIndex` (proyección inline con `UniqueIndex` compuesto): **fuera del alcance**, se posterga a slice dedicado.

## 9. Impacto en endpoints HTTP

- `POST /api/tenants/{tenantId}/presupuestos` — crea el presupuesto para el tenant de la ruta.
  - Request: `{ codigo, nombre, periodoInicio, periodoFin, monedaBase, profundidadMaxima }`.
  - Response `201 Created` con `Location` al `GET /api/tenants/{tenantId}/presupuestos/{id}`.
  - Response `400 Bad Request` ante cualquier violación de PRE-1..PRE-5 con problem details.
- `GET /api/tenants/{tenantId}/presupuestos/{id}` — lee el `PresupuestoReadModel`. `404` si no existe.

## 10. Preguntas abiertas

- [x] ¿Idempotencia? — resuelto en §7: diferida.
- [x] ¿Unicidad por `(TenantId, Codigo, Periodo)`? — diferida a slice propio; el MVP acepta el riesgo mientras tanto.

## 11. Checklist pre-firma

- [x] Todas las precondiciones mapean a un escenario Then (§6.3–6.7).
- [x] Todas las invariantes tocadas mapean a un escenario Then (INV-7 se valida como "el evento fija la MonedaBase"; INV-0 se cubre implícitamente por el test 6.1 y por el rechazo de Marten a nivel infra, documentado).
- [x] El happy path está presente (§6.1).
- [x] Preguntas abiertas resueltas o diferidas con justificación.
