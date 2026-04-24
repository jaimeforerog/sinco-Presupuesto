# Green notes — Slice 01 — CrearPresupuesto

**Autor:** green (retroactivo)
**Fecha:** 2026-04-24
**Spec:** `slices/01-crear-presupuesto/spec.md`.

**Nota de retroactividad:** la fase green de este slice ocurrió como parte del scaffold inicial, antes de acordar la metodología. No hubo disciplina "mínimo código para pasar el rojo" porque no había rojo. Este documento describe el estado del código resultante para que `refactorer` y `reviewer` puedan auditarlo.

---

## 1. Archivos de producción que realizan el slice

| Archivo | Rol |
|---|---|
| `src/SincoPresupuesto.Domain/Presupuestos/Presupuesto.cs` | Agregado raíz con `Create(cmd, id, ahora)` y `Apply(PresupuestoCreado)`. |
| `src/SincoPresupuesto.Domain/Presupuestos/EstadoPresupuesto.cs` | Enum del ciclo de vida. |
| `src/SincoPresupuesto.Domain/Presupuestos/Commands/CrearPresupuesto.cs` | Record del comando. |
| `src/SincoPresupuesto.Domain/Presupuestos/Events/PresupuestoCreado.cs` | Record del evento. |
| `src/SincoPresupuesto.Domain/SharedKernel/Moneda.cs` | Value object ISO 4217. |
| `src/SincoPresupuesto.Domain/SharedKernel/Dinero.cs` | Value object monto + moneda. |
| `src/SincoPresupuesto.Application/Presupuestos/CrearPresupuestoHandler.cs` | Handler Wolverine (infra-wire). |
| `src/SincoPresupuesto.Application/Presupuestos/PresupuestoProjection.cs` | Proyección inline. |
| `src/SincoPresupuesto.Application/Presupuestos/PresupuestoReadModel.cs` | Read model. |
| `src/SincoPresupuesto.Api/Endpoints/PresupuestoEndpoints.cs` | Endpoints HTTP (infra-wire). |

## 2. Candidatos para refactor identificados

### 2.1 `MonedasDistintasException` usa sintaxis experimental de primary constructor

`Dinero.cs` define la excepción así:

```csharp
public sealed class MonedasDistintasException(Moneda izquierda, Moneda derecha)
    : InvalidOperationException(...) { ... }
```

Es C# 12 válido, pero mezcla primary constructor con miembros expuestos. Sugerencia: evaluar si el lector lo entiende a primera vista; si no, convertir a constructor clásico. Decisión del refactorer.

### 2.2 `Presupuesto.Create` valida por cada campo en bloques `if` independientes

Podría extraerse un `ValidationResult` o un método helper `AssertRequerido(string valor, string nombre)` para reducir repetición. Aun así son 4 checks — no es obvio que el patrón pague. Decisión del refactorer.

### 2.3 Código duplicado de normalización

`Codigo.Trim()` y `Nombre.Trim()` aparecen en `Presupuesto.Create`. Si aparecen más campos trimables en slices futuros, extraer a helper. Por ahora, dos usos no justifican abstracción.

### 2.4 Moneda como struct con atajos estáticos

`Moneda.COP`, `Moneda.USD`, etc., son convenientes pero introducen acoplamiento a una lista hardcoded. Considerar si se mantienen o si se lee de un catálogo de monedas soportadas.

### 2.5 `Presupuesto.Apply(PresupuestoCreado)` es método público

Marten lo descubre por convención pero ser `public` expone mutación del agregado al caller. Explorar si Marten soporta `internal Apply` con `InternalsVisibleTo`. Posterga a slice de infra.

## 3. Decisiones deliberadas de simplicidad

- No hay validación de `Moneda` soportada por el tenant (p. ej. "COP está permitida para acme"). El dominio acepta cualquier ISO 4217 válida. Slice futuro cuando introduzcamos `ConfiguracionTenant`.
- No hay unicidad `(TenantId, Codigo, Periodo)` aún. Diferido a slice dedicado (spec §4 PRE-6).
- El handler no valida `tenantId` del path HTTP contra `cmd.TenantId`. Actualmente el endpoint los iguala en construcción. Slice futuro si se expone otro canal de ingesta.

## 4. `dotnet build` y `dotnet test`

**Retroactivo — no ejecutado en sandbox (sin .NET SDK).** El usuario debe correr localmente:

```bash
dotnet restore
dotnet build
dotnet test
```

Si alguna cosa falla, pasa el output al orquestador y se corrige antes de cerrar review.
