# Diseño: Fase 0 — Fundaciones técnicas (bloque fundacional)

- **Fecha**: 2026-07-13
- **Estado**: Aprobado (brainstorming)
- **Alcance**: Bloque fundacional de la Fase 0 del `plan-desarrollo-mvp-v1.md`. Cierra las decisiones de arquitectura que desbloquean el esqueleto y entrega un esqueleto que compila con los 6 bounded contexts delimitados (vacíos), BuildingBlocks y el host Web API.

## Contexto

CaseritoApp es un marketplace C2C para Bolivia (ver `CaseritoApp/Documentacion/`). El brief prioriza **solidez de diseño sin atajos** (§8.2) y que el modelo de datos/eventos anticipe pago/envío/disputa sin refactor. El andamiaje de homogeneidad ya está montado (ver `2026-07-13-andamiaje-homogeneidad-design.md`): `.editorconfig`, analizadores estrictos, CPM, tests de arquitectura, CI, Husky, CLAUDE.md, hook de formato, subagente y skill `nuevo-bounded-context`. Esta fase construye sobre él.

**Fuera de alcance de este ciclo** (ciclos posteriores): modelo de datos detallado de cada contexto, recomendación de framework móvil, diseño de Payments/Shipping/Disputes (solo forma de eventos), outbox transaccional, OCR/liveness.

## Decisiones tomadas

| Tema | Decisión |
|---|---|
| Topología | Monolito modular (un despliegue, módulos con límites estrictos) |
| Patrón interno | CQRS-lite con **MediatR**; `Result<T>` para errores esperados; **FluentValidation** |
| Pipeline behaviors | Validación, logging, transacción (Unit of Work), publicación de eventos |
| Persistencia | **EF Core** sobre **SQL Server**; una BD, **schema por contexto**, un `DbContext` por contexto, sin FK entre schemas (referencias por Id) |
| Eventos | Domain events in-process (notificaciones MediatR) en el mismo commit; contratos de integración como POCO `record`; **outbox diferido** |
| RBAC | Permisos ≠ roles; autorización basada en permisos (claims + policies); "vendedor verificado" = atributo del Cliente |
| PII/KYC | Imágenes en object storage cifrado; BD solo metadatos+referencia+estado; envelope encryption para campos sensibles; auditoría append-only; retención |
| Entregable | Esqueleto que compila: 6 contextos vacíos + BuildingBlocks + host Web API |

> Nota: MediatR pasó a un modelo comercial en versiones recientes. Antes de fijar la versión, revisar los términos de licencia y el umbral de ingresos para uso comercial.

## Estructura de proyectos

```
CaseritoApp/
├─ src/
│  ├─ BuildingBlocks/
│  │  ├─ CaseritoApp.BuildingBlocks.Domain/         (Result<T>, IDomainEvent, Entity/AggregateRoot base)
│  │  ├─ CaseritoApp.BuildingBlocks.Application/     (ICommand/IQuery, behaviors MediatR, IUnitOfWork)
│  │  ├─ CaseritoApp.BuildingBlocks.Infrastructure/  (envelope encryption, auditoría, base EF Core)
│  │  └─ CaseritoApp.BuildingBlocks.Contracts/       (contratos de integración: eventos POCO record)
│  ├─ Identity/        (Domain / Application / Infrastructure)
│  ├─ Catalog/         (Domain / Application / Infrastructure)
│  ├─ Chat/            (Domain / Application / Infrastructure)
│  ├─ Orders/          (Domain / Application / Infrastructure)
│  ├─ Reputation/      (Domain / Application / Infrastructure)
│  ├─ Notifications/   (Domain / Application / Infrastructure)
│  └─ Host/
│     └─ CaseritoApp.Host/                           (ASP.NET Core Web API que compone los módulos)
└─ tests/
   └─ CaseritoApp.ArchitectureTests/                 (reglas de capa + de contexto)
```

Cada contexto sigue el patrón de la skill `nuevo-bounded-context`: `Application → Domain`, `Infrastructure → Application`; dependencias hacia adentro. El smoke lib de la Fase pre-0 (`CaseritoApp.SmokeLib`) se elimina en este ciclo (su función de validación ya la cumplen los contextos reales). Esto implica una **migración**: `PiiRedaction` (hoy en `SmokeLib/Logging`) y su test se trasladan a `BuildingBlocks.Infrastructure`; y los tests de arquitectura smoke (`LayeringTests` sobre `SmokeLib.Domain/Application`) se reemplazan por reglas reales por contexto. Esto debe hacerse en un orden que mantenga la solución compilando y los tests verdes en cada commit.

## BuildingBlocks (contenido)

- **Domain**: `Result` y `Result<T>` (éxito/fallo con error tipado); `IDomainEvent`; bases `Entity`, `AggregateRoot` (con registro de domain events).
- **Application**: marcadores `ICommand`/`ICommand<T>`/`IQuery<T>` y sus handlers sobre MediatR; behaviors: `ValidationBehavior` (FluentValidation), `LoggingBehavior`, `UnitOfWorkBehavior` (abre/commitea transacción y dispara los domain events registrados tras el commit); `IUnitOfWork`.
- **Infrastructure**: `IEncryptor` (envelope encryption, con implementación pinchable; la real se cablea al elegir KMS/hosting), `IPiiAccessAuditor` (registro append-only), base de configuración EF Core (convenciones, `HasDefaultSchema` por contexto).
- **Contracts**: eventos de integración como `record` inmutables, p. ej. `OrderStatusChanged(Guid OrderId, string OldStatus, string NewStatus, DateTimeOffset Timestamp)`. Son el contrato que consumirán Payments/Shipping/Disputes.

## Eventos de dominio

- Los agregados registran domain events; el `UnitOfWorkBehavior` los publica (vía MediatR `INotification`) tras confirmar la transacción del command, dentro del mismo flujo (sin outbox por ahora).
- Los contratos de integración viven en `Contracts` y hoy no tienen publicador a un bus; se definen para que su forma no cambie al introducir el bus. La migración a **outbox transaccional + bus** es aditiva (nuevo behavior/worker), sin tocar los agregados.

## RBAC

- Tabla de permisos (string estables, p. ej. `kyc.approve`, `listing.moderate`), roles como conjuntos de permisos, y asignación usuario→roles.
- Autorización por **policy basada en permiso** (un `PermissionRequirement`/handler que traduce permisos a policies de ASP.NET Core), nunca `if (role == "Admin")`.
- Roles definidos (brief §8.1): Cliente, Moderador, Admin KYC, Admin plataforma, y Soporte + Sistema declarados aunque no operativos. "Vendedor verificado" = estado del Cliente (KYC aprobado), no rol.

## PII / KYC

- Documento (CI) y selfie → object storage cifrado; la BD (schema `identity`) guarda metadatos, referencia al blob y estado (`Pendiente/Aprobado/Rechazado`).
- Campos sensibles textuales (nº de documento) → envelope encryption vía `IEncryptor`.
- Todo acceso a un documento pasa por un servicio que registra en un log de auditoría append-only (quién, qué, cuándo).
- Estructura pensada para migrar a OCR/liveness sin re-solicitar documentos (brief §4.4). Política de retención documentada.

## Entregable y "listo cuando"

- Solución compila con rigor estricto (0 warnings) y `dotnet format --verify-no-changes` limpio.
- Los 6 contextos existen como proyectos por capa (vacíos de features), agregados a la solución, con sus reglas de capa verificadas por `CaseritoApp.ArchitectureTests`.
- `BuildingBlocks` provee Result, behaviors, contratos y abstracciones (encryption, auditoría, UoW).
- El host `CaseritoApp.Host` arranca (health endpoint) y registra los módulos y el pipeline MediatR.
- El documento técnico (este spec) queda aprobado.

## Verificación

1. `dotnet build CaseritoApp.sln` → 0 warnings / 0 errors.
2. `dotnet format CaseritoApp.sln --verify-no-changes` → sin cambios.
3. `dotnet test CaseritoApp.sln` → tests de arquitectura verdes (capas por contexto + regla de que ningún contexto referencia el ensamblado de otro contexto).
4. El host arranca y `GET /health` responde 200.
5. Cada contexto tiene su `DbContext` con `HasDefaultSchema("<contexto>")` (verificable por test o inspección), aunque aún sin entidades.

## Fuera de alcance (explícito, a ciclos posteriores)

- Modelo de datos detallado de cada contexto (entidades, propiedades, migraciones con tablas reales).
- Recomendación de framework móvil (Flutter/RN/MAUI) — ciclo aparte.
- Diseño interno de Payments/Shipping/Disputes (solo la forma de sus eventos aquí).
- Outbox transaccional + bus de mensajería.
- KYC automático (OCR/liveness).
- Completar la skill `nuevo-caso-de-uso` con el patrón CQRS-lite ya decidido (puede hacerse como parte del plan de este ciclo o del siguiente).
