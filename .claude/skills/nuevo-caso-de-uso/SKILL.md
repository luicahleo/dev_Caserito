---
name: nuevo-caso-de-uso
description: Genera un caso de uso (command o query) de un bounded context de CaseritoApp siguiendo el patrón CQRS-lite (MediatR + Result + FluentValidation) y su test. Úsalo al añadir una operación a un contexto.
---

# Nuevo caso de uso

Genera un command o query en `CaseritoApp/src/<Ctx>/CaseritoApp.<Ctx>.Application`.
Recibe: contexto `<Ctx>`, nombre del caso `<Caso>`, y si es command o query.

## Patrón (command con Result)

`Application/<Area>/<Caso>Command.cs`:
- `public sealed record <Caso>Command(...) : ICommand;` (o `ICommand<TResp>` si devuelve valor).
- `internal sealed class <Caso>Handler : ICommandHandler<<Caso>Command> { ... devuelve Result.Exito()/Result.Fallo(error) ... }`
- `public sealed class <Caso>Validator : AbstractValidator<<Caso>Command> { ... }`

`ICommand`, `ICommandHandler`, `IQuery`, `IQueryHandler`, `Result` vienen de
`CaseritoApp.BuildingBlocks.Application.Messaging` y `.Domain`.

## Reglas

- Errores esperados → `Result.Fallo(new Error("codigo", "mensaje"))`; nunca excepciones para flujo esperado.
- Validación de entrada → un `AbstractValidator`; el `ValidationBehavior` la ejecuta.
- Un handler no llama a otro contexto directamente; se comunica por Id o emitiendo un evento.
- Nombres y comentarios en español; el handler `internal sealed`.

## Test

Añade en el proyecto de tests del contexto un test que ejerza el handler con un doble
de sus dependencias, verificando el `Result` (éxito y fallo esperado).

## Verificar

Desde `CaseritoApp/`: `dotnet build CaseritoApp.sln && dotnet test CaseritoApp.sln` en verde.
