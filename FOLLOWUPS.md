# Follow-ups

Seguimiento de deuda técnica y trabajos pendientes generados por reviews de slices. Se cierra cada ítem cuando el slice que lo resuelve pasa review.

**Convención:**
- Un ítem por línea, formato `- [ ] #{N} — {título corto} — origen: slice-{N}, review §{sección}. Propuesta: {acción sugerida}.`
- Se marca `[x]` cuando el slice resolvente cierra review con `approved`.

---

## Abiertos

- [ ] #1 — Cobertura de ramas automatizada — origen: slice-01, review §3 (pasada 1). Propuesta: añadir `coverlet.collector` y step CI `dotnet test --collect:"XPlat Code Coverage"` con reporte.
- [ ] #2 — Unicidad `(TenantId, Codigo, Periodo)` — origen: slice-01, review §3. Propuesta: slice dedicado `PresupuestoCodigoIndex` con `UniqueIndex` compuesto de Marten.
- [ ] #3 — `ConfiguracionTenant` como prerequisito de `CrearPresupuesto` — origen: slice-01, review §3. Propuesta: slice `ConfiguracionTenant.CrearTenant` + validación cruzada en `CrearPresupuestoHandler` para asegurar que `MonedaBase` coincide con una moneda autorizada para el tenant.
- [ ] #4 — Slice retroactivo `00-shared-kernel` para `Dinero` y `Moneda` — origen: slice-01, review §3 (pasada 2). Propuesta: crear `slices/00-shared-kernel/spec.md` + notas, mover `DineroTests.cs` a `Slices/Slice00_SharedKernelTests.cs`, y cerrar gap de "todo test en un slice".
- [ ] #5 — Firma uniforme `CasoDeUso.Decidir(dados, cmd, ...) → IReadOnlyList<object>` — origen: slice-01, refactor-notes (pasada 2). Propuesta: evaluar en slice 02 (`AgregarRubro`) si la firma escala mejor. Si sí, retroactivar slice 01 para homogeneizar.
- [ ] #6 — Tests faltantes de `Dinero` — origen: slice-01, review §3 (pasada 2). Propuesta: al cerrar FOLLOWUPS #4, cubrir operadores `-`, `*`, `<`, `>`, `<=`, `>=`, helpers `Cero`/`EsCero`/`EsPositivo`/`EsNegativo`, `En(factor = 0)`, `En(factor < 0)`, y boundaries `ProfundidadMaxima ∈ {1, 15}` válidos.
- [ ] #7 — `Apply` público vs. internal en agregados — origen: slice-01, green-notes §2.5. Propuesta: investigar si Marten soporta `internal Apply` con `InternalsVisibleTo` para limitar la superficie pública del agregado.
- [ ] #8 — refactor `CrearPresupuestoHandler` para validar existencia de `ConfiguracionTenantActual` antes de iniciar el stream de `Presupuesto`. Lanzar `TenantNoConfiguradoException` (nueva excepción a introducir en `SharedKernel`). Origen: slice-02, spec §10 Q2.
- [ ] #9 — completitud de la lista ISO 4217 en `Moneda` — origen: slice-02, spec §12 + refactor-notes. Estado: green embebió ~180 códigos (lista amplia, considerada adecuada por refactorer). Followup abierto hasta confirmar paridad exacta con ISO 4217 vigente y cerrar con slice `00-shared-kernel` (followup #4).
- [ ] #11 — servicio de dominio `GeneradorCodigosJerarquicos` — origen: slice-03, spec §13. Calcula el siguiente código disponible dado un padre (`padre + "." + siguiente segmento libre`), recodifica subramas en `RubroMovido`, y sugiere overrides válidos a la UI. Vive fuera del agregado `Presupuesto` porque requiere conocer todos los rubros del presupuesto y su política de numeración (hotspots §4). Disparador: primer slice que agregue UI de creación de rubros o slice `RubroMovido`.
- [ ] #12 — introducir `RubroTipo` (Agrupador/Terminal) y cubrir INV-9 — origen: slice-03, spec §13. Al implementar `AsignarMontoARubro` (slice futuro) o `RubroConvertidoAAgrupador` (hotspots §1), modelar la distinción de tipo y escribir el escenario "no se puede agregar hijo a un rubro terminal con monto asignado". Implica extender el payload de `RubroAgregado` con `RubroTipo` (o derivarlo del estado al fold).
- [ ] #13 — escenario INV-3 en slice `AprobarPresupuesto` — origen: slice-03, spec §10 Q1 opción (a) + §13. Cuando exista el comando `AprobarPresupuesto`, garantizar que el test "AgregarRubro sobre presupuesto aprobado lanza `PresupuestoNoEsBorradorException`" se escribe en ese slice o retroactivamente en slice 03. Hoy la rama "lanza" en `Presupuesto.AgregarRubro` (línea 114) existe pero no está cubierta por test.

## Cerrados

- [x] #5 — firma uniforme `CasoDeUso.Decidir(dados, cmd, ...) → IReadOnlyList<object>` — origen: slice-01, refactor-notes (pasada 2). **Cerrado como no aplicable en review de slice 03** — con 3 slices implementados (Create factory sobre stream vacío, AgregarRubro método de instancia sobre agregado reconstruido, Ejecutar sobre fold existente) la forma OO actual es consistente dentro de cada patrón y escala. Unificar a `Decidir(dados, cmd, …)` implicaría transformar todo el dominio a funciones libres sin beneficio demostrable. Recomendación del modeler (slice-03 spec §13) ratificada por reviewer.
- [x] #10 — helper `RequireCampo(string valor, string nombreCampo)` en `SharedKernel` que abstrae `if IsNullOrWhiteSpace → throw CampoRequeridoException`. Origen: slice-02, refactor-notes (descartado #3). Disparador: tercer uso. **Cerrado en slice 03 refactor** — helper `SharedKernel.Requerir.Campo` introducido (ver `slices/03-agregar-rubro/refactor-notes.md`). Sustituidas 6 ocurrencias en `Presupuesto.Create` (3), `Presupuesto.AgregarRubro` (2) y `ConfiguracionTenant.Create` (1). El chequeo `rubroId == Guid.Empty` no aplica (tipo `Guid`, no `string`); se deja inline.
