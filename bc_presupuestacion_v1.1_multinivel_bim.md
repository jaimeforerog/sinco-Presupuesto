# BC Presupuestación v1.1 — Estructura multinivel e integración BIM

**Proyecto:** Sinco Presupuesto
**Stack:** .NET 9 + Marten + Wolverine + PostgreSQL + React
**Fecha:** 23 de abril de 2026
**Revisa:** `bc_presupuestacion_diseno.md` (v1.0) — este documento sustituye las secciones indicadas
**Estado:** Propuesta — requiere validación con Product Owner

---

## Índice

1. [Qué cambia frente a v1.0](#1-qué-cambia-frente-a-v10)
2. [Modelo de dominio multinivel](#2-modelo-de-dominio-multinivel)
3. [Integración BIM — diseño de alcance](#3-integración-bim--diseño-de-alcance)
4. [Invariantes actualizadas](#4-invariantes-actualizadas)
5. [Value objects nuevos y modificados](#5-value-objects-nuevos-y-modificados)
6. [Catálogo de eventos — delta v1.0 → v1.1](#6-catálogo-de-eventos--delta-v10--v11)
7. [JSON Schemas de eventos nuevos](#7-json-schemas-de-eventos-nuevos)
8. [Flujos end-to-end nuevos](#8-flujos-end-to-end-nuevos)
9. [Impacto en servicios y estructura .NET](#9-impacto-en-servicios-y-estructura-net)
10. [Preguntas abiertas](#10-preguntas-abiertas)

---

## 1. Qué cambia frente a v1.0

Se incorporan dos cambios estructurales al alcance del bounded context Presupuestación:

**Cambio 1 — Estructura multinivel (árbol n-ario).** La jerarquía rígida de tres niveles (`Capitulo → Subcapitulo → Item`) de v1.0 se generaliza a un árbol de profundidad arbitraria. Un presupuesto real de obra pública o de edificación compleja suele manejar 4, 5 o incluso 6 niveles de desagregación (por ejemplo: `Grupo → Capítulo → Subcapítulo → Actividad → Subactividad → Ítem terminal`). Limitar a tres niveles fuerza al presupuestador a aplanar o a duplicar códigos, y no refleja cómo trabajan los contratistas serios.

**Cambio 2 — Integración BIM.** El presupuesto se vincula a elementos del modelo BIM (Revit, IFC) y puede obtener cantidades automáticamente desde el modelo. También soporta clasificaciones estándar (MasterFormat, UniFormat, OmniClass). Esta es una capacidad diferenciadora clave observada en SINCO ADPRO y en Presto, y el mercado ya la considera requisito.

Secciones del documento v1.0 que quedan **obsoletas** y son reemplazadas por las de este documento:

- § 4.1 (agregado `Presupuesto` con Capitulo/Subcapitulo/Item)
- § 4.2 (invariantes — se actualizan)
- § 4.5 (value objects — se agregan)
- § 6.3–6.12 (eventos de Capitulo, Subcapitulo, Item — se reemplazan por eventos de `Nodo`)
- § 6.20 (tabla resumen — se actualiza al final de este documento)

Las demás secciones de v1.0 (prerequisitos, granularidad, convenciones de eventos, workflow de aprobación, servicios, estructura de solución, process managers) siguen siendo válidas con ajustes menores que se indican en línea.

---

## 2. Modelo de dominio multinivel

### 2.1 Concepto: `NodoPresupuestal`

Todo elemento estructural del presupuesto es un **Nodo**. Los nodos forman un árbol: cada nodo tiene un padre (o `null` si es raíz del árbol del presupuesto) y puede tener N hijos. Hay dos tipos de nodo:

- **Agrupador**: nodo interno. No tiene cantidad ni precio; su total es la suma de los totales de sus hijos. Corresponde a lo que antes se llamaba capítulo o subcapítulo, y a cualquier nivel intermedio. Ejemplos: "02 — Cimentación", "02.01 — Excavaciones", "02.01.03 — Excavaciones manuales".
- **Terminal** (o *hoja*): nodo final. Tiene unidad de medida, cantidad, precio unitario y opcionalmente un APU. Es una actividad cuantificable. Ejemplo: "02.01.03.02 — Excavación manual en material común, H ≤ 2 m, m³".

### 2.2 Estructura del agregado `Presupuesto`

```
Presupuesto (Aggregate Root)
├── Id, ProyectoId, Nombre, Descripcion, Moneda, Estado, AIU, Version
├── ProfundidadMaxima: int?      (default 10, configurable por presupuesto; hard cap: 15)
└── Nodos: List<NodoPresupuestal>   (plano — árbol reconstruido por ParentId)

NodoPresupuestal (abstract)
├── Id: NodoId
├── ParentId: NodoId?             (null = hijo directo del presupuesto, raíz del árbol)
├── Nivel: int                     (0 = raíz, 1, 2, 3, ...)
├── Codigo: CodigoJerarquico      ("02", "02.01", "02.01.03.02")
├── Nombre: string
├── Orden: int                     (posición entre hermanos)
├── Tipo: TipoNodo  (Agrupador | Terminal)
├── Clasificaciones: List<Clasificacion>   (opcional, 0..N)
└── VinculoBIM: VinculoBIM?                (solo si Tipo = Terminal)

NodoAgrupador : NodoPresupuestal
└── Hijos: List<NodoPresupuestal>   (navegación — se deriva en runtime)

NodoTerminal : NodoPresupuestal
├── Unidad: UnidadMedida
├── Cantidad: Cantidad
├── PrecioUnitario: Dinero
├── APUReferencia: APUReferencia?
└── FuenteCantidad: FuenteCantidad (Manual | BIM | APU)
```

### 2.3 Reglas del árbol

- **Raíz virtual**: el presupuesto actúa como raíz; los nodos con `ParentId = null` son "capítulos de primer nivel".
- **Nivel**: el nivel del nodo es 0 si `ParentId = null`, sino `padre.Nivel + 1`. Se calcula y se guarda denormalizado para evitar recorridos.
- **Profundidad máxima**: configurable por presupuesto (default 10, tope rígido 15 para evitar abusos).
- **Los terminales son hojas**: un terminal no puede tener hijos. Agregar un hijo a un terminal requiere *convertirlo* en agrupador (evento `NodoConvertidoAAgrupador`) — operación que destruye la cantidad/precio porque pierden sentido.
- **Los agrupadores pueden tener hijos mixtos**: agrupadores o terminales, a diferentes niveles si se quiere (aunque no es recomendable mezclar).
- **Códigos jerárquicos autogenerados**: cuando se agrega un nodo, el sistema sugiere el siguiente código disponible (`"02.03" → "02.04"` o `"02.01.04.07"`). El usuario puede sobreescribirlo siempre que permanezca único y mantenga el prefijo del padre.
- **Mover un nodo (drag & drop)**: recodifica toda su subrama. Se emite `NodoMovido` con la lista de códigos afectados.
- **Orden entre hermanos**: importa (determina el código autogenerado y el orden de presentación). Se maneja con un `int Orden` que se renumera al insertar/mover.

### 2.4 Ejemplo de árbol de 5 niveles

```
Presupuesto "Torre Valencia - Etapa 1"
├─ 01 Preliminares                                         [Agrupador, nivel 0]
│   ├─ 01.01 Instalaciones provisionales                   [Agrupador, nivel 1]
│   │   └─ 01.01.02 Campamento administrativo              [Terminal, nivel 2] 180 m²
│   └─ 01.02 Replanteo y localización                      [Terminal, nivel 1] 2.400 m²
├─ 02 Cimentación                                          [Agrupador, nivel 0]
│   ├─ 02.01 Excavaciones                                  [Agrupador, nivel 1]
│   │   ├─ 02.01.01 Excavación mecánica                    [Agrupador, nivel 2]
│   │   │   ├─ 02.01.01.01 Material común H ≤ 2m          [Terminal, nivel 3] 1.200 m³
│   │   │   └─ 02.01.01.02 Material común H > 2m          [Terminal, nivel 3] 380 m³
│   │   └─ 02.01.02 Excavación manual                      [Terminal, nivel 2] 220 m³
│   └─ 02.02 Pilotes                                       [Agrupador, nivel 1]
│       └─ 02.02.01 Pilote preexcavado Ø0.50m              [Agrupador, nivel 2]
│           ├─ 02.02.01.01 Concreto 3000 psi               [Terminal, nivel 3] 45 m³
│           └─ 02.02.01.02 Acero refuerzo 60000 psi        [Terminal, nivel 3] 5.200 kg
```

5 niveles de profundidad (0 a 3) y nodos mixtos (terminales directos en nivel 1 conviviendo con agrupadores).

---

## 3. Integración BIM — diseño de alcance

### 3.1 División de responsabilidades

**BIM no es responsabilidad única del BC Presupuestación.** Se propone dividirlo:

- **BC Modelos BIM** (nuevo, fuera de este documento): gestiona el ciclo de vida de los modelos BIM — ingesta de archivos IFC/RVT, extracción de elementos con GUID, versionado, visualización. Cada elemento BIM tiene un `ElementoBIMId` estable por modelo + versión.
- **BC Presupuestación** (este documento): consume eventos del BC de Modelos BIM para *vincular* nodos terminales a elementos BIM y *recibir cantidades* reconciliadas automáticamente.

La separación respeta DDD: el BC BIM tiene su propio lenguaje ubicuo (elemento, familia, parámetro, categoría IFC) y sus propios usuarios (coordinadores BIM), distinto al lenguaje del presupuestador.

### 3.2 Conceptos BIM relevantes en Presupuestación

- **ModeloBIMRef**: puntero a un modelo versionado en el BC BIM — `{ modeloId, version, disciplina? }`.
- **ElementoBIMRef**: puntero a un elemento específico dentro de un modelo — `{ modeloId, version, elementoGuid }`. El `elementoGuid` corresponde al `IfcGloballyUniqueId` cuando viene de IFC.
- **VinculoBIM**: asociación entre un nodo terminal del presupuesto y uno o más `ElementoBIMRef`. Cuando la cantidad del nodo proviene del modelo, se registra la *regla de extracción* (ej: "suma de Volumen" o "conteo de instancias").
- **Clasificación**: código en un sistema estándar asignado al nodo. Permite que el BIM connector sepa qué elementos asociar automáticamente. Sistemas soportados en MVP:
    - **MasterFormat** (CSI — numérico 6 dígitos, ej. "03 30 00 - Cast-in-Place Concrete"). Dominante en Norteamérica.
    - **UniFormat** (CSI — elemental, ej. "A1010 Standard Foundations"). Usado para estimaciones tempranas.
    - **OmniClass** (tablas 11, 21, 22 ..., ej. "21-03 10 30 - Structural Framing"). Más completo pero menos adoptado.

### 3.3 Modelo del vínculo

```
VinculoBIM (value object en NodoTerminal)
├── Elementos: List<ElementoBIMRef>
├── ReglaExtraccion: ReglaExtraccion
│   ├── Tipo: SumaVolumen | SumaArea | SumaLongitud | Conteo | Parametro
│   └── NombreParametro: string?   (si Tipo = Parametro)
├── Factor: decimal                 (multiplicador — ej. 1.05 por desperdicio)
├── FechaVinculo: DateTimeOffset
└── EstadoSincronizacion: Sincronizado | Desfasado | Desvinculado

FuenteCantidad enum:
├── Manual    — valor digitado por el presupuestador
├── BIM       — valor obtenido del modelo BIM
└── APU       — valor derivado del APU (cantidad × rendimiento)
```

### 3.4 Ciclo de reconciliación

Cuando el BC BIM publica `ModeloBIMVersionPublicada` con cambios en cantidades, el BC Presupuestación:

1. Consulta qué nodos están vinculados a elementos del modelo afectado.
2. Para cada uno, solicita al BC BIM la cantidad recalculada.
3. Si el presupuesto está en **estado Borrador**: emite `CantidadReconciliadaDesdeBIM` directamente — actualiza el nodo, queda con `FuenteCantidad = BIM`.
4. Si el presupuesto está en **estado Aprobado**: **no** se modifica el nodo. Se emite un evento `DesviacionBIMDetectada` y se crea una propuesta de `Modificacion` (en estado Borrador) con las líneas correspondientes. El residente decide si la promueve a trámite.

Esto respeta la invariante del baseline inmutable.

### 3.5 Clasificaciones — asignación y uso

Un nodo puede tener 0 o más clasificaciones (por ejemplo MasterFormat + UniFormat simultáneamente). Las clasificaciones habilitan:

- **Matching automático** con elementos BIM que tengan el mismo código en su propiedad de clasificación.
- **Reportes cruzados** (costo por división UniFormat, comparación con benchmarks del sector).
- **Interoperabilidad** con otros sistemas del sector (cost managers externos, consolidación corporativa).

---

## 4. Invariantes actualizadas

Las invariantes del Presupuesto (INV-1 a INV-9 de v1.0) se mantienen, **con estas actualizaciones y adiciones**:

| # | Invariante | Cambio |
|---|---|---|
| **INV-2** | Códigos jerárquicos únicos | Se mantiene, pero los códigos son autogenerados en base al padre. |
| **INV-3** | Regla de anidamiento | Generalizada: un nodo pertenece a exactamente un padre (o al presupuesto como raíz). |
| **INV-4** | Ítem en capítulo o subcapítulo | Obsoleta — reemplazada por **INV-4'**: un nodo terminal no puede tener hijos. |
| **INV-10** *(nuevo)* | **Profundidad** | `Nivel ≤ ProfundidadMaxima` del presupuesto. |
| **INV-11** *(nuevo)* | **Tipo de nodo inmutable con contenido** | Un agrupador con hijos no puede convertirse en terminal; se requiere eliminar o mover los hijos primero. Un terminal con ejecución registrada (en BC Ejecución) no puede convertirse en agrupador — esto se valida asincrónicamente vía consulta al read model. |
| **INV-12** *(nuevo)* | **Cantidad BIM requiere vínculo** | Un nodo con `FuenteCantidad = BIM` debe tener `VinculoBIM ≠ null` y al menos un `ElementoBIMRef`. |
| **INV-13** *(nuevo)* | **Clasificaciones válidas** | Cada `Clasificacion.sistema` debe pertenecer al catálogo cerrado {MasterFormat, UniFormat, OmniClass}. Los códigos se validan contra el catálogo externo (asincrónico, con snapshot local). |
| **INV-14** *(nuevo)* | **Reubicar no cruza tipo de padre** | Al mover un nodo, el nuevo padre debe ser un agrupador (o el presupuesto-raíz). |
| **INV-15** *(nuevo)* | **Vínculo BIM solo en terminales** | No se permite vincular un agrupador a un elemento BIM directamente. Los agrupadores heredan cantidades agregadas desde sus descendientes. |

---

## 5. Value objects nuevos y modificados

Se agregan al `SharedKernel` o al `Domain` del BC:

```csharp
public readonly record struct NodoId(Guid Value);

public enum TipoNodo { Agrupador = 1, Terminal = 2 }

public enum FuenteCantidad { Manual = 1, BIM = 2, APU = 3 }

public sealed record Clasificacion(
    SistemaClasificacion Sistema,
    string Codigo,
    string Descripcion);

public enum SistemaClasificacion
{
    MasterFormat = 1,
    UniFormat = 2,
    OmniClass = 3
}

public sealed record ModeloBIMRef(Guid ModeloId, int Version);

public sealed record ElementoBIMRef(
    Guid ModeloId,
    int Version,
    string ElementoGuid);   // IfcGloballyUniqueId o equivalente

public sealed record ReglaExtraccion(
    TipoReglaExtraccion Tipo,
    string? NombreParametro);

public enum TipoReglaExtraccion
{
    SumaVolumen = 1,
    SumaArea = 2,
    SumaLongitud = 3,
    Conteo = 4,
    Parametro = 5
}

public sealed record VinculoBIM(
    IReadOnlyList<ElementoBIMRef> Elementos,
    ReglaExtraccion ReglaExtraccion,
    decimal Factor,
    DateTimeOffset FechaVinculo,
    EstadoSincronizacion Estado);

public enum EstadoSincronizacion
{
    Sincronizado = 1,
    Desfasado = 2,
    Desvinculado = 3
}
```

El `CodigoJerarquico` de v1.0 se generaliza: regex `^\d{2}(\.\d{2}){0,14}$` (hasta 15 niveles), con validación adicional de continuidad (cada nodo hereda el prefijo del padre).

---

## 6. Catálogo de eventos — delta v1.0 → v1.1

### 6.1 Eventos **eliminados** (reemplazados por eventos de `Nodo`)

| Evento v1.0 | Reemplazo v1.1 |
|---|---|
| `CapituloAgregado` | `NodoAgrupadorAgregado` con `ParentId = null` |
| `CapituloRenombrado` | `NodoRenombrado` |
| `CapituloEliminado` | `NodoEliminado` |
| `SubcapituloAgregado` | `NodoAgrupadorAgregado` con `ParentId != null` |
| `SubcapituloRenombrado` | `NodoRenombrado` |
| `SubcapituloEliminado` | `NodoEliminado` |
| `ItemAgregado` | `NodoTerminalAgregado` |
| `ItemCantidadAjustada` | `NodoCantidadAjustada` |
| `ItemPrecioUnitarioActualizado` | `NodoPrecioUnitarioActualizado` |
| `ItemRenombrado` | `NodoRenombrado` |
| `ItemReubicado` | `NodoMovido` |
| `ItemEliminado` | `NodoEliminado` |

### 6.2 Eventos **nuevos**

Multinivel / estructura:

- `NodoAgrupadorAgregado`
- `NodoTerminalAgregado`
- `NodoRenombrado`
- `NodoMovido`
- `NodoEliminado`
- `NodoConvertidoAAgrupador` — un terminal pasa a ser agrupador (limpia cantidad/precio)
- `NodosReordenados` — reordenamiento entre hermanos (un solo evento por lote)
- `NodoCantidadAjustada`
- `NodoPrecioUnitarioActualizado`
- `NodoUnidadMedidaCambiada`

Clasificaciones:

- `ClasificacionAsignada`
- `ClasificacionRemovida`

Vínculo BIM:

- `VinculoBIMEstablecido`
- `VinculoBIMActualizado`
- `VinculoBIMRemovido`
- `CantidadImportadaDesdeBIM` (primera cuantificación)
- `CantidadReconciliadaDesdeBIM` (actualizaciones subsiguientes en borrador)
- `DesviacionBIMDetectada` (reconciliación bloqueada por estado Aprobado; genera propuesta de modificación)
- `FuenteCantidadCambiada` (usuario cambia entre Manual / BIM / APU para un nodo)

### 6.3 Nueva tabla resumen (reemplaza §6.20 de v1.0)

| # | Evento | Agregado | Estado req. | Integration |
|---|---|---|---|---|
| 1 | `PresupuestoBorradorCreado` | Presupuesto | - | Sí |
| 2 | `PresupuestoRenombrado` | Presupuesto | Borrador/EnAprobacion | No |
| 3 | `PresupuestoDescripcionActualizada` | Presupuesto | Borrador/EnAprobacion | No |
| 4 | `ProfundidadMaximaConfigurada` | Presupuesto | Borrador | No |
| 5 | `NodoAgrupadorAgregado` | Presupuesto | Borrador | No |
| 6 | `NodoTerminalAgregado` | Presupuesto | Borrador o via modificación | Sí |
| 7 | `NodoRenombrado` | Presupuesto | Borrador | No |
| 8 | `NodoMovido` | Presupuesto | Borrador | No |
| 9 | `NodoEliminado` | Presupuesto | Borrador o via modificación | Sí |
| 10 | `NodoConvertidoAAgrupador` | Presupuesto | Borrador | No |
| 11 | `NodosReordenados` | Presupuesto | Borrador | No |
| 12 | `NodoCantidadAjustada` | Presupuesto | Borrador o via modificación | Sí |
| 13 | `NodoPrecioUnitarioActualizado` | Presupuesto | Borrador o via modificación | Sí |
| 14 | `NodoUnidadMedidaCambiada` | Presupuesto | Borrador | No |
| 15 | `ClasificacionAsignada` | Presupuesto | Borrador | No |
| 16 | `ClasificacionRemovida` | Presupuesto | Borrador | No |
| 17 | `VinculoBIMEstablecido` | Presupuesto | Borrador | Sí |
| 18 | `VinculoBIMActualizado` | Presupuesto | Borrador | No |
| 19 | `VinculoBIMRemovido` | Presupuesto | Borrador | Sí |
| 20 | `CantidadImportadaDesdeBIM` | Presupuesto | Borrador | Sí |
| 21 | `CantidadReconciliadaDesdeBIM` | Presupuesto | Borrador | Sí |
| 22 | `DesviacionBIMDetectada` | Presupuesto | Aprobado | Sí |
| 23 | `FuenteCantidadCambiada` | Presupuesto | Borrador | No |
| 24 | `AIUConfigurado` | Presupuesto | Borrador | No |
| 25 | `AIUAjustado` | Presupuesto | Borrador | No |
| 26 | `PresupuestoSometidoAAprobacion` | Presupuesto | Borrador | Sí |
| 27 | `PresupuestoAprobado` | Presupuesto | EnAprobacion | **Sí (crítico)** |
| 28 | `PresupuestoRechazado` | Presupuesto | EnAprobacion | Sí |
| 29 | `PresupuestoCerrado` | Presupuesto | Aprobado | Sí |
| 30 | `PresupuestoArchivado` | Presupuesto | Cerrado | Sí |
| 31 | `ModificacionBorradorCreada` | Modificacion | - | No |
| 32 | `LineaModificacionAgregada` | Modificacion | Borrador | No |
| 33 | `LineaModificacionEliminada` | Modificacion | Borrador | No |
| 34 | `ModificacionSometidaAAprobacion` | Modificacion | Borrador | Sí |
| 35 | `ModificacionAprobada` | Modificacion | EnAprobacion | **Sí (crítico)** |
| 36 | `ModificacionRechazada` | Modificacion | EnAprobacion | Sí |
| 37 | `ModificacionAplicada` | Modificacion | Aprobada | Sí |

De 29 eventos en v1.0 se pasa a 37 en v1.1. La granularidad fina se mantiene.

---

## 7. JSON Schemas de eventos nuevos

### 7.1 `NodoAgrupadorAgregado`

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "presupuestacion/v1/nodo-agrupador-agregado.schema.json",
  "title": "NodoAgrupadorAgregado",
  "type": "object",
  "required": ["presupuestoId", "nodoId", "codigo", "nombre", "nivel", "orden"],
  "properties": {
    "presupuestoId": { "type": "string", "format": "uuid" },
    "nodoId": { "type": "string", "format": "uuid" },
    "parentId": { "type": ["string", "null"], "format": "uuid" },
    "codigo": { "type": "string", "pattern": "^\\d{2}(\\.\\d{2}){0,14}$" },
    "nombre": { "type": "string", "minLength": 1, "maxLength": 300 },
    "nivel": { "type": "integer", "minimum": 0, "maximum": 14 },
    "orden": { "type": "integer", "minimum": 0 }
  }
}
```

### 7.2 `NodoTerminalAgregado`

```json
{
  "$id": "presupuestacion/v1/nodo-terminal-agregado.schema.json",
  "type": "object",
  "required": [
    "presupuestoId", "nodoId", "codigo", "nombre",
    "nivel", "orden", "unidad", "cantidad", "precioUnitario", "fuenteCantidad"
  ],
  "properties": {
    "presupuestoId": { "type": "string", "format": "uuid" },
    "nodoId": { "type": "string", "format": "uuid" },
    "parentId": { "type": ["string", "null"], "format": "uuid" },
    "codigo": { "type": "string", "pattern": "^\\d{2}(\\.\\d{2}){0,14}$" },
    "nombre": { "type": "string", "minLength": 1, "maxLength": 300 },
    "nivel": { "type": "integer", "minimum": 0, "maximum": 14 },
    "orden": { "type": "integer", "minimum": 0 },
    "unidad": { "type": "string" },
    "cantidad": { "type": "string", "pattern": "^\\d+(\\.\\d{1,4})?$" },
    "precioUnitario": { "type": "string", "pattern": "^\\d+(\\.\\d{1,4})?$" },
    "fuenteCantidad": { "type": "string", "enum": ["Manual", "BIM", "APU"] },
    "apuReferencia": {
      "type": ["object", "null"],
      "properties": {
        "catalogoApuId": { "type": "string", "format": "uuid" },
        "version": { "type": "integer", "minimum": 1 }
      }
    }
  }
}
```

### 7.3 `NodoMovido`

Cambio de padre del nodo (drag & drop). El evento incluye los códigos nuevos de toda la subrama.

```json
{
  "$id": "presupuestacion/v1/nodo-movido.schema.json",
  "type": "object",
  "required": ["presupuestoId", "nodoId", "parentAnteriorId", "parentNuevoId", "codigosRecodificados"],
  "properties": {
    "presupuestoId": { "type": "string", "format": "uuid" },
    "nodoId": { "type": "string", "format": "uuid" },
    "parentAnteriorId": { "type": ["string", "null"], "format": "uuid" },
    "parentNuevoId": { "type": ["string", "null"], "format": "uuid" },
    "ordenNuevo": { "type": "integer", "minimum": 0 },
    "codigosRecodificados": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["nodoId", "codigoAnterior", "codigoNuevo", "nivelAnterior", "nivelNuevo"],
        "properties": {
          "nodoId": { "type": "string", "format": "uuid" },
          "codigoAnterior": { "type": "string" },
          "codigoNuevo": { "type": "string" },
          "nivelAnterior": { "type": "integer" },
          "nivelNuevo": { "type": "integer" }
        }
      }
    }
  }
}
```

### 7.4 `ClasificacionAsignada`

```json
{
  "$id": "presupuestacion/v1/clasificacion-asignada.schema.json",
  "type": "object",
  "required": ["presupuestoId", "nodoId", "sistema", "codigo"],
  "properties": {
    "presupuestoId": { "type": "string", "format": "uuid" },
    "nodoId": { "type": "string", "format": "uuid" },
    "sistema": { "type": "string", "enum": ["MasterFormat", "UniFormat", "OmniClass"] },
    "codigo": { "type": "string", "minLength": 1, "maxLength": 50 },
    "descripcion": { "type": "string", "maxLength": 300 }
  }
}
```

### 7.5 `VinculoBIMEstablecido`

```json
{
  "$id": "presupuestacion/v1/vinculo-bim-establecido.schema.json",
  "type": "object",
  "required": ["presupuestoId", "nodoId", "elementos", "reglaExtraccion", "factor"],
  "properties": {
    "presupuestoId": { "type": "string", "format": "uuid" },
    "nodoId": { "type": "string", "format": "uuid" },
    "elementos": {
      "type": "array",
      "minItems": 1,
      "items": {
        "type": "object",
        "required": ["modeloId", "version", "elementoGuid"],
        "properties": {
          "modeloId": { "type": "string", "format": "uuid" },
          "version": { "type": "integer", "minimum": 1 },
          "elementoGuid": { "type": "string", "minLength": 1, "maxLength": 64 }
        }
      }
    },
    "reglaExtraccion": {
      "type": "object",
      "required": ["tipo"],
      "properties": {
        "tipo": {
          "type": "string",
          "enum": ["SumaVolumen", "SumaArea", "SumaLongitud", "Conteo", "Parametro"]
        },
        "nombreParametro": { "type": ["string", "null"] }
      }
    },
    "factor": { "type": "string", "pattern": "^\\d+(\\.\\d{1,4})?$" }
  }
}
```

### 7.6 `CantidadImportadaDesdeBIM`

```json
{
  "$id": "presupuestacion/v1/cantidad-importada-desde-bim.schema.json",
  "type": "object",
  "required": ["presupuestoId", "nodoId", "cantidadAnterior", "cantidadNueva", "modeloVersion"],
  "properties": {
    "presupuestoId": { "type": "string", "format": "uuid" },
    "nodoId": { "type": "string", "format": "uuid" },
    "cantidadAnterior": { "type": "string" },
    "cantidadNueva": { "type": "string", "pattern": "^\\d+(\\.\\d{1,4})?$" },
    "modeloId": { "type": "string", "format": "uuid" },
    "modeloVersion": { "type": "integer", "minimum": 1 },
    "detalleExtraccion": {
      "type": "object",
      "properties": {
        "elementosProcesados": { "type": "integer" },
        "valorBase": { "type": "string" },
        "factorAplicado": { "type": "string" }
      }
    }
  }
}
```

### 7.7 `DesviacionBIMDetectada`

```json
{
  "$id": "presupuestacion/v1/desviacion-bim-detectada.schema.json",
  "type": "object",
  "required": [
    "presupuestoId", "nodoId", "cantidadBaseline",
    "cantidadBIMNueva", "modeloVersion", "modificacionSugeridaId"
  ],
  "properties": {
    "presupuestoId": { "type": "string", "format": "uuid" },
    "nodoId": { "type": "string", "format": "uuid" },
    "cantidadBaseline": { "type": "string" },
    "cantidadBIMNueva": { "type": "string" },
    "diferenciaPorcentual": { "type": "string" },
    "modeloId": { "type": "string", "format": "uuid" },
    "modeloVersion": { "type": "integer", "minimum": 1 },
    "modificacionSugeridaId": { "type": "string", "format": "uuid" }
  }
}
```

---

## 8. Flujos end-to-end nuevos

### 8.1 Flujo A — Construir un árbol de 5 niveles

```
1. Usuario crea Presupuesto en borrador (PresupuestoBorradorCreado).
2. Agrega "02 Cimentación" en raíz (parentId = null, nivel 0).
   → NodoAgrupadorAgregado { codigo: "02", nivel: 0 }
3. Bajo "02", agrega "02.01 Excavaciones" (parent = id de "02").
   → NodoAgrupadorAgregado { codigo: "02.01", nivel: 1 }
4. Bajo "02.01" agrega "02.01.01 Excavación mecánica".
   → NodoAgrupadorAgregado { codigo: "02.01.01", nivel: 2 }
5. Bajo "02.01.01" agrega "02.01.01.01 Material común H ≤ 2m" como terminal,
   cantidad 1200 m³, precio 45.000.
   → NodoTerminalAgregado { nivel: 3, cantidad: "1200", ... }
6. Repite con "02.01.01.02" en nivel 3.
```

El frontend representa esto como árbol expandible. React Query cache la proyección completa y SignalR empuja cambios en vivo cuando otro usuario edita la misma rama.

### 8.2 Flujo B — Vincular un nodo a BIM y cuantificar

```
1. Coordinador BIM publica "Modelo estructural v3" en el BC BIM.
   → BC BIM emite ModeloBIMVersionPublicada.
2. En Presupuestación, el presupuestador navega al nodo terminal
   "02.02.01.01 Concreto 3000 psi" y hace clic en "Vincular a BIM".
3. UI consulta al BC BIM elementos candidatos (filtrados por clasificación
   UniFormat "A1020 Special Foundations" si ya está asignada).
4. Usuario selecciona 24 elementos tipo IfcPile y configura regla:
   "Suma de Volumen" × factor 1.05 (5 % desperdicio).
5. Comando ConfigurarVinculoBIM:
   → VinculoBIMEstablecido { elementos: [...], reglaExtraccion, factor: 1.05 }
6. Handler invoca sincrónicamente al BC BIM para extraer la suma de volumen.
   Respuesta: 42.85 m³.
7. → CantidadImportadaDesdeBIM { cantidadAnterior: "0", cantidadNueva: "45.0000" (42.85 × 1.05) }
8. FuenteCantidadCambiada { nueva: "BIM" } (emitido implícitamente).
9. Proyección actualiza el nodo; UI muestra badge "BIM" al lado de la cantidad.
```

### 8.3 Flujo C — Reconciliación tras nueva versión de modelo (presupuesto en borrador)

```
1. BC BIM publica "Modelo estructural v4" (cambió geometría de los pilotes).
   → ModeloBIMVersionPublicada.
2. Proyector en Presupuestación detecta nodos vinculados al modelo
   afectado; por cada uno programa una tarea de reconciliación (Wolverine
   durable queue).
3. Para el nodo pilotes: consulta nueva cantidad al BC BIM. Resultado: 47.20 m³ × 1.05 = 49.56 m³.
4. → CantidadReconciliadaDesdeBIM { cantidadAnterior: "45.0000", cantidadNueva: "49.5600" }
5. UI muestra notificación "El modelo BIM cambió y se actualizaron 12 cantidades".
```

### 8.4 Flujo D — Reconciliación con presupuesto aprobado

```
1. Igual que 8.3 pasos 1-2, pero el presupuesto ya está en estado Aprobado.
2. El handler de reconciliación verifica estado: no puede modificar el nodo
   directamente. En su lugar:
   a. Busca si existe una Modificacion en borrador del mismo tipo (auto-BIM).
      Si no existe, la crea vía comando interno.
   b. Agrega una línea a esa Modificacion: LineaModificacionAgregada
      { tipo: AjustarCantidad, itemAfectadoId: pilotes.nodoId,
        valorAnterior: "45", valorNuevo: "49.56" }.
   c. Emite sobre el Presupuesto el evento informativo
      DesviacionBIMDetectada { ..., modificacionSugeridaId: modId }.
3. Frontend muestra una alerta al residente:
   "El modelo BIM v4 introduce 12 desviaciones — hay una modificación sugerida pendiente".
4. El residente revisa, edita concepto, agrega soporte, somete a aprobación.
   El workflow sigue el camino estándar de modificaciones.
```

Este comportamiento asegura que la baseline no se toca sin autorización, pero el sistema evita que las desviaciones pasen desapercibidas.

---

## 9. Impacto en servicios y estructura .NET

### 9.1 Servicios afectados

**Command handlers y dominio** (`Presupuestacion.Domain` y `Application`):

- `Presupuesto` adquiere métodos nuevos: `AgregarNodoAgrupador`, `AgregarNodoTerminal`, `MoverNodo`, `EliminarNodo`, `ConvertirAAgrupador`, `AsignarClasificacion`, `EstablecerVinculoBIM`, `ReconciliarCantidadBIM`.
- Se introduce un servicio de dominio `GeneradorCodigosJerarquicos` para computar códigos nuevos y rehacer códigos al mover/reordenar.
- El recálculo de totales se vuelve recursivo por niveles — se prueba con tests que cubran árboles anchos (100 hermanos) y profundos (15 niveles).

**Proyecciones**:

- `PresupuestoDetalleReadModel` cambia estructura: ahora guarda los nodos como lista plana con `parentId` + `ordenDentroDelPadre`. El frontend reconstruye el árbol.
- Nueva proyección `NodoBIMVinculosReadModel` — índice de nodos por `modeloId` para reconciliación rápida.
- Nueva proyección `NodosPorClasificacionReadModel` — para reportes cruzados por MasterFormat/UniFormat.

**Process managers / sagas**:

- Nuevo saga `ReconciliarCantidadesBIM` suscrito a `ModeloBIMVersionPublicada` del BC BIM. Despacha reconciliaciones por lote usando Wolverine durable queue.

### 9.2 Nuevo componente en la solución

```
src/Modulos/Presupuestacion/
├── SincoPresupuesto.Presupuestacion.BIMConnector/    [NUEVO]
│   ├── Consumers/                      [handlers de eventos del BC BIM]
│   ├── Clients/                        [cliente HTTP al BC BIM]
│   ├── Reconciliation/                 [saga de reconciliación]
│   └── Clasificacion/                  [validadores de códigos MasterFormat/etc.]
└── ...
```

El `BIMConnector` es la *capa anticorrupción* (ACL en términos DDD) entre el lenguaje del BC BIM y el lenguaje del BC Presupuestación. Traduce `ElementoIFC` ↔ `ElementoBIMRef`, `Volumen medido` ↔ `Cantidad con factor`.

### 9.3 Esquemas de Postgres / Marten — tablas nuevas

No se crean tablas nuevas manuales: Marten genera tablas de documentos para cada proyección. Las tablas relevantes que aparecerán son:

- `mt_doc_presupuestodetalle` (modificada: esquema de nodos más rico)
- `mt_doc_nodobimvinculos` (nueva)
- `mt_doc_nodosporclasificacion` (nueva)

Se recomienda agregar índices sobre:

- `(modeloId, version)` en `nodobimvinculos` para reconciliación.
- `(sistema, codigo)` en `nodosporclasificacion` para reportes.

---

## 10. Preguntas abiertas

Antes de congelar esta versión se sugiere resolver con el Product Owner:

1. **Profundidad máxima**: ¿dejamos 15 como tope rígido o lo hacemos ilimitado? Mi recomendación: 15 es más que suficiente y protege contra errores humanos.
2. **Sistemas de clasificación**: ¿MVP con los tres (MasterFormat + UniFormat + OmniClass) o empezar con solo uno? Recomiendo **UniFormat** en MVP por ser el más práctico para presupuestos tempranos.
3. **BC BIM existe o hay que construirlo?** Si no existe, hay dos opciones: (a) construirlo en paralelo como BC hermano, (b) modelar un *mini-BIM* dentro de Presupuestación como primer paso y separarlo después. Recomiendo (a) desde el inicio si el roadmap lo permite — la separación clara paga a mediano plazo.
4. **Formato BIM soportado en MVP**: ¿solo IFC (abierto, estándar), o también Revit (.rvt)? IFC es más simple de parsear y universal; Revit requiere Forge/APS o plugin nativo.
5. **Reconciliación automática vs manual**: ¿el sistema aplica cambios del modelo en borrador sin preguntar, o siempre pide confirmación? Propuesta: automático en borrador con opción de "deshacer", manual (vía modificación) en aprobado.
6. **Múltiples vínculos por nodo**: ¿un nodo terminal puede vincular elementos de *distintos modelos BIM* (estructural + arquitectónico + MEP)? Propuesta: sí, pero la regla de extracción debe ser la misma.
7. **Conversión terminal ↔ agrupador**: ¿permitido en borrador siempre, o bloqueado si el nodo ya fue usado en algún otro BC (ej. un otrosí menciona ese nodo)? Propuesta: bloqueado si tiene ejecución registrada.

---

*Fin del addendum v1.1.*
