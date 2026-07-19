# Backend .NET

Aplican primero las reglas del `AGENTS.md` raíz.

- Clean Architecture: Domain no depende hacia afuera; Application solo de
  Domain; Infrastructure implementa puertos; Host compone y autoriza.
- No crear referencias entre bounded contexts salvo contratos permitidos.
- CQRS-lite: MediatR + Result + FluentValidation; reglas de negocio en dominio.
- Policies y detalles de Identity permanecen en Host; no filtrarlos a Catalog u
  otros contextos.
- Versiones exclusivamente en `Directory.Packages.props`.
- Respetar `.editorconfig`, nullable y warnings-as-errors.
- Migraciones por contexto y schema; sin FK cruzada entre contextos.

Comandos desde este directorio:

```powershell
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes
```

Tests de integración requieren Docker por Testcontainers.MsSql. Empezar con
tests dirigidos y ejecutar la suite completa solo al cierre o ante cambios de
integración transversal.
