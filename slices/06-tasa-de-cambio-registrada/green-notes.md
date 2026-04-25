# Green notes — Slice 06 — RegistrarTasaDeCambio

**Implementador:** green
**Fecha:** 2026-04-24
**Estado:** implementación completa — 17 casos rojos → verdes (11 escenarios spec §6.1–§6.11).

---

## 1. Archivos modificados

Solo un archivo de producción tocado en este paso green. Todos los demás artefactos del slice (excepciones, comando, evento, record `RegistroDeTasa`, esqueleto del agregado con propiedad `Registros`) ya estaban presentes con la firma exacta tras el paso red.

### Archivo: `src/SincoPresupuesto.Domain/CatalogosDeTasas/CatalogoDeTasas.cs`

**Cambios:**

#### `Crear(cmd, ahora)` — factory sobre stream vacío
- Delega a un helper privado `ValidarYConstruir(cmd, ahora)` que aplica las cuatro precondiciones / normalizaciones y devuelve el evento.
- El cuerpo público es un único `return ValidarYConstruir(cmd, ahora);`.

#### `Ejecutar(cmd, ahora)` — método de instancia sobre stream existente
- Idéntico al body de `Crear`: `return ValidarYConstruir(cmd, ahora);`.
- A diferencia de `ConfiguracionTenant.Ejecutar` (que SIEMPRE lanza `TenantYaConfiguradoException`), aquí registra otra tasa más — coherente con INV-CT-1 de la spec §5 / §6.3 (last-write-wins permitido, no idempotencia anti-duplicado).
- No añade validación de "tupla `(desde, hacia, fecha)` ya existe" — la spec §7 lo declara explícitamente permitido (corrección manual o re-registración deliberada).

#### `ValidarYConstruir(cmd, ahora)` — helper privado estático
Compartido entre `Crear` y `Ejecutar`. Orden de validación exacto al firmado en spec §4 / §12.4:

1. `ArgumentNullException.ThrowIfNull(cmd)`.
2. **PRE-1**: `cmd.MonedaDesde == cmd.MonedaHacia` → `MonedasIgualesEnTasaException(cmd.MonedaDesde)`.
3. **PRE-2**: `cmd.Tasa <= 0m` → `TasaDeCambioInvalidaException(cmd.Tasa)`.
4. **PRE-3**: `cmd.Fecha > DateOnly.FromDateTime(ahora.UtcDateTime)` → `FechaDeTasaEnElFuturoException(cmd.Fecha, hoy)`. Inclusiva del día de hoy (cubre §6.9 caso límite).
5. **PRE-4 normalización** (no falla):
   - `Fuente` null/vacío/whitespace → `null`. Trim si tiene contenido.
   - `RegistradoPor` null/vacío/whitespace → `"sistema"`. Trim si tiene contenido.
6. Devuelve `new TasaDeCambioRegistrada(...)` con los campos del comando + `RegistradaEn = ahora` + valores normalizados.

#### `Apply(TasaDeCambioRegistrada e)` — fold
- `ArgumentNullException.ThrowIfNull(e)`.
- Construye un `RegistroDeTasa` espejo del evento y lo hace `_registros.Add(...)`.
- No asigna `Id` (la spec §6.11 / red-notes §4.3 deciden no asertarlo en tests; queda como `Guid.Empty` y Marten lo asigna en runtime — patrón slice 02).

---

## 2. Impulsos de refactor NO implementados

### 2.1 Duplicación entre `Crear` y `Ejecutar` (candidato firme para refactorer)

**Impulso:** ambos métodos públicos son una sola línea (`return ValidarYConstruir(cmd, ahora);`). El helper privado existe ya — la duplicación está aplastada al mínimo posible compatible con dos puntos de entrada distintos.

**Justificación de no-acción adicional:**
- La spec firma dos firmas públicas distintas: `static Crear` (factory) y `Ejecutar` (instancia). El nombre y la posición jerárquica importan al consumidor (Marten / handler aplicativo) — patrón slice 02.
- La forma actual ya extrae la lógica común a un solo helper, así que no hay duplicación de validaciones en sí. Lo único duplicado es la línea de delegación, que es ruido aceptable.
- **Candidato refactorer (variante baja-prioridad):** el patrón "Crear factory / Ejecutar instancia / helper compartido" se repite ya en dos agregados (`ConfiguracionTenant` y `CatalogoDeTasas`). Si surge un tercer caso, vale la pena extraer un patrón base (p.ej. `EventSourcedAggregate<TState, TCommand, TEvent>`). Por ahora, dos casos no justifican abstracción — disparador explícito en el momento del tercero.

### 2.2 Asignación de `Id` en `Apply` (candidato refactorer ligero)

**Impulso:** el comentario de `Id` en el código menciona "Igual al stream-id bien-conocido `CatalogoDeTasasStreamId.Value`", pero `Apply` no lo asigna. Marten lo hará en runtime.

**Justificación de no-acción:**
- El test §6.11 NO asierta sobre `agg.Id` (decisión documentada en red-notes §4.3 — el red optó por NO acoplar el dominio al `CatalogoDeTasasStreamId` que vive en `Application`). Si lo asignara aquí, el dominio dependería de un constante de Application.
- Mismo patrón que `ConfiguracionTenant.Apply` — no asigna `Id`.
- **Candidato refactorer:** si en futuro slice (06b infra-wire) el `Apply` necesita asignar `Id`, el ajuste será trivial. Disparador: introducción de la proyección `TasasDeCambioVigentes` o tests de integración que lo exijan.

### 2.3 `RegistroDeTasa` espejo de `TasaDeCambioRegistrada`

**Impulso:** el record `RegistroDeTasa` tiene exactamente los mismos siete campos que el evento `TasaDeCambioRegistrada`. Podría reutilizarse el evento como entrada de la lista directamente, evitando la duplicación de tipos.

**Justificación de no-acción:**
- La spec §12.4 firma explícitamente el record acompañante `RegistroDeTasa` y la propiedad `Registros: IReadOnlyList<RegistroDeTasa>`. Los tests §6.11 verifican `BeEquivalentTo(new RegistroDeTasa(...))`. Cambiar el tipo rompería los tests.
- Conceptualmente, separar "evento del stream" de "vista del agregado" es saludable a futuro: si el evento añade campos versionados (e.g. `IdempotencyKey`), el `RegistroDeTasa` puede mantenerse estable.
- **Candidato refactorer (baja prioridad):** si el record nunca diverge del evento durante varios slices, fusionarlos es válido. Disparador: PR de simplificación deliberada.

### 2.4 Helper `ValidarYConstruir` podría volverse público o expresarse como method-group

**Impulso:** el helper privado podría exponerse como `protected internal` para tests directos, o expresarse con expression-bodied delegate en lugar de método con `return`.

**Justificación de no-acción:**
- Privado es lo correcto: solo es detalle de implementación de `Crear` / `Ejecutar`. Los tests ya lo ejercen indirectamente via las dos entradas públicas (incluido §6.10 que verifica que el camino `Ejecutar` aplica las mismas validaciones que `Crear`).
- Las cinco-seis líneas de body son más legibles como bloque que como expression-bodied.

---

## 3. Decisiones deliberadas de código mínimo

### 3.1 PRE-3 con `DateOnly.FromDateTime(ahora.UtcDateTime)` — UTC, no local

La spec §12.4 recomienda UTC explícitamente: "consistente con multi-tenancy global". Se siguió la recomendación. Si en futuro un tenant pide "fecha-local-del-tenant", se inyectará la zona desde el handler — fuera del scope del agregado. Cubierto por test §6.9 con `T0 = 2026-04-24T12:00:00Z` y `Hoy = DateOnly(2026, 4, 24)` (igualdad por construcción de los fixtures).

### 3.2 `Trim()` de Fuente con contenido se conserva sin tests directos

La spec §6.5 menciona el caso `"BanRep   "` → `"BanRep"` (trim conservando contenido). El test del slice solo cubre los casos vacío/whitespace/null → `null` (theory de §6.5). El `Trim()` se aplica de todas formas porque la spec §12.4 lo pide (`cmd.Fuente.Trim()`) y red-notes no lo desviaron — el comportamiento queda implementado pero no ejercitado por test. Aceptable: el código no es complejidad agregada (una línea trivial); un test futuro de la proyección o del endpoint cubrirá el caso.

### 3.3 Sin handlers / proyección / stream-id / endpoints HTTP en este green

La spec §12.5 / §12.6 / §9 describen `CatalogoDeTasasStreamId`, `TasasDeCambioVigentesProjection` y endpoints HTTP. El red explícitamente los dejó fuera (red-notes §3 "Sin tocar"). El green respeta esa frontera: implementa solo el agregado de dominio, suficiente para que los 17 tests del slice pasen. Los artefactos infra-wire son trabajo paralelo (slice 06b o seguimiento de followups #24/#28/#29).

---

## 4. Verificación

### Build
```
dotnet build
Compilación correcta.
    0 Advertencia(s)
    0 Errores
```

### Tests del slice
```
dotnet test --filter "FullyQualifiedName~Slice06"
Correctas! - Con error: 0, Superado: 17, Omitido: 0, Total: 17
```

### Suite completa (sin regresiones)
```
dotnet test
Domain.Tests:        Superado: 159 / 159  (142 previos + 17 slice 06)
Integration.Tests:   Superado:  24 /  24
```

### Cobertura del slice sobre escenarios spec §6
| Escenario | Test xUnit | Verde |
|---|---|---|
| §6.1 happy path stream vacío | `RegistrarTasaDeCambio_sobre_stream_vacio_emite_TasaDeCambioRegistrada` | sí |
| §6.2 acumulación par/fecha distintos | `..._sobre_stream_existente_con_par_distinto_emite_segundo_evento` | sí |
| §6.3 last-write-wins (INV-CT-1) | `..._re_registra_mismo_par_y_fecha_emite_segundo_evento` | sí |
| §6.4 normalización RegistradoPor | `..._con_RegistradoPor_vacio_o_whitespace_usa_sistema_como_default` (3 inline) | sí |
| §6.5 normalización Fuente | `..._con_Fuente_null_o_whitespace_emite_evento_con_Fuente_null` (3 inline) | sí |
| §6.6 PRE-1 stream vacío | `..._con_monedas_iguales_sobre_stream_vacio_lanza_MonedasIgualesEnTasa` | sí |
| §6.7 PRE-2 tasa no positiva | `..._con_tasa_no_positiva_lanza_TasaDeCambioInvalida` (3 inline) | sí |
| §6.8 PRE-3 fecha futura | `..._con_fecha_futura_lanza_FechaDeTasaEnElFuturo` | sí |
| §6.9 Fecha == hoy se acepta | `..._con_fecha_igual_a_hoy_emite_evento` | sí |
| §6.10 PRE-1 stream existente | `..._con_monedas_iguales_sobre_stream_existente_lanza_MonedasIgualesEnTasa` | sí |
| §6.11 fold | `Fold_de_TasaDeCambioRegistrada_deja_el_agregado_con_historial_en_orden` | sí |

### Cobertura de invariantes
- **INV-CT-1** (re-registración permitida) — §6.3.
- **INV-NEW-CT-2** (append-only) — observada en §6.2 y §6.3 (el evento previo permanece, ningún borrado).
- **INV-13** (Dinero/Moneda en eventos) — vacua (la `Tasa` es `decimal` factor, no monto; el evento no contiene cantidades monetarias).
- **INV-14 / INV-15** — fuera del scope del slice (vive en consumidor — followup #24).

### Cobertura de excepciones nuevas
- `MonedasIgualesEnTasaException` (PRE-1) — §6.6 + §6.10.
- `TasaDeCambioInvalidaException` (PRE-2) — §6.7 (3 casos).
- `FechaDeTasaEnElFuturoException` (PRE-3) — §6.8.

### Sin regresiones
142 tests previos del dominio (slices 00–05) y 24 tests de integración siguen verdes. Sin tocar agregados, proyecciones ni handlers existentes.

---

## 5. Resumen de cambios

| Archivo | Cambio | Líneas (aprox.) |
|---------|--------|-----------------|
| `src/SincoPresupuesto.Domain/CatalogosDeTasas/CatalogoDeTasas.cs` | Implementar `Crear`, `Ejecutar`, `Apply` + helper `ValidarYConstruir` | +50, -3 |
| **Total** | — | ~50 líneas netas |

Cero modificaciones en tests. Cero modificaciones en otros slices.
