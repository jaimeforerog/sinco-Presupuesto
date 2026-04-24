# Resolución de hot spots — MVP "Núcleo Presupuestal Básico"

**Proyecto:** Sinco Presupuesto
**Fecha:** 2026-04-24
**Resuelve:** § 9 de `01-event-storming-mvp.md` (Hot spots / decisiones pendientes).
**Consistente con:** `bc_presupuestacion_v1.1_multinivel_bim.md` (árbol multinivel, BIM).
**Estado:** Propuesta — requiere validación del Product Owner.

---

## Resumen ejecutivo

| # | Hot spot | Decisión |
|---|---|---|
| 1 | Jerarquía de rubros | **Árbol n-ario**, profundidad configurable por presupuesto (default 10, tope rígido 15). |
| 2 | Multimoneda | **Multimoneda a nivel de partida.** Cada empresa (tenant) configura su `MonedaLocal`. El presupuesto tiene `MonedaBase` (default = MonedaLocal del tenant) inmutable. Las partidas pueden estar en cualquier moneda ISO 4217. Snapshot de tasas al aprobar congela el baseline. |
| 3 | Multitenancy | **Marten conjoint multi-tenant** (`Policy.MultiTenanted()`) con `tenant_id` en eventos y proyecciones. On-premise = un solo tenant. |
| 4 | Numeración de rubros | **Autogenerada por el sistema**, con override manual respetando prefijo del padre y unicidad. |
| 5 | Unicidad del código de presupuesto | **Proyección inline `PresupuestoCodigoIndex`** con Marten `UniqueIndex` compuesto `(TenantId, Codigo, PeriodoFiscal)`. |

---

## 1. Jerarquía de rubros

### Decisión

Se adopta un **árbol n-ario** (estructura multinivel) desde el MVP, alineado con la propuesta de `bc_presupuestacion_v1.1`. Cada `Rubro` (o `Nodo` en el lenguaje de v1.1) es **Agrupador** o **Terminal**:

- **Agrupador**: nodo interno. Total = suma de hijos. Puede tener hijos agrupadores o terminales.
- **Terminal**: hoja. Tiene monto asignado directamente (en MVP), y en v1.1 tendrá unidad/cantidad/precio.

**Profundidad**: configurable por presupuesto vía `ProfundidadMaxima` (default **10**, tope rígido **15**). En MVP la UI puede limitar la UX a 4 niveles por defecto si se quiere simplificar, pero el modelo lo soporta.

### Motivación

Un presupuesto real de obra rara vez cabe en 3 niveles fijos. Aplanar fuerza códigos compuestos artificiales y duplica identificadores. El costo de implementar árbol libre vs. fijo es comparable (un `ParentId` + validaciones), pero la flexibilidad a futuro es enorme.

### Invariantes que introduce

- `INV-A`: `Nivel ≤ ProfundidadMaxima` del presupuesto.
- `INV-B`: un terminal no puede tener hijos. Para convertirlo en agrupador se emite `RubroConvertidoAAgrupador` (limpia monto).
- `INV-C`: el nuevo padre en un movimiento debe ser Agrupador (o el presupuesto-raíz).
- `INV-D`: no se permiten ciclos. Como cada nodo tiene un único `ParentId` y este debe existir dentro del mismo agregado, los ciclos son estructuralmente imposibles si se valida en el command handler.

### Impacto en el MVP

El evento `RubroAgregado` ya contempla `RubroPadreId?`, así que el cambio es mínimo. Se agregan:

- `RubroConvertidoAAgrupador` (evento)
- `RubroMovido` (drag & drop — recodifica subrama, ver §4)
- `RubrosReordenados` (reordenamiento entre hermanos, batch)

### Alternativa descartada

Jerarquía fija `Grupo → Cuenta → Subcuenta`. Rechazada por inflexibilidad: distintos clientes manejan 4, 5 o 6 niveles de desagregación y las tres etiquetas fijas no mapean a sus planes de cuentas.

---

## 2. Multimoneda

### Decisión

**Multimoneda a nivel de partida con moneda local por empresa.**

- **Empresa (Tenant)** tiene una `MonedaLocal` configurada (p.ej. COP, USD, CLP, MXN, ARS, PEN). Se define al crear el tenant. Cambios posteriores son administrativos y emiten `MonedaLocalDelTenantCambiada` (no reescribe historia — aplica hacia adelante).
- **Presupuesto** tiene una `MonedaBase` (la moneda en la que se reporta el total). Por default hereda la `MonedaLocal` del tenant al momento de crear, pero el usuario puede sobrescribirla (ej. un contrato de obra internacional en USD aunque la empresa opere en COP). `MonedaBase` es **inmutable** tras la creación del presupuesto (reformula INV-7).
- **Partidas (Nodo Terminal / Rubro Terminal)**: pueden estar en **cualquier moneda ISO 4217**, no necesariamente la `MonedaBase`. Ejemplo típico: insumos importados en USD, mano de obra local en COP, equipo alquilado en EUR.
- **Catálogo de tasas de cambio**: agregado/proyección separada `TasaDeCambio { fecha, desde, hacia, tasa, fuente }`. Alimentado manualmente o por servicio externo (Banco Central, provider FX).

### Modelo — value object `Dinero`

```csharp
public readonly record struct Dinero(decimal Valor, Moneda Moneda)
{
    public static Dinero Cero(Moneda m) => new(0m, m);
    public Dinero En(Moneda destino, TasaDeCambio tasa) =>
        destino == Moneda ? this : new(Valor * tasa.Factor, destino);
    // + operadores aritméticos que lanzan si las monedas difieren
}

public readonly record struct Moneda(string CodigoIso4217); // "COP", "USD", "EUR", ...
```

Toda cantidad monetaria en el dominio usa `Dinero` — nunca `decimal` pelado.

### Snapshot de tasas al aprobar

Cuando se ejecuta `AprobarPresupuesto`:

1. El handler recolecta todas las monedas distintas presentes en las partidas.
2. Consulta las tasas vigentes (día de aprobación) hacia `MonedaBase` — una por cada moneda.
3. Si falta alguna tasa, la aprobación falla (INV-15 abajo).
4. El evento `PresupuestoAprobado` lleva un `SnapshotTasas: Dictionary<Moneda, decimal>` que congela la foto.

A partir de ahí:

- **Baseline** del presupuesto = totales calculados con `SnapshotTasas`. Inmutable. Es la cifra contra la cual se controlan ejecuciones y modificaciones.
- **Vista live** = totales recalculados con tasas del día. Útil para planning/what-if, no reemplaza el baseline.

### Flujo de ejecución (anticipado — fuera del MVP)

Cuando llegue el BC de Ejecución: las ejecuciones tienen su propia fecha y su propia tasa snapshot. La variación cambiaria se expone como una categoría de desviación separada (no mezclada con desviación de cantidad ni de precio unitario). Esto es clave para el control presupuestal serio.

### Motivación

- La realidad de obra en LatAm es multimoneda: cotizaciones en USD para importados, contratos locales en moneda del país. Forzar una moneda única obliga al presupuestador a hacer conversiones manuales y pierde la trazabilidad del valor original.
- El valor original de cada partida se preserva: en los eventos queda escrito "esta partida fue cotizada en 42.85 USD el 2026-04-24 con tasa 4.170 COP/USD". Reproducible, auditable, robusto.
- Congelar el baseline al aprobar es el comportamiento correcto para control presupuestal: el "deber ser" no debe moverse con el tipo de cambio.

### Invariantes — cambios sobre el MVP

- **INV-7 (reformulada)**: "La `MonedaBase` del presupuesto es inmutable tras crearse." (Antes decía "La moneda es inmutable".)
- **INV-13 (nueva)**: toda cantidad monetaria en un evento incluye su moneda original — los eventos llevan `Dinero`, nunca `decimal` pelado.
- **INV-14 (nueva)**: un presupuesto solo puede aprobarse si existen tasas de cambio vigentes hacia `MonedaBase` para todas las monedas presentes en sus partidas.
- **INV-15 (nueva)**: el baseline aprobado se calcula con `SnapshotTasas` del evento `PresupuestoAprobado` — inmutable.
- **INV-16 (nueva)**: la `MonedaLocal` del tenant debe existir (agregado `ConfiguracionTenant` iniciado) antes de crear el primer presupuesto.

### Eventos nuevos

| Evento | Agregado | Payload |
|---|---|---|
| `MonedaLocalDelTenantConfigurada` | ConfiguracionTenant | `TenantId`, `Moneda`, `ConfiguradaEn`, `ConfiguradaPor` |
| `MonedaLocalDelTenantCambiada` | ConfiguracionTenant | `TenantId`, `MonedaAnterior`, `MonedaNueva`, `Motivo`, `CambiadaEn`, `CambiadaPor` |
| `TasaDeCambioRegistrada` | CatalogoTasas | `Fecha`, `MonedaDesde`, `MonedaHacia`, `Tasa`, `Fuente` |
| `TasaDeCambioCorregida` | CatalogoTasas | `TasaAnteriorId`, `Fecha`, `MonedaDesde`, `MonedaHacia`, `TasaCorregida`, `Motivo` |

Además, los eventos de partida ahora llevan `Dinero` con moneda:

- `RubroMontoAsignado` (reemplaza `MontoAsignadoARubro` del MVP): payload `Monto: Dinero(valor, moneda)` en lugar de `Monto: decimal`.
- `PresupuestoAprobado` adquiere campo `SnapshotTasas: { [moneda]: tasaAMonedaBase }`.

### Proyecciones

- `ConfiguracionTenantActual` — `{TenantId, MonedaLocal, ...}`. Se consulta al crear presupuesto para heredar default.
- `TasasDeCambioVigentes` — última tasa por `(desde, hacia)`. Query frecuente al aprobar y en vistas live.
- `PresupuestoBaselineEnMonedaBase` — totales congelados al aprobar, usando `SnapshotTasas`.
- `PresupuestoVistaLive` — totales recalculados con tasas del día (flag `?asOf=today|baseline`).

### Impacto en el MVP

- Introducir value object `Dinero` desde el día 1.
- Un nuevo agregado liviano `ConfiguracionTenant` con el evento `MonedaLocalDelTenantConfigurada`. Se crea al onboarding del tenant.
- Un mini-catálogo de tasas: puede arrancar con UI manual ("cargar tasa del día") y evolucionar a integración con API FX (ej. banrep.gov.co para COP o exchangerate.host genérico).
- El workflow de aprobación valida INV-14 antes de emitir `PresupuestoAprobado`.
- Complejidad incremental estimada: +25–30 % del scope del MVP. Se justifica por ser un core concern del dominio (no se puede bolt-on después sin migrar eventos).

### Alternativas descartadas

- **Una moneda por presupuesto** (decisión anterior). Descartada por decisión del PO: no refleja la realidad de proyectos con insumos importados.
- **Multimoneda sin snapshot** (tasas "live" siempre, incluyendo baseline). Descartada: un baseline que cambia con el tipo de cambio hace imposible el control presupuestal serio — ya no sabes contra qué comparar.
- **Moneda por presupuesto + campo de "cotización en moneda extranjera" como anotación**. Descartada: es lo peor de ambos mundos — no hay aritmética FX real, y las anotaciones se vuelven inconsistentes.

---

## 3. Multitenancy

### Decisión

**Marten conjoint multi-tenant** como configuración por defecto.

- Cada evento y cada documento llevan `tenant_id` (Marten lo maneja nativo: `options.Policies.AllDocumentsAreMultiTenanted()` y `options.Events.TenancyStyle = TenancyStyle.Conjoint`).
- Las queries Marten filtran automáticamente por tenant del `IDocumentSession`.
- **On-premise**: se despliega con `tenant_id = "default"` (o el nombre del cliente). El mismo binario funciona.
- **SaaS**: cada organización cliente es un tenant. El login resuelve el `tenantId` y se pasa al `DocumentStore.LightweightSession(tenantId)`.

### Motivación

- Un único codebase que sirve ambos modelos de despliegue (SaaS y on-premise).
- Operacionalmente más barato que schema-per-tenant o DB-per-tenant (una sola BD a respaldar y migrar).
- Marten ya resuelve el filtrado por tenant — no hay que escribir WHERE a mano.
- Aislamiento suficiente para el sector (presupuestos de construcción no son datos regulados al nivel de salud/financiero).

### Cuándo escalar a schema-per-tenant o DB-per-tenant

Solo si aparece un requerimiento duro:

- Cliente enterprise exige aislamiento físico por compliance.
- Un tenant crece tanto que su volumen degrada al resto (improbable en este dominio).
- Residencia de datos por país (ej. un tenant debe tener BD en Chile específicamente).

En cualquiera de esos casos, Marten soporta `TenancyStyle.Separate` (schema por tenant) cambiando configuración — el modelo de dominio no cambia.

### Impacto en el MVP

- Agregar `TenantId` a la metadata de eventos (Marten lo hace solo si se configura multi-tenancy desde el inicio).
- Todas las proyecciones reciben el `tenant_id` en sus tablas automáticamente.
- El endpoint de login / JWT debe incluir `tenant_id` como claim.

### Alternativa descartada

"Un tenant por instalación" sin conjoint. Rechazada: fuerza a Sincosoft a operar N bases de datos si vende SaaS a múltiples clientes, multiplica el costo operativo.

---

## 4. Numeración de rubros

### Decisión

**Modelo híbrido: el sistema autogenera el código jerárquico; el usuario puede sobrescribirlo** respetando dos reglas:

1. **Prefijo del padre**: el código hijo debe comenzar con el código del padre + separador `.` (ej. padre `02` → hijo `02.01`, `02.02`, ..., `02.99`). El tramo propio es numérico de 2 dígitos.
2. **Unicidad dentro del presupuesto**.

El sistema sugiere el siguiente disponible al agregar (ej. si el último hijo de `02` es `02.07`, sugiere `02.08`). Mover un nodo **recodifica toda su subrama** y emite `RubroMovido` con la lista de `{ rubroId, codigoAnterior, codigoNuevo }` afectados.

### Formato canónico

Regex: `^\d{2}(\.\d{2}){0,14}$` (hasta 15 niveles, consistente con v1.1).

### Motivación

- **Autogeneración** cubre el 80% del uso (presupuestador no quiere teclear códigos).
- **Override** permite respetar el plan de cuentas específico de la organización (CAPECO, PAC, códigos internos).
- **Recodificación al mover** garantiza que el código refleje siempre la posición en el árbol.

### Invariantes que introduce

- `INV-E`: formato `^\d{2}(\.\d{2}){0,14}$`.
- `INV-F`: el código de un hijo debe extender el código del padre con exactamente un segmento adicional.
- `INV-G`: código único dentro del presupuesto.

### Impacto en el MVP

Se requiere un **servicio de dominio** `GeneradorCodigosJerarquicos` que:

- Calcule el siguiente código disponible dado un padre.
- Valide overrides contra reglas de prefijo y unicidad.
- Rehaga códigos al mover/reordenar subramas y devuelva la lista de cambios para el evento `RubroMovido`.

### Alternativa descartada

Código 100% manual. Rechazada: fricción alta en UI, riesgo de inconsistencias.
Código 100% automático sin override. Rechazada: no encaja con planes de cuentas impuestos por normativa o cliente.

---

## 5. Unicidad del código de presupuesto

### Decisión

**Implementar con una proyección inline `PresupuestoCodigoIndex`** + Marten `UniqueIndex` compuesto.

```csharp
public class PresupuestoCodigoIndex
{
    public Guid Id { get; set; }           // = PresupuestoId
    public string TenantId { get; set; } = default!;
    public string Codigo { get; set; } = default!;
    public string PeriodoFiscal { get; set; } = default!;  // ej. "2026"
}

// StoreOptions:
options.Schema.For<PresupuestoCodigoIndex>()
    .UniqueIndex(UniqueIndexType.Computed,
                 "uq_presup_codigo",
                 x => x.TenantId,
                 x => x.Codigo,
                 x => x.PeriodoFiscal);
```

La proyección se alimenta del evento `PresupuestoCreado` de forma **inline** (mismo transaction scope que el append del evento), de manera que si el índice único falla por colisión, la transacción entera se aborta y no se crea el stream.

En el handler, se captura `MartenCommandException` (o la excepción equivalente de `PostgresException` por violación de unicidad), y se traduce a un error de dominio `CodigoPresupuestoDuplicadoException` que la API expone como `HTTP 409 Conflict`.

### Motivación

- **El agregado no puede ver otros agregados.** Validar unicidad dentro del agregado `Presupuesto` requeriría cargar todos los streams — anti-patrón en ES.
- Marten + Postgres resuelven esto nativamente con `UniqueIndex`. No se reinventa la rueda.
- La proyección *inline* asegura que la validación ocurra en la misma transacción que el append — no hay ventana de inconsistencia.

### Alternativa descartada

- **Guardia vía query previa** (`¿existe un presupuesto con este código?` antes de escribir). Rechazada: es un TOCTOU clásico — dos creaciones concurrentes pueden ambas ver "no existe" y escribir.
- **Reservation pattern con stream especial**. Válido pero sobredimensionado para el MVP.
- **Validación sólo en la UI**. Rechazada: no es una garantía.

### Impacto en el MVP

- Nueva proyección `PresupuestoCodigoIndex` (inline).
- Un test de integración que dispare dos `CrearPresupuesto` concurrentes con el mismo código y verifique que exactamente uno gana.

---

## Actualización de invariantes del MVP

Sobre las INV-1 a INV-7 del documento `01-event-storming-mvp.md`:

- **INV-7** se **reformula**: "La `MonedaBase` del presupuesto es inmutable tras crearse" (antes: "La moneda es inmutable una vez creado el presupuesto").

Se **agregan**:

| # | Invariante |
|---|---|
| INV-8 | Profundidad de un rubro ≤ `ProfundidadMaxima` del presupuesto (default 10, tope 15). |
| INV-9 | Un rubro terminal no puede tener hijos. |
| INV-10 | El código de un rubro extiende el código del padre con exactamente un segmento `\.\d{2}`. |
| INV-11 | Código de rubro único dentro del presupuesto. |
| INV-12 | Unicidad del código de presupuesto resuelta por `UniqueIndex(TenantId, Codigo, PeriodoFiscal)` en la proyección `PresupuestoCodigoIndex`. |
| INV-13 | Toda cantidad monetaria en un evento se almacena como `Dinero(valor, moneda)` — nunca `decimal` pelado. |
| INV-14 | Un presupuesto solo puede aprobarse si existen tasas de cambio vigentes hacia `MonedaBase` para todas las monedas presentes en sus partidas. |
| INV-15 | El baseline del presupuesto aprobado se calcula con `SnapshotTasas` del evento `PresupuestoAprobado` — inmutable. |
| INV-16 | La `MonedaLocal` del tenant debe estar configurada antes de crear el primer presupuesto del tenant. |

---

## Eventos adicionales al MVP

A la tabla de § 5 se agregan.

**Jerarquía** (por decisión #1):

| Evento | Payload |
|---|---|
| `RubroConvertidoAAgrupador` | `PresupuestoId`, `RubroId`, `MontoAnterior: Dinero`, `ConvertidoEn` |
| `RubroMovido` | `PresupuestoId`, `RubroId`, `ParentAnteriorId?`, `ParentNuevoId?`, `OrdenNuevo`, `CodigosRecodificados[]` |
| `RubrosReordenados` | `PresupuestoId`, `ParentId?`, `NuevoOrden[] {rubroId, orden}` |

**Multimoneda** (por decisión #2):

| Evento | Agregado | Payload |
|---|---|---|
| `MonedaLocalDelTenantConfigurada` | ConfiguracionTenant | `TenantId`, `Moneda`, `ConfiguradaEn`, `ConfiguradaPor` |
| `MonedaLocalDelTenantCambiada` | ConfiguracionTenant | `TenantId`, `MonedaAnterior`, `MonedaNueva`, `Motivo`, `CambiadaEn`, `CambiadaPor` |
| `TasaDeCambioRegistrada` | CatalogoTasas | `Fecha`, `MonedaDesde`, `MonedaHacia`, `Tasa`, `Fuente` |
| `TasaDeCambioCorregida` | CatalogoTasas | `TasaAnteriorId`, `Fecha`, `MonedaDesde`, `MonedaHacia`, `TasaCorregida`, `Motivo` |

**Eventos del MVP que cambian de payload** (por adoptar `Dinero`):

| Evento | Cambio |
|---|---|
| `MontoAsignadoARubro` | `Monto: decimal` → `Monto: Dinero(valor, moneda)`. Se incluye también `TasaASnapshot` (tasa a `MonedaBase` del presupuesto en el momento del registro, informativa). |
| `PresupuestoAprobado` | Se agrega `SnapshotTasas: { [moneda]: tasaAMonedaBase }`. |

Nota: `RubroRetirado` ya existe y sigue siendo válido (eliminar un nodo). `RubroConvertidoAAgrupador` es la operación inversa conceptual cuando se quiere "anidar algo bajo un terminal".

---

## Próximos pasos

1. **Validar este documento con el Product Owner** (cosa de 15 min).
2. Reemplazar la § 9 de `01-event-storming-mvp.md` con una sección "§ 9 — Resueltas" que enlace acá.
3. Armar el scaffolding `.NET 9 + Marten + Wolverine + Docker Compose` con:
   - `Policies.AllDocumentsAreMultiTenanted()`
   - `Events.TenancyStyle = TenancyStyle.Conjoint`
   - Proyección inline `PresupuestoCodigoIndex` con `UniqueIndex`
4. Implementar el primer *slice*: `CrearPresupuesto` → `PresupuestoCreado` + `PresupuestoCodigoIndex` → endpoint GET.
5. Implementar `AgregarRubro` soportando `RubroPadreId` + autogeneración de código.

---

*Fin del documento de decisiones.*
