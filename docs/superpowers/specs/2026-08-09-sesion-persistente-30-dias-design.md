# Sesión persistente durante 30 días

## Objetivo

Mantener autenticado al usuario de Caserito al bloquear el móvil, cerrar la
aplicación o reiniciar el dispositivo, durante un máximo deslizante de 30 días
desde la última renovación válida de la sesión.

## No objetivos

- No añadir biometría, PIN propio ni bloqueo local de la aplicación.
- No identificar, registrar ni mostrar dispositivos o ubicaciones.
- No crear una pantalla de gestión de sesiones.
- No cambiar la duración ni el almacenamiento en memoria del access token.

## Diseño

El access token continuará únicamente en memoria. La persistencia seguirá
dependiendo de la cookie `refreshToken`, `HttpOnly`, que se emitirá con una
vigencia explícita de 30 días mediante `Max-Age`.

El refresh token persistido en el servidor tendrá la misma vigencia. Cada
restauración o renovación válida rotará el token y emitirá una nueva cookie con
otros 30 días, por lo que la duración será deslizante. El frontend ya intenta
restaurar la sesión al montar `AuthProvider`; no requiere cambios.

La duración será una única constante compartida por la emisión del token y la
cookie para impedir divergencias futuras.

## Seguridad y privacidad

- Se mantienen `HttpOnly`, `SameSite=Strict`, `Secure` fuera de desarrollo y
  testing, y `Path=/api/auth`.
- El cierre de sesión revoca el refresh token y elimina la cookie.
- El restablecimiento de contraseña continúa revocando las sesiones activas.
- Los tokens permanecen hasheados en base de datos y nunca se registran.
- No se recopilan identificadores adicionales del dispositivo ni PII.

## Errores

Una cookie ausente, expirada, revocada o reutilizada seguirá produciendo una
respuesta no autorizada y limpiará la cookie. El frontend mostrará el acceso
como no autenticado, sin revelar detalles internos.

## Pruebas

- La cookie de login incluye `Max-Age=2592000` además de los flags existentes.
- La cookie rotada por refresh vuelve a incluir la vigencia completa.
- Un refresh token emitido expira exactamente 30 días después de crearse.
- Un refresh token rotado obtiene una nueva ventana de 30 días.
- Las pruebas existentes de rotación, reutilización, logout y restablecimiento
  continúan pasando.

## Criterios de aceptación

1. La sesión puede restaurarse después de cerrar o reiniciar la aplicación
   mientras la cookie siga dentro de su ventana de 30 días.
2. Cada renovación válida reinicia la ventana de 30 días.
3. Cookie y registro persistido comparten la misma duración.
4. Logout, cambio de contraseña, expiración y detección de reutilización siguen
   invalidando la sesión.
5. No se debilitan los flags de seguridad ni se persiste el access token.
