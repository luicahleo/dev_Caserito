# Plan de implementación — Fase 5: reputación no manipulable

**Spec:** `docs/superpowers/specs/2026-07-25-reputacion-fase-5-design.md`  
**Rama:** `feature/fase-5-reputacion`  
**Alcance:** Reputation, adaptadores mínimos de Orders/Identity/Catalog en Host,
contrato OpenAPI y UI web. Sin pagos, QR, envíos, notificaciones ni disputas.

## Arquitectura

El bounded context Reputation implementa el agregado inmutable `Resena`,
CQRS-lite con MediatR + `Result` + FluentValidation y persistencia EF Core en el
schema `reputation`. Application consulta una referencia calificable mediante un
puerto; el Host lo adapta a Orders sin añadir referencias entre contextos.

El perfil público se compone en el Host a partir de una consulta pública mínima
de Identity y consultas públicas de Reputation. Catalog solo expone el GUID opaco
del vendedor en su DTO público. Ninguna tabla incorpora FK cruzadas.

## Reglas de ejecución

Para cada tarea:

1. escribir el test mínimo;
2. ejecutar el test y observar el fallo causal esperado;
3. implementar lo mínimo;
4. repetir el test dirigido hasta verde;
5. ejecutar vecinos proporcionales;
6. revisar capas, concurrencia, errores y anti-PII;
7. hacer el commit indicado.

Los comandos backend se ejecutan desde `CaseritoApp/`; los frontend desde `web/`.
No registrar requests, comentarios, puntuaciones, IDs de participantes, órdenes,
emails, tokens ni información KYC.

## Tarea 1 — Modelar la reseña en Domain

**Entradas**

- `AggregateRoot`, `Result`, `Error`.
- Invariantes del spec: GUID no vacíos, participantes distintos, rol,
  puntuación, comentario y UTC.

**Prueba roja**

Crear:

```text
CaseritoApp/tests/CaseritoApp.UnitTests/Reputation/ResenaTests.cs
```

Casos:

- crea y normaliza una reseña válida;
- admite límites 1/5 y comentario 10/500;
- rechaza puntuación fuera de rango;
- rechaza comentario vacío, corto o mayor de 500;
- rechaza GUID vacíos, autor igual a destinatario y rol desconocido;
- convierte la fecha a UTC.

Comando:

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj `
  --filter FullyQualifiedName~Reputation.ResenaTests
```

Fallo esperado: no existen `Resena`, `RolAutorResena` ni errores de Reputation.

**Implementación**

Crear:

```text
CaseritoApp/src/Reputation/CaseritoApp.Reputation.Domain/Resenas/Resena.cs
CaseritoApp/src/Reputation/CaseritoApp.Reputation.Domain/Resenas/RolAutorResena.cs
CaseritoApp/src/Reputation/CaseritoApp.Reputation.Domain/Resenas/ErroresResena.cs
```

Contrato de creación:

```text
Resena.Crear(
    orderId,
    autorId,
    destinatarioId,
    rolAutor,
    puntuacion,
    comentario,
    ocurrioEn) -> Result<Resena>
```

`Comentario` se persiste con `Trim()`. No se crean métodos de edición, borrado o
revelación. `Version` queda disponible para EF como `rowversion`.

**Verificación**

- test dirigido verde;
- suite unitaria de Reputation con 0 fallos;
- `git diff --check`.

**Commit**

```text
feat(reputation): modela resenas inmutables
```

## Tarea 2 — Definir casos de uso y consultas de Application

**Entradas**

- agregado de tarea 1;
- `ICommand`, `IQuery`, `Result`, FluentValidation;
- elegibilidad resuelta fuera del contexto.

**Prueba roja**

Crear:

```text
CaseritoApp/tests/CaseritoApp.UnitTests/Reputation/CrearResenaCommandHandlerTests.cs
CaseritoApp/tests/CaseritoApp.UnitTests/Reputation/ConsultasReputationHandlerTests.cs
```

Dobles en memoria para:

- orden calificable;
- repositorio de reseñas;
- consulta de reseñas.

Casos command:

- participante elegible crea una reseña con destinatario y rol derivados;
- orden no disponible produce `reputacion_orden_no_disponible`;
- duplicado produce `reputacion_resena_duplicada`;
- el handler no acepta IDs de autor/destinatario desde el request;
- el validador exige puntuación 1–5 y comentario 10–500.

Casos query:

- estado no devuelve contenido de la reseña;
- estado indica pendiente propia, espera y par revelado;
- tercero/orden no disponible recibe el mismo error genérico;
- resumen excluye reseñas sin par;
- promedio se redondea a una cifra y conteo es correcto;
- listado incluye solo pares completos, orden estable y paginación.

Fallo esperado: no existen puertos, commands, queries ni DTO.

**Implementación**

Crear bajo:

```text
CaseritoApp/src/Reputation/CaseritoApp.Reputation.Application/Resenas/
```

Archivos:

```text
IRepositorioResenas.cs
IConsultaResenas.cs
IConsultaOrdenCalificable.cs
DtosReputation.cs
CrearResenaCommand.cs
ObtenerEstadoResenaOrdenQuery.cs
ObtenerResumenReputacionQuery.cs
ListarResenasPublicasQuery.cs
```

Contratos principales:

```text
OrdenCalificable(OrderId, AutorId, DestinatarioId, RolAutor)

EstadoResenaOrdenDto(
    PuedeCalificar,
    AutorYaCalifico,
    ContraparteYaCalifico,
    Reveladas,
    EnviadaEn,
    ContraparteId)

ResumenReputacionDto(Promedio, Total)

ResenaPublicaDto(Puntuacion, Comentario, CreadaEn, RolAutor)
```

El command recibe únicamente `OrderId`, `ActorId`, `Puntuacion` y `Comentario`.
Las consultas públicas no devuelven IDs internos.

Agregar referencia de `CaseritoApp.UnitTests` a Reputation Application/Domain si
todavía no existe.

**Verificación**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj `
  --filter FullyQualifiedName~Reputation
```

Resultado esperado: todos los tests de Reputation verdes.

**Commit**

```text
feat(reputation): agrega casos de uso de resenas
```

## Tarea 3 — Persistir Reputation y proteger concurrencia

**Entradas**

- agregado y puertos de tareas 1–2;
- precedentes `OrdersDbContext`, configuración, repositorio, unit of work y
  design-time factory.

**Prueba roja**

Crear:

```text
CaseritoApp/tests/CaseritoApp.IntegrationTests/ReputationPersistenciaTests.cs
CaseritoApp/tests/CaseritoApp.ArchitectureTests/Persistence/ReputationSchemaTests.cs
```

Casos:

- guarda y recupera una reseña;
- el segundo `(OrderId, AuthorId)` viola unicidad;
- dos autores de la misma orden son válidos;
- consulta pública no devuelve un registro unilateral;
- con el par devuelve ambos y calcula el agregado;
- tabla en schema `reputation`, sin FK a otros schemas.

Fallo esperado: DbContext sin `DbSet`, configuración, repositorio ni migración.

**Implementación**

Crear:

```text
CaseritoApp/src/Reputation/CaseritoApp.Reputation.Infrastructure/Resenas/ConfiguracionResena.cs
CaseritoApp/src/Reputation/CaseritoApp.Reputation.Infrastructure/Resenas/RepositorioResenasEfCore.cs
CaseritoApp/src/Reputation/CaseritoApp.Reputation.Infrastructure/Resenas/ConsultaResenasEfCore.cs
CaseritoApp/src/Reputation/CaseritoApp.Reputation.Infrastructure/UnitOfWorkReputation.cs
CaseritoApp/src/Reputation/CaseritoApp.Reputation.Infrastructure/ConflictoUnicidadReputationException.cs
CaseritoApp/src/Reputation/CaseritoApp.Reputation.Infrastructure/DependencyInjection.cs
CaseritoApp/src/Reputation/CaseritoApp.Reputation.Infrastructure/DesignTimeReputationDbContextFactory.cs
```

Actualizar:

```text
CaseritoApp/src/Reputation/CaseritoApp.Reputation.Infrastructure/ReputationDbContext.cs
CaseritoApp/src/Reputation/CaseritoApp.Reputation.Infrastructure/CaseritoApp.Reputation.Infrastructure.csproj
```

Configurar:

- tabla `Reviews`;
- longitudes y `rowversion`;
- índice único `(OrderId, AuthorId)`;
- índices `(OrderId, RecipientId)` y
  `(RecipientId, CreatedAt, Id)`;
- traducción de SQL 2601/2627 a conflicto genérico.

Generar migración:

```powershell
dotnet ef migrations add ReputationInicial `
  --project src/Reputation/CaseritoApp.Reputation.Infrastructure `
  --startup-project src/Host/CaseritoApp.Host `
  --context ReputationDbContext
```

La migración debe crear solo el schema `reputation` y `Reviews`.

**Verificación**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj `
  --filter FullyQualifiedName~ReputationPersistenciaTests
dotnet test tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj `
  --filter FullyQualifiedName~ReputationSchemaTests
```

Requiere Docker para el primer comando. Resultado esperado: tests dirigidos
verdes y snapshot coherente.

**Commit**

```text
feat(reputation): persiste resenas y garantiza unicidad
```

## Tarea 4 — Adaptar Orders y exponer endpoints autenticados

**Entradas**

- puerto `IConsultaOrdenCalificable`;
- Orders conserva participantes y estado, pero sus DTO HTTP no los exponen;
- políticas de rate limiting y mapeo Problem Details existentes.

**Prueba roja**

Crear:

```text
CaseritoApp/tests/CaseritoApp.IntegrationTests/ReputationFlujoTests.cs
CaseritoApp/tests/CaseritoApp.IntegrationTests/ReputationAutorizacionTests.cs
```

Casos:

- `Completed` devuelve estado y permite crear;
- `Requested`, `Agreed`, `Cancelled` y `MarkedAsSold` no permiten crear;
- tercero y orden inexistente reciben `404` indistinguible;
- una reseña crea `201`;
- repetición devuelve `409`;
- la primera no filtra contenido;
- dos envíos simultáneos del mismo autor dejan una fila;
- dos participantes simultáneos dejan dos filas;
- sin autenticación devuelve `401`;
- puntuación/comentario inválidos devuelven `400`.

Fallo esperado: no existe adaptador, composición DI ni endpoints.

**Implementación**

Añadir a Orders Application una consulta interna mínima:

```text
CaseritoApp/src/Orders/CaseritoApp.Orders.Application/Ordenes/IConsultaOrdenCalificable.cs
```

Implementar en Infrastructure:

```text
CaseritoApp/src/Orders/CaseritoApp.Orders.Infrastructure/Ordenes/ConsultaOrdenCalificableEfCore.cs
```

El contrato de Orders devuelve participantes únicamente al adaptador del Host,
no al endpoint ni a los DTO actuales de órdenes.

Crear en Host:

```text
CaseritoApp/src/Host/CaseritoApp.Host/Reputation/ConsultaOrdenCalificableAdapter.cs
CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/ReputationEndpoints.cs
```

Actualizar:

```text
CaseritoApp/src/Host/CaseritoApp.Host/Program.cs
CaseritoApp/src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj
```

Cambios:

- registrar ensamblado y validadores de Reputation;
- `AgregarReputation`;
- registrar adaptador;
- migrar `ReputationDbContext` al arranque controlado;
- mapear endpoints;
- políticas `reputation-crear` y `reputation-consultas`;
- traducir conflicto único a `409` sin contenido sensible.

No modificar `OrderStatusChanged`, `OrdenDetalleDto` ni la máquina de estados.

**Verificación**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj `
  --filter "FullyQualifiedName~ReputationFlujoTests|FullyQualifiedName~ReputationAutorizacionTests"
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj `
  --filter FullyQualifiedName~Orders
```

Resultado esperado: nuevos flujos y regresión unitaria de Orders verdes.

**Commit**

```text
feat(host): integra reputation con ordenes completadas
```

## Tarea 5 — Componer perfil público y enlazar vendedor

**Entradas**

- resumen/listado público de Reputation;
- perfil privado actual de Identity;
- detalle público de Catalog sin vendedor.

**Prueba roja**

Crear o ampliar:

```text
CaseritoApp/tests/CaseritoApp.UnitTests/Identity/PerfilPublicoTests.cs
CaseritoApp/tests/CaseritoApp.IntegrationTests/ReputationPerfilPublicoTests.cs
CaseritoApp/tests/CaseritoApp.IntegrationTests/DescubrimientoAvisosTests.cs
```

Casos:

- perfil público existente devuelve nombre, ciudad y verificación;
- nunca devuelve email, roles, permisos o datos KYC;
- usuario inexistente devuelve `404`;
- sin reseñas devuelve promedio `null` y total cero;
- primera reseña unilateral no cambia el resumen;
- par revelado actualiza promedio y listado;
- listado pagina y ordena estable;
- detalle de aviso contiene solo `vendedorId` opaco adicional.

Fallo esperado: no existen consulta pública de Identity, composición ni campo de
Catalog.

**Implementación**

Identity:

```text
CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Perfil/ObtenerPerfilPublicoQuery.cs
```

Extender el puerto/repositorio de perfil con lectura pública que proyecte:

```text
PerfilPublicoDto(Id, Nombre, Ciudad, Verificado)
```

No reutilizar `PerfilDto` porque contiene email.

Catalog:

```text
CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/DtosAvisoPublico.cs
CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/ConsultaAvisosPublicaEfCore.cs
```

Añadir `VendedorId` solo a `AvisoPublicoDto`, no al resumen de búsqueda.

Host:

```text
CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/PerfilesPublicosEndpoints.cs
```

Componer Identity + Reputation y mapear:

```text
GET /api/publico/usuarios/{id}
GET /api/publico/usuarios/{id}/resenas
```

Aplicar `reputation-publico`. Las reseñas no exponen IDs de orden, autor o
destinatario.

**Verificación**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj `
  --filter "FullyQualifiedName~ReputationPerfilPublicoTests|FullyQualifiedName~DescubrimientoAvisosTests"
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj `
  --filter FullyQualifiedName~PerfilPublicoTests
```

Resultado esperado: tests dirigidos verdes.

**Commit**

```text
feat(host): publica perfiles con reputacion revelada
```

## Tarea 6 — Proteger arquitectura y anti-PII

**Entradas**

- implementación backend completa;
- reglas de aislamiento y logging existentes.

**Prueba roja**

Crear:

```text
CaseritoApp/tests/CaseritoApp.ArchitectureTests/Reputation/ReputationArchitectureTests.cs
CaseritoApp/tests/CaseritoApp.ArchitectureTests/Reputation/ReputationPiiTests.cs
```

Casos estáticos/dinámicos:

- Domain no referencia capas externas;
- Application solo referencia Domain y BuildingBlocks permitidos;
- Infrastructure no referencia Orders, Identity o Catalog;
- no hay FK cruzadas;
- endpoints/DTO públicos no contienen email, `OrderId`, autor o destinatario;
- logging no usa request, comentario, puntuación o IDs de negocio;
- errores no interpolan contenido sensible.

Fallo esperado: los nuevos proyectos todavía no están cubiertos por guardrails
específicos o referencias de tests.

**Implementación**

Actualizar referencias de:

```text
CaseritoApp/tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj
CaseritoApp/CaseritoApp.sln
```

Corregir cualquier hallazgo sin relajar tests ni reglas globales.

**Verificación**

```powershell
dotnet test tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj `
  --filter FullyQualifiedName~Reputation
```

Resultado esperado: guardrails de Reputation verdes.

**Commit**

```text
test(reputation): protege aislamiento y anti pii
```

## Tarea 7 — Regenerar OpenAPI y crear adaptadores web

**Entradas**

- endpoints backend finalizados;
- job vigente de contrato tipado.

**Prueba roja**

Crear:

```text
web/src/api/reputation.test.ts
```

Casos:

- obtiene estado de orden;
- crea reseña con body tipado;
- obtiene perfil y reseñas paginadas;
- convierte promedio `number | string` con `Number()`;
- propaga Problem Details mediante el cliente común.

Fallo esperado: rutas ausentes de `schema.d.ts` y adaptador inexistente.

**Implementación**

Regenerar:

```powershell
dotnet build src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj `
  -p:GenerateOpenApi=true
```

Actualizar derivados:

```text
CaseritoApp/artifacts/openapi/CaseritoApp.Host.json
web/src/api/schema.d.ts
```

Crear:

```text
web/src/api/reputation.ts
```

No escribir interfaces manuales paralelas; usar `components` y `paths` del
contrato generado.

**Verificación**

```powershell
npm run generate:api
npm run test -- --run src/api/reputation.test.ts
npm run typecheck
```

Resultado esperado: contrato reproducible, tests y typecheck verdes.

**Commit**

```text
feat(web): agrega contrato tipado de reputacion
```

## Tarea 8 — Integrar calificación en detalle de acuerdo

**Entradas**

- adaptadores web de tarea 7;
- `DetalleAcuerdoPage` y tests actuales.

**Prueba roja**

Ampliar:

```text
web/src/routes/DetalleAcuerdoPage.test.tsx
```

Casos:

- no consulta/muestra formulario antes de `Completed`;
- `Completed` pendiente muestra puntuación y comentario;
- valida 1–5 y 10–500;
- confirma que el envío es irreversible;
- éxito invalida estado y muestra espera;
- reseña unilateral nunca muestra contenido;
- par revelado muestra estado público;
- enlaza perfil de contraparte;
- `400`, `404`, `409` y `429` usan mensajes genéricos.

Fallo esperado: UI de acuerdo solo muestra cierre completado.

**Implementación**

Actualizar:

```text
web/src/routes/DetalleAcuerdoPage.tsx
```

Usar React Hook Form + Zod y MUI. El selector 1–5 debe ser accesible mediante
labels en español. El diálogo debe explicar:

- la reseña no puede editarse;
- permanece oculta hasta que la contraparte califique.

Invalidar:

```text
['reputation-order', orderId]
['public-profile', contraparteId]
['public-reviews', contraparteId]
```

No modificar acciones de cierre, pago o entrega.

**Verificación**

```powershell
npm run test -- --run src/routes/DetalleAcuerdoPage.test.tsx
npm run typecheck
npm run lint
```

Resultado esperado: test dirigido, typecheck y lint verdes.

**Commit**

```text
feat(web): permite calificar acuerdos completados
```

## Tarea 9 — Crear perfil público y enlace desde avisos

**Entradas**

- perfil/reseñas públicas tipadas;
- `AvisoPublicoDto.vendedorId`;
- router y detalle de aviso.

**Prueba roja**

Crear/ampliar:

```text
web/src/routes/PerfilPublicoPage.test.tsx
web/src/routes/DetalleAvisoPage.test.tsx
web/src/app/router.test.tsx
```

Casos:

- ruta `/usuarios/:id`;
- muestra nombre, ciudad y badge de verificación;
- muestra promedio con una cifra y total;
- estado vacío con promedio nulo;
- lista puntuación, comentario, fecha y rol, sin IDs;
- pagina reseñas;
- perfil inexistente muestra estado no disponible;
- detalle público de aviso enlaza al vendedor.

Fallo esperado: componente y ruta no existen; aviso no expone enlace.

**Implementación**

Crear:

```text
web/src/routes/PerfilPublicoPage.tsx
```

Actualizar:

```text
web/src/app/router.tsx
web/src/routes/DetalleAvisoPage.tsx
```

Usar TanStack Query, MUI y textos españoles UTF-8. No mostrar email ni inferir
identidad del autor de una reseña más allá de “Comprador” o “Vendedor”.

**Verificación**

```powershell
npm run test -- --run `
  src/routes/PerfilPublicoPage.test.tsx `
  src/routes/DetalleAvisoPage.test.tsx `
  src/app/router.test.tsx
npm run typecheck
npm run lint
npm run build
```

Resultado esperado: tests dirigidos, typecheck, lint y build verdes.

**Commit**

```text
feat(web): muestra perfiles publicos con reputacion
```

## Tarea 10 — Integración y revisión final

**Entradas**

- todas las tareas anteriores;
- spec aprobado;
- artefactos derivados.

**Revisión**

Comparar el diff completo con el spec:

- elegibilidad exclusivamente sobre `Completed`;
- una reseña por parte/orden;
- doble ciego sin vencimiento;
- agregado solo con pares;
- perfil público mínimo;
- capas y schemas independientes;
- autorización y errores no enumerables;
- comentarios ausentes de logs;
- ausencia de pagos, QR, envíos, notificaciones y disputas.

**Backend**

Desde `CaseritoApp/`:

```powershell
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes
```

La suite de integración requiere Docker. Registrar cantidades reales y cualquier
prueba impedida con su error causal.

**Frontend**

Desde `web/`:

```powershell
npm run generate:api
npm run typecheck
npm run lint
npm run test -- --run
npm run build
```

Verificar que regenerar el cliente no produzca diferencias.

**Git**

Desde la raíz:

```powershell
git diff --check
git status --short --branch
git log --oneline master..HEAD
```

El worktree debe quedar limpio. No mergear ni pushear.

Si la implementación queda incompleta, crear o reemplazar
`docs/ai/HANDOFF.md` con estado verificable y siguiente tarea exacta. Si queda
cerrada, no dejar handoff.

**Commit**

Solo si la integración exige correcciones o artefactos no incluidos en commits
anteriores:

```text
chore(reputation): completa integracion de fase 5
```

## Cobertura del spec

| Requisito | Tareas |
|---|---|
| Agregado e invariantes | 1 |
| Elegibilidad y CQRS | 2, 4 |
| Doble ciego y agregado | 2, 3, 4 |
| Unicidad y concurrencia | 3, 4 |
| Perfil público mínimo | 5, 9 |
| Enlace desde aviso | 5, 9 |
| Contrato OpenAPI | 7 |
| UI de calificación | 8 |
| Aislamiento y anti-PII | 3, 6, 10 |
| Verificación completa | 10 |
| Exclusiones de alcance | todas |
