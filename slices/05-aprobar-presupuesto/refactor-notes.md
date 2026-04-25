# Refactor notes — Slice 05 — AprobarPresupuesto

**Refactorer:** orquestador (subagente `refactorer` no invocado por economía de cupo; orquestador siguió `templates/agent-personas/refactorer.md`).
**Fecha:** 2026-04-24
**Estado inicial:** 142/142 dominio + 20/20 integración tras green.
**Estado final:** idéntico — 142/142 + 20/20, sin cambio de comportamiento.

---

## Cambios aplicados

| # | Tipo | Archivo | Descripción | Tests antes | Tests después |
|---|---|---|---|---|---|
| 1 | extract method | `src/SincoPresupuesto.Domain/Presupuestos/Presupuesto.cs` | Nuevo `private void RequerirBorrador()` que centraliza el patrón `if (Estado != EstadoPresupuesto.Borrador) throw new PresupuestoNoEsBorradorException(Estado);`. Sustituye los 3 sitios donde se aplicaba: `AgregarRubro` (línea 131), `AsignarMontoARubro` (línea 190), `AprobarPresupuesto` (línea 256). Disparador del followup metodológico "tercer uso" alcanzado, mismo precedente que `Requerir.Campo` cerrando #10 en slice 03. Helper privado de instancia (no kernel) porque opera sobre `Estado` del agregado. Comentarios en cada call-site referencian §6.X de slice 05 que ejercita "lanza" — followup #13 ahora ya cubierto. | 142 pass | 142 pass |

## Refactors descartados

| # | Sugerido por | Motivo para no aplicar |
|---|---|---|
| 1 | green-notes §2 — helper `EsTerminal(Rubro)` o `TieneHijos(Guid)` | Hay 2 usos: (a) `AsignarMontoARubro` línea 203 (`_rubros.Any(r => r.PadreId == cmd.RubroId)` para detectar Agrupador), (b) `AprobarPresupuesto` línea 264 (`!_rubros.Any(otro => otro.PadreId == r.Id)` para filtrar terminales). Disparador "tercer uso" no alcanzado todavía. Adicionalmente, los call-sites tienen semánticas inversas (uno chequea Agrupador, el otro filtra Terminal); extraer un solo helper requiere decidir nomenclatura, lo cual es decisión que prefiere posponer hasta que aparezca el tercer caso o slice `RubroConvertidoAAgrupador` lo formalice. Followup #12 (introducir `RubroTipo` explícito) también lo cubre cuando se aborde. |
| 2 | green-notes §2 — proyección `PresupuestoReadModel` con `Estado`/`MontoTotal`/`AprobadoEn`/`AprobadoPor`/`SnapshotTasas` | Feature, no refactor. Va en fase `infra-wire` del slice 05 — actualizar `PresupuestoProjection.Apply(PresupuestoAprobado)` y los campos del read model. |
| 3 | green-notes §2 — `DomainExceptionHandler.Mapear` con 2 casos nuevos (`PresupuestoSinMontosException → 400`, `AprobacionConMultimonedaNoSoportadaException → 400`) | Feature de wire, va en `infra-wire` del slice 05. |
| 4 | green-notes §2 — Cierre de followup #13 confirmado | No es refactor — es verificación de que slice 05 §6.7/§6.8/§6.9 ejercitan por primera vez la rama "lanza" declarada en slices 03 y 04. Confirmado: `RequerirBorrador()` (refactor #1) lo recoge en un solo helper que esos tests cubren. |
| 5 | considerado por refactorer — reorder de miembros | El método `AprobarPresupuesto` ya quedó en posición correcta (entre `AsignarMontoARubro` y `ValidarFormatoDelCodigo`), agrupado con los otros comandos antes del banner `// Apply methods`. Same orden que tras refactor de slice 04. Sin cambio. |
| 6 | considerado — promover `RequerirBorrador()` al `SharedKernel` (helper genérico para guards de estado) | Especulativo — solo `Presupuesto` tiene `EstadoPresupuesto`. Un agregado futuro con su propia máquina de estados (p.ej. `TasaDeCambio` con estados `Vigente/Reemplazada`) tendría su propio enum y guard distinto. Esperar al segundo caso para extraer pattern genérico. |

## Verificación

```
$ dotnet build --nologo
    0 Advertencia(s)
    0 Errores

$ dotnet test --filter "FullyQualifiedName~Domain" tests/SincoPresupuesto.Domain.Tests --nologo
    Correctas! - Con error: 0, Superado: 142, Omitido: 0, Total: 142
```

(La suite de integración no la corre refactorer — la corrió el orquestador antes y después: 20/20 sin cambios; el refactor es puramente interno al agregado y no afecta serialización ni HTTP).

## Followups para FOLLOWUPS.md

- **#13 cierra completamente con este slice + refactor.** El comentario en `RequerirBorrador()` y los 3 call-sites referencian Slice05_AprobarPresupuesto_*_post_aprobacion (§6.7, §6.8, §6.9), que ejercitan la rama "lanza" por primera vez para cada uno de los 3 comandos.
- **Sin followup nuevo** generado por este refactor.

## Hand-off a reviewer

Slice listo para auditoría. Items a considerar:

1. El único refactor aplicado es la extracción de `RequerirBorrador()` (3 usos centralizados). Cero cambio de comportamiento.
2. Cobertura de ramas del slice: esperar ≥ 95% en `AprobarPresupuesto` y `Apply(PresupuestoAprobado)` — todas las ramas tienen test (a diferencia de slice 03/04 donde INV-3 quedaba sin cubrir).
3. La rama "violación INV-3" en `AgregarRubro` y `AsignarMontoARubro` ahora tiene cobertura — Slice05 §6.8 y §6.9 las ejercitan.
4. Deudas heredadas (proyección + `DomainExceptionHandler` mapeo) se abordan en fase `infra-wire`.
