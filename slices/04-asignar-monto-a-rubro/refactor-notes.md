# Refactor notes — Slice 04 — AsignarMontoARubro

**Refactorer:** orquestador (el subagente `refactorer` agotó el cupo de uso; el orquestador lo ejecutó directamente siguiendo la persona `templates/agent-personas/refactorer.md`).
**Fecha:** 2026-04-24
**Estado inicial:** `dotnet test` 125/125 verdes tras green. 0 warnings, 0 errors.
**Estado final:** idéntico — 125/125 verdes, sin regresiones.

---

## Cambios aplicados

| # | Tipo | Archivo | Descripción | Tests antes | Tests después |
|---|---|---|---|---|---|
| 1 | reorder | `src/SincoPresupuesto.Domain/Presupuestos/Presupuesto.cs` | Movido `AsignarMontoARubro` (método público de comando) para que quede agrupado junto a `Create` y `AgregarRubro` **antes** del banner `// Apply methods`; `Apply(MontoAsignadoARubro)` queda agrupado con los otros `Apply` bajo el banner. Motivo: el banner del archivo señala "a partir de aquí, fold methods"; la colocación previa rompía esa intención al mezclar un comando nuevo entre los `Apply` existentes. Cero cambio de comportamiento — solo reorden de miembros. | 125 pass | 125 pass |

## Refactors descartados

| # | Sugerido por | Motivo para no aplicar |
|---|---|---|
| 1 | green-notes §2.1 — `Rubro.Monto` endurecer encapsulamiento | **Ya aplicado** entre la fase green y esta de refactor (probablemente por linter/IDE o ajuste del usuario durante la fase). `Rubro.cs` ahora expone `public Dinero Monto { get; internal set; }` + método `internal void AsignarMonto(Dinero)`; `Presupuesto.Apply(MontoAsignadoARubro)` invoca el método en vez de asignar directamente. Cumple el criterio de "setter controlado sin exponerlo al mundo" sin cambios de tests. |
| 2 | green-notes §2.2 — Proyección `PresupuestoReadModel.Rubros` + `Monto` | Feature, no refactor. Se difiere a fase `infra-wire` de slice 04. Ningún test actual ejercita la proyección; refactorer no agrega código sin test que lo justifique. |
| 3 | green-notes §2.3 — `DomainExceptionHandler.Mapear` con 3 casos nuevos | Feature de wire, no refactor. Se aplica en `infra-wire` del slice 04 (`RubroNoExisteException → 409`, `RubroEsAgrupadorException → 409`, `MontoNegativoException → 400`). |
| 4 | green-notes §2.4 — helper `Requerir.Id(Guid, string)` generalizando `Guid.Empty → CampoRequeridoException` | Hoy hay **2 usos** del patrón (en `AgregarRubro` y `AsignarMontoARubro`). El precedente de followup #10 (`Requerir.Campo`) exigió **3 usos** antes de extraer. Mismo criterio aplicado por disciplina; seguirá abierto como followup #10 extendido o uno nuevo cuando llegue el tercer uso. |
| 5 | green-notes §2.6 — guard `RequerirBorrador()` reutilizable | 2 usos hoy (`AgregarRubro` y `AsignarMontoARubro`). Mismo criterio del punto 4 — esperar a tercer uso. |
| 6 | green-notes §2.5 — reordenar validaciones PRE-2 (existe rubro) vs PRE-3 (monto ≥ 0) | El orden elegido por green es el del brief de spec §4. Ningún test combina violaciones simultáneas; reordenar sería cambio gratuito que podría romper sutilezas si un test futuro las mezcla. **Conservar.** |
| 7 | green-notes §3.4 — `.First(...)` sin `FirstOrDefault` en `Apply(MontoAsignadoARubro)` | Invariante del stream: si el `MontoAsignadoARubro` se emitió, el rubro existe. Usar `.First()` hace explícita esa invariante ("si falta, es bug de integridad, no de este código"). Decisión consistente con el `Apply(RubroAgregado)` del slice 03. **Conservar.** |

## Verificación

```
$ dotnet build --nologo
    0 Advertencia(s)
    0 Errores

$ dotnet test --nologo
    Correctas! - Con error: 0, Superado: 125, Omitido: 0, Total: 125
```

## Handoff a reviewer

Slice listo para auditoría. Items a considerar:

- El único refactor aplicado es cosmético (reorder). El valor es de navegabilidad — el banner `// Apply methods` volvió a marcar una frontera clara.
- `Rubro.Monto` quedó endurecido (internal setter + método `AsignarMonto`) — efectivamente cerró el impulso #1 de green. Verificar que la mutación sigue siendo exclusiva del fold (ningún otro call-site invoca `AsignarMonto`).
- 4 impulsos de green (proyección, handler HTTP, `Requerir.Id`, `RequerirBorrador`) quedan como deuda documentada — con disparador claro para cada uno.
- Cobertura de ramas del slice: esperar ≥ 95% en `AsignarMontoARubro` y `Apply(MontoAsignadoARubro)` — la única no cubierta es la rama INV-3 "lanza" (diferida a followup #13, misma situación que slice 03).
