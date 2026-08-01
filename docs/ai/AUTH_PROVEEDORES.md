# Operación de Google y Facebook Login

## Configuración segura

Configurar los valores reales únicamente mediante `dotnet user-secrets` en desarrollo o el gestor de secretos del despliegue:

- `Authentication:Google:ClientId` y `Authentication:Google:ClientSecret`.
- `Authentication:Facebook:AppId` y `Authentication:Facebook:AppSecret`.

Las variables de entorno equivalentes usan doble guion bajo, como muestra `.env.example`. No guardar credenciales, códigos OAuth ni tokens en Git, logs o incidencias.

## Callbacks

Registrar exactamente estas URL, sustituyendo los orígenes por los de cada entorno:

- Google, desarrollo: `https://localhost:<puerto>/signin-google`.
- Facebook, desarrollo: `https://localhost:<puerto>/signin-facebook`.
- Google, producción: `https://<dominio-produccion>/signin-google`.
- Facebook, producción: `https://<dominio-produccion>/signin-facebook`.

El navegador y la PWA deben usar el mismo origen que la API. Producción requiere HTTPS.

## Alta de proveedores

En Google Cloud Console, crear un cliente OAuth web, configurar la pantalla de consentimiento, solicitar solo identidad y email, y registrar el callback exacto. En Meta for Developers, añadir Facebook Login para web, configurar el dominio y el callback exacto, y solicitar solo identidad pública y email.

Antes de habilitar Facebook en producción, publicar una política de privacidad accesible, verificar el dominio y publicar la URL o procedimiento de eliminación de datos exigido por Meta. Completar la revisión y cambiar la aplicación al modo de producción.

## Despliegue y revocación

Inyectar los cuatro valores desde el gestor de secretos y reiniciar el host. Fuera de Development/Testing, una configuración parcial provoca fallo de arranque. Para deshabilitar un proveedor, retirar conjuntamente sus dos valores; las cuentas locales y los demás métodos de acceso permanecen disponibles. Rotar ambos valores del proveedor si existe sospecha de exposición.

## Prueba manual

Usar aplicaciones sandbox y usuarios de prueba, nunca cuentas personales:

1. Comprobar que `/api/auth/external/providers` anuncia solo proveedores configurados.
2. Probar login asociado, alta nueva, Facebook sin email y vinculación a una cuenta existente.
3. Cancelar el consentimiento y verificar el mensaje genérico y el retorno local seguro.
4. Confirmar que URL, consola, logs y almacenamiento no contienen email, identificadores externos, códigos ni tokens.
5. Repetir en navegador y PWA instalada.

Esta comprobación requiere credenciales sandbox y no forma parte de la suite automatizada.
