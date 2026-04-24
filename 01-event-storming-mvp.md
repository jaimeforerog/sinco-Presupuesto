# Event Storming — MVP "Núcleo Presupuestal Básico"

**Proyecto:** Sinco Presupuesto
**Fecha:** 2026-04-24
**Alcance:** Definición de presupuesto, rubros/cuentas, asignación inicial y consulta de saldo.
**Fuera de alcance:** Compromisos, ejecuciones, modificaciones, traslados, aprobaciones multinivel, workflows.

---

## 1. Bounded Context

Para el MVP trabajamos con un único contexto acotado: **Gestión Presupuestal**.
A medida que crezca el sistema aparecerán contextos adicionales (Ejecución, Modificaciones, Maestros/Catálogos, Reportería, Identidad), pero es deliberado mantenerlos implícitos ahora para no sobreingenierizar.

## 2. Actores

| Actor | Rol en el MVP |
|---|---|
| Responsable de Presupuesto | Crea el presupuesto, define rubros y asigna montos. |
| Analista Financiero | Consulta saldos y estructura presupuestal. |
| Administrador | Activa y cierra presupuestos. |

## 3. Agregado principal: `Presupuesto`

El agregado raíz es **Presupuesto**. Los **Rubros** viven como *entities* dentro del agregado porque:

- La invariante fuerte "suma de montos asignados a rubros = monto total del presupuesto" se valida en la misma frontera transaccional.
- En el MVP el número de rubros por presupuesto es acotado (decenas, no miles).
- Si más adelante los rubros necesitan ciclo de vida independiente (p. ej. aprobación por rubro), se evaluará promoverlos a agregado aparte.

### 3.1 Identidad

- `PresupuestoId` (GUID). Stream de eventos: `presupuesto-{PresupuestoId}`.
- `RubroId` (GUID) — único dentro del presupuesto.

### 3.2 Estados del Presupuesto (máquina de estados)

```
  Borrador  ──AprobarPresupuesto──▶  Aprobado
     │                                   │
     │                                   ▼
     │                              ActivarPresupuesto
     │                                   │
     │                                   ▼
     └───────────────────────────▶   Activo  ──CerrarPresupuesto──▶  Cerrado
```

En **Borrador** se puede estructurar y asignar libremente. A partir de **Aprobado** el presupuesto queda congelado (en el MVP; modificaciones vendrán después). **Activo** representa el presupuesto en vigencia durante el periodo fiscal. **Cerrado** es el estado final.

## 4. Comandos (intenciones del usuario)

| Comando | Precondiciones | Resultado esperado |
|---|---|---|
| `CrearPresupuesto` | No existe un presupuesto con el mismo código/periodo. | Emite `PresupuestoCreado`. |
| `AgregarRubro` | Presupuesto en Borrador. Código de rubro único dentro del presupuesto. | Emite `RubroAgregado`. |
| `AsignarMontoARubro` | Presupuesto en Borrador. Monto ≥ 0. | Emite `MontoAsignadoARubro`. |
| `QuitarRubro` | Presupuesto en Borrador. Rubro existe y está en monto 0. | Emite `RubroRetirado`. |
| `AprobarPresupuesto` | Presupuesto en Borrador. Al menos un rubro con monto > 0. | Emite `PresupuestoAprobado`. |
| `ActivarPresupuesto` | Presupuesto en Aprobado. Fecha actual dentro del rango del periodo. | Emite `PresupuestoActivado`. |
| `CerrarPresupuesto` | Presupuesto en Activo. | Emite `PresupuestoCerrado`. |

## 5. Eventos de dominio (hechos inmutables, en pasado)

| Evento | Payload |
|---|---|
| `PresupuestoCreado` | `PresupuestoId`, `Codigo`, `Nombre`, `PeriodoInicio`, `PeriodoFin`, `Moneda`, `CreadoEn`, `CreadoPor` |
| `RubroAgregado` | `PresupuestoId`, `RubroId`, `Codigo`, `Nombre`, `RubroPadreId?`, `AgregadoEn` |
| `MontoAsignadoARubro` | `PresupuestoId`, `RubroId`, `Monto`, `MontoAnterior`, `AsignadoEn`, `AsignadoPor` |
| `RubroRetirado` | `PresupuestoId`, `RubroId`, `RetiradoEn` |
| `PresupuestoAprobado` | `PresupuestoId`, `MontoTotal`, `AprobadoEn`, `AprobadoPor` |
| `PresupuestoActivado` | `PresupuestoId`, `ActivadoEn`, `ActivadoPor` |
| `PresupuestoCerrado` | `PresupuestoId`, `CerradoEn`, `CerradoPor` |

Todos los eventos incluyen metadata implícita de Marten (versión de stream, timestamp, correlation id).

## 6. Invariantes / Reglas de negocio

1. Un código de presupuesto es único por periodo fiscal.
2. El monto asignado a un rubro no puede ser negativo.
3. No se pueden agregar, retirar ni reasignar rubros en estado distinto a **Borrador**.
4. Para aprobar un presupuesto debe existir al menos un rubro con monto > 0.
5. Un presupuesto solo puede activarse si la fecha actual está dentro del rango `[PeriodoInicio, PeriodoFin]`.
6. No se puede cerrar un presupuesto que no esté activo.
7. La `MonedaBase` del presupuesto es inmutable una vez creado. Las partidas pueden expresarse en cualquier moneda ISO 4217 (ver `02-decisiones-hotspots-mvp.md` §2).

## 7. Proyecciones (Read Models)

Marten generará estas vistas desde los eventos. Cada una tiene su propósito y responde a una pregunta específica de la UI.

| Proyección | Tipo Marten | Pregunta que responde |
|---|---|---|
| `PresupuestoResumen` | `SingleStreamProjection` | "¿Qué presupuestos existen y en qué estado están?" |
| `EstructuraPresupuestal` | `SingleStreamProjection` | "Dame el árbol de rubros con montos asignados de este presupuesto." |
| `SaldoPorRubro` | `SingleStreamProjection` | "¿Cuánto hay asignado a cada rubro?" (en MVP: saldo = asignado, porque aún no hay ejecución). |
| `PresupuestosPorPeriodo` | `MultiStreamProjection` | "Lista todos los presupuestos del periodo fiscal X." |

> Nota: en el MVP `SaldoPorRubro` es trivialmente igual al monto asignado. Se modela como proyección separada porque en la próxima iteración (compromisos/ejecuciones) evolucionará para incluir `comprometido`, `ejecutado` y `disponible = asignado - comprometido - ejecutado`.

## 8. Políticas / Process Managers

En el MVP no hay automatizaciones cross-aggregate. Los candidatos a políticas (fuera de alcance) son:

- Cuando se cierra un presupuesto → notificar a usuarios con compromisos pendientes.
- Cuando se activa un presupuesto → enviar correo al Responsable.
- Cuando un periodo fiscal termina → disparar cierre automático.

Estas se implementarán con Wolverine cuando tengamos el contexto de Ejecución.

## 9. Hot spots — RESUELTAS (2026-04-24)

Las cinco decisiones pendientes de esta sección quedaron resueltas en `02-decisiones-hotspots-mvp.md`. Resumen:

| # | Hot spot | Decisión |
|---|---|---|
| 1 | Jerarquía de rubros | **Árbol n-ario** (profundidad default 10, tope 15). |
| 2 | Multimoneda | **Multimoneda a nivel de partida**: tenant tiene `MonedaLocal`; presupuesto tiene `MonedaBase` (default = MonedaLocal, inmutable); partidas en cualquier ISO 4217; snapshot de tasas al aprobar congela el baseline. |
| 3 | Multitenancy | **Marten conjoint multi-tenant** (`TenancyStyle.Conjoint`). |
| 4 | Numeración de rubros | **Autogenerada con override** que respete prefijo del padre y unicidad. |
| 5 | Unicidad del código de presupuesto | Proyección inline `PresupuestoCodigoIndex` + `UniqueIndex(TenantId, Codigo, PeriodoFiscal)`. |

Ver el documento `02-decisiones-hotspots-mvp.md` para motivación, alternativas descartadas, invariantes adicionales (INV-8..INV-12) y eventos nuevos (`RubroConvertidoAAgrupador`, `RubroMovido`, `RubrosReordenados`).

## 10. Mapeo a código (.NET + Marten + Wolverine)

Estructura tentativa de la solución:

```
SincoPresupuesto.sln
├── src/
│   ├── SincoPresupuesto.Domain/            # Events, Aggregates, Value Objects
│   ├── SincoPresupuesto.Application/       # Command handlers (Wolverine), queries
│   ├── SincoPresupuesto.Infrastructure/    # Marten config, projections
│   ├── SincoPresupuesto.Api/               # ASP.NET Core minimal APIs
│   └── SincoPresupuesto.Web/               # React (Vite) — o proyecto separado
└── tests/
    ├── SincoPresupuesto.Domain.Tests/
    └── SincoPresupuesto.Integration.Tests/
```

Convenciones Marten:
- Agregado `Presupuesto` rehidratado con `AggregateStream<Presupuesto>`.
- Comando → handler Wolverine → `session.Events.Append(streamId, events)` → `session.SaveChangesAsync()`.
- Proyecciones inline para las que deben estar consistentes de inmediato (`PresupuestoResumen`); async para las más pesadas.

## 11. Próximos pasos sugeridos

1. Resolver los hot spots de la sección 9 (10–15 min de conversación).
2. Escribir el **scaffolding del proyecto .NET** con Marten + Wolverine + Docker Compose para Postgres.
3. Implementar el primer *slice* completo: `CrearPresupuesto` → evento → proyección `PresupuestoResumen` → endpoint GET.
4. Agregar `AgregarRubro` y `AsignarMontoARubro` siguiendo el mismo patrón.
5. Tests de integración con Marten (usar `IntegrationContext` de Marten.Testing o Testcontainers).
