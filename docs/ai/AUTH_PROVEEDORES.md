# Operación de Google y Facebook Login

## Configuración segura

Configurar los valores reales únicamente mediante `dotnet user-secrets` en desarrollo o el gestor de secretos del despliegue:

- `Authentication:Google:ClientId` y `Authentication:Google:ClientSecret`.
- `Authentication:Facebook:AppId` y `Authentication:Facebook:AppSecret`.

Las variables de entorno equivalentes usan doble guion bajo, como muestra `.env.example`. No guardar credenciales, códigos OAuth ni tokens en Git, logs o incidencias.

## Qué significa «sandbox» en este caso

`Sandbox` es una forma informal de llamar al entorno restringido con el que se prueba la integración antes de abrirla al público. No es un servidor OAuth que Caserito tenga que instalar.

- **Google:** no entrega una cuenta o un dominio sandbox separado. Se crea un proyecto en Google Cloud, se configura la aplicación OAuth como `External` con estado de publicación `Testing` y se añaden explícitamente las cuentas de Google autorizadas como usuarios de prueba. Google limita este modo a esos usuarios y recomienda separar los proyectos de prueba y producción.
- **Meta/Facebook:** se crea una aplicación en Meta for Developers y se mantiene en modo desarrollo. Solo las personas con un rol en la aplicación y los usuarios de prueba creados desde el panel pueden iniciar sesión. Meta sí permite crear usuarios de prueba administrados por la aplicación.

Aunque Caserito esté desplegado en el servidor que será productivo, mientras todavía no se haya lanzado se puede probar con las aplicaciones OAuth restringidas. Conviene llamar a ese despliegue `preproducción` o `staging` para no confundir la infraestructura con el estado público de Google/Meta. No usar cuentas personales ni abrir las aplicaciones OAuth al público para estas pruebas.

Referencias oficiales:

- [Google: configurar audiencia y usuarios de prueba](https://support.google.com/cloud/answer/15549945).
- [Google: preparar y verificar una aplicación OAuth](https://support.google.com/cloud/answer/13461325).
- [Meta: usuarios de prueba](https://developers.facebook.com/docs/development/build-and-test/test-users/).

## Qué son los callbacks

Después de que el usuario se identifica en Google o Facebook, el proveedor devuelve el navegador a una URL del backend de Caserito. Esa URL es el **callback** o URI de redirección.

Registrar un callback significa copiar la URL HTTPS completa en la configuración del cliente OAuth del proveedor. Debe coincidir exactamente en protocolo, dominio, puerto, ruta y barra final; de lo contrario Google devuelve `redirect_uri_mismatch` y Meta rechaza la redirección. El callback recibe y valida la respuesta del proveedor; nunca debe apuntar a una página arbitraria del frontend.

[Google documenta la coincidencia exacta de las URI de redirección](https://developers.google.com/identity/protocols/oauth2/web-server#creatingcred).

## Callbacks de Caserito

Registrar exactamente estas URL, sustituyendo los orígenes por los de cada entorno:

- Google, desarrollo: `https://localhost:<puerto>/signin-google`.
- Facebook, desarrollo: `https://localhost:<puerto>/signin-facebook`.
- Google, producción: `https://<dominio-produccion>/signin-google`.
- Facebook, producción: `https://<dominio-produccion>/signin-facebook`.

El navegador y la PWA deben usar el mismo origen que la API. Producción requiere HTTPS.

Si el despliegue de prueba usa, por ejemplo, `https://pre.caserito.example`, registrar:

- `https://pre.caserito.example/signin-google` en el cliente web de Google.
- `https://pre.caserito.example/signin-facebook` en Facebook Login de Meta.

No registrar `/api/auth/external/callback`: esa es una ruta interna posterior de Caserito, no el callback de protocolo que invoca el proveedor.

## Alta de proveedores

En Google Cloud Console, crear un cliente OAuth web, configurar la pantalla de consentimiento, solicitar solo identidad y email, y registrar el callback exacto. En Meta for Developers, añadir Facebook Login para web, configurar el dominio y el callback exacto, y solicitar solo identidad pública y email.

Antes de habilitar Facebook en producción, publicar una política de privacidad accesible, verificar el dominio y publicar la URL o procedimiento de eliminación de datos exigido por Meta. Completar la revisión y cambiar la aplicación al modo de producción.

## Despliegue y revocación

Inyectar los cuatro valores desde el gestor de secretos y reiniciar el host. Fuera de Development/Testing, una configuración parcial provoca fallo de arranque. Para deshabilitar un proveedor, retirar conjuntamente sus dos valores; las cuentas locales y los demás métodos de acceso permanecen disponibles. Rotar ambos valores del proveedor si existe sospecha de exposición.

## Preparación recomendada para un despliegue aún no lanzado

1. Confirmar el dominio HTTPS público y estable desde el que se probará Caserito.
2. Crear proyectos/aplicaciones OAuth exclusivos de prueba; no reutilizar todavía los definitivos de producción.
3. En Google, mantener el estado `Testing`, solicitar solo identidad y email y añadir las cuentas de prueba autorizadas.
4. En Meta, mantener la aplicación en modo desarrollo y usar roles o usuarios de prueba de la aplicación.
5. Registrar los dos callbacks exactos del dominio de prueba.
6. Guardar `ClientId`, `ClientSecret`, `AppId` y `AppSecret` en el gestor de secretos del despliegue. Nunca enviarlos por chat, guardarlos en archivos versionados ni exponerlos al frontend.
7. Reiniciar el backend y comprobar que `/api/auth/external/providers` devuelve `facebook` y `google`.

## Guía de prueba manual

Usar una ventana privada del navegador y usuarios de prueba, nunca cuentas personales. Para cada caso, revisar que la URL solo contenga rutas locales o códigos genéricos y nunca email, identificadores externos, códigos OAuth o tokens.

1. **Capacidades:** abrir el login y confirmar el orden Facebook, Google, separador y correo/contraseña. Si se retira la configuración completa de un proveedor, su botón no debe aparecer y el login tradicional debe continuar disponible.
2. **Alta con Google:** usar un usuario de prueba nuevo con email verificado. Completar la ciudad, confirmar que se crea una sola cuenta, se inicia sesión y el email queda confirmado.
3. **Alta con Facebook:** usar un usuario de prueba nuevo. Confirmar el onboarding mínimo y que Caserito exige su confirmación local de email. Si Meta no entrega email, comprobar que el formulario lo solicita.
4. **Login asociado:** cerrar sesión y volver a entrar con cada proveedor. Debe restaurarse perfil, rol, permisos y estado KYC mediante los JWT/refresh propios de Caserito.
5. **Coincidencia de email:** partir de una cuenta local existente e iniciar acceso externo con el mismo email. Caserito debe pedir login o recuperación y nunca vincular automáticamente ni crear un duplicado.
6. **Vinculación:** autenticar la cuenta local correcta y completar la vinculación. Repetir el callback o la vinculación no debe duplicar datos ni fallar de forma insegura.
7. **Retorno local:** iniciar desde una ruta interna protegida y confirmar que se vuelve a ella. Probar un retorno como `https://malicioso.example` o `//malicioso.example`; Caserito debe reemplazarlo por `/perfil`.
8. **Cancelación y error:** cancelar el consentimiento en ambos proveedores. Debe mostrarse un mensaje recuperable y genérico, sin detalles del proveedor ni de la cuenta.
9. **Regresión tradicional:** probar registro por correo, confirmación, login, refresh, logout, recuperación y restablecimiento de contraseña.
10. **Anti-PII:** inspeccionar barra de direcciones, historial de red, consola, `localStorage`, `sessionStorage` y logs del backend. No deben aparecer email, nombre, clave del proveedor, códigos OAuth, cookies, access tokens ni refresh tokens.
11. **PWA:** repetir alta, login, cancelación y retorno desde la PWA instalada; debe abrir el mismo flujo web y regresar al mismo origen.

Registrar cada caso como `Correcto`, `Falló` o `No ejecutado`, junto con navegador, fecha y entorno. En incidencias usar datos ficticios y mensajes genéricos; no adjuntar capturas o trazas que contengan PII, cookies o tokens.

Esta comprobación requiere credenciales sandbox y no forma parte de la suite automatizada.
