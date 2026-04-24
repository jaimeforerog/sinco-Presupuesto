# Sinco Presupuesto

Sistema de control presupuestal con **Event Sourcing + CQRS + EDA** sobre .NET 9, Marten/Wolverine y Azure Container Apps.

## Estado del scaffold (iteración 1)

Este scaffold contiene el **MVP núcleo mínimo** end-to-end:

- `CrearPresupuesto` → `PresupuestoCreado` persistido en el event store.
- Proyección inline `PresupuestoReadModel` para lectura.
- Value objects `Dinero` y `Moneda` (multimoneda ISO 4217).
- Multi-tenant conjoint (un schema, discriminador `tenant_id`).
- Endpoint HTTP `POST /api/tenants/{tenantId}/presupuestos` y `GET /…/{id}`.
- Tests unitarios del agregado y del value object `Dinero`.
- Dockerfile + bicep para desplegar en Azure Container Apps + Postgres Flexible Server.

Faltan (próximas iteraciones): agregados `ConfiguracionTenant`, `TasaDeCambio`, resto del ciclo de vida del presupuesto (aprobar/activar/cerrar), árbol de rubros, SnapshotTasas.

## Stack

| Capa | Tecnología |
|---|---|
| Event Store / CQRS | Marten 7 (sobre PostgreSQL 16) |
| Command/message bus | Wolverine 3 |
| Runtime | .NET 9 (ASP.NET Core minimal APIs) |
| Compute Azure | Container Apps (scale-to-zero) |
| DB Azure | Database for PostgreSQL Flexible Server |

## Estructura

```
sinco presupuesto/
├── SincoPresupuesto.sln
├── global.json  Directory.Build.props  Directory.Packages.props
├── src/
│   ├── SincoPresupuesto.Domain/         # Agregados, eventos, value objects
│   ├── SincoPresupuesto.Application/    # Handlers Wolverine, proyecciones
│   ├── SincoPresupuesto.Infrastructure/ # (placeholder) adaptadores externos
│   └── SincoPresupuesto.Api/            # Host ASP.NET, endpoints, DI
├── tests/
│   └── SincoPresupuesto.Domain.Tests/
├── infra/main.bicep                     # ACA + Postgres Flexible Server
├── Dockerfile
└── .dockerignore
```

## Requisitos

- .NET SDK 9.0.100 o superior
- Docker Desktop (para build del container y Postgres local opcional)
- Azure CLI (para desplegar a Azure)
- Postgres 16 corriendo en local **o** una cadena de conexión a un servidor remoto

## Ejecutar en local

1. Levanta Postgres (opción rápida con Docker):

   ```bash
   docker run -d --name sinco-pg -p 5432:5432 \
     -e POSTGRES_DB=sinco_presupuesto \
     -e POSTGRES_PASSWORD=postgres \
     postgres:16
   ```

2. Restaura, compila y ejecuta:

   ```bash
   dotnet restore
   dotnet build
   dotnet run --project src/SincoPresupuesto.Api
   ```

3. Prueba el endpoint:

   ```bash
   curl -X POST http://localhost:5080/api/tenants/acme/presupuestos \
     -H "Content-Type: application/json" \
     -d '{
       "codigo": "OBRA-2026-01",
       "nombre": "Torre Norte",
       "periodoInicio": "2026-01-01",
       "periodoFin": "2026-12-31",
       "monedaBase": "COP",
       "profundidadMaxima": 10
     }'
   ```

4. Ejecuta los tests:

   ```bash
   dotnet test
   ```

## Build del contenedor

```bash
docker build -t sincopresupuesto-api:local .
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__Postgres="Host=host.docker.internal;Port=5432;Database=sinco_presupuesto;Username=postgres;Password=postgres" \
  sincopresupuesto-api:local
```

## Despliegue a Azure

1. Crea resource group y despliega infraestructura:

   ```bash
   az group create -n rg-sinco-presupuesto-dev -l eastus2

   az deployment group create \
     -g rg-sinco-presupuesto-dev \
     -f infra/main.bicep \
     -p environmentName=dev \
        postgresAdminLogin=sincoadmin \
        postgresAdminPassword='<ponlo-aqui>'
   ```

2. Push de la imagen al ACR creado:

   ```bash
   ACR=$(az deployment group show -g rg-sinco-presupuesto-dev -n main --query properties.outputs.acrLoginServer.value -o tsv)
   az acr login --name "${ACR%%.*}"
   docker tag sincopresupuesto-api:local "$ACR/sincopresupuesto-api:latest"
   docker push "$ACR/sincopresupuesto-api:latest"
   ```

3. Actualiza el Container App para que tome la nueva imagen (ya está configurado con `:latest` pero fuerza el rollout):

   ```bash
   az containerapp revision restart -g rg-sinco-presupuesto-dev -n ca-sincopresupuesto-dev
   ```

## Decisiones arquitectónicas relevantes

- **Event Sourcing por agregado**: cada `Presupuesto` es un stream (`presupuesto-{id}`).
- **Multi-tenant conjoint** (Marten): un schema, `tenant_id` como discriminador. Aplicable a eventos y proyecciones. El endpoint exige `tenantId` en la ruta.
- **Multimoneda a nivel de partida**: `Dinero(Valor, Moneda)` es obligatorio en todo el dominio. La aritmética entre monedas distintas lanza `MonedasDistintasException`.
- **`MonedaBase` inmutable** tras crear el presupuesto (reformula INV-7).
- **Profundidad del árbol** configurable por presupuesto (default 10, tope rígido 15).

## Roadmap — estado actual

**Slices implementados (dominio completo + infra-wire + tests end-to-end):**

| # | Slice | Dominio | Endpoints HTTP | Integration |
|---|---|---|---|---|
| 00 | shared-kernel (`Dinero`, `Moneda`, excepciones) | ✅ | — | ✅ |
| 01 | `CrearPresupuesto` | ✅ | `POST/GET /api/tenants/{t}/presupuestos` | ✅ |
| 02 | `ConfigurarMonedaLocalDelTenant` | ✅ | `POST/GET /api/tenants/{t}/configuracion` | ✅ |
| 03 | `AgregarRubro` | ✅ | `POST /…/rubros` | ✅ |
| 04 | `AsignarMontoARubro` | ✅ | `POST /…/rubros/{r}/monto` | ✅ |
| `_obs-visor-eventos` | Visor de Eventos (transversal) | — | `GET /diag/*` + UI | ✅ |

**Tests:** 145/145 verdes (125 dominio + 20 integración HTTP→PG bajo Testcontainers).

**Pendientes en la ruta crítica al MVP:**

1. **Slice 05 `AprobarPresupuesto`** — cierra followup #13 (INV-3 en estado ≠ Borrador). Versión mínima acepta solo partidas en `MonedaBase`; multimoneda completo sigue con `TasaDeCambio`.
2. **Slice 06 `TasaDeCambioRegistrada`** — desbloquea `SnapshotTasas` del baseline.
3. **Slice 07 `ActivarPresupuesto`** + **Slice 08 `CerrarPresupuesto`** — completan el ciclo de vida.
4. **Slice `QuitarRubro`** — event-storming §4 (rubros con monto=0).
5. **Followups técnicos**: CI con coverlet (#1), `PresupuestoCodigoIndex` (#2), `TenantNoConfiguradoException` (#8), `GeneradorCodigosJerarquicos` (#11), `RubroConvertidoAAgrupador`/`RubroMovido` para árbol dinámico.

Ver `FOLLOWUPS.md` para el backlog completo (14 items abiertos) y `slices/*/review-notes.md` para auditorías por slice.
