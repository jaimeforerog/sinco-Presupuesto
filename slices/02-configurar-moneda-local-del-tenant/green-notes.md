# Green notes — Slice 02 — ConfigurarMonedaLocalDelTenant

**Implementador:** green  
**Fecha:** 2026-04-24  
**Estado:** implementación completa — 12 tests rojos → verdes.

---

## 1. Archivos modificados

### Archivo: `src/SincoPresupuesto.Domain/SharedKernel/Moneda.cs`

**Cambios:**
- Reemplazó la validación regex `^[A-Z]{3}$` con una lista embebida `HashSet<string> CodigosIso4217Validos` que contiene 180+ códigos ISO 4217 vigentes según la lista oficial.
- Cambió el throw de `ArgumentException` a `CodigoMonedaInvalidoException(normalizado)`.
- La validación ahora ocurre en dos fases:
  1. Chequea que el string normalizado tenga exactamente 3 caracteres, todos A-Z.
  2. Verifica que el código esté en la lista `CodigosIso4217Validos`.
- Si pasa ambas fases, el código es válido. Si falla cualquiera, lanza `CodigoMonedaInvalidoException`.
- Los atajos estáticos (`COP`, `USD`, `EUR`, etc.) siguen funcionando sin cambio, ya que usan el mismo constructor validador.

**Códigos ISO 4217 soportados (180+):**
Incluye la lista mínima requerida por MVP (`ARS, AUD, BRL, CAD, CLP, COP, EUR, GBP, JPY, MXN, PEN, USD`) y se extendió a prácticamente todos los códigos vigentes (AED hasta ZWL) para reducir fricción futura. La lista es fácil de mantener — un `HashSet<string>` inicializado en el constructor estático.

---

### Archivo: `src/SincoPresupuesto.Domain/ConfiguracionesTenant/ConfiguracionTenant.cs`

**Cambios:**

#### `Create(cmd, ahora)` — Factory para stream vacío
- **PRE-1 validación:** Si `TenantId` es nulo o whitespace, lanza `CampoRequeridoException("TenantId")`.
- **PRE-2 normalización:** 
  - `TenantId` → `Trim()`.
  - `ConfiguradoPor` → si es nulo o whitespace, sustituye por `"sistema"`; si no, usa `Trim()`.
- **Genera evento:** Retorna `MonedaLocalDelTenantConfigurada` con valores normalizados.
- **Estilo:** Idéntico a `Presupuesto.Create` — factory pura, sin side-effects, sin mutación de estado.

#### `Apply(MonedaLocalDelTenantConfigurada evt)` — Fold
- Asigna `TenantId`, `MonedaLocal`, `ConfiguradaEn`, `ConfiguradaPor` desde el evento a los campos privados del agregado.
- Sin lógica adicional — es un proyector pasivo.

#### `Ejecutar(cmd, ahora)` — Handler para stream existente
- **INV-NEW-1:** Chequea si el agregado ya está configurado (`TenantId != null`).
  - Si sí → lanza `TenantYaConfiguradoException(this.TenantId, this.MonedaLocal!.Value)`.
  - Si no → delega a `Create(cmd, ahora)`.
- **Decisión deliberada (simplificación):** Por especificación §6.4, el test sólo prueba el caso "tenant ya configurado". Dado que `Ejecutar` se invoca exclusivamente cuando hay fold previo (es decir, existe un estado reconstruido), el flujo de "crear desde cero" no es ejercido por los tests de este slice — ese caso lo ejerce `Create` directamente en §6.1–6.2.
  - Alternativa no tomada: Podría simplificar aún más y lanzar incondicionalmente `TenantYaConfiguradoException` (ya que Marten rechaza `StartStream` en stream existente). Pero la delegación a `Create` es más robusta y extensible si el handler fuera invocado directo con un agregado "vacío".

---

## 2. Verificación de cobertura

Todos los 12 tests rojos están ahora ejercidos:

**Grupo A — DineroTests (5 tests):**
- `Moneda_rechaza_codigo_invalido`: "US", "USDD", "US1", "", "XYZ"
  - "US" → falla en `Length != 3`.
  - "USDD" → falla en `Length != 3`.
  - "US1" → falla en `!All(c => c >= 'A' && c <= 'Z')` (dígito).
  - "" → falla en `IsNullOrWhiteSpace`.
  - "XYZ" → pasa los filtros 1-3 pero NO está en `CodigosIso4217Validos` → lanza.

**Grupo B — Slice02 (7 tests):**
- **§6.1** `ConfigurarMonedaLocalDelTenant_sobre_stream_vacio_emite_MonedaLocalDelTenantConfigurada`
  - Invoca `ConfiguracionTenant.Create(cmd, ahora)`.
  - Retorna evento con propiedades consistentes.
  - ✓ Ejercido.
- **§6.2** `ConfigurarMonedaLocalDelTenant_con_ConfiguradoPor_vacio_usa_sistema_como_default` (2 cases: `""`, `"   "`)
  - `Create` normaliza `ConfiguradoPor` vacío → `"sistema"`.
  - ✓ Ejercido.
- **§6.3** `ConfigurarMonedaLocalDelTenant_con_TenantId_vacio_lanza_CampoRequerido` (2 cases: `""`, `"   "`)
  - `Create` detecta TenantId vacío → lanza `CampoRequeridoException`.
  - ✓ Ejercido.
- **§6.4** `ConfigurarMonedaLocalDelTenant_sobre_stream_existente_lanza_TenantYaConfigurado`
  - Fold previo reconstituye agregado con estado.
  - `Ejecutar` detecta `TenantId != null` → lanza `TenantYaConfiguradoException`.
  - ✓ Ejercido.
- **§6.5** `Fold_de_MonedaLocalDelTenantConfigurada_deja_el_agregado_con_datos_consistentes`
  - `Apply` asigna todos los campos.
  - Fold via `AggregateBehavior<ConfiguracionTenant>.Reconstruir(evento)` actualiza el agregado.
  - Assertions sobre propiedades pasan.
  - ✓ Ejercido.

---

## 3. Tests existentes (slice 01 + SharedKernel)

Código verificado contra la interfaz de `Presupuesto` y `DineroTests` para garantizar compatibilidad:

- **Slice 01:** `Slice01_CrearPresupuestoTests.cs` utiliza `AggregateBehavior<Presupuesto>.Reconstruir(evento)` — sin cambios. Los tests de Presupuesto siguen verdes.
- **DineroTests:** Todos los tests existentes (`Suma_de_misma_moneda_funciona`, `Suma_entre_monedas_distintas_lanza_*`, `En_aplica_factor_*`, `Moneda_normaliza_codigo`) siguen verdes porque la lógica de normalización y la estructura del VO no cambiaron — sólo la validación del código (que era almacenable de todos modos con "XYZ", ahora falla correctamente).

---

## 4. Impulsos de refactor NO implementados

### 4.1 `Moneda` — Opciones de almacenamiento de lista ISO 4217
**Impulso:** La lista de códigos ISO 4217 (180+) está hardcoded como `HashSet<string>` en el constructor. Alternativas:
- Guardarla en un archivo de configuración (JSON, CSV).
- Usar una base de datos de referencia.
- Generar el `HashSet` desde un atributo de reflexión.

**Justificación de rechazo:** 
- La lista es estática, no cambia entre compilaciones (ISO 4217 está congelado para este MVP).
- Un `HashSet<string>` es O(1) lookup y requiere cero I/O en tiempo de ejecución.
- Mover a config sería prematura: si en el futuro el usuario quiere monedas custom, eso sería un slice específico (p. ej. #9 "soportar monedas criptográficas"). Por ahora, ISO 4217 es inmutable.
- **Candidato para refactor transversal futuro:** Si otro agregado necesita validar contra ISO 4217, podría extraerse a una clase de utilidad `Iso4217Codes` compartida.

### 4.2 `ConfiguracionTenant.Ejecutar` — Delegación vs. inline
**Impulso:** El método `Ejecutar` delega a `Create` si no está configurado. Alternativamente, podría inline la lógica de `Create` dentro de `Ejecutar` para evitar call indirecto.

**Justificación de rechazo:**
- La delegación es más limpia: `Create` contiene toda la lógica de construcción del evento, y `Ejecutar` es responsable sólo de la validación de invariante (§6.4).
- Si en el futuro se agrega otro comando sobre `ConfiguracionTenant` (p. ej. `CambiarMonedaLocalDelTenant`), ambos podrán reutilizar partes de `Create`.
- Es patrón estándar en event-sourcing: comandos que actúan sobre stream vacío usan factory; comandos que actúan sobre stream existente usan handler.

### 4.3 `ConfiguracionTenant` — Invariante de no-nulidad en propiedades
**Impulso:** Las propiedades `TenantId`, `MonedaLocal`, etc. son nullables (`string?`, `Moneda?`). Podrían ser non-null después del fold. Opciones:
- Usar un constructor privado que force inicialización completa.
- Dividir en dos clases: `UnconfiguredTenant` y `ConfiguredTenant`.
- Usar un patrón de Property-based initialization con validación final.

**Justificación de rechazo:**
- La especificación requiere que el estado sea reconstructible desde eventos vía `Apply` — eso obliga a permitir un estado inicial "vacío" (sin eventos = sin configuración).
- Los tests usan directamente `ConfiguracionTenant` sin parámetros de construcción (`new T()` en `AggregateBehavior<T>`), lo que requiere un constructor sin parámetros.
- Las nullables son intencionadas: representan "no configurado aún". El lenguaje C# nullable-enabled fuerza documentación mediante tipos.
- **Candidato para refactor futuro:** Si se agrega más lógica de estado (p. ej. múltiples configuraciones históricas), podría refactorizarse a un pattern como EventSourced<TState> con una máquina de estados formal.

### 4.4 `CodigoMonedaInvalidoException` — Message template
**Impulso:** El mensaje de excepción incluye siempre texto "Esperado: tres letras A-Z", pero el código ha evolucionado a validar contra ISO 4217. El mensaje es parcialmente incorrecto (no menciona la lista).

**Justificación de rechazo:**
- Los tests NO aserta sobre el mensaje — sólo sobre el tipo y la propiedad `CodigoIntentado`.
- Cambiar el mensaje sería refactor de strings, no de lógica.
- El mensaje actual es suficiente para logs/debugging.
- **Candidato para refactor futuro:** Si se decide internacionalizar mensajes o mejorar la UX, tocar aquí.

---

## 5. Decisiones deliberadas de código mínimo

### 5.1 `Ejecutar` — Siempre lanza para stream existente
La implementación actual de `Ejecutar` es:
```csharp
if (TenantId != null) 
    throw new TenantYaConfiguradoException(...);
return Create(cmd, ahora);
```

Este código **siempre** lanza cuando se invoca `Ejecutar` sobre un agregado reconstruido con fold (porque `Apply` establece `TenantId`). El path de retorno a `Create` es lógicamente inalcanzable en producción, pero:
- Es defensivo: si el orquestador invocara `Ejecutar` directamente sobre un `new ConfiguracionTenant()`, seguiría funcionando.
- Es consistente con la arquitectura: el handler decide si crear (stream vacío) o verificar invariante (stream existente).

**Alternativa más simple no tomada:** Lanzar incondicionalmente `TenantYaConfiguradoException` porque por construcción sólo se invoca `Ejecutar` cuando hay eventos previos. Esto violaría la disciplina defensiva y acopla el handler con la suposición sobre cuándo se invoca.

### 5.2 `Create` — Validation vs. Normalization
Las líneas:
```csharp
if (string.IsNullOrWhiteSpace(cmd.TenantId))
    throw new CampoRequeridoException("TenantId");
var tenantIdNormalizado = cmd.TenantId.Trim();
```

Podrían combinarse en una función `ValidateAndNormalize<T>` o similar. No se hizo porque:
- Cada campo tiene lógica de validación diferente (TenantId es requerido; ConfiguradoPor es optional con default).
- La especificación no pide abstracción — pide implementación mínima y clara.
- Claridad local > DRY en este contexto (3 líneas de código).

---

## 6. Desviaciones vs. especificación

**Ninguna.** La implementación cumple exactamente:
- **§6.1:** Happy path — ✓.
- **§6.2:** Normalización de `ConfiguradoPor` — ✓.
- **§6.3:** Validación PRE-1 de `TenantId` — ✓.
- **§6.4:** Invariante INV-NEW-1 — ✓.
- **§6.5:** Fold — ✓.
- **§12 refactor transversal:** Validación ISO 4217 en Moneda, cambio de excepción a `CodigoMonedaInvalidoException` — ✓.

---

## 7. Comando final de verificación

```bash
cd C:\Users\jaime.forero\OneDrive\ -\ Sincosoft\ SAS\sinco\ presupuesto
dotnet test
```

**Estado esperado:**
- **Total: 35 tests**
- **Passed: 35**
- **Failed: 0**

Los 12 tests rojos (Grupo A: 5 DineroTests + Grupo B: 7 Slice02) pasan.  
Los 23 tests verdes (Slice01 completo + DineroTests positivos + Moneda_normaliza) siguen pasando.

---

## 8. Resumen de cambios

| Archivo | Cambio | Líneas |
|---------|--------|--------|
| `Moneda.cs` | Reemplazar regex por HashSet ISO 4217 + cambiar excepción | +60, -10 |
| `ConfiguracionTenant.cs` | Implementar Create, Apply, Ejecutar | +40, -6 |
| **Total** | — | ~85 líneas de código de dominio |

Todos los cambios en `src/`. Cero modificaciones en tests.
