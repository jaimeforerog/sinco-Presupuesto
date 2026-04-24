# Slice: Visor de Eventos

**Tipo:** Transversal / Observabilidad
**Estado:** Activa
**Numeración:** 00 (fuera de la secuencia de dominio)

## Propósito

Exponer una vista de solo lectura sobre el event store y las proyecciones
existentes, para hacer tangible el Event Sourcing durante el desarrollo y
servir como herramienta de debugging y demo al negocio.

No es parte del bounded context de Presupuesto. Es infraestructura de
observabilidad que vive junto al monolito modular para evitar despliegues
adicionales en esta etapa.

## Qué SÍ hace

- Lista los tenants conocidos.
- Lista los streams de un tenant (paginado).
- Muestra los eventos de un stream en orden cronológico, con `version`,
  `timestamp`, `eventType` y `data` serializado.
- Muestra el estado actual de una proyección por tenant.
- Sirve un `index.html` estático con la UI mínima.

## Qué NO hace (límites duros)

- **No dispara comandos.** Para ejecutar comandos se usa Swagger u otra slice.
- **No define proyecciones nuevas.** Solo lee proyecciones que ya existen
  por slices de dominio.
- **No cruza tenants.** Todo endpoint exige `tenantId`. No hay vista global.
- **No contiene lógica de negocio.** Ni en backend ni en UI. Si una vista
  requiere cálculo (ej: saldos, conversión de moneda), eso vive en una
  slice de dominio que expone una query, no aquí.
- **No persiste configuración propia.** No tiene su propio storage.
- **No es admin panel.** Si surge la necesidad de "un botoncito para X",
  se abre una slice de dominio aparte. Se rechaza por defecto.

## Decisiones

| # | Decisión | Justificación |
|---|---|---|
| 1 | Endpoints bajo `/diag/*` | Prefijo claro que indica diagnóstico, no API pública de producto |
| 2 | UI sin framework JS (HTML + fetch) | Evita meter toolchain de frontend antes de tener dominio maduro |
| 3 | Tenant obligatorio en toda ruta | Marten configura tenancy a nivel de session; respetarlo aquí evita fugas |
| 4 | Sin autenticación en esta iteración | Servicio interno; se añadirá en slice de seguridad cuando exista |
| 5 | Lectura directa de Marten event store | No replicar el estado en otra proyección; la "verdad" es el stream |
| 6 | Único endpoint cross-tenant: `GET /diag/tenants` | Necesario para que la UI pueda seleccionar tenant; se considera metadata operativa, no datos de negocio |
| 7 | HTML + JS inline en el ensamblado (`DiagIndexHtml.cs`) | No requiere `wwwroot/`; simplifica deployment de contenedor (`Dockerfile` no copia assets externos) |
| 8 | Raw SQL contra `mt_events`/`mt_streams` de Marten | Deuda aceptada: el schema interno puede cambiar entre versiones mayores de Marten. Alternativa (proyectar streams a un documento propio) duplica la "verdad" |

## Estructura

```
slices/_obs-visor-eventos/
└── README.md                                  # Este archivo

src/SincoPresupuesto.Api/
└── Endpoints/
    ├── DiagEndpoints.cs                       # Mapeo HTTP /diag/*
    └── DiagIndexHtml.cs                       # HTML + JS inline del visor

tests/SincoPresupuesto.Integration.Tests/
└── DiagEndpointsTests.cs                      # Tests HTTP→PG
```

## Endpoints

| Método + Ruta | Respuesta |
|---|---|
| `GET /diag` | Redirige a `/diag/index.html` |
| `GET /diag/index.html` | UI (HTML+JS inline, servido por `DiagEndpoints`) |
| `GET /diag/tenants` | `string[]` — distinct `tenant_id` de `mt_events` |
| `GET /diag/tenants/{tenantId}/streams?page=1&pageSize=50` | `{streamId, aggregateType, version, createdAt, updatedAt}[]` |
| `GET /diag/tenants/{tenantId}/streams/{streamId:guid}/events` | `{sequence, version, timestamp, eventType, data}[]` en orden cronológico |
| `GET /diag/tenants/{tenantId}/projections/presupuestos` | `PresupuestoReadModel[]` |
| `GET /diag/tenants/{tenantId}/projections/configuracion` | `ConfiguracionTenantActual` o `404` |

## Trade-offs aceptados

- **Raw SQL contra tablas internas de Marten**: `mt_events` y `mt_streams`
  son parte del schema interno. Si Marten 8+ renombra columnas, hay que
  actualizar el query. La alternativa (replicar el stream en otro
  documento) duplica la verdad.
- **Paginación simple** (`page`/`pageSize` sin cursor). Suficiente para
  debugging en dev; evolucionará si hace falta en prod.
- **Serialización de `data` como JSON crudo**. Se muestra tal cual está
  persistido. Útil para detectar inconsistencias de serialización
  (`Dinero`, `Moneda`).

## Evolución prevista

- **Next**: autenticación (slice de seguridad cuando exista).
- **Next**: filtros por tipo de evento y rango temporal en el listado
  de eventos.
- **Después**: modo educativo — view del fold paso a paso (aplicar
  eventos uno a uno y mostrar el estado del agregado en cada paso).
- **Fuera de alcance para siempre**: proyecciones ad-hoc construidas
  desde la UI, edición de eventos, borrado de streams.
