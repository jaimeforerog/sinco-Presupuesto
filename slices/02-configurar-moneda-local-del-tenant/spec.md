# Slice 02 — ConfigurarMonedaLocalDelTenant

**Autor:** domain-modeler
**Fecha:** 2026-04-24
**Estado:** firmado
**Agregado afectado:** `ConfiguracionTenant`
**Decisiones previas relevantes:**
- `02-decisiones-hotspots-mvp.md` §2 (multimoneda: tenant con `MonedaLocal`).
- `01-event-storming-mvp.md` §6, §9 (eventos de dominio, hot spots resueltos).
- Memoria `project_multimoneda.md` (tenant con `MonedaLocal`, base de default de `Presupuesto.MonedaBase`).
- Memoria `project_stack_decision.md` (multi-tenant conjoint en Marten).
- Memoria `project_metodologia.md` (excepciones heredan de `DominioException`; tests asertan por tipo, no por mensaje).

---

## 1. Intención

Un administrador del tenant (empresa) configura la moneda local de operación del tenant en el contexto de Gestión Presupuestal. Esta moneda es el default de `MonedaBase` al crear nuevos presupuestos (slice 01) y sirve como unidad de reporte agregada del tenant. El `ConfiguracionTenant` es un agregado event-sourced nuevo cuyo `StreamId` coincide con el `TenantId` (uno-a-uno). Este slice cubre la **configuración inicial** del tenant — cambios posteriores son un comando aparte fuera del MVP.

Se elige el nombre `ConfigurarMonedaLocalDelTenant` (en vez de `CrearTenant` que el usuario sugirió en lenguaje coloquial) porque:
- El tenant como entidad corporativa ya existe fuera del BC (Identity/Onboarding).
- Este agregado **registra la configuración** del tenant en el BC de Gestión Presupuestal, no "crea" el tenant.
- El nombre deja espacio a futuros comandos del mismo agregado (p.ej. `CambiarMonedaLocalDelTenant` administrativo, `ConfigurarAnioFiscalPorDefecto`, etc.).

## 2. Comando

```csharp
public sealed record ConfigurarMonedaLocalDelTenant(
    string TenantId,
    Moneda MonedaLocal,
    string ConfiguradoPor = "sistema");
```

El `Moneda` VO llega validado por construcción (ver §12 "Impacto en SharedKernel"). El slice 02 confía en esa invariante y **no** revalida ISO 4217 dentro del agregado.

## 3. Evento(s) emitido(s)

| Evento | Payload | Cuándo |
|---|---|---|
| `MonedaLocalDelTenantConfigurada` | `TenantId`, `Moneda`, `ConfiguradaEn`, `ConfiguradaPor` | Al aceptar el comando sobre un stream vacío. |

## 4. Precondiciones

Todas las excepciones heredan de `SincoPresupuesto.Domain.SharedKernel.DominioException`. Los tests verifican **tipo + propiedades**, nunca mensajes.

- `PRE-1`: `TenantId` no es nulo ni vacío — excepción: `CampoRequeridoException` con `NombreCampo = "TenantId"`.
- `PRE-2` (normalización, no fallo): `ConfiguradoPor` vacío o whitespace → se sustituye por `"sistema"` al emitir el evento.

Nota: no hay PRE de validación ISO 4217 aquí. La validación vive en el VO `Moneda` (ver §12). La incorrecta construcción de `Moneda` desde un string inválido es responsabilidad del endpoint HTTP (que devuelve `400`) y del slice 00 (tests del SharedKernel).

## 5. Invariantes tocadas

- `INV-16`: "El tenant debe tener `MonedaLocal` configurada antes de crear el primer presupuesto." Este slice **establece** esa invariante para el tenant por primera vez. Los handlers de `Presupuesto` la honrarán (followup #8).
- `INV-NEW-1` (de este agregado): la `ConfiguracionTenant` de un tenant se inicializa una sola vez. Reintentar el comando sobre un stream existente lanza `TenantYaConfiguradoException`.
- `INV-NEW-2`: la `MonedaLocal` tras la primera configuración es inmutable bajo este comando. Cambios administrativos requieren un comando aparte (fuera del MVP).

## 6. Escenarios Given / When / Then

### 6.1 Happy path

**Given** stream vacío (no hay configuración previa del tenant).
**When** `ConfigurarMonedaLocalDelTenant(TenantId="acme", MonedaLocal=Moneda.COP, ConfiguradoPor="admin-alice")` con `ahora` dado desde fuera.
**Then** emite un único `MonedaLocalDelTenantConfigurada` con `TenantId="acme"`, `Moneda=Moneda.COP`, `ConfiguradaEn=ahora`, `ConfiguradaPor="admin-alice"`.

### 6.2 `ConfiguradoPor` vacío → default "sistema"

**Given** stream vacío.
**When** comando válido con `ConfiguradoPor = ""` o `"   "`.
**Then** emite `MonedaLocalDelTenantConfigurada` con `ConfiguradaPor = "sistema"`.

### 6.3 Violación de `PRE-1` — TenantId vacío

**Given** stream vacío.
**When** comando con `TenantId = ""` o `"   "`.
**Then** lanza `CampoRequeridoException` con `NombreCampo = "TenantId"`.

### 6.4 Violación de `INV-NEW-1` — tenant ya configurado

**Given** stream contiene `MonedaLocalDelTenantConfigurada { TenantId: "acme", Moneda: Moneda.COP, ... }`.
**When** `ConfigurarMonedaLocalDelTenant(TenantId="acme", MonedaLocal=Moneda.USD)`.
**Then** lanza `TenantYaConfiguradoException` con `TenantId = "acme"` y `MonedaLocalActual = Moneda.COP`.

### 6.5 Fold del evento — `ConfiguracionTenant` refleja el estado

**Given** evento `MonedaLocalDelTenantConfigurada` producido por el comando.
**When** reconstruir el agregado aplicando ese evento (fold).
**Then** el agregado tiene `TenantId` correcto, `MonedaLocal` fijada, `ConfiguradaEn` y `ConfiguradaPor` consistentes.

## 7. Idempotencia / retries

- **No idempotente por diseño**: cada `ConfigurarMonedaLocalDelTenant` quiere configurar el tenant por primera vez y solo una vez. Reintentos son un error del caller.
- **Protección en dominio**: el agregado rechaza reintentos lanzando `TenantYaConfiguradoException` (ver §6.4). Complementario: Marten rechaza `StartStream` sobre stream existente, pero la excepción del dominio es la primaria porque puede interceptarse antes.
- **IdempotencyKey**: no se introduce. El flujo de onboarding garantiza ejecución única.

## 8. Impacto en proyecciones / read models

- **Nuevo** `ConfiguracionTenantActual` — documento plano `{ TenantId, MonedaLocal, ConfiguradaEn, ConfiguradaPor }`. Proyección **inline** `SingleStreamProjection<ConfiguracionTenantActual>`.
- La proyección es inline porque la `MonedaLocal` es dato de referencia que otros slices consultan de inmediato (sin eventual consistency): el handler de `CrearPresupuesto` la va a necesitar en el followup #8.

## 9. Impacto en endpoints HTTP

- **`POST /api/tenants/{tenantId}/configuracion/moneda-local`** — configura la moneda local del tenant.
  - Request body: `{ monedaLocal: "COP", configuradoPor?: "…" }`.
  - `201 Created` con `Location` al `GET`. Body: `{ tenantId, monedaLocal, configuradaEn, configuradaPor }`.
  - `400 Bad Request` si `monedaLocal` no es ISO 4217 válida (falla en `new Moneda(string)`) o si `TenantId` vacío.
  - `409 Conflict` si el tenant ya fue configurado (`TenantYaConfiguradoException`).
- **`GET /api/tenants/{tenantId}/configuracion`** — lee `ConfiguracionTenantActual`. `200` con el documento, `404` si no existe.

## 10. Preguntas abiertas

- [x] **¿Dónde validamos ISO 4217?** — Resuelto: en el VO `Moneda` mismo. PRE-2 del slice sale del scope; entra como refactor transversal (§12).
- [x] **¿Cómo se acopla con `CrearPresupuesto`?** — Resuelto: el dominio de `Presupuesto` permanece independiente; el handler de `CrearPresupuesto` valida que `ConfiguracionTenantActual` exista y lanza `TenantNoConfiguradoException` si no. Refactor del handler es followup #8 (no pertenece a slice 02).
- [x] **Nombre de la excepción de reintento** — Resuelto: `TenantYaConfiguradoException`.

## 11. Checklist pre-firma

- [x] Todas las precondiciones mapean a un escenario Then (§6.3).
- [x] Todas las invariantes tocadas mapean a un escenario Then (INV-NEW-1 → §6.4; INV-16 se establece en §6.1; INV-NEW-2 es declarativa del agregado sin comando que la viole en este slice).
- [x] El happy path está presente (§6.1).
- [x] Impactos en SharedKernel, infra y otros slices están documentados (§8, §9, §12, §13).
- [x] Preguntas abiertas resueltas.

## 12. Impacto en SharedKernel (refactor transversal incluido en el slice)

Este slice incluye un **refactor transversal necesario** en el SharedKernel para que la promesa "el VO `Moneda` es válido por construcción" se cumpla:

1. **`Moneda`** deja de aceptar cualquier regex `[A-Z]{3}` y pasa a validar contra una lista hardcoded de los **códigos ISO 4217 vigentes** (fuente: [ISO 4217 current codes](https://www.iso.org/iso-4217-currency-codes.html), lista a embed como `static readonly HashSet<string>` o similar). La lista inicial mínima que DEBE soportar el MVP: `COP, USD, EUR, MXN, CLP, PEN, ARS, BRL, GBP, CAD, JPY` (se puede ampliar a ISO completa si el ejercicio de adopción es barato — decisión del implementer; la completitud de la lista va al slice 00 retroactivo, followup #4).
2. **`Moneda`** pasa a lanzar `CodigoMonedaInvalidoException(string codigoIntentado) : DominioException` en vez de `ArgumentException` al construir con código fuera de la lista.
3. **`CodigoMonedaInvalidoException`** y **`TenantYaConfiguradoException`** son excepciones nuevas en `SincoPresupuesto.Domain.SharedKernel` (o en el módulo del agregado, a criterio del implementer).
4. **Tests existentes afectados:**
   - `DineroTests.Moneda_rechaza_codigo_invalido` — cambia el tipo esperado de `ArgumentException` a `CodigoMonedaInvalidoException`. Los casos de prueba (`"US"`, `"USDD"`, `"US1"`, `""`) siguen siendo válidos, pero se agrega `"XYZ"` (tres letras que no son ISO) como caso crítico que hoy pasa incorrectamente.
   - `DineroTests.Moneda_normaliza_codigo` — sin cambio de comportamiento (normaliza y luego valida).

## 13. Follow-ups generados por este slice

Se agregan a `FOLLOWUPS.md`:

- **#8** — refactor `CrearPresupuestoHandler` para validar existencia de `ConfiguracionTenantActual` antes de iniciar el stream de `Presupuesto`. Lanzar `TenantNoConfiguradoException` si no existe. Introduce la excepción en `SharedKernel`. Origen: slice-02, spec §10 Q2.
- **#9** — ampliar la lista ISO 4217 de `Moneda` a la lista completa (actualmente se embed la lista mínima del MVP). Origen: slice-02, spec §12. Se cierra naturalmente con slice 00 retroactivo (followup #4).
