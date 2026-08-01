# Diseño: autenticación con Google y Facebook

- **Fecha**: 2026-08-01
- **Estado**: Aprobado durante brainstorming
- **Alcance**: añadir Google y Facebook como métodos de registro, inicio de sesión y vinculación para la PWA, conservando correo/contraseña y la sesión JWT propia de Caserito.

## Objetivo

Reducir la fricción de entrada, especialmente para usuarios procedentes de Facebook Marketplace, sin delegar en los proveedores el modelo de usuario, los roles, los permisos, KYC ni la sesión de Caserito.

## No objetivos

- No integrar Apple, Microsoft, TikTok ni otros proveedores.
- No consumir APIs sociales, importar contactos, publicaciones, avatar ni actividad.
- No persistir access tokens o refresh tokens de Google o Meta.
- No sustituir el registro, login, recuperación o confirmación por correo existentes.
- No crear una aplicación móvil nativa ni SDK específicos de Android/iOS.
- No implementar en esta feature la eliminación completa de una cuenta Caserito; las URL y el procedimiento de eliminación exigidos por Meta son un prerrequisito de publicación y deben apuntar al mecanismo legal/operativo vigente.

## Contexto existente

Caserito es una PWA React servida en el mismo origen que una API .NET. El contexto Identity usa ASP.NET Core Identity (`ApplicationUser : IdentityUser<Guid>`), roles, JWT de acceso en memoria y refresh token propio en cookie `httpOnly`. El email confirmado, los permisos y el estado KYC se incorporan al JWT de Caserito. Las tablas estándar de Identity ya incluyen `AspNetUserLogins`, por lo que no se necesita una entidad ni migración específica para las asociaciones externas.

## Decisiones

| Tema | Decisión |
|---|---|
| Proveedores | Google y Facebook mediante OAuth/OIDC web del backend |
| Sesión final | El proveedor autentica; Caserito emite su JWT y refresh token actuales |
| Identidad estable | Asociación por `LoginProvider + ProviderKey`; nunca por email como clave externa |
| Login externo temporal | Cookie `IdentityConstants.ExternalScheme`, `httpOnly`, `Secure` fuera de Development/Testing, `SameSite=Lax`, vida corta y limitada a rutas de auth |
| Retorno a la PWA | Redirección al mismo origen; nunca access token, refresh token ni PII en query string |
| Registro nuevo | Se crea el usuario solo cuando están disponibles email, nombre y ciudad; antes se conserva un ticket externo temporal protegido |
| Cuenta existente por email | No se vincula automáticamente; requiere autenticarse con un método ya asociado; si perdió el acceso usa la recuperación local existente |
| Email Google | Se marca confirmado solo cuando el proveedor entrega `email_verified=true`; si falta o no está verificado, se usa el flujo local de confirmación |
| Email Facebook | Se considera pendiente de confirmación local; si Facebook no lo entrega, se solicita durante el onboarding |
| Datos sociales | Solo identificador estable, nombre y email mínimo; no se guarda avatar ni tokens del proveedor |
| Ruta de retorno | Solo rutas locales relativas validadas; valor por defecto `/perfil` |
| Registro tradicional | Permanece disponible sin cambios |

## Arquitectura y responsabilidades

### Host

- Registra Google y Facebook con credenciales obtenidas de configuración segura.
- Mantiene JWT Bearer como esquema de autenticación de la API y añade una cookie exclusivamente temporal para el handshake externo.
- Expone endpoints minimal API bajo `/api/auth/external` para iniciar el challenge, recibir el resultado interno y completar/vincular la cuenta.
- Traduce el resultado final a la sesión propia mediante un servicio común que reutiliza roles, permisos, KYC, access JWT y refresh token.
- Valida proveedor permitido, `state`/correlation, callback, ticket temporal y ruta de retorno.

### Identity Infrastructure/Application

- Encapsula la resolución de un login externo y evita que el endpoint contenga todas las ramas de negocio.
- Usa `UserManager.FindByLoginAsync`, `FindByEmailAsync`, `CreateAsync` sin contraseña y `AddLoginAsync`.
- Publica `UsuarioRegistrado` y asigna el rol `Cliente` exactamente una vez al crear un usuario.
- Reutiliza el mecanismo de confirmación de email para correos no confiables y para vinculación por enlace.

### Web

- Añade botones “Continuar con Facebook” y “Continuar con Google” antes del formulario tradicional.
- Navega el documento al endpoint del backend; no ejecuta OAuth mediante JavaScript ni conoce secretos.
- Añade rutas de resultado/onboarding que completan la sesión mediante el refresh ya existente.
- Conserva y valida la ruta originalmente solicitada.

## Flujos

### Usuario ya asociado al proveedor

1. La PWA navega a `GET /api/auth/external/{proveedor}/start`.
2. El backend crea propiedades de autenticación con retorno local protegido y ejecuta el challenge.
3. El middleware valida la respuesta del proveedor y crea la identidad externa temporal.
4. El callback obtiene `ProviderKey` y encuentra al usuario mediante `FindByLoginAsync`.
5. Caserito emite refresh token, establece su cookie y redirige a `/auth/external/completado`.
6. La PWA llama a `refresh`, recibe el access token, carga el perfil y navega al retorno validado.

### Usuario nuevo con datos completos

1. Tras el callback no existe asociación ni email ocupado.
2. Se crea un ticket temporal protegido con proveedor, clave externa y claims mínimos.
3. La PWA solicita únicamente los datos ausentes; normalmente ciudad.
4. `POST /api/auth/external/complete` revalida el ticket, crea `ApplicationUser`, asigna `Cliente`, añade el login externo y publica `UsuarioRegistrado`.
5. Si el email no es confiable, se envía la confirmación local.
6. Se emite la sesión de Caserito y se elimina el ticket temporal.

### Email perteneciente a una cuenta existente

1. No se crea otra cuenta y no se llama a `AddLoginAsync` automáticamente.
2. La PWA muestra un mensaje genérico indicando que debe verificarse la cuenta existente, sin exponer datos adicionales.
3. El usuario se autentica con contraseña u otro proveedor ya vinculado; con una sesión válida se confirma la vinculación pendiente.
4. Si perdió ese acceso, usa la recuperación de contraseña local ya existente antes de vincular.
5. Tras verificar la sesión, se añade el login externo y se emite/continúa la sesión.

### Facebook sin email

1. El ticket conserva únicamente proveedor, clave y nombre disponibles.
2. El onboarding solicita email y ciudad.
3. El email queda sin confirmar y se usa el proceso local actual.
4. No se considera el perfil verificado ni se omite KYC por proceder de Facebook.

### Cancelación o error

El callback borra la cookie externa y redirige a `/login` con un código opaco y acotado (`cancelado`, `no_disponible` o `fallo`). La UI muestra un texto genérico en español. No se colocan mensajes del proveedor, emails, identificadores ni tokens en la URL o logs.

## Contrato HTTP previsto

- `GET /api/auth/external/{provider}/start?returnUrl=/ruta`: inicia Google o Facebook; proveedor desconocido → 404.
- `GET /api/auth/external/callback`: destino interno posterior al middleware; no es una API consumida con `fetch`.
- `GET /api/auth/external/pending`: devuelve únicamente los campos de onboarding necesarios, nunca `ProviderKey` ni tokens.
- `POST /api/auth/external/complete`: recibe nombre/ciudad/email solo cuando correspondan y completa el alta.
- `POST /api/auth/external/link`: exige una sesión Caserito válida y vincula la identidad pendiente a ese usuario.

Los nombres exactos pueden ajustarse al generar OpenAPI, pero deben conservar la separación entre challenge por navegación, ticket temporal y sesión final. Los endpoints mutadores usan antiforgery o una defensa equivalente basada en cookie same-site y token; no se desactiva globalmente.

## Persistencia y consistencia

- `AspNetUserLogins` almacena proveedor, clave externa y usuario; su clave compuesta impide reutilizar una identidad externa.
- No hay migración si el esquema estándar existente ya contiene esa tabla.
- La creación de usuario, rol y login debe tratar fallos parciales explícitamente. El plan verificará una transacción o compensación segura para no dejar usuarios sin rol o asociaciones a medias.
- La vinculación es idempotente: repetir el callback del mismo proveedor devuelve la cuenta asociada y no crea otra.
- Un usuario puede vincular Google y Facebook a la misma cuenta.

## Seguridad, privacidad y anti-PII

- Secretos en user-secrets o variables de entorno: `Authentication:Google:{ClientId,ClientSecret}` y `Authentication:Facebook:{AppId,AppSecret}`; nunca versionados.
- Solicitar los scopes mínimos de identidad y email. No pedir permisos de Marketplace ni Graph adicionales.
- No registrar claims completos, email, nombre, `ProviderKey`, códigos OAuth, cookies ni tokens.
- Confiar en el middleware oficial para correlation/state y validación del protocolo; HTTPS obligatorio en producción.
- Cookies externas cortas, `httpOnly`, `SameSite=Lax`; cookie refresh conserva las reglas actuales.
- Validar `returnUrl` como ruta local para impedir open redirects.
- Rotar secretos y revocar la integración sin invalidar las cuentas locales ya creadas; estas mantienen otros métodos vinculados o pueden recuperar acceso por correo.
- Mantener mensajes genéricos para impedir enumeración de cuentas.
- Antes de activar Facebook en producción deben existir política de privacidad, dominio verificado y URL/procedimiento público de eliminación de datos aceptado por Meta.

## UI y accesibilidad

- Orden visual: Facebook, Google, separador “o”, formulario tradicional.
- Botones con texto, icono identificable, foco visible y nombre accesible; no depender solo del color.
- Mantener enlaces de recuperación y registro.
- Estado de espera durante retorno y onboarding; impedir dobles envíos.
- Mensajes: cancelación recuperable, proveedor temporalmente no disponible, datos pendientes y vinculación requerida.
- La PWA instalada y el navegador usan exactamente el mismo flujo web y origen.

## Configuración y operación

- Configurar callbacks HTTPS exactos para desarrollo y producción en Google Cloud Console y Meta for Developers.
- Añadir plantillas sin secretos a `appsettings`/documentación y valores reales al gestor de secretos del despliegue.
- Si un proveedor no tiene configuración, el host debe fallar al arrancar fuera de Development/Testing o no anunciarlo mediante un endpoint de capacidades; nunca mostrar un botón condenado a fallar.
- Meta requiere pasar la app a producción y completar sus requisitos de revisión/configuración. El login local y Google deben seguir disponibles si Facebook está caído.

## Pruebas

- Unitarias para normalización de proveedor, validación de retorno, resolución de estados y política de email confirmado.
- Integración sin llamadas reales a Google/Meta, sustituyendo el handler externo por una identidad controlada:
  - usuario asociado inicia sesión y recibe refresh;
  - usuario nuevo completa ciudad, recibe rol y login externo;
  - Google verificado confirma email; Facebook inicia confirmación local;
  - Facebook sin email solicita onboarding;
  - email existente exige vinculación y no duplica usuario;
  - vinculación válida añade el segundo proveedor;
  - callback repetido es idempotente;
  - proveedor, ticket o retorno inválidos se rechazan sin filtrar PII.
- Frontend: botones/redirección, conservación del retorno, bootstrap tras callback, onboarding y mensajes genéricos.
- Regresión completa de login, registro, confirmación, recuperación, refresh, logout, roles, permisos y KYC.

## Criterios de aceptación

1. Un usuario puede registrarse e iniciar sesión con Google o Facebook desde navegador y PWA instalada.
2. La sesión resultante usa el JWT y refresh token propios de Caserito y conserva claims, roles, permisos y KYC.
3. Correo/contraseña continúa funcionando sin cambios de comportamiento.
4. Una coincidencia de email nunca vincula automáticamente ni crea una cuenta duplicada.
5. Facebook sin email conduce a un onboarding mínimo y confirmación local.
6. No aparecen tokens, secretos ni PII en URLs, logs o almacenamiento web.
7. Los retornos externos no permiten redirecciones fuera de Caserito.
8. Google y Facebook pueden vincularse a una misma cuenta de forma segura e idempotente.
9. Contrato OpenAPI, cliente generado, pruebas backend/frontend, builds, formato y lint quedan verdes.
10. La activación productiva queda documentada con callbacks, secretos y prerrequisitos de Google/Meta.

## Trabajo diferido

- Gestión de métodos vinculados desde Perfil/Seguridad y desvinculación con regla de conservar al menos un acceso.
- Eliminación autoservicio completa de la cuenta Caserito y callback automatizado de eliminación de Meta.
- Métricas de conversión por proveedor diseñadas sin PII.
- Apple, Microsoft u otros proveedores, solo con evidencia de demanda.
