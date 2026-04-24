# Metodología de trabajo — Sinco Presupuesto

**Estado:** vigente desde 2026-04-24
**Alcance:** aplica a todo código de producción del MVP y fases siguientes.
**Tipo de metodología:** TDD estricto (Given/When/Then sobre eventos) + squad multiagente especializado de 5 roles.

---

## 1. Principios rectores

1. **Ni una línea de producción sin un test rojo que la justifique.** Si el código existe sin test previo que lo fuerce, se borra o se marca como deuda técnica.
2. **Los tests hablan de comportamiento, no de estado interno.** En un dominio event-sourced, el comportamiento observable es el conjunto de eventos emitidos (o la excepción de invariante). Los tests se escriben en esa misma forma.
3. **Un test rojo, un test verde, un refactor.** No se escribe un segundo test sin haber refactorizado el código que pasó el anterior. El refactor puede ser "ninguno" (y así se registra), pero la decisión es consciente.
4. **Los handoffs entre agentes son con artefactos, no con contexto implícito.** Cada agente produce un archivo concreto que el siguiente consume. Nada de "seguí trabajando sobre lo que dejé hablado".
5. **La unidad de trabajo es un comando del dominio.** Un commit = un comando con su spec, sus tests, su implementación y su review firmada.

---

## 2. Ciclo TDD aplicado a Event Sourcing

### 2.1 Forma canónica Given / When / Then

```csharp
[Fact]
public void AgregarRubro_en_un_presupuesto_en_borrador_emite_RubroAgregado()
{
    // GIVEN: historial de eventos previos
    var dados = new object[]
    {
        new PresupuestoCreado(/*...*/),
    };

    // WHEN: un comando
    var cmd = new AgregarRubro(/*...*/);
    var resultado = Decidir(dados, cmd);

    // THEN: eventos esperados (o excepción de invariante)
    resultado.Should().ContainSingle().Which.Should()
        .BeOfType<RubroAgregado>()
        .Which.Codigo.Should().Be("1.01");
}
```

El `Decidir(historial, comando)` es una función pura derivada del agregado: hace `fold` de los eventos para reconstruir el estado, ejecuta el comando, y devuelve los eventos nuevos. No toca Marten ni base de datos — esos tests viven en el nivel de integración.

### 2.2 Fases del ciclo

| Fase | Qué se hace | Quién | Criterio de paso |
|---|---|---|---|
| **0. Spec** | Define eventos, comandos, invariantes del slice. | `domain-modeler` | Firma del orquestador (usuario). |
| **1. Red** | Escribir tests que fallen, uno por escenario de la spec. | `red` | Todos los tests compilan y fallan. |
| **2. Green** | Código mínimo para pasar el **último** test rojo. | `green` | Todos los tests compilan y pasan. |
| **3. Refactor** | Limpiar sin cambiar comportamiento. | `refactorer` | Tests siguen pasando; warnings en cero. |
| **4. Review** | Auditar el slice completo. | `reviewer` | Review notes firmadas. |

Se pasa a la siguiente fase solo cuando se cumple el criterio. Nunca se solapan.

### 2.3 Mocks y dobles de prueba

- **Agregados**: cero mocks. Siempre Given/When/Then sobre eventos reales.
- **Handlers**: cero mocks de `IDocumentSession`. Se prueban con **Marten embebido** (testcontainers Postgres) en nivel de integración, no unitario.
- **Endpoints HTTP**: `WebApplicationFactory<Program>` con BD efímera.
- **Servicios externos** (FX rates, email): interfaz en Domain, mock solo en test del handler que los consume.

---

## 3. Squad de agentes (5 roles)

Cada rol tiene un **prompt persona** estable (ver §5), consume artefactos específicos y produce artefactos específicos. El orquestador (la conversación principal con el usuario) es quien invoca a cada agente vía la herramienta Agent.

### 3.1 `domain-modeler`

**Consume:** pregunta del usuario o decisión previa (p.ej. event storming).
**Produce:** `slices/{N}-{comando}/spec.md` siguiendo la plantilla `templates/slice-spec.md`.
**Regla:** nunca escribe código ni tests. Si la spec requiere descubrir más del negocio, lo nota explícitamente.

### 3.2 `red`

**Consume:** `slices/{N}-{comando}/spec.md`.
**Produce:** archivos de test nuevos/modificados bajo `tests/`, más `slices/{N}-{comando}/red-notes.md`.
**Regla:** todos los tests deben compilar y fallar con mensaje claro. Si un test falla por "no compila" se considera no-rojo y se corrige.

### 3.3 `green`

**Consume:** tests rojos.
**Produce:** cambios en `src/` mínimos para pasar el último test rojo, sin tocar otros.
**Regla:** prohibido refactorizar. Prohibido agregar código que ningún test ejerza. Si el impulso aparece, se anota en `green-notes.md` como candidato para refactor.

### 3.4 `refactorer`

**Consume:** código que pasa todos los tests, notas de green.
**Produce:** diff de refactor + `refactor-notes.md` (qué cambió y por qué).
**Regla:** los tests no se tocan (salvo renombrar). Cero cambios de comportamiento.

### 3.5 `reviewer`

**Consume:** todo el slice (spec + tests + impl + notas).
**Produce:** `review-notes.md` con uno de tres veredictos: **approved**, **approved-with-followups**, **request-changes**.
**Regla:** si `request-changes`, vuelve al rol correspondiente (red, green o refactorer) con los puntos específicos.

### 3.6 Roles que asume el orquestador

- **infra-wire**: registrar handler en Wolverine, proyección en Marten, endpoint HTTP, DTOs. Se ejecuta **después** de que el slice pasó review.
- **azure-ops**: bicep, pipelines, observabilidad. Cadencia por hito, no por slice.
- **doc-writer**: ADR o actualización del README cuando hay una decisión arquitectónica o cambio de contrato público.

---

## 4. Workflow por comando (ejemplo completo)

Supongamos que toca implementar `AgregarRubro`:

```
1. Usuario:     "Sigamos con AgregarRubro."
2. Orquestador: invoca domain-modeler con event-storming §4 como input.
                → produce slices/02-agregar-rubro/spec.md
                → usuario firma.
3. Orquestador: invoca red con la spec.
                → produce tests/.../AgregarRubroTests.cs (rojo)
                → verifica que compilan y fallan.
4. Orquestador: invoca green con los tests.
                → produce cambios en src/
                → verifica que todos los tests pasan.
5. Orquestador: invoca refactorer.
                → produce diff limpio + notas.
                → verifica que los tests siguen pasando.
6. Orquestador: invoca reviewer.
                → si approved → commit.
                → si request-changes → vuelve a (3) o (4) según aplique.
7. Orquestador: como infra-wire, registra el handler, expone el endpoint,
                actualiza OpenAPI, escribe test de integración HTTP→PG.
8. Orquestador: presenta el slice cerrado al usuario.
```

Estructura de carpeta del slice:

```
slices/
  02-agregar-rubro/
    spec.md
    red-notes.md
    green-notes.md
    refactor-notes.md
    review-notes.md
```

Los slices son archivos vivos dentro del repo y se preservan como trazabilidad. Se borran solo si un slice se abandona.

---

## 5. Prompts persona de los agentes

Los prompts completos de cada rol viven en `templates/agent-personas/` (se crean en la próxima iteración; por ahora aplica este contrato):

- **domain-modeler**: "Eres un domain modeler experto en Event Sourcing. Tu única tarea es producir una spec siguiendo `templates/slice-spec.md`. No escribes código. No propones implementación. Si la spec requiere más info, anótalo explícitamente en un bloque `# Preguntas abiertas`."

- **red**: "Eres un test writer TDD estricto. Recibes una spec y escribes tests Given/When/Then en xUnit + FluentAssertions. Prohibido escribir código de producción. Prohibido tocar tests que no correspondan al slice. Prohibidos los mocks del dominio."

- **green**: "Eres un implementer minimalista. Tu único objetivo es hacer pasar el último test rojo con el código más simple posible. Prohibido refactorizar, prohibido anticipar requerimientos futuros, prohibido añadir código que ningún test ejerza."

- **refactorer**: "Eres un refactorer disciplinado. Tu único objetivo es limpiar tras una fase green sin cambiar comportamiento. Los tests deben seguir pasando idénticos. Si notas que un test está mal diseñado, lo anotas en `refactor-notes.md` pero no lo tocas — eso lo decide el reviewer."

- **reviewer**: "Eres un revisor técnico con ojo de auditor. Tu veredicto es uno de: approved / approved-with-followups / request-changes. Auditas cobertura de ramas del agregado, claridad del test como documentación del comportamiento, completitud de invariantes, y coherencia con las decisiones previas (event storming, hot spots, multimoneda)."

---

## 6. Definition of Done de un slice

Un slice se considera cerrado cuando **todos** estos ítems están marcados:

- [ ] `spec.md` firmado por el usuario.
- [ ] Tests Given/When/Then cubren todos los escenarios de la spec (happy path + cada invariante + cada precondición).
- [ ] `dotnet test` en verde, sin warnings tratados como error.
- [ ] Cobertura de ramas del agregado afectado reportada (objetivo inicial **≥ 85 %**; se ajusta por ADR si hay rama genuinamente inalcanzable).
- [ ] `refactor-notes.md` presente (aunque diga "sin cambios").
- [ ] `review-notes.md` con veredicto **approved** o **approved-with-followups** (en el segundo caso los follow-ups van a un archivo `FOLLOWUPS.md` del repo).
- [ ] Handler registrado en Wolverine; proyección en Marten si aplica.
- [ ] Endpoint HTTP expuesto y documentado en OpenAPI si el slice lo implica.
- [ ] Test de integración HTTP → Postgres pasa para el happy path.
- [ ] Commit único con mensaje `feat(slice-{N}): {comando}` y referencia al `spec.md`.

---

## 7. Excepciones y reglas de pragmatismo

1. **Spike de exploración.** Cuando un problema requiere entender una API externa (p.ej. Wolverine), se hace un spike en rama aparte. Se tira todo y se reimplementa en TDD. El spike nunca se mergea.
2. **Refactor sin cambio de comportamiento en otro slice.** Si el refactorer necesita tocar código fuera del slice actual, abre un slice `refactor-{N}` separado con sus propios tests de regresión.
3. **Bug en producción.** TDD al revés: test que reproduce el bug (rojo) → fix (verde) → refactor. Mismo workflow, distinta semántica.

---

## 8. Contratos de calidad del código

- `nullable` habilitado, `TreatWarningsAsErrors=true` en todos los proyectos.
- Naming en español para conceptos de dominio (`Presupuesto`, `Rubro`, `Moneda`) y en inglés para plumbing (`Program`, `Handler`, `Projection`).
- Records para eventos y comandos; clases para agregados.
- `TimeProvider` inyectado — prohibido `DateTime.UtcNow` en dominio.
- `Guid.NewGuid()` solo en handlers; en dominio se recibe el id desde fuera.
