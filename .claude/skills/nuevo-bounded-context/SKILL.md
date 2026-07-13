---
name: nuevo-bounded-context
description: Genera el esqueleto de proyectos por capas (Domain, Application, Infrastructure) de un nuevo bounded context de CaseritoApp, respetando Clean Architecture y CPM. Úsalo al crear un contexto nuevo (Identity, Catalog, etc.).
---

# Nuevo bounded context

Crea la estructura estándar de un bounded context bajo `CaseritoApp/src/`.
Recibe el nombre del contexto (p. ej. `Catalog`) como argumento.

## Pasos

Sea `<Ctx>` el nombre en PascalCase (ej. `Catalog`). Desde `CaseritoApp/`:

1. Crear los tres proyectos por capa (sin `Version=`, CPM los resuelve):
   ```bash
   dotnet new classlib -n CaseritoApp.<Ctx>.Domain      -o src/<Ctx>/Domain
   dotnet new classlib -n CaseritoApp.<Ctx>.Application  -o src/<Ctx>/Application
   dotnet new classlib -n CaseritoApp.<Ctx>.Infrastructure -o src/<Ctx>/Infrastructure
   rm src/<Ctx>/Domain/Class1.cs src/<Ctx>/Application/Class1.cs src/<Ctx>/Infrastructure/Class1.cs
   ```

2. Referencias hacia adentro:
   ```bash
   dotnet add src/<Ctx>/Application reference src/<Ctx>/Domain
   dotnet add src/<Ctx>/Infrastructure reference src/<Ctx>/Application
   ```

3. Agregar a la solución:
   ```bash
   dotnet sln add src/<Ctx>/Domain src/<Ctx>/Application src/<Ctx>/Infrastructure
   ```

4. Añadir la regla de capas del nuevo contexto en
   `tests/CaseritoApp.ArchitectureTests` (copiar el patrón de `LayeringTests.cs`,
   sustituyendo los namespaces por `CaseritoApp.<Ctx>.Domain` y `.Application`).

5. Verificar:
   ```bash
   dotnet build CaseritoApp.sln && dotnet test CaseritoApp.sln
   ```
   Ambos deben terminar en verde.

## Nota

El patrón interno (CQRS/MediatR/Result, validación, mapeo) se define en Fase 0.
Esta skill solo crea el esqueleto de capas; no impone patrón de casos de uso.
