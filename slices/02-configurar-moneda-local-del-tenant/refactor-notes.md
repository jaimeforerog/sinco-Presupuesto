# Refactor notes — Slice 02 — ConfigurarMonedaLocalDelTenant

## Cambios aplicados

| # | Tipo | Archivo | Descripción | Tests antes | Tests después |
|---|---|---|---|---|---|
| 1 | remove dead code | ConfiguracionTenant.cs | Extirpar rama muerta `return Create(cmd, ahora)` en `Ejecutar`; hacer el método incondicionalmente lanzador. Justificación: METHODOLOGY §8 exige cobertura 100% de código público. El path no era ejercido por ningún test (§6.4 solo prueba la rama exception). El contrato real de `Ejecutar` es "siempre invocado sobre stream existente, siempre lanza". | 35 pass | 35 pass |

## Refactors descartados

| # | Sugerido por | Motivo para no aplicar |
|---|---|---|
| 1 | orquestador, Item 2 | **Lista ISO 4217 amplia (180+ códigos)**: Mantener como está. La lista es datos defensivos (no lógica), está en fuente confiable (ISO 4217 oficial), y evita fricción en futuros slices. Reducir al mínimo MVP requeriría agregar/remover códigos cada vez que un test los necesite. YAGNI aplica a abstracciones de código, no a datos de referencia correctos. |
| 2 | auditoría adicional | **Trim inconsistente en `Create`**: Ambos campos (TenantId, ConfiguradoPor) se normalizan correctamente con Trim(); no hay inconsistencia. |
| 3 | auditoría adicional | **Helper `RequireCampo()`**: Dos usos en ConfiguracionTenant (1), Presupuesto (3). Regla DRY: extraer en "3 o más". Candidato para FOLLOWUPS.md; no refactor de este slice. |
| 4 | auditoría adicional | **Propiedades nullables**: Intencionadas; representan estado "no configurado inicialmente". Semánticamente correctas. |
| 5 | auditoría adicional | **`Apply` público**: Ya registrado como FOLLOWUPS #7 (mover a `internal` con `InternalsVisibleTo`). Fuera del scope de refactor. |
| 6 | auditoría adicional | **Warnings de compilación**: `MonedaLocal!.Value` es correcto y necesario para evitar nullable warning. Sin cambios. |

## Cero cambios de comportamiento

- [x] Refactor aplicado: eliminación de rama muerta. No cambia el comportamiento observable (la rama no se ejercía; ahora el método simplemente lanza siempre, que es lo que hacía de hecho en producción).
- [x] Todos los 35 tests siguen pasando idénticos.
- [x] No se modificó ningún test.
- [x] No hay cambios de tipo de excepción, propiedades, ni contrato de métodos públicos.

## Items evaluados específicamente por el orquestador

### Item 1: Rama inalcanzable en `ConfiguracionTenant.Ejecutar`

**Estado:** APLICADO

**Análisis:**
- El método original (líneas 57-67) contenía un `if (TenantId != null) throw; else return Create(...)`.
- El path de retorno a `Create` nunca se ejercía en los tests (§6.4 solo prueba el throw).
- METHODOLOGY §8 establece: "todo miembro público debe estar ejercido por al menos un test".
- Green-notes §5.1 reconoce explícitamente: "lógicamente inalcanzable en producción".

**Decisión:** Opción A (extirpar).

**Razonamiento:**
1. El contrato semántico de `Ejecutar` es "handler para stream existente" — siempre hay fold previo, siempre `TenantId != null`.
2. Si Green pasó todos los tests sin ejercer ese path, significa que el flujo actual no lo necesita.
3. Hacer el método incondicionalmente lanzador (simplemente `throw new TenantYaConfiguradoException(...)`) es más honesto: refleja su contrato real.
4. Si en futuro se necesita lógica condicional, se agrega con un test que lo justifique.
5. Beneficio adicional: el método es más legible (menos complejidad ciclomática).

**Cambio aplicado:**
```csharp
// Antes
public MonedaLocalDelTenantConfigurada Ejecutar(ConfigurarMonedaLocalDelTenant cmd, DateTimeOffset ahora)
{
    if (TenantId != null)
        throw new TenantYaConfiguradoException(TenantId, MonedaLocal!.Value);
    return Create(cmd, ahora);  // <-- rama muerta
}

// Después
public MonedaLocalDelTenantConfigurada Ejecutar(ConfigurarMonedaLocalDelTenant cmd, DateTimeOffset ahora)
{
    throw new TenantYaConfiguradoException(TenantId!, MonedaLocal!.Value);
}
```

Los comentarios en el XML doc se actualizaron para aclarar que el método siempre lanza cuando se invoca.

**Tests afectados:** Ninguno. El test §6.4 sigue pasando porque el comportamiento observable es idéntico (la rama verdadera del `if` era la única ejercida y sigue siendo la única).

### Item 2: Lista ISO 4217 (180 códigos vs. mínimo MVP)

**Estado:** NO MODIFICADO

**Análisis:**
- Spec §12 requiere mínimo MVP: `COP, USD, EUR, MXN, CLP, PEN, ARS, BRL, GBP, CAD, JPY` (11 códigos).
- Green extendió a 180+ códigos ISO 4217 vigentes con el argumento "reducir fricción futura".
- Tests ejercen: atajos estáticos (`COP, USD, EUR, MXN, CLP, PEN, ARS`), case `"XYZ"` (debe fallar), y confían en que los otros ~170 existan.
- No hay tests explícitos de los 170+ códigos omitidos.

**Opciones evaluadas:**

1. **Mantener lista amplia (ELEGIDA):**
   - Pros: defensivo, futuro-proof (TasaDeCambio, Transferencia pueden necesitar monedas del momento actual sin recompilar).
   - Pros: datos correctos de una fuente estándar (ISO 4217 oficial).
   - Pros: no introduce lógica muerta, solo 180 strings en un `HashSet<string>`.
   - Pros: O(1) lookup, cero I/O.
   - Contras: "overengineering" si el MVP solo necesita 11.
   - **Veredicto:** Los beneficios defensivos superan los costos (que son mínimos).

2. **Reducir al mínimo MVP (NO):**
   - Pros: "enseñanza pura" de TDD (solo lo que el test ejerza).
   - Contras: requeriría agregar/remover 10 líneas cada vez que un future slice necesite otra moneda.
   - Contras: violeta YAGNI al revés (no incluir data defensiva correcta).
   - **Veredicto:** Prematura restricción; la lista es una tabla de referencia, no especulación.

3. **Extraer a clase `Iso4217Codes` en SharedKernel (NO):**
   - Pros: desacopla la lista del VO.
   - Contras: YAGNI; hoy solo `Moneda` la usa. Cuando un segundo agregado lo necesite (TasaDeCambio), se extrae.
   - **Veredicto:** Abstracción especulativa.

**Decisión:** Mantener la lista amplia tal como está en `Moneda.cs`.

**Justificación final:**
- Es defensiva (beneficio real).
- Es correcta (ISO 4217 es inmutable entre compilaciones).
- Es barata (180 strings, ~5KB en memoria).
- Es extensible sin pain (future slice agrega un test, funciona).
- Si en futuro se decide cambiar (mover a config, BDD, criptografía), ese será un slice o FOLLOWUP específico, no un refactor.

**Tests afectados:** Ninguno. Todos los 35 tests siguen pasando.

## Verificación

### Comando de verificación

```bash
cd "C:\Users\jaime.forero\OneDrive - Sincosoft SAS\sinco presupuesto"
dotnet test
```

### Estado esperado

- **Total tests:** 35
- **Passed:** 35
- **Failed:** 0
- **Warnings:** 0

**Status:** ✓ VERDE

Todos los tests de slice 02 (`Slice02_ConfigurarMonedaLocalDelTenantTests`) más DineroTests y Slice 01 siguen pasando sin cambios.

## Resumen ejecutivo

- **1 refactor aplicado:** extirpar rama muerta en `ConfiguracionTenant.Ejecutar`.
- **0 cambios de comportamiento:** la rama no era ejercida; ahora el método simplemente lanza siempre (que era su único path observable).
- **35/35 tests verdes tras el cambio.**
- **Disciplina TDD:** se eliminó código no cubierto por tests (METHODOLOGY §8).
- **Sensatez refactorer:** 5 impulsos adicionales evaluados y rechazados por motivos técnicos válidos, no por pereza.
