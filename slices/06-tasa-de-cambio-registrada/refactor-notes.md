# Refactor notes — Slice 06 — RegistrarTasaDeCambio

**Refactorer:** orquestador (siguiendo `templates/agent-personas/refactorer.md`).
**Fecha:** 2026-04-24
**Estado inicial:** 159/159 dominio + 24/24 integración tras green.
**Estado final:** idéntico — 159/159 + 24/24, sin cambios.

---

## Cero cambios

**Motivo:** Green ya extrajo el helper privado `ValidarYConstruir(cmd, ahora)` durante la fase verde para evitar duplicación entre `Crear` y `Ejecutar`. El método público `Crear` y `Ejecutar` son one-liners delegando al helper. La estructura del agregado es minimal y consistente con el patrón de `ConfiguracionTenant` (slice 02).

## Refactors descartados

| # | Sugerido por | Motivo para no aplicar |
|---|---|---|
| 1 | green-notes §2 — base `EventSourcedAggregate<...>` para abstraer Crear/Ejecutar/Apply | Especulativo. Solo 2 agregados con este patrón (`ConfiguracionTenant` slice 02 + `CatalogoDeTasas` slice 06). El disparador "tercer caso" del precedente `Requerir.Campo` no se alcanza. Footprint distinto entre los dos: ConfiguracionTenant.Ejecutar SIEMPRE lanza; CatalogoDeTasas.Ejecutar acumula. La abstracción tendría que parametrizar la diferencia → complica. Esperar al tercer agregado para validar. |
| 2 | green-notes §2 — asignación de `Id` en `Apply` referenciando `CatalogoDeTasasStreamId` | El stream-id vive en `Application` (capa superior). Acoplar dominio al `Application` violaría dependencias. Marten asigna `Id` por reflexión al rehidratar — el `Id` no necesita poblarse en el fold. Disparador documentado: tests de integración del slice 06b cuando comprueben que `agg.Id == streamId`. |
| 3 | green-notes §2 — fusionar `RegistroDeTasa` con `TasaDeCambioRegistrada` (mismas propiedades) | Conceptualmente: el evento es "lo que ocurrió" (inmutable); `RegistroDeTasa` es "el ítem del historial expuesto al lector del agregado" (vista de fold). Si en futuro `TasaDeCambioRegistrada` cambia (p.ej. v2 con `TasaInversa`), `RegistroDeTasa` puede mantener forma estable hacia el resto del dominio. Mantener separación deliberadamente. Coste: 7 líneas duplicadas. Beneficio: separación evento/vista. Decisión: conservar. |
| 4 | green-notes §2 — extraer helper `Trim()` de strings opcionales | Un solo uso (`Fuente`). Especulativo. |
| 5 | considerado por refactorer — promover `ValidarYConstruir` a clase externa | Es lógica del agregado. Mantenerla privada estática del agregado es lo natural. |

## Verificación

```
$ dotnet build --nologo
    0 Advertencia(s) / 0 Errores
$ dotnet test --nologo
    Domain: 159/159 ✅ / Integration: 24/24 ✅
```

## Hand-off a reviewer

Slice listo para auditoría. Items a considerar:

1. Cobertura del agregado: todas las ramas tienen test (PRE-1/2/3 happy + violación; PRE-4 normalización Fuente y RegistradoPor; fold; acumulación; last-write-wins; caso límite Fecha=hoy).
2. La separación `Crear`/`Ejecutar` con cuerpo idéntico es deliberada (replica patrón slice 02 — `Crear` factory para stream vacío, `Ejecutar` para fold existente). El handler del slice 06b (infra-wire) decide cuál llamar según `AggregateStreamAsync` retorne `null` o no.
3. Deudas heredadas (proyección `TasasDeCambioVigentes` + endpoints HTTP + mapeo de las 3 excepciones nuevas) se abordan en `infra-wire` del slice 06.
