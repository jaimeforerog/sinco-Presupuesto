# Bounded Context: Presupuestación — Diseño detallado

**Proyecto:** Sinco Presupuesto
**Stack:** .NET 9 + Marten + Wolverine + PostgreSQL + React
**Fecha:** 23 de abril de 2026
**Versión:** 2.1 (v2.0 + estrategia Tipo B: baseline inmutable + modificaciones como deltas que se proyectan sobre el baseline para componer el vigente)

---

## Índice

1. [Alcance y fronteras del contexto](#1-alcance-y-fronteras-del-contexto)
2. [Prerequisitos](#2-prerequisitos)
3. [Análisis de granularidad (3 dimensiones)](#3-análisis-de-granularidad-3-dimensiones)
4. [Modelo de dominio](#4-modelo-de-dominio)
5. [Estrategia de baselining — Tipo B con deltas](#5-estrategia-de-baselining--tipo-b-con-deltas)
6. [Integración BIM — diseño de alcance](#6-integración-bim--diseño-de-alcance)
7. [Convenciones de eventos](#7-convenciones-de-eventos)
8. [Catálogo completo de eventos (JSON Schema)](#8-catálogo-completo-de-eventos-json-schema)
9. [Servicios necesarios](#9-servicios-necesarios)
10. [Estructura de solución .NET](#10-estructura-de-solución-net)
11. [Flujos end-to-end de ejemplo](#11-flujos-end-to-end-de-ejemplo)
12. [Checklist de implementación](#12-checklist-de-implementación)
13. [Preguntas abiertas](#13-preguntas-abiertas)

---

## 1. Alcance y fronteras del contexto

### 1.1 Qué hace el contexto Presupuestación

Este *bounded context* es responsable de todo el ciclo de vida del presupuesto de una obra, desde su creación en borrador hasta su cierre, pasando por la aprobación (línea base) y las modificaciones autorizadas (otrosíes, adicionales, reformas).

Responsabilidades propias:

- Construir la estructura jerárquica del presupuesto como un **árbol n-ario** de profundidad arbitraria (nodos agrupadores y nodos terminales).
- Asociar a cada nodo terminal unidad de medida, cantidad, precio unitario y opcionalmente un APU.
- Configurar el AIU (Administración, Imprevistos, Utilidad).
- Gestionar el flujo de aprobación (workflow).
- **Congelar el baseline** (presupuesto base) tras la aprobación: el stream del Presupuesto no admite más cambios estructurales.
- **Registrar y aprobar modificaciones como deltas** (otrosíes, adicionales, reformas) que viven en sus propios streams y se componen sobre el baseline para calcular el presupuesto vigente.
- **Asignar clasificaciones estándar** (MasterFormat, UniFormat, OmniClass) a los nodos.
- **Vincular nodos terminales a elementos del modelo BIM** y recibir cantidades reconciliadas automáticamente desde el BC BIM.
- Mantener trazabilidad completa e inmutable de todos los cambios.
- Calcular en tiempo real los tres estados: **Base** (inmutable tras aprobación), **Vigente** (Base + Σ modificaciones aprobadas y aplicadas) y **Acumulado de modificaciones** (solo los deltas).
- Emitir eventos de integración para que otros contextos se enteren.

### 1.2 Qué NO hace (responsabilidad de otros contextos)

- **Catálogo de insumos y APUs de referencia** → BC Catálogo. Presupuestación *usa* APUs pero no gestiona el maestro.
- **Gestión de modelos BIM** (ingesta de IFC/RVT, versionado, visualización) → BC Modelos BIM. Presupuestación consume sus eventos y consulta sus datos.
- **Captura de ejecución real** → BC Ejecución. Presupuestación recibe eventos de ejecución para proyectar curvas S pero no captura consumos.
- **Facturación, compras, tesorería, nómina** → BCs diferentes. Integración vía eventos.
- **Cronograma de obra** → BC Planeación.
- **Autenticación y autorización** → responsabilidad transversal (identity provider).

### 1.3 Lenguaje ubicuo (glosario)

| Término | Definición |
|---|---|
| **Presupuesto** | Documento estructurado que cuantifica el costo total de una obra. Agregado raíz de este BC. |
| **Nodo** | Elemento de la estructura del presupuesto. Puede ser agrupador o terminal. |
| **Nodo agrupador** | Nodo intermedio sin cantidad propia; su total es la suma de sus hijos. Equivale a los antiguos "capítulo", "subcapítulo" y cualquier nivel intermedio. |
| **Nodo terminal** (hoja) | Nodo final con unidad de medida, cantidad, precio unitario y opcional APU. Es una actividad cuantificable. |
| **Código jerárquico** | Identificador humano de un nodo (ej. `02.01.03.02`) que refleja su posición en el árbol. |
| **APU** | Análisis de Precio Unitario; descomposición del costo unitario en insumos (materiales, mano de obra, equipo, transporte). |
| **Insumo** | Elemento de entrada a un APU. |
| **AIU** | Administración, Imprevistos, Utilidad — sobrecostos aplicados al costo directo. |
| **Línea base / Baseline / Presupuesto Base** | Versión del presupuesto congelada al momento de aprobación, **inmutable**. No admite más cambios estructurales en su stream. Es la referencia contractual. |
| **Presupuesto Vigente** | Proyección computada = Base + Σ deltas de modificaciones aprobadas y aplicadas. Refleja el estado actual "efectivo" del presupuesto. |
| **Acumulado de modificaciones** | Suma de todos los deltas (por nodo, por capítulo y global) producidos por modificaciones aplicadas. Permite responder "¿cuánto han sumado los otrosíes?" en una consulta directa. |
| **Modificación** | Cambio autorizado al presupuesto aprobado (otrosí, adicional, reforma, reconciliación BIM). Vive en su propio agregado y sus líneas son **deltas**. |
| **Delta** | Variación relativa respecto al baseline: `+70 m³`, `−15 m³`, `+1.500 COP/m³`, o la suma del total de un nodo nuevo agregado por modificación. |
| **Corte** | Período de medición (semanal/quincenal/mensual) para reportar ejecución. |
| **Costo directo** | Suma de (cantidad × precio unitario) de todos los nodos terminales. |
| **Costo total** | Costo directo × (1 + %A + %I + %U). |
| **Clasificación** | Código en un sistema estándar (MasterFormat, UniFormat, OmniClass) asignado a un nodo. |
| **MasterFormat / UniFormat / OmniClass** | Sistemas de clasificación de partidas de construcción — estándares CSI e ISO ampliamente usados para interoperabilidad. |
| **BIM** | *Building Information Modeling* — metodología de diseño basada en modelos 3D parametrizados. |
| **Elemento BIM** | Objeto dentro de un modelo (muro, pilote, losa) identificado por un GUID estable (p.ej. `IfcGloballyUniqueId`). |
| **IFC** | *Industry Foundation Classes* — formato abierto estándar (ISO 16739) para intercambio de modelos BIM. |
| **Vínculo BIM** | Asociación entre un nodo terminal y uno o más elementos BIM, con una regla de extracción de cantidades. |
| **Fuente de cantidad** | Origen del valor de la cantidad de un terminal: Manual, BIM o APU. |

---

## 2. Prerequisitos

### 2.1 Prerequisitos de negocio

Antes de escribir código el equipo debe cerrar con el Product Owner o gerente de obra:

- **Glosario** validado con ejemplos reales (el de la sección 1.3).
- **Profundidad máxima del árbol**: default 10, tope rígido 15. Configurable por presupuesto.
- **Formato de códigos jerárquicos**: patrón `dd(\.dd)*` con validación de continuidad del prefijo del padre.
- **Política de autogeneración de códigos**: el sistema sugiere el siguiente disponible; el usuario puede sobrescribir siempre que mantenga unicidad y prefijo.
- **Unidades de medida aceptadas**: catálogo cerrado (m², m³, ml, kg, und, gl, hr, día, mes) o extendido; decisión pendiente.
- **Política de monedas**: un presupuesto = una moneda en MVP.
- **Política de aprobación**: en MVP dos niveles (residente/director → gerente).
- **Política de modificaciones**: niveles de aprobación; si permite ejecutar antes de aprobar (no).
- **Redondeos y precisión**: decimales en precios (4), cantidades (4), totales (2); uso obligatorio de `decimal` en C#.
- **IVA**: fuera del presupuesto base en MVP.
- **AIU global** en MVP (no por capítulo).
- **Sistemas de clasificación**: cuáles soportar en MVP (recomendación: UniFormat).
- **Formato BIM**: IFC obligatorio en MVP; Revit opcional vía conversión a IFC.
- **Política de reconciliación BIM**: automática en borrador, vía modificación en aprobado.

### 2.2 Prerequisitos técnicos

**Entorno**: .NET 9 SDK (o .NET 10 cuando esté disponible), PostgreSQL 16+, Docker Compose, Node 20+, pnpm, IDE (Rider o Visual Studio 2022).

**NuGet principales**:

| Paquete | Versión aprox. 2026 | Propósito |
|---|---|---|
| `Marten` | 7.x | Event Store + Document DB sobre Postgres |
| `WolverineFx` | 3.x | Mediator + Message Bus + Outbox |
| `WolverineFx.Http` | 3.x | Endpoints HTTP sobre comandos |
| `WolverineFx.Marten` | 3.x | Integración Marten ↔ Wolverine (outbox transaccional) |
| `Microsoft.AspNetCore.OpenApi` | 9.x | OpenAPI nativo |
| `Serilog.AspNetCore` | 8.x | Logging estructurado |
| `OpenTelemetry.Extensions.Hosting` | 1.x | Tracing distribuido |
| `FluentValidation` | 11.x | Validación de comandos |
| `JsonSchema.Net` | latest | Validación contra JSON Schemas |

**npm principales (React)**: `react 19`, `vite`, `typescript`, `@tanstack/react-query`, `ag-grid-react` o `@tanstack/react-table`, `zustand`, `react-hook-form` + `zod`, `tailwindcss` + `shadcn/ui`, `@microsoft/signalr`.

**Infraestructura mínima**: PostgreSQL con backups y *point-in-time recovery* habilitado, Seq o Grafana Loki, OpenTelemetry Collector + Jaeger, ingress con TLS, almacenamiento S3-compatible (MinIO en local) para adjuntos.

### 2.3 Prerequisitos organizacionales

- **Product Owner accesible**, con conocimiento real de obra.
- **Equipo con familiaridad en DDD**; al menos un senior con experiencia en Event Sourcing.
- **Convenciones**: nombres de dominio en español, tecnicismos en inglés.
- **Event Storming**: sesión mínima de 1 día antes de cerrar el catálogo de eventos.
- **ADRs** para cada decisión significativa (stack, granularidad, estrategia de versionado, BIM).

### 2.4 Prerequisitos de datos

- **Catálogo de unidades de medida** (seed).
- **Monedas ISO-4217** (COP por defecto).
- **Usuarios y roles** (desde BC Identidad).
- **Proyectos** (desde BC Portafolio).
- **Catálogo de clasificaciones**: MasterFormat (CSI), UniFormat (CSI) y/o OmniClass con sus códigos y descripciones. Puede mantenerse localmente con snapshot del catálogo oficial.
- **Acceso al BC Modelos BIM**: endpoint o contrato de eventos para consultar modelos y extraer cantidades.

---

## 3. Análisis de granularidad (3 dimensiones)

### 3.1 Granularidad de agregados

**Dilema**: ¿un agregado `Presupuesto` que contiene toda la jerarquía, o varios agregados más pequeños (`Nodo` por separado)?

| Opción | Pros | Contras |
|---|---|---|
| **A. Un solo agregado** (Presupuesto contiene todos los nodos) | Invariantes transaccionales fuertes (unicidad de códigos, totales consistentes). Simplicidad de razonamiento. | Streams largos; contención en edición concurrente. Requiere snapshots. |
| **B. Varios agregados** (cada Nodo como raíz) | Streams cortos; sin contención. | Rompe invariantes fuertes (unicidad, jerarquía); consistencia eventual dentro del BC; mucha complejidad extra. |
| **C. Presupuesto como raíz + Modificación como agregado lateral** | Invariantes preservadas dentro del Presupuesto; Modificaciones con su propio workflow; streams manejables con snapshots. | Requiere un *process manager* que aplique `Modificacion → Presupuesto`. |

**Decisión: Opción C.**

Durante la elaboración el Presupuesto se edita por pocas personas en pocas semanas; las invariantes son fuertes. Con snapshots cada 100 eventos y el daemon async de Marten, la carga se mantiene en milisegundos incluso con 2.000 eventos. Las modificaciones son workflows independientes, ameritan su propio agregado.

### 3.2 Granularidad de eventos

**Dilema**: ¿eventos finos (`NodoCantidadAjustada`) o gruesos (`NodoModificado` con diff)?

**Decisión: granularidad fina**, alineada a la intención del usuario. Un evento = una decisión de negocio. Esto preserva la semántica de auditoría, simplifica proyecciones y permite integración granular con otros BCs.

**Operaciones en lote**: cuando el usuario importa un catálogo de 500 nodos, se emiten 500 `NodoTerminalAgregado` con el mismo `correlationId` apuntando a un `ImportacionIniciada`, no un evento agregado.

### 3.3 Granularidad de servicios

**Decisión: monolito modular con fronteras estrictas**, preparado para extraer microservicios cuando la carga lo justifique.

Wolverine + Marten sobre PostgreSQL cubren eventos, mensajería y persistencia sin broker externo. Las fronteras entre BCs se garantizan a nivel de código (namespaces, folders, `NetArchTest`).

Servicios/procesos lógicamente separados (aunque al inicio en el mismo deploy):

1. Command API
2. Query API
3. Projection Workers (Marten async daemon)
4. Integration Events Publisher
5. Process Manager / Sagas
6. BIM Connector (nuevo — ACL hacia el BC BIM)
7. SignalR Hub

Cuando el volumen lo justifique, cada uno puede extraerse a su contenedor sin tocar el dominio.

---

## 4. Modelo de dominio

### 4.1 Agregado `Presupuesto`

```
Presupuesto (Aggregate Root)
├── Id: PresupuestoId
├── ProyectoId: ProyectoId
├── Nombre: string
├── Descripcion: string?
├── Moneda: Moneda (ISO-4217)
├── Estado: EstadoPresupuesto (Borrador | EnAprobacion | Aprobado | Rechazado | Cerrado | Archivado)
├── AIU: AIU { pctAdmin, pctImprevistos, pctUtilidad }
├── ProfundidadMaxima: int  (default 10, tope rígido 15)
├── Nodos: List<NodoPresupuestal>   (plano; árbol se reconstruye por ParentId)
├── BaselineCongelada: bool
├── AprobadoEn: DateTimeOffset?
├── AprobadoPor: UserId?
└── Version: long (versión del stream)

NodoPresupuestal (abstract)
├── Id: NodoId
├── ParentId: NodoId?             (null = hijo directo del Presupuesto)
├── Nivel: int                     (0 = raíz del árbol)
├── Codigo: CodigoJerarquico      ("02", "02.01", "02.01.03.02")
├── Nombre: string
├── Orden: int                     (posición entre hermanos)
├── Tipo: TipoNodo (Agrupador | Terminal)
├── Clasificaciones: List<Clasificacion>
└── VinculoBIM: VinculoBIM?       (solo Terminal)

NodoAgrupador : NodoPresupuestal
└── Hijos: navegación derivada en runtime

NodoTerminal : NodoPresupuestal
├── Unidad: UnidadMedida
├── Cantidad: Cantidad
├── PrecioUnitario: Dinero
├── APUReferencia: APUReferencia?
└── FuenteCantidad: FuenteCantidad (Manual | BIM | APU)
```

**Ejemplo de árbol de 5 niveles**:

```
Presupuesto "Torre Valencia - Etapa 1"
├─ 01 Preliminares                                      [Agrupador, nivel 0]
│   ├─ 01.01 Instalaciones provisionales                [Agrupador, nivel 1]
│   │   └─ 01.01.02 Campamento administrativo           [Terminal, nivel 2] 180 m²
│   └─ 01.02 Replanteo y localización                   [Terminal, nivel 1] 2.400 m²
├─ 02 Cimentación                                       [Agrupador, nivel 0]
│   ├─ 02.01 Excavaciones                               [Agrupador, nivel 1]
│   │   ├─ 02.01.01 Excavación mecánica                 [Agrupador, nivel 2]
│   │   │   ├─ 02.01.01.01 Material común H ≤ 2m       [Terminal, nivel 3] 1.200 m³
│   │   │   └─ 02.01.01.02 Material común H > 2m       [Terminal, nivel 3]   380 m³
│   │   └─ 02.01.02 Excavación manual                   [Terminal, nivel 2]   220 m³
│   └─ 02.02 Pilotes                                    [Agrupador, nivel 1]
│       └─ 02.02.01 Pilote preexcavado Ø0.50m           [Agrupador, nivel 2]
│           ├─ 02.02.01.01 Concreto 3000 psi            [Terminal, nivel 3]    45 m³
│           └─ 02.02.01.02 Acero refuerzo 60000 psi     [Terminal, nivel 3] 5.200 kg
```

### 4.2 Reglas del árbol

- **Raíz virtual**: el Presupuesto es la raíz; los nodos con `ParentId = null` son el nivel 0.
- **Nivel denormalizado**: se guarda para evitar recorridos; se actualiza al mover.
- **Terminales son hojas**: no pueden tener hijos.
- **Conversión terminal → agrupador**: permitida solo si no tiene ejecución registrada en BC Ejecución; destruye `Cantidad`, `PrecioUnitario`, `APUReferencia` (pierden sentido).
- **Códigos autogenerados**: al agregar un nodo, el sistema sugiere el siguiente código; el usuario puede sobrescribir respetando unicidad y prefijo del padre.
- **Mover un nodo**: recodifica toda la subrama; se emite un único `NodoMovido` con la lista de `codigosRecodificados`.
- **Orden entre hermanos**: `int Orden` renumerable.

### 4.3 Invariantes del Presupuesto

Se validan dentro del agregado antes de emitir eventos.

| # | Invariante |
|---|---|
| INV-1 | **Baseline inmutable**: tras `PresupuestoAprobado` el stream del Presupuesto solo admite eventos de ciclo de vida (`PresupuestoCerrado`, `PresupuestoArchivado`). Todo cambio estructural posterior vive en agregados `Modificacion`. |
| INV-2 | Los `CodigoJerarquico` son únicos dentro del presupuesto. |
| INV-3 | Un nodo pertenece a exactamente un padre (o al presupuesto-raíz). |
| INV-4 | Un nodo terminal no puede tener hijos. |
| INV-5 | `Cantidad` ≥ 0 y `PrecioUnitario` ≥ 0. |
| INV-6 | `pctAdmin + pctImprevistos + pctUtilidad` ≥ 0 y cada uno ∈ [0, 100]. |
| INV-7 | La `Moneda` no cambia después de agregar el primer nodo terminal. |
| INV-8 | En estado `Cerrado` o `Archivado` no se aceptan comandos salvo `Desarchivar`. |
| INV-9 | Transiciones de estado explícitas; saltos inválidos rechazados. |
| INV-10 | `Nivel ≤ ProfundidadMaxima`. |
| INV-11 | Un agrupador con hijos no puede convertirse en terminal. Un terminal con ejecución no puede convertirse en agrupador. (Aplica solo en Borrador.) |
| INV-12 | Un nodo con `FuenteCantidad = BIM` debe tener `VinculoBIM ≠ null` y al menos un `ElementoBIMRef`. |
| INV-13 | Cada `Clasificacion.Sistema` pertenece al catálogo {MasterFormat, UniFormat, OmniClass}. |
| INV-14 | Al mover un nodo, el nuevo padre debe ser un agrupador (o presupuesto-raíz). |
| INV-15 | Vínculos BIM solo en terminales. |

### 4.4 Agregado `Modificacion` (líneas como deltas)

Las modificaciones son la única vía para alterar el presupuesto tras la aprobación. Sus **líneas se expresan como deltas** (variaciones relativas al estado vigente al momento de aprobarse la modificación); el vigente se compone proyectando baseline + deltas acumulados.

```
Modificacion (Aggregate Root)
├── Id: ModificacionId
├── PresupuestoId: PresupuestoId                 (referencia)
├── Tipo: TipoModificacion (Otrosi | Adicional | Reforma | AutoBIM)
├── Consecutivo: int                              (secuencial por presupuesto)
├── Concepto: string
├── Estado: EstadoModificacion (Borrador | EnAprobacion | Aprobada | Rechazada | Aplicada)
├── Lineas: List<LineaModificacionDelta>
├── AdjuntosUrls: List<Uri>
├── SolicitadaPor: UserId
├── SolicitadaEn: DateTimeOffset
├── AprobadaPor: UserId?
├── AprobadaEn: DateTimeOffset?
├── AplicadaEn: DateTimeOffset?
└── Version: long

LineaModificacionDelta (abstract)
├── Id: LineaModificacionId
├── Tipo: TipoLineaDelta
├── NodoAfectadoId: NodoId?                       (null si el tipo es "AgregarNodoNuevo")
├── ImpactoMonetarioEstimado: Dinero              (se recalcula al componer)
└── Justificacion: string?

TipoLineaDelta:
├── AjustarCantidadDelta     { deltaCantidad: decimal }       // +70 m³, −15 m³
├── AjustarPrecioDelta       { deltaPrecio: decimal }          // +1.500 COP/m³
├── AgregarNodoTerminal      { codigo, nombre, parentId, unidad,
│                              cantidad, precioUnitario, apuRef? }  // implica delta = +(cant × precio)
├── AgregarNodoAgrupador     { codigo, nombre, parentId }     // solo estructural, delta monetario = 0
├── EliminarNodo             { }                               // delta = −(cantidad_vigente × precio_vigente)
├── RenombrarNodo            { nombreNuevo }                   // cosmético, delta = 0
├── AsignarClasificacion     { sistema, codigo, descripcion }  // delta = 0
├── VincularBIM              { elementos, regla, factor }      // delta = 0 (cambia fuente)
└── CambiarUnidad            { unidadNueva }                   // requiere también delta cantidad
```

**Reglas de composición de deltas** (clave del Tipo B):

- **Orden determinístico**: los deltas se aplican en orden de `AplicadaEn` de la modificación (fecha de aplicación, no de aprobación).
- **Múltiples deltas sobre el mismo nodo**: se suman aritméticamente. Dos otrosíes que sumen +70 m³ y +30 m³ resultan en cantidad_vigente = cantidad_base + 100 m³.
- **Delta de cantidad y delta de precio en el mismo nodo**: total_vigente = (cantidad_base + Σ Δcantidad) × (precio_base + Σ Δprecio). Ojo: el `ImpactoMonetarioEstimado` guardado en cada línea es *al momento de su creación*; el impacto real de una línea cuando coexiste con otras deltas se recalcula siempre en la proyección.
- **Agregar nodo nuevo**: el nodo nace dentro del stream de la Modificación. Su `NodoId` debe ser único frente a los nodos del baseline y frente a nodos creados por otras modificaciones.
- **Eliminar nodo del baseline**: el nodo sigue existiendo en el baseline; la proyección del vigente lo excluye. Si se intenta eliminar un nodo ya eliminado por otra modificación, la modificación no pasa validación.
- **Deltas sobre nodos agregados por una modificación previa**: permitidos; la proyección resuelve la cadena.

### 4.5 Invariantes de la Modificación

| # | Invariante |
|---|---|
| INV-M-1 | Solo se puede crear si el Presupuesto referenciado está `Aprobado`. |
| INV-M-2 | Estado `Aplicada` no acepta cambios. |
| INV-M-3 | Transición `Aprobada → Aplicada` emite únicamente `ModificacionAplicada` sobre el stream de la Modificación. **No** genera eventos sobre el stream del Presupuesto. |
| INV-M-4 | Cada línea debe tener valores coherentes con su tipo (p.ej. `AjustarCantidadDelta` requiere `NodoAfectadoId` y un `deltaCantidad ≠ 0`). |
| INV-M-5 | El `NodoAfectadoId` de una línea debe existir en el presupuesto vigente al momento de la aprobación (sea un nodo del baseline o uno agregado por una modificación previa ya aplicada). |
| INV-M-6 | `EliminarNodo` solo aplica sobre nodos con ejecución = 0. Si el nodo ya fue ejecutado parcialmente, se rechaza — hay que reducir cantidad a lo ejecutado, no eliminar. |
| INV-M-7 | El `Consecutivo` es estrictamente creciente dentro del presupuesto (no hay huecos). |

### 4.6 Value objects

```csharp
public readonly record struct PresupuestoId(Guid Value);
public readonly record struct NodoId(Guid Value);
public readonly record struct ModificacionId(Guid Value);

public sealed record CodigoJerarquico(string Valor)
{
    // Regex: ^\d{2}(\.\d{2}){0,14}$  — hasta 15 niveles
    // Valida continuidad del prefijo del padre al asignar.
}

public sealed record Cantidad(decimal Valor);          // >= 0, 4 decimales
public sealed record Dinero(decimal Monto, Moneda Moneda);
public sealed record Moneda(string CodigoIso);
public sealed record UnidadMedida(string Codigo, string Descripcion);
public sealed record AIU(decimal PctAdmin, decimal PctImprevistos, decimal PctUtilidad);
public sealed record APUReferencia(Guid CatalogoApuId, int Version);

public enum TipoNodo { Agrupador = 1, Terminal = 2 }
public enum FuenteCantidad { Manual = 1, BIM = 2, APU = 3 }
public enum SistemaClasificacion { MasterFormat = 1, UniFormat = 2, OmniClass = 3 }

public sealed record Clasificacion(
    SistemaClasificacion Sistema,
    string Codigo,
    string Descripcion);

public sealed record ModeloBIMRef(Guid ModeloId, int Version);

public sealed record ElementoBIMRef(
    Guid ModeloId,
    int Version,
    string ElementoGuid);   // IfcGloballyUniqueId o equivalente

public enum TipoReglaExtraccion
{
    SumaVolumen = 1,
    SumaArea = 2,
    SumaLongitud = 3,
    Conteo = 4,
    Parametro = 5
}

public sealed record ReglaExtraccion(
    TipoReglaExtraccion Tipo,
    string? NombreParametro);

public enum EstadoSincronizacion { Sincronizado = 1, Desfasado = 2, Desvinculado = 3 }

public sealed record VinculoBIM(
    IReadOnlyList<ElementoBIMRef> Elementos,
    ReglaExtraccion ReglaExtraccion,
    decimal Factor,
    DateTimeOffset FechaVinculo,
    EstadoSincronizacion Estado);
```

---

## 5. Estrategia de baselining — Tipo B con deltas

### 5.1 Por qué Tipo B

Entre los tres arquetipos posibles para manejar cambios al presupuesto (baseline mutable, baseline inmutable + proyección, baseline versionado), este BC adopta **Tipo B — baseline inmutable + proyección del vigente a partir de deltas**. Razones:

- **Claridad contractual**: el baseline es el presupuesto firmado con el cliente; modificarlo cambia el contrato. Preservarlo intocable hace que la trazabilidad sea directa (lectura de un read model, no reconstrucción de stream).
- **Alineación con PMBOK/EVM**: el *Planned Value* (PV) viene siempre del baseline, el *Estimate at Completion* (EAC) del vigente; tenerlos separados físicamente hace trivial calcular indicadores.
- **Análisis directo**: "¿cuánto han sumado los otrosíes?" se responde con una `SELECT SUM(...)` sobre el read model de acumulado, sin recorrer causaciones.
- **UX explícita**: la tabla del presupuesto muestra cuatro columnas por nodo — **Base | Σ Deltas | Vigente | Ejecutado** — alineada con cómo piensan gerentes y residentes.
- **Reversibilidad limpia**: desaplicar una modificación es marcarla como "desaplicada" y disparar reproyección, no emitir eventos compensatorios.

### 5.2 Deltas como primera clase

Las modificaciones contienen líneas expresadas como **deltas** (variaciones relativas), no como valores absolutos. Ejemplos:

- `AjustarCantidadDelta { nodoId: "02.01.01", deltaCantidad: +70 }` — suma 70 unidades a la cantidad vigente.
- `AjustarPrecioDelta { nodoId: "02.02.01", deltaPrecio: +1.500 }` — suma 1.500 al precio unitario vigente.
- `AgregarNodoTerminal { ... }` — crea un nodo nuevo; el delta es el total del nodo.
- `EliminarNodo { nodoId }` — el delta es el negativo del total vigente del nodo.

La UI puede mostrar al usuario el **valor resultante** (cantidad vigente tras aplicar la modificación) para facilitar la edición, pero la fuente de verdad es el delta.

### 5.3 Composición del vigente

Dado un nodo con `cantidad_base`, `precio_base`, y un conjunto ordenado de deltas aplicables, el cálculo es:

```
cantidad_vigente = cantidad_base + Σ deltaCantidad_i  (para todas las modificaciones aplicadas, en orden)
precio_vigente   = precio_base   + Σ deltaPrecio_i
total_vigente    = cantidad_vigente × precio_vigente

Σ Δ del nodo     = total_vigente − total_base
```

Para agrupadores: `total_vigente = Σ total_vigente de hijos`. Para el presupuesto completo: `total_vigente = Σ total_vigente de nodos raíz × (1 + AIU)`.

El AIU aplica sobre el costo directo vigente, no sobre el baseline, salvo que el cliente haya exigido AIU congelado — en ese caso se guarda `AIUBaseline` separado de `AIUVigente`.

### 5.4 Proyecciones que materializan los tres estados

Tres read models consumen los streams y materializan los estados relevantes:

| Read Model | Qué contiene | Actualización | Uso |
|---|---|---|---|
| `PresupuestoBaseReadModel` | Árbol del baseline aprobado, con cantidades y precios originales. | Inline hasta `PresupuestoAprobado`; inmutable después. | Consulta contractual, PV en EVM, reportes "vs firmado". |
| `AcumuladoModificacionesReadModel` | Por nodo: suma de deltas de cantidad, de precio, y monetario neto. Lista de modificaciones que lo tocaron. | Async, reacciona a `ModificacionAplicada` y a sus `LineaModificacionAgregada`. | Responder "¿cuánto han sumado los otrosíes?" por nodo/capítulo/global. |
| `PresupuestoVigenteReadModel` | Árbol del presupuesto vigente con los cuatro valores por nodo (base, Δ, vigente, ejecutado). | Async multi-stream: combina Base + Acumulado. Se reproyecta al aplicar una modificación o al reportar ejecución. | Vista principal del usuario, EAC en EVM, reportes operativos. |

En Marten, `PresupuestoVigenteReadModel` se implementa como `MultiStreamProjection<PresupuestoVigente, Guid>` que agrupa por `PresupuestoId` y consume eventos de todos los streams relacionados.

### 5.5 Ejemplo numérico

Baseline del nodo `02.01.01.01`: cantidad = 1.200 m³, precio = 28.500 COP/m³, total = 34.200.000 COP.

- Otrosí #1 aprobado y aplicado 2026-06-10: `AjustarCantidadDelta +70 m³`.
- Otrosí #2 aprobado y aplicado 2026-07-15: `AjustarPrecioDelta +1.500 COP/m³` (aumento acero).
- Reconciliación BIM (AutoBIM) aplicada 2026-08-02: `AjustarCantidadDelta −15 m³`.

Resultado vigente a 2026-08-03:

```
cantidad_vigente = 1.200 + 70 − 15 = 1.255 m³
precio_vigente   = 28.500 + 1.500 = 30.000 COP/m³
total_vigente    = 1.255 × 30.000 = 37.650.000 COP
Σ Δ del nodo     = 37.650.000 − 34.200.000 = +3.450.000 COP
```

El read model materializa las cuatro columnas para que la UI muestre:

| Nodo | Base | Σ Deltas | Vigente | Ejecutado |
|---|---:|---:|---:|---:|
| 02.01.01.01 | 34.200.000 | +3.450.000 | 37.650.000 | 12.500.000 |

### 5.6 Consistencia y orden de aplicación

- El orden oficial de aplicación es `AplicadaEn` ascendente. Aprobaciones en paralelo se serializan en el momento de aplicar.
- Si se pretende aplicar una modificación cuyas líneas entran en conflicto con lo vigente (p.ej. ajuste de cantidad en un nodo ya eliminado por otra modificación), la aplicación se rechaza y emite `ModificacionNoAplicable` con detalle del conflicto.
- La proyección `PresupuestoVigenteReadModel` es idempotente: se puede reconstruir en cualquier momento desde cero corriendo el daemon, y debe producir el mismo resultado.

### 5.7 Cambios respecto a Tipo A

Lo que **desaparece** frente a la versión Tipo A (v2.0):

- El process manager `AplicarModificacionAprobada` **no** traduce líneas a eventos sobre el stream del Presupuesto. Solo emite `ModificacionAplicada`.
- Los eventos `NodoCantidadAjustada`, `NodoPrecioUnitarioActualizado`, `NodoTerminalAgregado`, `NodoEliminado` dejan de emitirse sobre el stream del Presupuesto tras `PresupuestoAprobado`. Viven exclusivamente para la etapa de Borrador.
- La tabla de eventos marca esos eventos como "solo Borrador" en lugar de "Borrador o via modificación".

Lo que se **agrega**:

- Tipos de línea de modificación nuevos (detallados en §4.4).
- Read models `PresupuestoBaseReadModel`, `AcumuladoModificacionesReadModel`, `PresupuestoVigenteReadModel`.
- Evento `ModificacionNoAplicable` para rechazo en aplicación.

---

## 6. Integración BIM — diseño de alcance

### 6.1 División de responsabilidades

La gestión de modelos BIM **no** es responsabilidad del BC Presupuestación. Se propone la siguiente división:

- **BC Modelos BIM** (BC hermano, fuera de este documento): gestiona el ciclo de vida de los modelos BIM — ingesta de archivos IFC/RVT, extracción de elementos con GUID, versionado, visualización. Cada elemento BIM se identifica por un `ElementoBIMId` estable por modelo + versión.
- **BC Presupuestación** (este documento): consume eventos del BC BIM para vincular nodos terminales a elementos BIM, recibir cantidades reconciliadas automáticamente, y emitir eventos cuando la reconciliación resulta en cambios.

La separación respeta DDD: el BC BIM tiene su propio lenguaje ubicuo (elemento, familia, parámetro, categoría IFC) y sus propios usuarios (coordinadores BIM).

### 6.2 Vínculo nodo ↔ elemento BIM

Un nodo **terminal** puede vincularse a uno o más elementos BIM, potencialmente de modelos distintos (estructural + arquitectónico + MEP). El vínculo incluye:

- Lista de `ElementoBIMRef` (modelo, versión, GUID).
- **Regla de extracción**: `SumaVolumen`, `SumaArea`, `SumaLongitud`, `Conteo`, o `Parametro` (campo específico del elemento).
- **Factor multiplicador** (ej. `1.05` para desperdicio).

Los agrupadores **no** se vinculan directamente a BIM; su cantidad se deriva de los terminales descendientes.

### 6.3 Clasificaciones y *matching*

Un nodo puede tener 0..N clasificaciones en sistemas estándar (MasterFormat, UniFormat, OmniClass). Las clasificaciones habilitan:

- **Matching automático** con elementos BIM que contengan el mismo código en sus propiedades (parámetros compartidos).
- **Reportes cruzados**: costo por división UniFormat, comparaciones con *benchmarks*.
- **Interoperabilidad** con sistemas externos de cost management y consolidación corporativa.

### 6.4 Ciclo de reconciliación

Cuando el BC BIM publica `ModeloBIMVersionPublicada` con cambios geométricos, el BC Presupuestación:

1. Consulta qué nodos están vinculados al modelo afectado (proyección `NodoBIMVinculosReadModel`).
2. Por cada nodo, consulta al BC BIM la cantidad recalculada.
3. **Si el presupuesto está en Borrador**: emite `CantidadReconciliadaDesdeBIM` directamente; el nodo queda con `FuenteCantidad = BIM`.
4. **Si el presupuesto está en Aprobado**: **no** modifica el nodo. Emite `DesviacionBIMDetectada` y crea (o actualiza) una `Modificacion` de tipo `AutoBIM` en estado Borrador, con líneas por cada desviación. El residente decide si la promueve al flujo de aprobación.

Esto preserva la inmutabilidad del baseline.

### 6.5 Sistemas de clasificación soportados

- **MasterFormat** (CSI) — numérico 6 dígitos, ej. `03 30 00 — Cast-in-Place Concrete`. Dominante en Norteamérica; útil para procurement.
- **UniFormat** (CSI) — elemental, ej. `A1010 — Standard Foundations`. Útil para estimaciones tempranas. **Recomendado para MVP**.
- **OmniClass** (varias tablas) — más completo, menor adopción.

En MVP se sugiere habilitar UniFormat y dejar las otras dos detrás de un feature flag.

---

## 7. Convenciones de eventos

### 7.1 Metadata común

Todo evento de dominio se persiste con metadata estándar. Parte se guarda automáticamente en `mt_events` (Marten); la adicional viaja en *event headers*.

```json
{
  "eventId": "01HWX7P3E4JK7...",          // ULID / UUID v7
  "eventType": "NodoTerminalAgregado",
  "eventVersion": 1,
  "occurredAt": "2026-04-23T14:22:11.123Z",
  "aggregateId": "8f2a1c...",
  "aggregateType": "Presupuesto",
  "aggregateVersion": 47,
  "tenantId": "empresa-acme",
  "userId": "user/42",
  "correlationId": "01HWX7...",
  "causationId": "01HWX7...",
  "payload": { /* cuerpo del evento */ }
}
```

### 7.2 Versionado de eventos

- Todos los eventos inician en `eventVersion: 1`.
- **Cambio compatible** (campo opcional): mantener versión, usar defaults en deserialización.
- **Cambio incompatible**: crear `NodoTerminalAgregadoV2`, registrar *upcaster* en Marten (`IEventUpcaster<Old, New>`), seguir leyendo el evento antiguo.
- Nunca editar eventos existentes en producción.

### 7.3 Nomenclatura

- Nombres en **pretérito perfecto, voz pasiva, español** (ubiquitous language): `NodoTerminalAgregado`, `PresupuestoAprobado`.
- Comandos en **infinitivo**: `AgregarNodoTerminal`, `AprobarPresupuesto`.
- Archivos JSON Schema en `kebab-case`: `nodo-terminal-agregado.schema.json`.
- CLR record types: `public sealed record NodoTerminalAgregado(...)`.

### 7.4 Reglas de payload

- Identificadores siempre presentes.
- Números decimales serializados como **string** para evitar pérdida de precisión JSON: `"450.0000"`.
- Fechas en ISO-8601 UTC.
- Enums como strings.
- Referencias externas con versión (ej. `apuReferencia.version`).

---

## 8. Catálogo completo de eventos (JSON Schema)

Los schemas usan `$schema: "https://json-schema.org/draft/2020-12/schema"` y se publican en `contracts/schemas/presupuestacion/v1/`. A continuación los principales.

### 8.1 `PresupuestoBorradorCreado`

**Cuándo**: comando `CrearBorradorPresupuesto`.
**Invariantes**: `proyectoId` existe; usuario con permisos; nombre único en el proyecto.

```json
{
  "$id": "presupuestacion/v1/presupuesto-borrador-creado.schema.json",
  "type": "object",
  "required": ["presupuestoId", "proyectoId", "nombre", "moneda", "profundidadMaxima"],
  "properties": {
    "presupuestoId": { "type": "string", "format": "uuid" },
    "proyectoId": { "type": "string", "format": "uuid" },
    "nombre": { "type": "string", "minLength": 3, "maxLength": 200 },
    "descripcion": { "type": ["string", "null"], "maxLength": 2000 },
    "moneda": { "type": "string", "pattern": "^[A-Z]{3}$" },
    "profundidadMaxima": { "type": "integer", "minimum": 3, "maximum": 15 }
  }
}
```

```csharp
public sealed record PresupuestoBorradorCreado(
    Guid PresupuestoId, Guid ProyectoId, string Nombre,
    string? Descripcion, string Moneda, int ProfundidadMaxima);
```

### 8.2 `PresupuestoRenombrado` y `PresupuestoDescripcionActualizada`

Patrón trivial: `{ presupuestoId, valorAnterior, valorNuevo }`.

### 8.3 `NodoAgrupadorAgregado`

```json
{
  "$id": "presupuestacion/v1/nodo-agrupador-agregado.schema.json",
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

### 8.4 `NodoTerminalAgregado`

```json
{
  "$id": "presupuestacion/v1/nodo-terminal-agregado.schema.json",
  "type": "object",
  "required": [
    "presupuestoId", "nodoId", "codigo", "nombre",
    "nivel", "orden", "unidad", "cantidad",
    "precioUnitario", "fuenteCantidad"
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

**Ejemplo**:
```json
{
  "presupuestoId": "a8f2c1d4-...",
  "nodoId": "d4e9a2f1-...",
  "parentId": "b1c2d3e4-...",
  "codigo": "02.01.01.01",
  "nombre": "Excavación mecánica material común H ≤ 2m",
  "nivel": 3,
  "orden": 0,
  "unidad": "m3",
  "cantidad": "1200.0000",
  "precioUnitario": "28500.0000",
  "fuenteCantidad": "Manual",
  "apuReferencia": { "catalogoApuId": "f6d8...", "version": 3 }
}
```

### 8.5 `NodoRenombrado`

```json
{
  "$id": "presupuestacion/v1/nodo-renombrado.schema.json",
  "type": "object",
  "required": ["presupuestoId", "nodoId", "nombreAnterior", "nombreNuevo"],
  "properties": {
    "presupuestoId": { "type": "string", "format": "uuid" },
    "nodoId": { "type": "string", "format": "uuid" },
    "nombreAnterior": { "type": "string" },
    "nombreNuevo": { "type": "string", "minLength": 1, "maxLength": 300 }
  }
}
```

### 8.6 `NodoMovido`

Cambio de padre (drag & drop). Incluye recodificación de toda la subrama en un único evento.

```json
{
  "$id": "presupuestacion/v1/nodo-movido.schema.json",
  "type": "object",
  "required": [
    "presupuestoId", "nodoId", "parentNuevoId",
    "ordenNuevo", "codigosRecodificados"
  ],
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

### 8.7 `NodoEliminado`, `NodoConvertidoAAgrupador`, `NodosReordenados`

Patrones directos; payload mínimo con `presupuestoId`, `nodoId` (o lista), y valores anteriores/nuevos donde aplique.

### 8.8 `NodoCantidadAjustada`

```json
{
  "$id": "presupuestacion/v1/nodo-cantidad-ajustada.schema.json",
  "type": "object",
  "required": ["presupuestoId", "nodoId", "cantidadAnterior", "cantidadNueva", "motivo"],
  "properties": {
    "presupuestoId": { "type": "string", "format": "uuid" },
    "nodoId": { "type": "string", "format": "uuid" },
    "cantidadAnterior": { "type": "string" },
    "cantidadNueva": { "type": "string", "pattern": "^\\d+(\\.\\d{1,4})?$" },
    "motivo": { "type": "string", "maxLength": 500 }
  }
}
```

### 8.9 `NodoPrecioUnitarioActualizado`

Análogo a `NodoCantidadAjustada` con `precioAnterior` y `precioNuevo`.

### 8.10 `NodoUnidadMedidaCambiada`

`{ presupuestoId, nodoId, unidadAnterior, unidadNueva, motivo }`. Solo en borrador.

### 8.11 `ClasificacionAsignada`

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

### 8.12 `ClasificacionRemovida`

`{ presupuestoId, nodoId, sistema, codigo }`.

### 8.13 `VinculoBIMEstablecido`

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

### 8.14 `VinculoBIMActualizado` y `VinculoBIMRemovido`

`VinculoBIMActualizado`: cambios en regla o factor sin romper vínculo.
`VinculoBIMRemovido`: `{ presupuestoId, nodoId, motivo }` — vuelve `FuenteCantidad` a `Manual`.

### 8.15 `CantidadImportadaDesdeBIM`

Primera importación desde un modelo. Distinta de reconciliaciones posteriores.

```json
{
  "$id": "presupuestacion/v1/cantidad-importada-desde-bim.schema.json",
  "type": "object",
  "required": ["presupuestoId", "nodoId", "cantidadAnterior", "cantidadNueva", "modeloId", "modeloVersion"],
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

### 8.16 `CantidadReconciliadaDesdeBIM`

Mismo payload que `CantidadImportadaDesdeBIM`; se emite en reconciliaciones subsiguientes (presupuesto en borrador).

### 8.17 `DesviacionBIMDetectada`

Presupuesto en estado Aprobado; la reconciliación no modifica el nodo y registra la desviación + id de la modificación sugerida.

```json
{
  "$id": "presupuestacion/v1/desviacion-bim-detectada.schema.json",
  "type": "object",
  "required": [
    "presupuestoId", "nodoId", "cantidadBaseline",
    "cantidadBIMNueva", "modeloId", "modeloVersion", "modificacionSugeridaId"
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

### 8.18 `FuenteCantidadCambiada`

`{ presupuestoId, nodoId, fuenteAnterior, fuenteNueva, motivo }`.

### 8.19 `AIUConfigurado` / `AIUAjustado`

```json
{
  "$id": "presupuestacion/v1/aiu-configurado.schema.json",
  "type": "object",
  "required": ["presupuestoId", "pctAdmin", "pctImprevistos", "pctUtilidad"],
  "properties": {
    "presupuestoId": { "type": "string", "format": "uuid" },
    "pctAdmin": { "type": "string", "pattern": "^\\d+(\\.\\d{1,2})?$" },
    "pctImprevistos": { "type": "string", "pattern": "^\\d+(\\.\\d{1,2})?$" },
    "pctUtilidad": { "type": "string", "pattern": "^\\d+(\\.\\d{1,2})?$" }
  }
}
```

`AIUAjustado` agrega `valorAnterior`, `valorNuevo` y `motivo`.

### 8.20 Eventos de workflow

`PresupuestoSometidoAAprobacion`, `PresupuestoAprobado` (crítico — marca baseline), `PresupuestoRechazado`, `PresupuestoCerrado`, `PresupuestoArchivado`. Payloads mínimos con `presupuestoId`, `actorId`, `fecha`, y campos específicos (totales, motivo, documento respaldo).

### 8.21 Eventos del agregado `Modificacion` (con líneas delta)

`ModificacionBorradorCreada`, `LineaModificacionDeltaAgregada`, `LineaModificacionDeltaEliminada`, `ModificacionSometidaAAprobacion`, `ModificacionAprobada`, `ModificacionRechazada`, `ModificacionAplicada` (crítico — dispara reproyección), `ModificacionNoAplicable`.

**8.21.1 `ModificacionBorradorCreada`**

```json
{
  "$id": "presupuestacion/v1/modificacion-borrador-creada.schema.json",
  "type": "object",
  "required": ["modificacionId", "presupuestoId", "tipo", "consecutivo", "concepto", "solicitadaPor"],
  "properties": {
    "modificacionId": { "type": "string", "format": "uuid" },
    "presupuestoId": { "type": "string", "format": "uuid" },
    "tipo": { "type": "string", "enum": ["Otrosi", "Adicional", "Reforma", "AutoBIM"] },
    "consecutivo": { "type": "integer", "minimum": 1 },
    "concepto": { "type": "string", "minLength": 3, "maxLength": 500 },
    "solicitadaPor": { "type": "string", "format": "uuid" }
  }
}
```

**8.21.2 `LineaModificacionDeltaAgregada`**

```json
{
  "$id": "presupuestacion/v1/linea-modificacion-delta-agregada.schema.json",
  "type": "object",
  "required": ["modificacionId", "lineaId", "tipoLinea", "impactoMonetarioEstimado"],
  "properties": {
    "modificacionId": { "type": "string", "format": "uuid" },
    "lineaId": { "type": "string", "format": "uuid" },
    "nodoAfectadoId": { "type": ["string", "null"], "format": "uuid" },
    "tipoLinea": {
      "type": "string",
      "enum": [
        "AjustarCantidadDelta", "AjustarPrecioDelta",
        "AgregarNodoTerminal", "AgregarNodoAgrupador",
        "EliminarNodo", "RenombrarNodo",
        "AsignarClasificacion", "VincularBIM", "CambiarUnidad"
      ]
    },
    "payload": {
      "type": "object",
      "description": "Campos específicos por tipo de línea",
      "oneOf": [
        {
          "required": ["deltaCantidad"],
          "properties": {
            "deltaCantidad": { "type": "string", "pattern": "^-?\\d+(\\.\\d{1,4})?$" }
          }
        },
        {
          "required": ["deltaPrecio"],
          "properties": {
            "deltaPrecio": { "type": "string", "pattern": "^-?\\d+(\\.\\d{1,4})?$" }
          }
        },
        {
          "required": ["codigo", "nombre", "parentId", "unidad", "cantidad", "precioUnitario"],
          "properties": {
            "codigo": { "type": "string" },
            "nombre": { "type": "string" },
            "parentId": { "type": ["string", "null"], "format": "uuid" },
            "unidad": { "type": "string" },
            "cantidad": { "type": "string" },
            "precioUnitario": { "type": "string" },
            "apuReferencia": { "type": ["object", "null"] }
          }
        }
      ]
    },
    "impactoMonetarioEstimado": { "type": "string", "pattern": "^-?\\d+(\\.\\d{1,2})?$" },
    "justificacion": { "type": ["string", "null"], "maxLength": 1000 }
  }
}
```

Ejemplos de uso:

```json
// Sumar 70 m³ al nodo 02.01.01.01
{
  "modificacionId": "...", "lineaId": "...",
  "nodoAfectadoId": "abc-...",
  "tipoLinea": "AjustarCantidadDelta",
  "payload": { "deltaCantidad": "70.0000" },
  "impactoMonetarioEstimado": "1995000.00",
  "justificacion": "Ampliación de área de excavación por solicitud del cliente"
}

// Restar 15 m³ (reconciliación BIM)
{
  "tipoLinea": "AjustarCantidadDelta",
  "payload": { "deltaCantidad": "-15.0000" },
  "impactoMonetarioEstimado": "-427500.00"
}

// Agregar precio unitario a un nodo
{
  "tipoLinea": "AjustarPrecioDelta",
  "payload": { "deltaPrecio": "1500.0000" },
  "impactoMonetarioEstimado": "1800000.00",
  "justificacion": "Aumento de precio del acero (IPC + 8%)"
}
```

**8.21.3 `ModificacionAplicada`**

```json
{
  "$id": "presupuestacion/v1/modificacion-aplicada.schema.json",
  "type": "object",
  "required": ["modificacionId", "presupuestoId", "aplicadaEn", "totalDeltaMonetario"],
  "properties": {
    "modificacionId": { "type": "string", "format": "uuid" },
    "presupuestoId": { "type": "string", "format": "uuid" },
    "aplicadaEn": { "type": "string", "format": "date-time" },
    "totalDeltaMonetario": { "type": "string", "pattern": "^-?\\d+(\\.\\d{1,2})?$" },
    "nodosAfectados": {
      "type": "array",
      "items": { "type": "string", "format": "uuid" }
    }
  }
}
```

Este evento es **el disparador** para que `PresupuestoVigenteReadModel` recomponga el estado; no se emiten eventos adicionales sobre el stream del Presupuesto.

**8.21.4 `ModificacionNoAplicable`**

```json
{
  "$id": "presupuestacion/v1/modificacion-no-aplicable.schema.json",
  "type": "object",
  "required": ["modificacionId", "presupuestoId", "motivo", "conflictos"],
  "properties": {
    "modificacionId": { "type": "string", "format": "uuid" },
    "presupuestoId": { "type": "string", "format": "uuid" },
    "motivo": { "type": "string" },
    "conflictos": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["lineaId", "razon"],
        "properties": {
          "lineaId": { "type": "string", "format": "uuid" },
          "nodoAfectadoId": { "type": ["string", "null"], "format": "uuid" },
          "razon": {
            "type": "string",
            "enum": [
              "NodoInexistente", "NodoYaEliminado",
              "CantidadResultanteNegativa", "PrecioResultanteNegativo",
              "EjecucionRegistrada", "CodigoDuplicado"
            ]
          },
          "detalle": { "type": "string" }
        }
      }
    }
  }
}
```

### 8.22 Tabla resumen (38 eventos, Tipo B)

En Tipo B los eventos estructurales del Presupuesto (`NodoTerminalAgregado`, `NodoCantidadAjustada`, etc.) solo se emiten en estado **Borrador**. Todo cambio posterior a `PresupuestoAprobado` vive en el stream de la Modificación correspondiente y se proyecta sobre el baseline.

| # | Evento | Agregado | Estado requerido | Integration |
|---|---|---|---|---|
| 1 | `PresupuestoBorradorCreado` | Presupuesto | - | Sí |
| 2 | `PresupuestoRenombrado` | Presupuesto | Borrador/EnAprobacion | No |
| 3 | `PresupuestoDescripcionActualizada` | Presupuesto | Borrador/EnAprobacion | No |
| 4 | `ProfundidadMaximaConfigurada` | Presupuesto | Borrador | No |
| 5 | `NodoAgrupadorAgregado` | Presupuesto | **Solo Borrador** | No |
| 6 | `NodoTerminalAgregado` | Presupuesto | **Solo Borrador** | Sí |
| 7 | `NodoRenombrado` | Presupuesto | **Solo Borrador** | No |
| 8 | `NodoMovido` | Presupuesto | **Solo Borrador** | No |
| 9 | `NodoEliminado` | Presupuesto | **Solo Borrador** | Sí |
| 10 | `NodoConvertidoAAgrupador` | Presupuesto | **Solo Borrador** | No |
| 11 | `NodosReordenados` | Presupuesto | **Solo Borrador** | No |
| 12 | `NodoCantidadAjustada` | Presupuesto | **Solo Borrador** | Sí |
| 13 | `NodoPrecioUnitarioActualizado` | Presupuesto | **Solo Borrador** | Sí |
| 14 | `NodoUnidadMedidaCambiada` | Presupuesto | **Solo Borrador** | No |
| 15 | `ClasificacionAsignada` | Presupuesto | **Solo Borrador** | No |
| 16 | `ClasificacionRemovida` | Presupuesto | **Solo Borrador** | No |
| 17 | `VinculoBIMEstablecido` | Presupuesto | **Solo Borrador** | Sí |
| 18 | `VinculoBIMActualizado` | Presupuesto | **Solo Borrador** | No |
| 19 | `VinculoBIMRemovido` | Presupuesto | **Solo Borrador** | Sí |
| 20 | `CantidadImportadaDesdeBIM` | Presupuesto | **Solo Borrador** | Sí |
| 21 | `CantidadReconciliadaDesdeBIM` | Presupuesto | **Solo Borrador** | Sí |
| 22 | `DesviacionBIMDetectada` | Presupuesto | Aprobado | Sí |
| 23 | `FuenteCantidadCambiada` | Presupuesto | **Solo Borrador** | No |
| 24 | `AIUConfigurado` | Presupuesto | Borrador | No |
| 25 | `AIUAjustado` | Presupuesto | Borrador | No |
| 26 | `PresupuestoSometidoAAprobacion` | Presupuesto | Borrador | Sí |
| 27 | `PresupuestoAprobado` | Presupuesto | EnAprobacion — **congela stream** | **Sí (crítico)** |
| 28 | `PresupuestoRechazado` | Presupuesto | EnAprobacion | Sí |
| 29 | `PresupuestoCerrado` | Presupuesto | Aprobado | Sí |
| 30 | `PresupuestoArchivado` | Presupuesto | Cerrado | Sí |
| 31 | `ModificacionBorradorCreada` | Modificacion | - | No |
| 32 | `LineaModificacionDeltaAgregada` | Modificacion | Borrador | No |
| 33 | `LineaModificacionDeltaEliminada` | Modificacion | Borrador | No |
| 34 | `ModificacionSometidaAAprobacion` | Modificacion | Borrador | Sí |
| 35 | `ModificacionAprobada` | Modificacion | EnAprobacion | Sí |
| 36 | `ModificacionRechazada` | Modificacion | EnAprobacion | Sí |
| 37 | `ModificacionAplicada` | Modificacion | Aprobada — **dispara reproyección** | **Sí (crítico)** |
| 38 | `ModificacionNoAplicable` | Modificacion | Aprobada | Sí |

Eventos marcados "dispara reproyección" (`ModificacionAplicada`) son los que el *projection daemon* de `PresupuestoVigenteReadModel` consume para recomponer el estado vigente agregando los deltas al baseline. `ModificacionNoAplicable` es el evento de rechazo cuando la aplicación detecta conflictos (p.ej. intento de ajuste sobre nodo eliminado por modificación previa).

---

## 9. Servicios necesarios

### 9.1 Servicios internos del BC

**9.1.1 Command API (ASP.NET Core + Wolverine.Http)**

Expone endpoints HTTP para comandos. Wolverine descubre handlers públicos y los conecta automáticamente.

```csharp
public static class PresupuestoEndpoints
{
    [WolverinePost("/presupuestos")]
    public static async Task<IResult> Crear(
        CrearBorradorPresupuesto cmd, IMessageBus bus)
    {
        var result = await bus.InvokeAsync<PresupuestoCreadoResponse>(cmd);
        return Results.Created($"/presupuestos/{result.PresupuestoId}", result);
    }

    [WolverinePost("/presupuestos/{id:guid}/nodos/agrupadores")]
    public static Task<NodoAgregadoResponse> AgregarAgrupador(
        Guid id, AgregarNodoAgrupador cmd, IMessageBus bus)
        => bus.InvokeAsync<NodoAgregadoResponse>(cmd with { PresupuestoId = id });

    [WolverinePost("/presupuestos/{id:guid}/nodos/terminales")]
    public static Task<NodoAgregadoResponse> AgregarTerminal(
        Guid id, AgregarNodoTerminal cmd, IMessageBus bus)
        => bus.InvokeAsync<NodoAgregadoResponse>(cmd with { PresupuestoId = id });

    [WolverinePost("/presupuestos/{id:guid}/nodos/{nodoId:guid}/vinculo-bim")]
    public static Task EstablecerVinculoBIM(
        Guid id, Guid nodoId, EstablecerVinculoBIM cmd, IMessageBus bus)
        => bus.InvokeAsync(cmd with { PresupuestoId = id, NodoId = nodoId });
}
```

**9.1.2 Query API (read models)**

Lecturas desde proyecciones materializadas.

```csharp
app.MapGet("/presupuestos/{id:guid}", async (Guid id, IQuerySession session) =>
{
    var proj = await session.LoadAsync<PresupuestoDetalleReadModel>(id);
    return proj is null ? Results.NotFound() : Results.Ok(proj);
});

app.MapGet("/presupuestos/{id:guid}/nodos/por-clasificacion", async (
    Guid id, string sistema, string codigo, IQuerySession session) =>
{
    var resultados = await session.Query<NodosPorClasificacionReadModel>()
        .Where(n => n.PresupuestoId == id && n.Sistema == sistema && n.Codigo == codigo)
        .ToListAsync();
    return Results.Ok(resultados);
});
```

**9.1.3 Projection Workers (Marten projections)**

Tres modos (inline, async, live). Las proyecciones clave en Tipo B son las tres que materializan los estados Base / Acumulado / Vigente:

1. **`PresupuestoBaseReadModel`** (single stream, inline hasta `PresupuestoAprobado`): árbol del baseline. Se actualiza con cada evento estructural en Borrador; al aprobarse queda inmutable. Sirve como referencia contractual y como PV de EVM.
2. **`AcumuladoModificacionesReadModel`** (multi-stream, async): por nodo del presupuesto consolida `Σ deltaCantidad`, `Σ deltaPrecio`, impacto monetario total, y la lista de `modificacionId` que lo tocaron. Se alimenta de `ModificacionAplicada` y de las `LineaModificacionDeltaAgregada` del stream correspondiente.
3. **`PresupuestoVigenteReadModel`** (multi-stream, async): compone Base + Acumulado en un único árbol con cuatro valores por nodo: `base`, `deltas`, `vigente`, `ejecutado`. Es la vista principal del usuario y la fuente para EAC. Su implementación es una `MultiStreamProjection<PresupuestoVigente, Guid>` de Marten que agrupa eventos por `PresupuestoId` (atrapando tanto los del Presupuesto como los de sus Modificaciones).

Proyecciones de apoyo que se mantienen:

4. **`PresupuestoDetalleReadModel`** (single stream, async): árbol plano (`parentId` + `orden`) optimizado para el frontend durante **edición en borrador**. Tras la aprobación la vista principal pasa a ser `PresupuestoVigenteReadModel`.
5. **`PresupuestoListReadModel`** (single stream, async): listados (por proyecto, por estado).
6. **`HistoricoAuditoriaReadModel`** (multi-stream, async): bitácora de cambios (quién, cuándo, qué evento) consultable.
7. **`CurvaBaselineReadModel`** (live, on-demand): distribución temporal del baseline — se calcula bajo demanda desde el baseline read model.
8. **`NodoBIMVinculosReadModel`** (single stream, async): índice `(modeloId, modeloVersion) → nodos vinculados`, para reconciliación rápida.
9. **`NodosPorClasificacionReadModel`** (single stream, async): índice `(sistema, codigo) → nodos`.

```csharp
// Ejemplo de la multi-stream projection del vigente
public class PresupuestoVigenteProjection : MultiStreamProjection<PresupuestoVigente, Guid>
{
    public PresupuestoVigenteProjection()
    {
        Identity<PresupuestoBorradorCreado>(e => e.PresupuestoId);
        Identity<NodoTerminalAgregado>(e => e.PresupuestoId);
        Identity<NodoCantidadAjustada>(e => e.PresupuestoId);
        // ... resto de eventos del Presupuesto

        // Eventos del agregado Modificacion: se agrupan por PresupuestoId (no por ModificacionId)
        Identity<ModificacionBorradorCreada>(e => e.PresupuestoId);
        Identity<LineaModificacionDeltaAgregada>(e => /* resolver desde modificacionId */);
        Identity<ModificacionAplicada>(e => e.PresupuestoId);
        Identity<ModificacionNoAplicable>(e => e.PresupuestoId);
    }

    public PresupuestoVigente Create(PresupuestoBorradorCreado e) => new(e.PresupuestoId);

    public void Apply(NodoTerminalAgregado e, PresupuestoVigente v) => v.AgregarNodoBase(e);
    public void Apply(NodoCantidadAjustada e, PresupuestoVigente v) => v.AjustarCantidadBase(e);
    // ...

    public void Apply(ModificacionAplicada e, PresupuestoVigente v) => v.AplicarDeltas(e);
    public void Apply(ModificacionNoAplicable e, PresupuestoVigente v) => v.MarcarConflicto(e);
}
```

**9.1.4 Integration Event Publisher**

Observa el event store y publica eventos de integración hacia otros BCs. Implementación mediante `IEventListener` de Marten que escribe al outbox de Wolverine.

```csharp
public class PublicarPresupuestoAprobadoListener : IEventListener
{
    public async Task AfterCommitAsync(IDocumentSession session,
        IChangeSet changes, CancellationToken ct)
    {
        foreach (var e in changes.GetEvents().OfType<IEvent<PresupuestoAprobado>>())
        {
            var integration = new PresupuestoAprobadoIntegrationEvent(
                e.Data.PresupuestoId, e.Data.TotalConAIU, e.Data.BaselineAt);
            await session.Publish(integration);
        }
    }
}
```

**9.1.5 Process Manager: `AplicarModificacionAprobada` (Tipo B)**

En Tipo B el process manager se simplifica drásticamente: **no traduce líneas a eventos sobre el Presupuesto**. Solo valida que los deltas sean aplicables contra el vigente actual y emite `ModificacionAplicada` (o `ModificacionNoAplicable` si hay conflictos) sobre el stream de la Modificación. La proyección `PresupuestoVigenteReadModel` hace el resto.

```csharp
public static class AplicarModificacionAprobadaHandler
{
    public static async Task Handle(
        ModificacionAprobada evt,
        IDocumentSession session,
        IVigenteQueryService vigente)
    {
        var mod = await session.Events.AggregateStreamAsync<Modificacion>(evt.ModificacionId);
        var snapshot = await vigente.ObtenerSnapshotAsync(mod.PresupuestoId);

        var conflictos = ValidadorDeltas.Validar(mod.Lineas, snapshot);

        if (conflictos.Count > 0)
        {
            session.Events.Append(mod.Id,
                new ModificacionNoAplicable(
                    mod.Id, mod.PresupuestoId,
                    "Se detectaron conflictos al aplicar los deltas",
                    conflictos));
        }
        else
        {
            var totalDelta = mod.Lineas.Sum(l => l.ImpactoMonetarioEstimado.Monto);
            var nodosAfectados = mod.Lineas
                .Where(l => l.NodoAfectadoId.HasValue)
                .Select(l => l.NodoAfectadoId!.Value)
                .Distinct()
                .ToArray();

            session.Events.Append(mod.Id,
                new ModificacionAplicada(
                    mod.Id, mod.PresupuestoId,
                    DateTimeOffset.UtcNow,
                    totalDelta,
                    nodosAfectados));
        }

        await session.SaveChangesAsync();
        // El daemon async de Marten reproyectará PresupuestoVigenteReadModel
    }
}
```

Ventaja: idempotencia natural — reproyectar desde cero el read model siempre produce el mismo resultado, y revertir una modificación es emitir un evento `ModificacionRevertida` (futuro) que el proyector descuenta.

**9.1.6 BIM Connector (capa anticorrupción — ACL)**

Componente nuevo que aísla al dominio de Presupuestación del lenguaje del BC Modelos BIM. Funciones:

- **Consumer** de eventos del BC BIM: `ModeloBIMVersionPublicada`, `ElementosBIMActualizados`.
- **Cliente HTTP** para consultas puntuales al BC BIM (extraer cantidad para un set de elementos con una regla).
- **Traducción**: `ElementoIFC` / `ElementoRevit` → `ElementoBIMRef`; conceptos de parámetros BIM → `ReglaExtraccion`.
- **Saga de reconciliación** (`ReconciliarCantidadesBIM`): despacha por lote los recálculos usando Wolverine durable queue.

**9.1.7 SignalR Hub**

Notifica al frontend en tiempo real cuando cambia el presupuesto que el usuario tiene abierto. Un proyector async publica al hub tras procesar cada evento del stream relevante.

**9.1.8 Authorization service (transversal)**

Se consume en cada comando. Valida tenant, rol y proyecto.

### 9.2 Mapa visual de servicios

```
┌─────────────────────────────────────────────────────────────────┐
│                        Frontend React SPA                        │
└──────┬──────────────────────────────────────────┬───────────────┘
       │ HTTP (comandos/queries)                  │ WebSocket (SignalR)
       ▼                                          ▼
┌──────────────────────────────┐    ┌────────────────────────────┐
│  Command API                 │    │  SignalR Hub               │
│  (ASP.NET Core + Wolverine)  │    │  (push de actualizaciones) │
└──────┬───────────────────────┘    └──────────▲─────────────────┘
       │                                       │
       ▼                                       │
┌──────────────────────────────────┐  ┌────────┴────────────────┐
│  Dominio (Presupuesto,           │  │  Projection Workers     │
│  Modificacion) + handlers puros  │  │  (Marten async daemon)  │
└──────┬───────────────────────────┘  └────────▲────────────────┘
       │ AppendEvent                           │
       ▼                                       │
┌─────────────────────────────────────────────┴──────────────────┐
│                   PostgreSQL (Marten)                           │
│  mt_events │ read models │ outbox (Wolverine)                   │
└─────────────────────────────────────────┬──────────────────────┘
                                          │
                    ┌─────────────────────┼──────────────────────┐
                    ▼                     ▼                      ▼
         ┌──────────────────┐  ┌───────────────────┐  ┌──────────────────┐
         │ BIM Connector    │  │ Integration Event │  │ Sagas/Process    │
         │ (ACL hacia BC    │  │ Publisher         │  │ Managers         │
         │  Modelos BIM)    │  │ (a otros BCs)     │  │                  │
         └──────────────────┘  └───────────────────┘  └──────────────────┘
```

### 9.3 Query API vs Command API — ¿mismo proceso?

En MVP, sí — el mismo host expone ambos. Cuando los patrones de carga diverjan (muchas más lecturas que escrituras), se separan en procesos distintos contra la misma base.

---

## 10. Estructura de solución .NET

```
sinco-presupuesto/
├── src/
│   ├── SincoPresupuesto.Host/                    [proyecto ejecutable]
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── SincoPresupuesto.Host.csproj
│   │
│   ├── Modulos/
│   │   └── Presupuestacion/
│   │       ├── SincoPresupuesto.Presupuestacion.Api/
│   │       │   ├── Endpoints/                     [Minimal APIs + Wolverine.Http]
│   │       │   ├── Contracts/                     [DTOs]
│   │       │   └── Middleware/
│   │       │
│   │       ├── SincoPresupuesto.Presupuestacion.Application/
│   │       │   ├── Commands/                      [AgregarNodoAgrupador, etc.]
│   │       │   ├── Queries/
│   │       │   ├── Handlers/
│   │       │   ├── Validators/                    [FluentValidation]
│   │       │   └── Sagas/                         [AplicarModificacionAprobada, ReconciliarBIM]
│   │       │
│   │       ├── SincoPresupuesto.Presupuestacion.Domain/
│   │       │   ├── Presupuesto/                   [Presupuesto.cs + Apply methods]
│   │       │   ├── Modificacion/
│   │       │   ├── Nodos/                         [NodoPresupuestal, agrupador, terminal]
│   │       │   ├── ValueObjects/                  [CodigoJerarquico, Dinero, AIU, etc.]
│   │       │   ├── BIM/                           [VinculoBIM, ReglaExtraccion, Clasificacion]
│   │       │   ├── Events/                        [records de eventos]
│   │       │   ├── Services/                      [GeneradorCodigosJerarquicos]
│   │       │   └── Exceptions/
│   │       │
│   │       ├── SincoPresupuesto.Presupuestacion.Infrastructure/
│   │       │   ├── Marten/                        [config, upcasters]
│   │       │   ├── Projections/
│   │       │   │   ├── Base/                      [PresupuestoBaseReadModel + projection]
│   │       │   │   ├── Acumulado/                 [AcumuladoModificacionesReadModel]
│   │       │   │   ├── Vigente/                   [PresupuestoVigenteReadModel
│   │       │   │   │                               — MultiStream que compone Base + Acumulado]
│   │       │   │   ├── Auditoria/
│   │       │   │   ├── BIM/                       [NodoBIMVinculosReadModel]
│   │       │   │   └── Clasificacion/
│   │       │   ├── IntegrationEvents/
│   │       │   └── Authorization/
│   │       │
│   │       ├── SincoPresupuesto.Presupuestacion.BIMConnector/     [ACL]
│   │       │   ├── Consumers/                     [eventos del BC BIM]
│   │       │   ├── Clients/                       [cliente HTTP]
│   │       │   ├── Reconciliation/                [saga]
│   │       │   └── Clasificacion/                 [validadores de catálogos]
│   │       │
│   │       └── SincoPresupuesto.Presupuestacion.Contracts/
│   │           ├── IntegrationEvents/             [eventos públicos v1/v2/...]
│   │           └── Schemas/                       [JSON Schemas .json]
│   │
│   └── Shared/
│       ├── SincoPresupuesto.SharedKernel/         [Dinero, Moneda transversales]
│       └── SincoPresupuesto.BuildingBlocks/       [infra: outbox, idempotencia]
│
├── tests/
│   ├── SincoPresupuesto.Presupuestacion.Domain.Tests/
│   ├── SincoPresupuesto.Presupuestacion.Application.Tests/
│   ├── SincoPresupuesto.Presupuestacion.Integration.Tests/   [TestContainers]
│   └── SincoPresupuesto.Presupuestacion.Architecture.Tests/  [NetArchTest]
│
├── contracts/
│   └── schemas/
│       └── presupuestacion/v1/                    [38 JSON Schemas]
│
├── web/
│   └── sinco-presupuesto-web/                     [React + Vite]
│       ├── src/features/presupuesto/
│       │   ├── api/                               [React Query hooks]
│       │   ├── components/
│       │   │   ├── ArbolPresupuesto.tsx          [árbol n-ario con AG Grid]
│       │   │   ├── NodoEditor.tsx
│       │   │   ├── VinculoBIMPanel.tsx
│       │   │   └── ClasificacionSelector.tsx
│       │   └── pages/
│       └── App.tsx
│
├── infra/
│   ├── docker-compose.yml                         [postgres, seq, jaeger]
│   └── k8s/
│
├── docs/
│   ├── adr/
│   └── bc_presupuestacion_diseno.md               [este documento]
│
├── .editorconfig
├── Directory.Build.props
├── SincoPresupuesto.sln
└── README.md
```

### 10.1 Reglas de arquitectura (NetArchTest)

```csharp
[Fact]
public void Domain_no_depende_de_Infrastructure()
{
    var result = Types.InAssembly(typeof(Presupuesto).Assembly)
        .ShouldNot().HaveDependencyOn("SincoPresupuesto.Presupuestacion.Infrastructure")
        .GetResult();
    Assert.True(result.IsSuccessful);
}

[Fact]
public void Domain_no_depende_de_BIMConnector()
{
    // El dominio debe ser ignorante del connector.
    // El connector traduce y emite comandos al dominio.
}
```

### 10.2 `Program.cs` del Host (resumen)

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWolverine(opts =>
{
    opts.Services.AddMarten(o =>
    {
        o.Connection(builder.Configuration.GetConnectionString("Postgres")!);
        o.Events.StreamIdentity = StreamIdentity.AsGuid;
        // Proyecciones Tipo B — baseline inmutable + vigente como composición
        o.Projections.Add<PresupuestoBaseProjection>(ProjectionLifecycle.Inline);
        o.Projections.Add<AcumuladoModificacionesProjection>(ProjectionLifecycle.Async);
        o.Projections.Add<PresupuestoVigenteProjection>(ProjectionLifecycle.Async);  // MultiStream
        // Proyecciones de apoyo
        o.Projections.Add<PresupuestoDetalleProjection>(ProjectionLifecycle.Async);
        o.Projections.Add<NodoBIMVinculosProjection>(ProjectionLifecycle.Async);
        o.Projections.Add<NodosPorClasificacionProjection>(ProjectionLifecycle.Async);
    })
    .IntegrateWithWolverine()
    .UseLightweightSessions()
    .AddAsyncDaemon(DaemonMode.HotCold);

    opts.PublishAllMessages().ToLocalQueue("presupuesto-integration");
});

builder.Services.AddWolverineHttp();
builder.Services.AddSignalR();
builder.Services.AddBIMConnector(builder.Configuration);

var app = builder.Build();
app.MapWolverineEndpoints();
app.MapHub<PresupuestoHub>("/hubs/presupuesto");
app.Run();
```

---

## 11. Flujos end-to-end de ejemplo

### 11.1 Flujo A — Construir un árbol multinivel

```
1. Usuario crea Presupuesto en borrador (PresupuestoBorradorCreado, profundidadMax: 10).
2. Agrega "02 Cimentación" en raíz (parentId = null, nivel 0).
   → NodoAgrupadorAgregado { codigo: "02", nivel: 0 }
3. Bajo "02", agrega "02.01 Excavaciones" (parent = id de "02").
   → NodoAgrupadorAgregado { codigo: "02.01", nivel: 1 }
4. Bajo "02.01" agrega "02.01.01 Excavación mecánica" (agrupador, nivel 2).
5. Bajo "02.01.01" agrega "02.01.01.01 Material común H ≤ 2m" como terminal,
   cantidad 1200 m³, precio 45.000 COP.
   → NodoTerminalAgregado { nivel: 3, cantidad: "1200", fuenteCantidad: "Manual" }
6. Frontend recibe actualización por SignalR y pinta nuevo nodo en el árbol.
```

### 11.2 Flujo B — Agregar un nodo y ver total actualizado

```
1. POST /presupuestos/{id}/nodos/terminales con AgregarNodoTerminal cmd.
2. Wolverine.Http deserializa, valida con FluentValidation.
3. Handler carga agregado: session.Events.AggregateStreamAsync<Presupuesto>(id).
4. presupuesto.AgregarNodoTerminal(...) valida invariantes (estado Borrador,
   parent existe y es agrupador, código único, cantidad/precio >= 0).
5. Retorna new NodoTerminalAgregado(...). Handler hace session.Events.Append.
6. session.SaveChangesAsync() → transacción ACID: evento + outbox +
   inline projection (PresupuestoTotalesReadModel).
7. Marten async daemon procesa: actualiza PresupuestoDetalleReadModel.
8. Projection trigger: SignalR empuja delta al cliente.
9. React actualiza Zustand store, tabla re-renderiza con nuevo total.
```

Latencias típicas: comando → respuesta 20–80 ms; evento → read model async 50–500 ms; evento → integración 100 ms – 2 s.

### 11.3 Flujo C — Aprobación del presupuesto

```
1. POST /presupuestos/{id}/someter → PresupuestoSometidoAAprobacion, estado EnAprobacion.
2. Notificación al gerente (integration event → BC Notificaciones).
3. POST /presupuestos/{id}/aprobar → handler valida estado y rol Aprobador.
4. → PresupuestoAprobado { baselineAt: now, aprobadoPor, totalConAIU, ... }
5. Proyección async: snapshot del baseline en tabla dedicada (para time travel).
6. Integration Event Publisher: publica PresupuestoAprobadoIntegrationEvent
   → Ejecución inicializa; Contabilidad provisiona; Contratos permite subcontratar.
7. SignalR empuja cambio de estado a la UI en vivo.
```

### 11.4 Flujo D — Vincular nodo a BIM y cuantificar

```
1. Coordinador BIM publica "Modelo estructural v3" en BC BIM
   → ModeloBIMVersionPublicada.
2. En Presupuestación, presupuestador navega a nodo "02.02.01.01 Concreto 3000 psi"
   y hace clic en "Vincular a BIM".
3. UI consulta al BC BIM candidatos (filtra por UniFormat "A1020" si ya asignado).
4. Usuario selecciona 24 elementos IfcPile y configura:
   regla = SumaVolumen, factor = 1.05 (5 % desperdicio).
5. POST /presupuestos/{id}/nodos/{nodoId}/vinculo-bim
   → VinculoBIMEstablecido { elementos, reglaExtraccion, factor }
6. Handler invoca BIM Connector para extraer suma de volumen. Respuesta: 42.85 m³.
7. → CantidadImportadaDesdeBIM { cantidadAnterior: "0", cantidadNueva: "45.0000" }
8. → FuenteCantidadCambiada { fuenteAnterior: "Manual", fuenteNueva: "BIM" }
9. Proyección actualiza; UI muestra badge "BIM" junto a la cantidad.
```

### 11.5 Flujo E — Reconciliación BIM en borrador

```
1. BC BIM publica "Modelo estructural v4" (geometría de pilotes cambió).
2. Saga ReconciliarCantidadesBIM consume ModeloBIMVersionPublicada.
3. Consulta proyección NodoBIMVinculosReadModel: 12 nodos afectados.
4. Por cada nodo: consulta BIM Connector nueva cantidad.
5. Estado = Borrador → emite CantidadReconciliadaDesdeBIM directamente.
6. UI muestra toast: "Modelo BIM v4 actualizó 12 cantidades".
```

### 11.6 Flujo F — Reconciliación BIM con presupuesto aprobado (Tipo B)

```
1. Igual paso 1-3 de Flujo E, pero estado = Aprobado.
   El stream del Presupuesto está congelado.
2. Saga ReconciliarCantidadesBIM:
   a. Busca Modificacion AutoBIM en Borrador para ese presupuesto.
      Si no existe, crea una con comando interno.
   b. Por cada desviación, agrega una línea delta a la Modificación:
      LineaModificacionDeltaAgregada {
         tipoLinea: AjustarCantidadDelta,
         nodoAfectadoId: nodoPilotes,
         payload: { deltaCantidad: "+4.56" },   // diferencia nueva - vigente actual
         impactoMonetarioEstimado: "136800"
      }
   c. Sobre el stream del Presupuesto emite únicamente un evento informativo
      DesviacionBIMDetectada { nodoId, cantidadBaseline, cantidadBIMNueva,
                                modificacionSugeridaId }.
      Este evento NO modifica el baseline — es una notificación auditada.
3. Frontend alerta al residente:
   "Modelo BIM v4 introduce 12 desviaciones — hay modificación AutoBIM pendiente".
4. El residente revisa, edita concepto, adjunta soporte, somete a aprobación.
   El workflow sigue el camino estándar de modificaciones (Flujo G).
```

El baseline permanece contractualmente intocado. El delta entra al vigente solo cuando la modificación se aprueba y se emite `ModificacionAplicada`. Hasta entonces, el reporte contractual ("vs presupuesto firmado") no se ve afectado.

### 11.7 Flujo G — Modificación aprobada aplicada al presupuesto (Tipo B)

```
1. Residente crea ModificacionBorrador tipo Adicional con 3 líneas delta:
   • AjustarCantidadDelta sobre 02.01.01.01 con deltaCantidad = +70 m³
   • AjustarPrecioDelta sobre 02.01.01.01 con deltaPrecio = +1.500 COP/m³
   • AgregarNodoTerminal "02.01.05 Sobreancho de excavación" cantidad 80 m³
2. Cada acción → LineaModificacionDeltaAgregada sobre el stream Modificacion.
   (El stream del Presupuesto NO recibe eventos — está congelado desde su aprobación.)
3. Residente somete → ModificacionSometidaAAprobacion.
4. Director + Gerente aprueban → ModificacionAprobada.
5. Process Manager AplicarModificacionAprobada:
   a. Carga la Modificación.
   b. Consulta el snapshot actual del vigente (PresupuestoVigenteReadModel).
   c. Valida cada línea contra el vigente:
      • El nodo afectado existe y no está eliminado.
      • cantidad_vigente + deltaCantidad ≥ 0.
      • precio_vigente + deltaPrecio ≥ 0.
      • El nuevo nodo no colide con códigos existentes.
      • No hay ejecución registrada si se pretende eliminar.
   d. Si hay conflictos → emite ModificacionNoAplicable con el detalle.
      Si no → emite ModificacionAplicada con totalDeltaMonetario y nodosAfectados.
6. El daemon de Marten reproyecta PresupuestoVigenteReadModel:
   • El baseline se mantiene intocado.
   • El acumulado se suma.
   • El vigente muestra cantidad = 1.200 + 70 = 1.270, precio = 28.500 + 1.500 = 30.000,
     total nuevo + total sobreancho.
7. SignalR empuja delta al cliente. UI pinta las cuatro columnas:
   | Nodo         | Base       | Σ Δ       | Vigente    | Ejecutado |
   | 02.01.01.01  | 34.200.000 | +3.900.000| 38.100.000 |  ...      |
   | 02.01.05     |          0 | +2.280.000|  2.280.000 |  ...      |
```

**Diferencias clave respecto a Tipo A:**

- El stream del Presupuesto no recibe eventos nuevos — permanece como quedó al aprobarse.
- No se emiten `NodoCantidadAjustada`, `NodoTerminalAgregado`, etc. tras la aprobación.
- La bitácora de qué cambió y por qué vive en el stream de la Modificación, agrupada por modificación (auditable naturalmente).
- Revertir una modificación es emitir `ModificacionRevertida` que se descuenta del acumulado — sin necesidad de eventos compensatorios sobre el Presupuesto.

Consistencia eventual entre pasos 5 y 6: hay una ventana de 50–500 ms donde la UI aún puede mostrar el vigente previo. `ModificacionAplicada` es el evento que dispara la reproyección; hasta que el daemon lo procesa, el estado vigente no incluye el nuevo delta.

### 11.8 Flujo H — Consulta "cuánto han sumado los otrosíes"

```
1. Usuario abre dashboard del presupuesto.
2. GET /presupuestos/{id}/acumulado-modificaciones?agrupadoPor=capitulo
3. Query API lee AcumuladoModificacionesReadModel:
   SELECT capituloCodigo, SUM(deltaMonetario) AS total,
          COUNT(DISTINCT modificacionId) AS numModificaciones
   FROM acumulado_modificaciones
   WHERE presupuesto_id = :id
   GROUP BY capituloCodigo
4. Respuesta:
   [
     { "capitulo": "02 Cimentación",       "total": "+8.450.000", "mods": 3 },
     { "capitulo": "03 Estructura",         "total": "+12.300.000","mods": 2 },
     { "capitulo": "05 Muros Mampostería",  "total":  "-850.000",  "mods": 1 }
   ]
5. UI pinta tabla + drill-down a la lista de modificaciones específicas.
```

En Tipo A esta consulta requeriría recorrer eventos `NodoCantidadAjustada`/`NodoPrecioUnitarioActualizado` con causationId apuntando a modificaciones y reconstruir. En Tipo B es una `SELECT ... GROUP BY` directa sobre un read model.

---

## 12. Checklist de implementación

### Antes de escribir código

- [ ] Event Storming con Product Owner (1 día mínimo).
- [ ] Glosario validado y publicado.
- [ ] ADRs iniciales: stack, granularidad, versionado, BIM, clasificaciones MVP.
- [ ] JSON Schemas del núcleo versionados en git.
- [ ] Convenciones (`.editorconfig`, `Directory.Build.props`).

### Sprint 1 — walking skeleton (2 semanas)

- [ ] Solución .NET con estructura modular.
- [ ] Docker Compose con Postgres y Seq.
- [ ] Marten configurado; `AppendEvent` + `AggregateStream` funcionando.
- [ ] Wolverine configurado; comando `CrearBorradorPresupuesto` operativo.
- [ ] Un GET con read model inline.
- [ ] Test de integración con TestContainers.
- [ ] React skeleton con login dummy y pantalla conectada al backend.

### Sprints 2–4 — árbol multinivel (4–6 semanas)

- [ ] Agregar/eliminar/renombrar/mover nodos (agrupadores y terminales).
- [ ] Generador de códigos jerárquicos.
- [ ] Proyección del árbol.
- [ ] UI: árbol expandible con AG Grid, edición inline, drag & drop.
- [ ] Proyección de totales inline.
- [ ] AIU configurable.
- [ ] Exportación a Excel.
- [ ] 90 % cobertura en Domain, incluyendo árboles profundos (15 niveles) y anchos (100 hermanos).

### Sprints 5–6 — workflow de aprobación + baseline inmutable (3 semanas)

- [ ] Comandos someter/aprobar/rechazar.
- [ ] Permisos por rol.
- [ ] **Congelación del stream tras `PresupuestoAprobado`**: cualquier comando estructural posterior es rechazado por el dominio.
- [ ] **`PresupuestoBaseReadModel`** se bloquea (inmutable) al aprobar.
- [ ] Consulta "presupuesto en fecha X" (time travel) — devuelve siempre el baseline para fechas post-aprobación.
- [ ] Integration events publicados a un canal de prueba.

### Sprints 7–8 — clasificaciones (2 semanas)

- [ ] Asignar/remover clasificaciones (UniFormat en MVP).
- [ ] Validación contra catálogo local.
- [ ] Proyección por clasificación.
- [ ] Reportes cruzados en UI.

### Sprints 9–10 — integración BIM (4 semanas)

- [ ] BIM Connector como ACL.
- [ ] Establecer/actualizar/remover vínculo BIM.
- [ ] Importación inicial de cantidad.
- [ ] Saga de reconciliación (borrador).
- [ ] Reconciliación en aprobado via Modificacion AutoBIM.
- [ ] UI: panel de vínculo, badge de fuente de cantidad, alertas de reconciliación.

### Sprints 11–13 — modificaciones con deltas + proyección vigente (4 semanas)

- [ ] Agregado `Modificacion` con líneas delta (todos los `TipoLineaDelta`).
- [ ] Validador de deltas contra vigente (`ValidadorDeltas`).
- [ ] Process manager `AplicarModificacionAprobada` simplificado (solo emite `ModificacionAplicada` / `ModificacionNoAplicable`).
- [ ] **`AcumuladoModificacionesReadModel`** funcional.
- [ ] **`PresupuestoVigenteReadModel`** como `MultiStreamProjection` que compone Base + Acumulado.
- [ ] UI con cuatro columnas por nodo: Base | Σ Δ | Vigente | Ejecutado.
- [ ] UI para crear/listar/aprobar modificaciones con editor de líneas delta (muestra valor resultante para usabilidad, guarda delta).
- [ ] Historial consolidado navegable por modificación.
- [ ] Endpoint `GET /presupuestos/{id}/acumulado-modificaciones` operativo.

### Sprint 14 — pulido y pilotaje (2 semanas)

- [ ] Log de auditoría consultable.
- [ ] SignalR para concurrencia 2+ usuarios.
- [ ] Tracing end-to-end (React → evento).
- [ ] OpenAPI publicado.
- [ ] Proyecto piloto con cliente real.

Total estimado a MVP con multinivel + BIM: ~22 semanas.

---

## 13. Preguntas abiertas

**Decisiones ya tomadas** (registro):

- ✅ Estrategia de baseline: **Tipo B** (baseline inmutable + vigente como proyección). Decidido 2026-04-23.
- ✅ Formato de líneas de modificación: **deltas** (no valores absolutos). Decidido 2026-04-23.
- ✅ Composición de deltas: suma aritmética por orden de aplicación.

**Abiertas — sugerido cerrar con Product Owner**:

1. **Profundidad máxima**: ¿15 niveles rígidos o configurable sin tope? Recomendación: 15 como tope.
2. **Sistemas de clasificación en MVP**: ¿solo UniFormat o los tres? Recomendación: UniFormat en MVP, los otros detrás de feature flag.
3. **BC Modelos BIM**: ¿existe, se construye en paralelo, o se arranca con un mini-BIM dentro de Presupuestación? Recomendación: construir BC hermano en paralelo.
4. **Formato BIM en MVP**: ¿solo IFC, o también Revit? Recomendación: IFC en MVP, Revit opcional vía conversión.
5. **Reconciliación**: ¿automática en borrador con "deshacer", vía modificación en aprobado? Recomendación: sí, como se describe.
6. **Múltiples vínculos por nodo**: ¿elementos de modelos distintos en un solo nodo? Recomendación: sí, misma regla de extracción.
7. **Conversión terminal ↔ agrupador**: ¿bloqueada si hay ejecución registrada? Recomendación: sí.
8. **AIU global vs por capítulo**: ¿MVP solo global? Recomendación: sí.
9. **Monedas**: ¿una por presupuesto en MVP? Recomendación: sí.
10. **Workflow de aprobación**: ¿niveles fijos (residente → director → gerente) o configurable? Recomendación: configurable por proyecto, default dos niveles.
11. **AIU sobre vigente o congelado**: si el cliente firmó contrato con AIU X, ¿este se aplica sobre el vigente o queda congelado como el baseline? Recomendación: configurable por presupuesto, default = aplica sobre vigente.
12. **Reversión de modificaciones aplicadas**: ¿es necesaria en MVP? (evento `ModificacionRevertida`). Recomendación: no en MVP; se logra con una modificación nueva que contenga los deltas opuestos.
13. **Edición en UI**: ¿el usuario ingresa el delta directamente (`+70 m³`) o ingresa el valor resultante y el sistema calcula el delta? Recomendación: valor resultante con vista previa del delta — menos fricción para el usuario, fuente de verdad sigue siendo el delta.
14. **Límites de un mismo nodo tocado por muchas modificaciones**: ¿hay un tope razonable (ej. 10 modificaciones por nodo) antes de recomendar rehacer el baseline? Sin recomendación firme.

---

*Fin del documento. Versión 2.1 — Tipo B con deltas.*
