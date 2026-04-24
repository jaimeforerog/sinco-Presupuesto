# Investigación: Sistemas de Presupuesto para Control y Administración de Proyectos de Construcción

**Autor:** Jaime Forero
**Fecha:** 23 de abril de 2026
**Proyecto:** Sinco Presupuesto — App de control presupuestal con Event Sourcing y EDA
**Versión:** 1.0

---

## Tabla de contenido

1. [Resumen ejecutivo](#1-resumen-ejecutivo)
2. [Introducción y alcance](#2-introducción-y-alcance)
3. [Parte I — Marco metodológico y conceptual](#parte-i--marco-metodológico-y-conceptual)
    - 3.1 [Naturaleza del presupuesto en proyectos de construcción](#31-naturaleza-del-presupuesto-en-proyectos-de-construcción)
    - 3.2 [Estructura del presupuesto: WBS/EDT, APU y AIU](#32-estructura-del-presupuesto-wbsedt-apu-y-aiu)
    - 3.3 [PMBOK aplicado al control presupuestal](#33-pmbok-aplicado-al-control-presupuestal)
    - 3.4 [Earned Value Management (EVM)](#34-earned-value-management-evm)
    - 3.5 [Ciclo de vida del control presupuestal](#35-ciclo-de-vida-del-control-presupuestal)
    - 3.6 [Indicadores clave y buenas prácticas](#36-indicadores-clave-y-buenas-prácticas)
4. [Parte II — Análisis del mercado de software](#parte-ii--análisis-del-mercado-de-software)
    - 4.1 [SINCO ERP / SINCO ADPRO (Colombia)](#41-sinco-erp--sinco-adpro-colombia)
    - 4.2 [Presto (España / global)](#42-presto-españa--global)
    - 4.3 [OPUS (México / LatAm)](#43-opus-méxico--latam)
    - 4.4 [Neodata (México)](#44-neodata-méxico)
    - 4.5 [Primavera P6 y SAP PS](#45-primavera-p6-y-sap-ps)
    - 4.6 [Cuadro comparativo](#46-cuadro-comparativo)
    - 4.7 [Brechas observadas y oportunidades](#47-brechas-observadas-y-oportunidades)
5. [Parte III — Arquitectura técnica: Event Sourcing y EDA](#parte-iii--arquitectura-técnica-event-sourcing-y-eda)
    - 5.1 [Conceptos fundamentales](#51-conceptos-fundamentales)
    - 5.2 [Event Sourcing en detalle](#52-event-sourcing-en-detalle)
    - 5.3 [CQRS como complemento natural](#53-cqrs-como-complemento-natural)
    - 5.4 [Event-Driven Architecture](#54-event-driven-architecture)
    - 5.5 [Por qué este stack encaja con control presupuestal](#55-por-qué-este-stack-encaja-con-control-presupuestal)
    - 5.6 [Modelo de dominio propuesto](#56-modelo-de-dominio-propuesto)
    - 5.7 [Catálogo inicial de eventos](#57-catálogo-inicial-de-eventos)
    - 5.8 [Stack tecnológico recomendado](#58-stack-tecnológico-recomendado)
    - 5.9 [Desafíos a anticipar](#59-desafíos-a-anticipar)
6. [Parte IV — Recomendaciones para el proyecto Sinco Presupuesto](#parte-iv--recomendaciones-para-el-proyecto-sinco-presupuesto)
    - 6.1 [Bounded contexts sugeridos](#61-bounded-contexts-sugeridos)
    - 6.2 [Alcance del MVP](#62-alcance-del-mvp)
    - 6.3 [Roadmap sugerido](#63-roadmap-sugerido)
7. [Conclusiones](#7-conclusiones)
8. [Referencias](#8-referencias)

---

## 1. Resumen ejecutivo

El presupuesto es, después del cronograma, el instrumento de control más importante de cualquier proyecto de construcción. A diferencia de la contabilidad —que mira hacia atrás— el presupuesto mira hacia adelante: proyecta lo que *debería* costar la obra, compara contra lo que *realmente* está costando y alerta cuando la brecha se abre. En proyectos de infraestructura y edificación, donde los márgenes son estrechos y los imprevistos abundan, un sistema de control presupuestal deficiente es la causa más frecuente de pérdida de utilidad y de sobrecostos que superan el 20 o 30 por ciento del valor contratado.

Esta investigación aborda tres dimensiones complementarias. La primera es metodológica: qué es un presupuesto de obra, cómo se estructura en niveles (EDT, capítulos, subcapítulos, ítems, APU, insumos), cómo se integra con las recomendaciones del *Project Management Body of Knowledge* (PMBOK) y cómo la técnica de *Earned Value Management* permite detectar desviaciones antes de que se vuelvan catastróficas. La segunda es de mercado: un panorama de las herramientas dominantes en Latinoamérica (SINCO, OPUS, Neodata) y en el mundo hispano (Presto, Primavera P6, SAP PS), sus fortalezas y sus limitaciones. La tercera es arquitectónica: cómo los patrones de *Event Sourcing*, *CQRS* y *Event-Driven Architecture* (EDA) encajan de forma natural con las necesidades de un sistema presupuestal moderno —auditabilidad total, reconstrucción histórica, análisis multidimensional, integración con otros sistemas— y qué consideraciones técnicas debe tener en cuenta el equipo antes de adoptarlos.

La conclusión central es que la combinación Event Sourcing + CQRS + EDA no es un capricho técnico: responde con precisión a los tres requisitos no negociables del dominio presupuestal, que son la *trazabilidad* de cada cambio (auditoría y cumplimiento regulatorio), la *capacidad de análisis retrospectivo* ("¿cuál era el presupuesto el día 15 cuando se firmó este otrosí?") y la *integración asíncrona* con ERP, contabilidad, compras y ejecución de obra. Sin embargo, la complejidad que introducen estos patrones exige un equipo con experiencia en DDD, un alcance inicial (MVP) estrecho y una estrategia clara de versionado de eventos, *snapshots* y consistencia eventual.

---

## 2. Introducción y alcance

El proyecto *Sinco Presupuesto* tiene como objetivo construir una aplicación para el control y administración presupuestal de proyectos de construcción, utilizando una arquitectura basada en eventos (*Event-Driven Architecture*) y persistencia por *Event Sourcing*. El nombre del proyecto sugiere, adicionalmente, una inspiración en el producto colombiano SINCO ERP, referente del sector en la región andina.

Este documento busca servir de insumo para las siguientes decisiones del proyecto:

- **Alcance funcional**: qué procesos cubrir en el MVP y cuáles diferir.
- **Modelo de dominio**: cómo modelar presupuestos, ejecuciones, modificaciones, aprobaciones y cortes de obra como agregados y eventos.
- **Stack técnico**: qué tecnologías adoptar para event store, bus de eventos, proyecciones, API y frontend.
- **Posicionamiento de mercado**: qué ofrecen los competidores y dónde un producto nuevo puede diferenciarse.

El alcance geográfico y sectorial se centra en proyectos de **construcción e infraestructura** en América Latina, con énfasis en el contexto colombiano (normativa INVIAS/IDU, estructura APU + AIU, salario mínimo 2026). La investigación no cubre presupuestos corporativos de TI, presupuesto público general, ni sistemas ERP genéricos; aunque se mencionan donde sirven de contraste.

---

## Parte I — Marco metodológico y conceptual

### 3.1 Naturaleza del presupuesto en proyectos de construcción

Un presupuesto de obra es, en términos simples, **la cuantificación monetaria de todo lo que un proyecto de construcción va a consumir para entregar un producto definido**: materiales, mano de obra, equipos, subcontratos, administración, imprevistos y utilidad. A diferencia de un presupuesto empresarial —que es periódico y suele organizarse por centro de costo— el presupuesto de obra se organiza por *entregable constructivo* (cimentación, estructura, mampostería, acabados) y se ejecuta a lo largo de toda la vida del proyecto.

Dentro del ciclo de vida del proyecto, el presupuesto pasa por varios estados:

- **Presupuesto preliminar o de factibilidad**: estimación gruesa usada para decidir si el proyecto es viable. Margen de error típico: ±20-30 %.
- **Presupuesto definitivo (baseline)**: la versión aprobada que se convierte en la *línea base de costo* contra la cual se medirán todas las desviaciones. Margen esperado: ±5-10 %.
- **Presupuesto ejecutado o actualizado**: la línea base más todas las modificaciones aprobadas (adicionales, otrosís, reformas, cambios de alcance). Este es el presupuesto "vivo".
- **Costo real**: lo que efectivamente se ha gastado a la fecha, capturado desde contabilidad, almacén, contratos y nómina.

La tarea del sistema de control presupuestal es mantener sincronizados estos estados, capturar cada cambio con trazabilidad, y producir indicadores que permitan a la gerencia decidir con anticipación.

### 3.2 Estructura del presupuesto: WBS/EDT, APU y AIU

El presupuesto de obra tiene una estructura jerárquica que, en la práctica colombiana y latinoamericana, suele tomar la forma:

```
PROYECTO
 └── CAPÍTULO             (ej. 01 — Preliminares, 02 — Cimentación)
      └── SUBCAPÍTULO      (ej. 02.01 — Excavaciones)
           └── ACTIVIDAD / ÍTEM   (ej. 02.01.03 — Excavación manual en material común)
                └── APU    (Análisis de Precio Unitario)
                     ├── Insumos materiales
                     ├── Insumos de mano de obra
                     ├── Insumos de equipo
                     └── Transportes / otros
```

Esta estructura coincide conceptualmente con la **Estructura de Desglose del Trabajo (EDT)** o *Work Breakdown Structure* (WBS) del PMBOK, aunque el WBS se enfoca en entregables y el presupuesto agrega la dimensión económica.

El **Análisis de Precio Unitario (APU)** es el corazón del presupuesto. Cada APU detalla, para una unidad de medida (m², m³, ml, kg, und), el consumo de cada insumo multiplicado por su rendimiento y precio. Por ejemplo, un APU de "muro en ladrillo prensado e = 12 cm" descompone el costo por m² en:

- Ladrillo (1.05 und/m² × precio unitario)
- Mortero de pega (0.02 m³/m² × precio del mortero, que a su vez es otro APU o análisis)
- Oficial de mampostería (0.8 h/m² × jornal + prestaciones)
- Ayudante (0.8 h/m² × jornal + prestaciones)
- Herramienta menor (% sobre mano de obra, típicamente 5-10 %)

Una vez calculados todos los APUs, el **presupuesto total** se obtiene sumando, por ítem, (cantidad de obra × precio unitario), agrupado por capítulos. Sobre ese subtotal llamado **costo directo** se aplica el **AIU**:

- **A — Administración**: gastos generales de la obra (campamento, director, residente, vigilancia, servicios). Típico: 8-15 %.
- **I — Imprevistos**: reserva de contingencia para riesgos identificados. Típico: 2-5 %.
- **U — Utilidad**: margen del contratista. Típico: 5-10 %.

En Colombia, los APUs de referencia los publica **INVIAS** para 140 provincias y el **IDU** para Bogotá. Estas referencias son críticas para licitaciones públicas y para validar ofertas privadas. El sistema de control presupuestal debe poder importarlos, versionarlos y compararlos.

### 3.3 PMBOK aplicado al control presupuestal

La *Guía del PMBOK* del Project Management Institute dedica un área completa —**Gestión de los Costos del Proyecto**— a la dimensión presupuestal. Los procesos que define son:

1. **Planificar la gestión de costos**: cómo se estimará, presupuestará, gestionará y controlará el costo.
2. **Estimar los costos**: aproximar los recursos monetarios necesarios para completar las actividades.
3. **Determinar el presupuesto**: sumar los costos estimados para establecer una línea base de costo autorizada (la baseline).
4. **Controlar los costos**: monitorear el estado del proyecto para actualizar los costos y gestionar cambios sobre la línea base.

Sobre el cuarto proceso se apoya todo sistema de control presupuestal: la línea base autorizada no cambia salvo por **control integrado de cambios**, que en construcción se manifiesta como *otrosíes*, *actas de modificación* y *reformas presupuestales*. Cada uno de esos cambios es, desde una óptica de dominio, un evento que modifica el agregado *Presupuesto* de forma auditable.

PMBOK también introduce el concepto de **reserva de gestión** (separada de los imprevistos del AIU) como el colchón que el *sponsor* reserva para riesgos no identificados. El sistema debe permitir visualizarla, consumirla y reportar su saldo.

### 3.4 Earned Value Management (EVM)

El *Earned Value Management* —Gestión del Valor Ganado, en español— es la técnica estándar del PMBOK para integrar en un solo marco el alcance, el cronograma y el costo. Define tres valores fundamentales:

- **PV — Planned Value (Valor Planificado)**: cuánto debería haberse ejecutado a la fecha, en términos de presupuesto, según la línea base. También se llama BCWS (*Budgeted Cost of Work Scheduled*).
- **EV — Earned Value (Valor Ganado)**: cuánto *efectivamente* se ha ejecutado a la fecha, valorado a costos de la línea base. BCWP (*Budgeted Cost of Work Performed*).
- **AC — Actual Cost (Costo Real)**: cuánto ha costado realmente el trabajo ejecutado. ACWP (*Actual Cost of Work Performed*).

A partir de estos tres valores se derivan los indicadores que responden las dos preguntas críticas de cualquier gerente de obra:

**¿Estamos atrasados o adelantados?**
- **SV — Schedule Variance** = EV − PV (si < 0, atrasado)
- **SPI — Schedule Performance Index** = EV / PV (si < 1, atrasado)

**¿Estamos gastando más o menos de lo presupuestado?**
- **CV — Cost Variance** = EV − AC (si < 0, sobrecosto)
- **CPI — Cost Performance Index** = EV / AC (si < 1, sobrecosto)

Y, la proyección más útil de todas —el **EAC (Estimate At Completion)**, que estima cuánto terminará costando el proyecto si continúa al ritmo actual:

- **EAC** = BAC / CPI, donde BAC (*Budget At Completion*) es el presupuesto total aprobado.

Un buen sistema de control presupuestal debe calcular estos indicadores al cierre de cada corte de obra (semanal, quincenal o mensual) y visualizarlos con *curvas S* que superponen PV, EV y AC en el tiempo.

### 3.5 Ciclo de vida del control presupuestal

Operacionalmente, el control presupuestal en una obra sigue un ciclo que se repite en cada período de corte:

1. **Elaboración del presupuesto base**: cuantificación, APUs, AIU, aprobación.
2. **Congelación de la línea base**: se bloquea como referencia inmutable.
3. **Planeación de ejecución**: se carga el cronograma y se distribuye el presupuesto en el tiempo (curva S planeada).
4. **Captura de ejecución real**:
    - Salidas de almacén (consumo de materiales)
    - Nómina y asistencia (mano de obra)
    - Actas parciales de contratos y subcontratos
    - Causaciones contables
5. **Medición de avance físico**: el residente reporta cantidades ejecutadas por ítem.
6. **Cálculo de EV, AC, indicadores y proyecciones**.
7. **Gestión de cambios**: adicionales, otrosíes, reformas, ajustes.
8. **Cierre de corte y reporte a gerencia**.

El sistema debe soportar cada uno de estos pasos con trazabilidad total: quién reportó qué, cuándo, con qué soporte documental y bajo qué aprobaciones.

### 3.6 Indicadores clave y buenas prácticas

Adicionalmente a los indicadores EVM, la práctica de obra maneja otros indicadores relevantes:

- **% de ejecución física por ítem** (cantidad ejecutada / cantidad contratada)
- **% de ejecución económica por capítulo** (costo incurrido / presupuesto del capítulo)
- **Curva S** (acumulado planeado vs. acumulado real)
- **Rendimientos reales vs. APU** (señal temprana de error en rendimientos)
- **Variación de precios unitarios de insumos** (especialmente acero, cemento, combustible)
- **Saldos de contratos y subcontratos** (contratado − ejecutado − facturado)
- **Saldo de imprevistos consumido vs. disponible**

Las buenas prácticas incluyen: cerrar cortes en fechas fijas y sin retrasos, separar claramente ajustes de precios de ajustes de cantidades, documentar cada modificación con soporte, y mantener versiones históricas consultables del presupuesto en cada fecha relevante (firmas de contratos, otrosíes, cortes). Esta última práctica es precisamente la que hace que *Event Sourcing* sea un encaje natural: la capacidad de reconstruir el estado en cualquier punto del tiempo deja de ser un problema.

---

## Parte II — Análisis del mercado de software

Este capítulo revisa las herramientas más usadas en el mercado latinoamericano e hispano para la elaboración y control de presupuestos de obra. Se privilegian las soluciones especializadas sobre los ERP genéricos.

### 4.1 SINCO ERP / SINCO ADPRO (Colombia)

SINCO es un ERP colombiano enfocado exclusivamente en constructoras, inmobiliarias y concesionarios viales. Según su documentación oficial, el sistema cuenta con 14 módulos que cubren finanzas, presupuesto, almacén, contratos, nómina, compras, inventarios y sala de ventas, y reporta más de 2.543 empresas clientes en la región.

El módulo específico de presupuesto es **SINCO ADPRO** (Administración de Proyectos de Construcción), que ofrece:

- Elaboración de presupuestos por tipos de costo, capítulos, niveles, subcapítulos, actividades, ítems (APU), insumos, categorías (aceros, concretos, maderas) y tipos de insumo (materiales, equipos, mano de obra).
- Validación del presupuesto frente a la ejecución y generación automática de ajustes a partir de reformas aprobadas.
- Submódulo **Presupuestos EDT** alineado con sistemas de clasificación globales como **MasterFormat**, **UniFormat** y **OmniClass**.
- Integración con metodología BIM.
- Automatización de la causación contable a partir de cortes de obra, entradas de almacén, avance por contratos y traslados.

**Fortalezas**: profundo conocimiento del dominio constructor colombiano, integración total con el ERP (contabilidad, tesorería, nómina, almacén), catálogo regional de insumos.

**Debilidades percibidas**: curva de aprendizaje alta, interfaz monolítica tradicional, modelo comercial de licenciamiento que dificulta la adopción en empresas pequeñas, baja apertura a integraciones vía API.

### 4.2 Presto (España / global)

**Presto**, desarrollado por RIB Spain, es un software de presupuestos y mediciones enfocado en **BIM** para edificación y obra civil. Se vende como un programa integrado de gestión del costo y del tiempo para proyectistas, directores de obra, *project managers* y constructoras.

**Fortalezas**: integración nativa con modelos BIM (Revit, IFC), cuantificaciones automáticas desde el modelo, reportes flexibles, fuerte presencia en España y adopción creciente en LatAm para proyectos BIM.

**Debilidades percibidas**: enfocado al presupuesto y control de obra, pero no reemplaza el ERP; requiere integración con sistemas contables y de nómina.

### 4.3 OPUS (México / LatAm)

**OPUS**, de la empresa ECOSOFT, acumula más de tres décadas en el mercado mexicano y latinoamericano. Su foco principal es la planeación, programación y control de proyectos con énfasis en **precios unitarios**.

**Fortalezas**: interfaz moderna e intuitiva (según comparativas del sector mexicano), flexibilidad en personalización de reportes, potente integración con Excel, base instalada muy grande (>200.000 empresas reportadas).

**Debilidades percibidas**: fuerte orientación al formato mexicano de APU y AIU, menos alineado con la normativa colombiana, módulos administrativos menos robustos que los de un ERP completo.

### 4.4 Neodata (México)

**Neodata** es otro referente mexicano para presupuestos de obra, con amplia base de usuarios en el sector público y privado.

**Fortalezas**: flexibilidad para adaptarse a diferentes tipos de proyectos, herramientas para costos unitarios, planificación de recursos y gestión de proyectos.

**Debilidades percibidas**: fuerte competencia con OPUS en México; fuera de ese mercado su adopción es limitada.

### 4.5 Primavera P6 y SAP PS

**Oracle Primavera P6** es el estándar de facto para proyectos de gran escala (infraestructura, minería, oil & gas). No es un software de presupuesto en sentido estricto: es un planificador, pero permite cargar costos al cronograma y hacer análisis de EVM. Se suele combinar con un sistema de presupuesto upstream (Presto, OPUS, SAP) y funciona como el motor de EVM corporativo.

**SAP PS** (*Project System*) es el módulo de proyectos dentro de SAP ERP. Permite modelar proyectos con WBS, presupuestar cada elemento y capturar costos reales desde los otros módulos de SAP (MM, FI, HR). Es potente para empresas ya con SAP, pero tiene un costo y complejidad fuera del alcance de la mayoría de constructoras medianas.

### 4.6 Cuadro comparativo

| Característica                          | SINCO ADPRO | Presto | OPUS | Neodata | Primavera P6 | SAP PS |
|-----------------------------------------|:-----------:|:------:|:----:|:-------:|:------------:|:------:|
| APU con estructura latinoamericana      | Sí          | Parcial| Sí   | Sí      | No           | No     |
| AIU configurable                         | Sí          | Sí     | Sí   | Sí      | No nativo    | Parcial|
| Integración BIM                          | Sí (ADPRO)  | Nativa | Sí   | Sí      | No nativa    | No nativa |
| EVM (curva S, CPI, SPI)                  | Parcial     | Sí     | Parcial| Parcial| Sí (fuerte) | Sí     |
| ERP integrado (contabilidad, nómina)    | **Sí**      | No     | No   | No      | No           | Sí     |
| Control de contratos y subcontratos      | Sí          | Parcial| Sí   | Parcial | No nativo    | Sí     |
| APIs abiertas / integraciones modernas   | Limitado    | Medio  | Limitado| Limitado| Medio     | Medio  |
| Modelo SaaS / cloud-first                | Parcial     | Parcial| No   | No      | Sí           | Sí     |
| Audit trail granular                     | Medio       | Medio  | Medio| Medio   | Alto         | Alto   |
| Precio (orden de magnitud)               | Medio-alto  | Medio  | Medio| Medio   | Alto         | Muy alto |

### 4.7 Brechas observadas y oportunidades

Al revisar las herramientas existentes, surgen varias brechas que un producto nuevo, bien posicionado, podría explotar:

- **Auditabilidad granular a nivel de evento**. Los sistemas tradicionales guardan el estado actual y registran bitácoras limitadas. Reconstruir el presupuesto exacto de una fecha pasada suele ser imposible o requiere backups ad-hoc. Un sistema *Event Sourced* entrega esto nativamente.
- **APIs abiertas y eventos hacia afuera**. Integrar un ERP tradicional con otras herramientas (BIM, tableros Power BI, flujos de aprobación en Monday/Asana, facturación electrónica) suele ser una lucha. Un sistema EDA expone *outbound events* que terceros consumen sin fricción.
- **Experiencia de usuario moderna**. La mayoría de las herramientas se ven y se sienten como software de los años 2000. Un producto con UX web moderno, atajos de teclado, colaboración en vivo y *real-time updates* tiene una oportunidad comercial clara.
- **Análisis what-if**. Simular escenarios ("¿qué pasa con el EAC si el acero sube 15 %?") es débil en las herramientas actuales.
- **Precio accesible para constructoras medianas y pequeñas**. El segmento de proyectos de entre 500 millones y 10.000 millones de pesos está mal atendido: los sistemas grandes son caros y los pequeños son Excel.

---

## Parte III — Arquitectura técnica: Event Sourcing y EDA

### 5.1 Conceptos fundamentales

Esta sección asume familiaridad con DDD (*Domain-Driven Design*). Los términos se usan como los define Eric Evans (*Domain-Driven Design*, 2003) y Vaughn Vernon (*Implementing Domain-Driven Design*, 2013).

Tres patrones conforman la base arquitectónica del proyecto:

- **Event Sourcing**: el estado del sistema se deriva de una secuencia inmutable de eventos de dominio. El *event store* es la única fuente de verdad.
- **CQRS — Command Query Responsibility Segregation**: separación explícita entre el lado de escritura (comandos que cambian el estado) y el lado de lectura (consultas sobre proyecciones materializadas).
- **Event-Driven Architecture (EDA)**: los servicios se comunican entre sí publicando y consumiendo eventos a través de un *broker* (Kafka, RabbitMQ, AWS EventBridge), en lugar de llamarse sincrónicamente por HTTP.

Los tres se pueden aplicar de forma independiente, pero en la práctica se combinan porque se refuerzan.

### 5.2 Event Sourcing en detalle

En un sistema tradicional —*CRUD / state-oriented*— cuando se modifica un presupuesto se hace un `UPDATE` sobre una tabla, y el estado anterior se pierde (a menos que se use auditoría). En un sistema *Event Sourced*, lo que se persiste es el **evento de cambio**:

```
Stream: Presupuesto#8f2a
  ├─ PresupuestoCreado        (2026-01-15)  { proyectoId, moneda, nombre, ... }
  ├─ CapituloAgregado          (2026-01-15)  { codigo: "02", nombre: "Cimentación" }
  ├─ ItemAgregado              (2026-01-16)  { capituloCodigo: "02.01.03", cantidad: 450, ... }
  ├─ PresupuestoAprobado       (2026-01-28)  { aprobadoPor: "user/42", baseline: true }
  ├─ OtrosiRegistrado          (2026-02-10)  { monto: 45000000, concepto: "...", aprobadoPor }
  └─ ItemCantidadAjustada      (2026-02-12)  { itemId: "...", cantidadNueva: 520 }
```

El estado actual del presupuesto se obtiene aplicando los eventos en orden sobre un agregado en memoria. El estado *en cualquier fecha del pasado* se obtiene igual, deteniendo la aplicación de eventos en esa fecha.

Las ventajas críticas para el dominio presupuestal son:

- **Audit trail gratuito**: cada cambio es un evento con quién, cuándo, qué y por qué.
- **Time travel**: "dame el presupuesto tal como estaba el 15 de febrero" es una consulta nativa.
- **Reconstrucción y re-proyección**: si se descubre un error en un reporte, se regenera la proyección desde el *event stream*.
- **Integración natural con EDA**: los eventos son, a la vez, la persistencia y el canal de comunicación entre servicios.

Las complicaciones que introduce son:

- **Versionado de eventos (schema evolution)**: un `PresupuestoCreado` de 2026 puede tener campos diferentes en 2028. Hay que planear *upcasters*, nuevos tipos de evento o doble publicación.
- **Snapshots**: en agregados con miles de eventos, recargar todo el stream para cada operación es caro. Se guardan snapshots cada N eventos.
- **Consultas ad-hoc**: el *event store* no es consultable por SQL tradicional; hay que proyectar a modelos de lectura.

### 5.3 CQRS como complemento natural

CQRS separa el modelo de escritura del modelo de lectura. En Event Sourcing esta separación es casi obligatoria: escribes eventos, pero lees desde **proyecciones** que materializan el estado en formas optimizadas para cada consulta.

Un sistema presupuestal típicamente tiene estas proyecciones:

- **Presupuesto detallado** (árbol completo de capítulos, subcapítulos, ítems, APU) — para la vista principal.
- **Ejecución por capítulo** (contratado, ejecutado, facturado, saldo) — para el tablero de control.
- **Curva S** (PV, EV, AC por fecha) — para EVM.
- **Búsqueda de ítems por texto** (full-text) — para agregar actividades rápidamente.
- **Variaciones de insumos** (precios históricos por referencia) — para análisis.
- **Saldos de contratos** — para tesorería y compras.

Cada proyección se alimenta suscribiéndose al *event stream* y puede estar en un motor distinto: PostgreSQL para la jerarquía, Elasticsearch para búsqueda, Redis para tableros en tiempo real, ClickHouse o TimescaleDB para series temporales.

### 5.4 Event-Driven Architecture

EDA es la dimensión de *integración*: cómo los distintos bounded contexts (presupuesto, compras, ejecución, contabilidad) se enteran de los cambios relevantes de los demás sin acoplarse fuertemente.

Ejemplo de flujo inter-contexto:

1. El usuario registra en **Compras** una *orden de compra* por cemento.
2. *Compras* publica `OrdenCompraEmitida` en el bus.
3. *Presupuesto* consume el evento, lo asocia al APU correspondiente, actualiza el compromiso del rubro y proyecta un nuevo saldo disponible.
4. *Contabilidad* consume el mismo evento y causa un pasivo.
5. *Notificaciones* avisa al residente.

Apache Kafka es la referencia de la industria para este tipo de *event backbone*. En finanzas se usa masivamente: Netflix, por ejemplo, construyó sobre Kafka una plataforma de datos financieros para *tracking* de gastos y reportes financieros. ironSource utiliza la API de *Kafka Streams* específicamente para casos de *budget management, monitoring and alerting* en tiempo real.

### 5.5 Por qué este stack encaja con control presupuestal

Resumiendo el *fit* entre el dominio y los patrones:

| Necesidad del dominio | Patrón que la resuelve |
|---|---|
| Auditar quién cambió qué y cuándo, con soporte documental | Event Sourcing (cada evento lleva metadata de autor, fecha, correlación) |
| Reconstruir el presupuesto aprobado en una fecha histórica | Event Sourcing (*time travel*) |
| Certificar a un auditor externo que la línea base es inmutable | Event Sourcing (eventos *append-only*) |
| Tableros en tiempo real con múltiples vistas (financiera, física, contractual) | CQRS (una proyección por vista) |
| Integrar con ERP, BIM, compras, almacén, nómina | EDA (eventos *outbound* que terceros consumen) |
| Escalar lecturas (muchos consultan, pocos escriben) | CQRS (proyecciones escalan independiente) |
| Análisis *what-if* y simulaciones | Event Sourcing (rehidratar agregado con eventos simulados) |

### 5.6 Modelo de dominio propuesto

A nivel de modelo estratégico (DDD), se sugieren estos **bounded contexts** iniciales:

- **Presupuestación** — elaboración, aprobación, versionado, modificaciones.
- **Ejecución y cortes de obra** — captura de avance físico y económico.
- **Contratos** — proveedores, subcontratos, actas.
- **Catálogo de insumos y APUs** — maestros compartidos, importación INVIAS/IDU.
- **Reportes y analítica** — proyecciones de EVM, curvas S, alertas.

Dentro de *Presupuestación*, los **agregados** principales son:

- **Presupuesto** (aggregate root) — controla el árbol completo y las reglas de consistencia: no se modifica tras ser aprobado salvo por eventos de modificación; el total es la suma de capítulos, etc.
- **APU** (aggregate root separado) — cuando un APU cambia, dispara revaluaciones en los presupuestos que lo usan (vía eventos, no referencias directas).
- **Modificación** (aggregate root) — un otrosí, adicional o reforma que, al aprobarse, emite eventos sobre uno o más presupuestos.

### 5.7 Catálogo inicial de eventos

A modo de punto de partida —no exhaustivo— los eventos de dominio iniciales podrían ser:

**Contexto Presupuestación**

- `PresupuestoBorradorCreado`
- `CapituloAgregado`, `CapituloRenombrado`, `CapituloEliminado`
- `ItemAgregado`, `ItemCantidadAjustada`, `ItemPrecioActualizado`, `ItemEliminado`
- `AIUConfigurado`
- `PresupuestoSometidoAAprobacion`
- `PresupuestoAprobado` (establece la *baseline*)
- `PresupuestoRechazado`
- `OtrosiRegistrado`, `OtrosiAprobado`
- `ReformaAdicionalRegistrada`, `ReformaAdicionalAprobada`
- `PresupuestoCerrado`

**Contexto Ejecución**

- `CorteDeObraIniciado`
- `AvanceFisicoReportado`
- `CorteDeObraCerrado`
- `CostoRealImputado`
- `IndicadoresEVMCalculados`

**Contexto Contratos**

- `ContratoFirmado`
- `ActaParcialAprobada`
- `ContratoLiquidado`

**Contexto Catálogo**

- `InsumoCreado`, `InsumoPrecioActualizado`
- `APUCreado`, `APURendimientoAjustado`

Cada evento debe llevar, además del payload, metadata estándar: `eventId`, `aggregateId`, `aggregateVersion`, `occurredAt`, `userId`, `correlationId`, `causationId`.

### 5.8 Stack tecnológico recomendado

Sin pretender ser prescriptivo, una combinación probada para este tipo de sistema sería:

- **Lenguaje/Framework**: TypeScript + NestJS, o Java/Kotlin + Axon Framework, o C# + EventStoreDB + MediatR. Cualquiera de los tres tiene comunidad fuerte.
- **Event Store**: EventStoreDB (especializado), Kurrent, o PostgreSQL con tabla `events` bien diseñada (viable y más barato para iniciar).
- **Bus de eventos**: Apache Kafka o Redpanda (compatible con Kafka). Alternativa liviana: RabbitMQ, AWS SNS/SQS.
- **Proyecciones / read models**: PostgreSQL (relacional), Elasticsearch (búsqueda), Redis (cache y tiempo real), TimescaleDB (curva S y series EVM).
- **API**: REST + GraphQL para lecturas complejas; WebSocket / SSE para actualizaciones en vivo.
- **Frontend**: React/Next.js, con una librería de tablas potente (AG Grid, TanStack Table) dada la naturaleza tabular del presupuesto.
- **Observabilidad**: OpenTelemetry para tracing distribuido (crítico cuando hay muchos eventos en juego), Grafana + Loki + Prometheus.
- **Infraestructura**: Kubernetes o un PaaS (Railway, Fly.io) para empezar sin complejidad operativa.

### 5.9 Desafíos a anticipar

Event Sourcing no es gratuito. El equipo debe entrar con los ojos abiertos a los siguientes problemas:

- **Consistencia eventual entre proyecciones**. Una acción de escritura no se ve inmediatamente reflejada en la lectura; hay un *lag* de milisegundos a segundos. La UX debe diseñarse para esto (optimistic updates, reintentos, indicadores de "procesando").
- **Versionado de eventos**. Hay que decidir desde el día uno la política: *upcasting*, *weak schema*, *double-write*, o event types versionados. Los errores aquí son muy caros de revertir.
- **Snapshots**. Cuándo generarlos, cada cuántos eventos, cómo versionarlos.
- **Idempotencia de consumidores**. Los eventos pueden entregarse más de una vez; los consumidores deben ser idempotentes.
- **Ordenamiento**. Kafka garantiza orden por partición, no global. El diseño de la clave de partición importa (ej. `presupuestoId`).
- **Testing**. Testear un sistema event-sourced requiere estrategias específicas (*given-events, when-command, then-events*). La inversión en tooling de pruebas paga temprano.
- **Curva de aprendizaje**. El equipo que viene de CRUD necesita tiempo para interiorizar estos patrones. Empezar con un *walking skeleton* ayuda.
- **Overkill para el MVP**. Hay que resistir la tentación de modelar todo el dominio como event-sourced desde el día uno. Solo los agregados con alto valor auditable lo ameritan.

---

## Parte IV — Recomendaciones para el proyecto Sinco Presupuesto

### 6.1 Bounded contexts sugeridos

Para un MVP con alcance manejable, se sugiere partir con dos bounded contexts event-sourced y el resto con CRUD tradicional + publicación de eventos hacia afuera:

- **Event-sourced**: Presupuestación, Modificaciones.
- **CRUD + outbox de eventos**: Catálogo de insumos y APUs, Ejecución, Usuarios y permisos.
- **Proyecciones**: Reportes y analítica.

Esta separación da trazabilidad total donde más importa (línea base y modificaciones) sin pagar el costo de ES en todos los contextos.

### 6.2 Alcance del MVP

Se sugiere que el MVP incluya:

1. Creación y edición de presupuestos con jerarquía completa (capítulos → subcapítulos → ítems → APU → insumos).
2. Configuración de AIU.
3. Flujo de aprobación con *baseline* inmutable.
4. Registro de otrosíes y reformas con trazabilidad.
5. Importación de APUs de referencia (INVIAS/IDU en CSV o Excel).
6. Dashboard básico con curva S (PV simple, sin EV/AC todavía).
7. Exportación a Excel / PDF.
8. API REST para consulta externa.
9. Log de auditoría completo (es gratuito con ES).

Deliberadamente *fuera* del MVP:
- Integración con ERP/contabilidad.
- Módulo de contratos completo.
- EVM completo con EV y AC (requiere captura fina de ejecución).
- BIM.
- Multi-moneda y multi-compañía.

### 6.3 Roadmap sugerido

**Fase 0 — Descubrimiento (2-4 semanas)**: *Event Storming* con un gerente de obra real. Catálogo de eventos validado. Mockups de UI. Decisión final de stack.

**Fase 1 — Walking skeleton (4-6 semanas)**: crear un presupuesto mínimo, agregar un ítem, aprobarlo, consultarlo, auditarlo. End-to-end funcionando con ES + un solo read model.

**Fase 2 — MVP funcional (3-4 meses)**: el alcance descrito en 6.2.

**Fase 3 — Piloto (2-3 meses)**: uso real en un proyecto pequeño o mediano. Retroalimentación, ajuste de UX, detección de gaps.

**Fase 4 — Ejecución y EVM (siguientes 3-4 meses)**: captura de ejecución, curvas S completas, indicadores EVM, alertas.

**Fase 5 — Integraciones y escala**: conectores ERP, API pública, multi-tenant.

---

## 7. Conclusiones

El control presupuestal en proyectos de construcción es un dominio maduro, bien documentado y con herramientas establecidas. La estructura jerárquica EDT + APU + AIU, el marco del PMBOK y la técnica de *Earned Value Management* conforman un cuerpo de conocimiento sólido sobre el que se puede construir. Desde la perspectiva metodológica, el reto no es reinventar: es digitalizar bien lo que ya existe y eliminar la fricción que hoy sufren las constructoras con Excel o con sistemas legados.

Desde la perspectiva de mercado, el panorama latinoamericano está dominado por SINCO en Colombia, OPUS y Neodata en México, Presto para los flujos BIM, y Primavera/SAP para proyectos grandes. Todos atienden bien las necesidades funcionales básicas. La oportunidad está en lo no funcional: auditabilidad granular, APIs abiertas, experiencia de usuario moderna, colaboración en tiempo real y análisis *what-if*. Estos son, casualmente, los atributos que una arquitectura bien diseñada sobre Event Sourcing y EDA entrega como efecto lateral.

Desde la perspectiva arquitectónica, el encaje entre el dominio presupuestal y los patrones Event Sourcing + CQRS + EDA es, probablemente, uno de los mejores que ofrece el repertorio de arquitectura moderna. El presupuesto es inherentemente *event-shaped*: una línea base que cambia a lo largo del tiempo por decisiones discretas (aprobaciones, otrosíes, reformas), que deben quedar registradas de forma inmutable y que deben poder reconstruirse en cualquier fecha. Lo mismo aplica a la ejecución: cada consumo, cada corte, cada acta es un evento. Sin embargo, la complejidad inherente a estos patrones exige disciplina: un MVP pequeño, un equipo con experiencia en DDD, y una estrategia clara de versionado de eventos desde el día uno.

La recomendación final es avanzar con cautela ambiciosa: event-sourcing en los contextos donde la auditabilidad es innegociable, CRUD tradicional con publicación de eventos en los demás, y una obsesión por entregar valor de negocio real en cada iteración. El mercado no necesita otro ERP lento y caro; necesita una herramienta que respete el conocimiento del sector y esté construida con la arquitectura del siglo XXI.

---

## 8. Referencias

**Metodología y PMBOK**
- Project Management Institute. *Guía de los Fundamentos para la Dirección de Proyectos (PMBOK)*. [PDF en Topodata](https://topodata.com/wp-content/uploads/2019/10/PMBOK_Guide5th_Spanish.pdfJOFO.pdf)
- Wrike. *What Is PMBOK in Project Management?* [wrike.com](https://www.wrike.com/project-management-guide/faq/what-is-pmbok-in-project-management/)
- Editeca. *Guía PMBOK: Qué es y por qué es necesaria en la Gestión de Proyectos de Construcción*. [editeca.com](https://editeca.com/guia-pmbok-que-es-gestion-de-proyectos-de-construccion/)
- EALDE. *Presupuesto de proyectos según el PMBOK: Cómo elaborarlo*. [ealde.es](https://www.ealde.es/elementos-presupuesto-proyecto/)
- PMI. *La gestión del valor ganado y su aplicación*. [pmi.org](https://www.pmi.org/learning/library/es-las-mejores-practicas-de-gestion-del-valor-ganado-7045)

**APU y normativa Colombia**
- PresuCosto. *Guía Completa APU Colombia 2026*. [presucosto.com](https://presucosto.com/guia-apu-colombia)
- INVIAS. *Análisis de precio unitario – APU*. [invias.gov.co](https://www.invias.gov.co/index.php/archivo-y-documentos/analisis-precios-unitarios/12099-glosario-analisis-de-precios-unitarios-de-referencia-2021/file)
- INVIAS. *Análisis de Precios Unitarios (APU) Regionalizados*. [invias.gov.co](https://www.invias.gov.co/index.php/informacion-institucional/hechos-de-transparencia/analisis-de-precio-unitarios)
- IDU. *Portafolio económico*. [idu.gov.co](https://www.idu.gov.co/page/siipviales/economico/portafolio)
- Data Construcción. *Los Análisis de Precios Unitarios*. [dataconstruccion.com](https://www.dataconstruccion.com/blog/analisis-de-precios-unitarios-apus)

**Software comercial de presupuesto**
- SINCO ERP. *Sitio oficial*. [sinco.co](https://www.sinco.co/)
- SINCO ADPRO. *Software de Presupuesto y Control de Obras*. [sinco.co](https://www.sinco.co/soluciones/gestion-del-negocio/administracion-de-proyectos-de-construccion)
- Blog SINCO. *Integración BIM y Presupuesto EDT en SINCO ADPRO*. [blog.sinco.co](https://blog.sinco.co/integraci%C3%B3n-de-la-metodolog%C3%ADa-bim-y-el-presupuesto-edt-en-sinco-adpro)
- ComparaSoftware Colombia. *Sinco ERP: Precios*. [comparasoftware.co](https://www.comparasoftware.co/sinco-erp)
- AnalisisDePreciosUnitarios. *Neodata o Opus: ¿Cuál es Mejor? Guía Comparativa México 2025*. [analisisdepreciosunitarios.com](https://analisisdepreciosunitarios.com/que-es-mejor-opus-o-neodata)
- AnalisisDePreciosUnitarios. *Opus Costos 2025: Guía de Precios, Módulos y Licencias*. [analisisdepreciosunitarios.com](https://analisisdepreciosunitarios.com/cuanto-cuesta-el-programa-opus)
- Foco en Obra. *Top 10 Software Presupuestos Obra de Construcción 2026*. [focoenobra.com](https://focoenobra.com/blog/programas-presupuestos-obra/)
- StelOrder. *Mejores programas para hacer Presupuestos de Obra 2026*. [stelorder.com](https://www.stelorder.com/blog/mejores-programas-presupuestos-de-obra/)

**Event Sourcing, CQRS y EDA**
- Microsoft Learn. *CQRS Pattern — Azure Architecture Center*. [learn.microsoft.com](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs)
- Microsoft Learn. *Event Sourcing Pattern — Azure Architecture Center*. [learn.microsoft.com](https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing)
- Microservices.io. *Pattern: Event sourcing*. [microservices.io](https://microservices.io/patterns/data/event-sourcing.html)
- Mia-Platform. *Understanding Event Sourcing and CQRS Pattern*. [mia-platform.eu](https://mia-platform.eu/blog/understanding-event-sourcing-and-cqrs-pattern/)
- Upsolver. *CQRS, Event Sourcing Patterns and Database Architecture*. [upsolver.com](https://www.upsolver.com/blog/cqrs-event-sourcing-build-database-architecture)
- DEV Community. *Event-Driven Architecture, Event Sourcing, and CQRS: How They Work Together*. [dev.to](https://dev.to/yasmine_ddec94f4d4/event-driven-architecture-event-sourcing-and-cqrs-how-they-work-together-1bp1)
- GeeksforGeeks. *Difference Between CQRS and Event Sourcing*. [geeksforgeeks.org](https://www.geeksforgeeks.org/system-design/difference-between-cqrs-and-event-sourcing/)

**EDA aplicada a finanzas**
- Confluent. *Event-Driven Architecture (EDA): A Complete Introduction*. [confluent.io](https://www.confluent.io/learn/event-driven-architecture/)
- Apache Kafka. *Powered By*. [kafka.apache.org](https://kafka.apache.org/powered-by/)
- Infosys. *White Paper: Event Driven Microservices with Apache Kafka*. [infosys.com](https://www.infosys.com/industries/financial-services/insights/documents/event-driven-microservices.pdf)
- Redpanda. *Event-driven architectures with Apache Kafka*. [redpanda.com](https://www.redpanda.com/guides/kafka-use-cases-event-driven-architecture)
- BOS Fintech. *Game changer in banking: the secrets of Event-Driven Architecture*. [bosfintech.com](https://bosfintech.com/game-changer-in-banking-the-secrets-of-event-driven-architecture/)
- Medium (Lukas Niessen). *Event Sourcing, CQRS and Micro Services: Real FinTech Example*. [medium.com](https://lukasniessen.medium.com/this-is-a-detailed-breakdown-of-a-fintech-project-from-my-consulting-career-9ec61603709c)

---

*Fin del documento.*
