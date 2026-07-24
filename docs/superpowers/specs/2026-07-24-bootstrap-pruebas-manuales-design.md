# Bootstrap y pruebas manuales de las fases 1–3

Fecha: 2026-07-24  
Estado: aprobado

## Objetivo

Permitir reconstruir en una PC de desarrollo un entorno mínimo y reproducible
para probar registro/perfil, KYC, catálogo, chat y moderación de las fases 1–3,
sin implementar funcionalidad de Fase 4.

## No objetivos

- Activar cuentas de prueba en Production o Testing.
- Sembrar KYC, avisos, conversaciones, mensajes o reportes.
- Sustituir las pruebas automatizadas por pruebas manuales.
- Guardar credenciales o datos personales reales en Git, logs o documentación.

## Bootstrap de usuarios

El Host incorpora un bootstrap exclusivo de `Development`, deshabilitado por
defecto. Solo se ejecuta cuando `BootstrapPruebas:Habilitado=true` y están
presentes todos los datos requeridos de las tres cuentas.

Las cuentas se configuran con variables de entorno y usan datos exclusivamente
sintéticos:

- administrador: rol único `AdminPlataforma`;
- vendedor: rol único `Cliente`, sin verificación KYC inicial;
- comprador: rol único `Cliente`.

El bootstrap se ejecuta después de migrar Identity y sembrar roles. Usa
`UserManager<ApplicationUser>`; no modifica el esquema ni escribe directamente
en las tablas de Identity.

## Seguridad e idempotencia

- Fuera de Development nunca crea ni modifica usuarios, aunque el flag esté activo.
- Sin habilitación explícita o con configuración incompleta, el arranque continúa
  sin crear cuentas.
- Un correo ya existente se omite completamente: no se cambia contraseña, perfil,
  roles, KYC ni ningún otro dato.
- No se registran correos, contraseñas, tokens, documentos, imágenes, mensajes,
  reportes ni identificadores.
- Los errores públicos y de arranque son genéricos y no reflejan configuración.
- `.env.example` contiene solo nombres y valores vacíos; `.env` permanece ignorado.

La creación de cada usuario nuevo asigna su rol requerido inmediatamente. Si
Identity rechaza una cuenta nueva, el arranque falla con un error genérico para
evitar presentar un entorno parcialmente preparado como válido. Una ejecución
posterior nunca corrige ni completa una cuenta que ya exista, por la regla de no
modificar usuarios existentes.

## Configuración

Docker Compose traduce variables planas del `.env` a configuración .NET:

- `CASERITO_BOOTSTRAP_ENABLED`
- `CASERITO_BOOTSTRAP_ADMIN_EMAIL`, `..._PASSWORD`, `..._NAME`, `..._CITY`
- `CASERITO_BOOTSTRAP_SELLER_EMAIL`, `..._PASSWORD`, `..._NAME`, `..._CITY`
- `CASERITO_BOOTSTRAP_BUYER_EMAIL`, `..._PASSWORD`, `..._NAME`, `..._CITY`

El valor por defecto del flag en Compose es `false`; los demás valores usan
cadena vacía.

## Procedimiento manual

README documenta:

1. preparación segura del `.env` y de imágenes sintéticas;
2. reconstrucción y comprobaciones de SQL Server, API y web;
3. registro, login y perfil;
4. solicitud y aprobación KYC;
5. publicación, búsqueda y detalle;
6. contacto, tiempo real, reconexión, recuperación y lectura;
7. cierre/reapertura y bloqueo/desbloqueo;
8. reportes de aviso, conversación, mensaje y contraparte;
9. moderación de avisos y chat;
10. cierre seguro y limpieza opcional.

Las pruebas usan dos sesiones de navegador. Las credenciales se consultan solo
en el `.env` local y nunca se copian a la documentación o al chat.

## Pruebas automatizadas

Las pruebas de integración del bootstrap demuestran:

- no ejecución fuera de Development;
- desactivación por defecto y ante configuración incompleta;
- roles exactos y vendedor no verificado;
- idempotencia;
- preservación total de usuarios existentes;
- ausencia de datos sensibles en logs.

## Criterios de aceptación

1. Una base vacía puede preparar las tres cuentas mínimas únicamente en Development.
2. El administrador puede revisar KYC y moderar avisos y chat.
3. Vendedor y comprador conservan únicamente `Cliente`; el vendedor prueba KYC real.
4. Production, Testing y la configuración desactivada no crean cuentas.
5. Ningún secreto ni dato sensible queda versionado o registrado.
6. README permite repetir las pruebas de fases 1–3 sin pasos SQL manuales.
7. Backend, frontend, Compose y verificaciones de formato quedan verdes.
8. Las pruebas manuales solo se declaran superadas tras confirmación del usuario.
