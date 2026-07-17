# Diseño — Bloque "Endurecimiento pre-producción del KYC" (backend)

> Bloque de Fase 1 (Identidad). Cierra dos follow-ups PRE-PRODUCCIÓN del Bloque E
> (`2026-07-15-kyc-manual-pii-design.md`, §11) hoy diferidos. Fecha: 2026-07-17.

## 1. Objetivo y alcance

Endurecer el KYC manual antes de un eventual despliegue a producción, cerrando los
dos follow-ups de mayor valor/riesgo y menor superficie del Bloque E, sin depender
de infraestructura externa.

**Dentro de este bloque (todo backend, bounded context Identity):**

1. **Fail-fast del encryptor**: fuera de Development/Testing, si `IEncryptor` sigue
   siendo `PassthroughEncryptor`, abortar el arranque del host (análogo al fail-fast
   de `Jwt:Key`). Evita escribir imágenes de documento/selfie en claro en producción.
2. **Concurrencia optimista (rowversion)** en el agregado `VerificacionKyc`: hoy una
   doble aprobación puede publicar `UserVerified` dos veces y dos subidas concurrentes
   pueden crear dos solicitudes `Pendiente`.

**Fuera de este bloque (diferido, por decisión de alcance):**

- **Encryptor real (envelope/KMS)** — ítem 2 de los follow-ups. Queda detrás de la
  interfaz `IEncryptor` sin cablear un proveedor concreto hasta que haya decisión de
  infra/VPS. Este bloque solo añade el guardrail (fail-fast) que fuerza a cablearlo
  antes de producción.
- **Purga por retención + gap de atomicidad blobs↔SaveChanges** (§11.1 del Bloque E)
  — el follow-up más independiente; irá en su propio bloque.
- **Publish-after-commit / outbox** — la doble publicación cosmética de `UserVerified`
  del perdedor de una carrera queda como deuda ligada al outbox (ver §4).

## 2. Componente 1 — Fail-fast del encryptor

**Estado actual:** `AgregarIdentity(IConfiguration)` registra `IEncryptor` de forma
incondicional como `PassthroughEncryptor`
(`Identity.Infrastructure/DependencyInjection.cs`, hoy línea 54). En producción esto
cifraría con identidad → PII biométrica en claro en disco.

**Diseño (patrón de referencia: fail-fast de `Jwt:Key` en `AgregarAutenticacionJwt`,
`OpcionesJwt.Validate(...).ValidateOnStart()`):**

- `AgregarIdentity` pasa a recibir también `IHostEnvironment` (hoy solo toma
  `IConfiguration`). La llamada en `Program.cs` (hoy línea 29) pasa a
  `AgregarIdentity(builder.Configuration, builder.Environment)`.
- **Development/Testing** → se registra `PassthroughEncryptor` (comportamiento actual,
  intacto para dev y para los tests de integración con `PassthroughEncryptor`).
- **Fuera de Development/Testing** → no hay encryptor real cableado (ítem 2 diferido),
  así que **no se registra `PassthroughEncryptor`** y se añade una validación de
  arranque que **aborta el host** con un mensaje claro, en vez de arrancar inseguro.
- La validación corre **al construir el host** (equivalente a `ValidateOnStart`), no
  en el primer request, para que un despliegue mal configurado ni siquiera levante.

**Mecanismo concreto (fijado).** `AgregarIdentity` decide el registro de `IEncryptor`
según el entorno y **lanza `InvalidOperationException` durante la composición** cuando
`!(IsDevelopment() || IsEnvironment("Testing"))` y no hay encryptor real cableado. La
composición corre en `Program.cs` (hoy línea 29), **antes** de `builder.Build()` y de
servir cualquier request, por lo que un despliegue mal configurado falla al arrancar.
El criterio de entorno es idéntico al de la clave efímera de JWT
(`IsDevelopment() || IsEnvironment("Testing")`). Se prefiere lanzar en la composición
—en vez de un `IValidateOptions`/hosted-validation— por ser lo más simple, determinista
y directamente testeable (invocar `AgregarIdentity` con un entorno `Production` lanza).

- **Sin escape hatch.** La consecuencia deliberada es que producción no bootea hasta
  que se cablee cifrado real (ítem 2 diferido). El deploy a VPS está diferido, así que
  esto no bloquea nada hoy; convierte "silenciosamente inseguro" en "no arranca hasta
  cablear cripto real". No se ofrece flag de bypass (a diferencia de
  `Migraciones:EjecutarAlArranque`), porque un bypass anularía el propósito de
  seguridad.

**Mensaje de error** (sin PII): estilo del de `Jwt:Key`, p. ej. *"IEncryptor está
configurado como PassthroughEncryptor fuera de Development/Testing: se requiere un
encryptor real (envelope/KMS) antes de producción."*

### Test (Componente 1)

- **Unit/arch**: invocar `AgregarIdentity` con un `IHostEnvironment` de entorno
  `Production` (sin encryptor real) → lanza `InvalidOperationException`. En
  `Development` y `Testing` → no lanza y `IEncryptor` resuelve a `PassthroughEncryptor`.

## 3. Componente 2 — Concurrencia optimista (rowversion en la raíz)

**Estado actual:** el agregado `VerificacionKyc` no tiene token de concurrencia. Como
`UnitOfWorkBehavior` invoca `GuardarCambiosAsync` (→ `SaveChangesAsync`) **después** del
handler, dos requests concurrentes pueden ambos leer, mutar y persistir sin conflicto.

Dos escenarios problemáticos:

- **Doble aprobación** de la misma solicitud: ambos handlers leen la solicitud
  `Pendiente`, ambos llaman `Aprobar` (modifican la **hija** `SolicitudKyc`), ambos
  publican `UserVerified`, ambos hacen commit.
- **Dos subidas concurrentes** del mismo usuario: ambos handlers no ven `Pendiente`,
  ambos añaden una **hija** `SolicitudKyc` nueva → dos `Pendiente`. (Cuando el agregado
  aún no existe, la PK `UsuarioId` ya bloquea el segundo insert; el problema real es
  cuando el agregado ya existe, p. ej. tras un rechazo previo.)

### 3.1 Token de concurrencia en la raíz

- Propiedad **shadow** `Version` (`byte[]`) en `VerificacionKyc`, configurada como
  `IsRowVersion()` en `ConfiguracionKyc.Configurar`. SQL Server la auto-mantiene
  (columna `rowversion`); las filas existentes quedan versionadas automáticamente.

### 3.2 Touch-root (propagar cambios de la hija a la raíz)

EF Core **no** bumpea la versión de la raíz cuando solo cambia una hija: la raíz no
entra en el `UPDATE` y su `rowversion` no se chequea. Para cubrir ambos escenarios se
override `SaveChangesAsync` en `IdentityDbContext`:

- Antes de delegar en `base.SaveChangesAsync`, recorrer
  `ChangeTracker.Entries<SolicitudKyc>()`; para cada hija en estado
  `Added | Modified | Deleted`, marcar la raíz `VerificacionKyc` dueña como `Modified`
  (equivalente a "tocar" su `Version` para forzar el chequeo de concurrencia).
- La raíz dueña se localiza por la FK sombra `VerificacionKycId` (= `UsuarioId`), ya
  cargada en el `ChangeTracker` (los handlers hacen `Include(v => v.Solicitudes)`).

Con esto:

- **Doble aprobación** (hija `Modified`) → la 2ª pierde → `DbUpdateConcurrencyException`.
- **Dos `Pendiente`** sobre agregado existente (hija `Added`) → la 2ª pierde igual.

### 3.3 Mapeo del conflicto a HTTP 409

- Nuevo `ConcurrenciaBehavior<TRequest, TResponse>` (MediatR pipeline behavior en
  BuildingBlocks.Application.Behaviors) que envuelve la ejecución en `try/catch`,
  captura `DbUpdateConcurrencyException` y devuelve
  `Result.Fallo(new Error(ErroresKyc.ConflictoConcurrencia, …))` (o su equivalente en
  el tipo `Result` genérico) en vez de propagar la excepción.
- **Orden de registro (crítico):** MediatR ejecuta los behaviors en orden de registro
  (externo→interno). En `Program.cs` el orden actual es
  `Logging → Validation → UnitOfWork` (hoy líneas 18-20). `ConcurrenciaBehavior` debe
  registrarse **antes** de `UnitOfWorkBehavior` (que es quien lanza la excepción al
  hacer `SaveChanges`) para poder envolverlo y capturarla. Se registra justo antes de
  `UnitOfWorkBehavior`.
- Nuevo código de error `ErroresKyc.ConflictoConcurrencia`; el mapeo `Result`→HTTP de
  los endpoints KYC lo traduce a **409 Conflict**, consistente con los 409 que KYC ya
  usa (ya pendiente/aprobado, transición inválida). El cliente reintenta con estado
  fresco.

### 3.4 Migración

- Migración nueva **`KycConcurrencia`** que agrega la columna `rowversion` a
  `VerificacionesKyc`. Comando estándar de EF (ver CLAUDE.md), `--output-dir Migrations`.

### 3.5 Residual documentado (no se resuelve aquí)

El publish de `UserVerified` ocurre **dentro del handler**, antes del commit del
`UnitOfWorkBehavior`. El perdedor de una doble aprobación logueará una línea de evento
y **luego** fallará el commit (→ 409); el estado **committeado** queda con una sola
aprobación. Como el publicador es **solo-log** y el **outbox está diferido** (decisión
del Bloque E / Fase 0), esa línea es un artefacto cosmético, no una doble verificación
real. El fix definitivo (publish-after-commit, típicamente vía outbox transaccional)
pertenece al bloque de outbox y se deja anotado como deuda. **No** se intenta el
reintento automático con reejecución del handler, precisamente porque reejecutaría el
publish.

### Tests (Componente 2)

- **Integración (Testcontainers.MsSql, `CaseritoApiFactory`):**
  - Dos aprobaciones concurrentes de la misma solicitud → exactamente una `204` y una
    `409`; en BD la solicitud queda `Aprobada` una sola vez.
  - Dos subidas concurrentes del mismo usuario (con agregado ya existente tras un
    rechazo) → exactamente una crea la solicitud y una `409`; en BD hay una sola
    `Pendiente`.
- Nota: el touch-root no es unit-testeable de forma significativa sin BD; su cobertura
  es vía los tests de integración anteriores.

## 4. No-objetivos / deuda anotada

- Encryptor real (envelope/KMS): diferido; este bloque solo fuerza a cablearlo antes de
  prod (§2).
- Publish-after-commit / outbox transaccional: diferido; el residual cosmético de §3.5
  queda anotado.
- Purga por retención + gap de atomicidad blobs↔SaveChanges (§11.1 del Bloque E): su
  propio bloque.

## 5. Ramas, modelos y ejecución

- Rama `feat/kyc-endurecimiento-preprod`.
- Orden de ejecución: **Componente 1** (más pequeño y aislado) → **Componente 2**.
- Implementers con **Sonnet** (dominio/EF/analizadores estrictos); **revisión final con
  Opus**. Guardrail de commits del implementer: solo `git add <archivos>` + commit; nada
  de `reset`/`rebase`/`checkout`/`amend`.
