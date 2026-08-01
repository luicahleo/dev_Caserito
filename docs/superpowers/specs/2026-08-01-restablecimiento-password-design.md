# Diseño — Restablecimiento de contraseña

> Fecha: 2026-08-01.
> Estado: aprobado en brainstorming.
> Contexto: Caserito ya dispone de registro, login, confirmación de correo, correo SMTP, JWT de acceso y refresh tokens rotatorios. Falta el flujo de recuperación para usuarios que olvidaron su contraseña.

## 1. Objetivo

Permitir que un usuario solicite por correo un enlace temporal y restablezca su contraseña sin revelar si una cuenta existe, sin exponer tokens ni otros datos sensibles y cerrando todas sus sesiones después del cambio.

## 2. Alcance

Incluye:

- Solicitud anónima de recuperación mediante correo.
- Correo con enlace temporal de restablecimiento.
- Token de ASP.NET Core Identity válido durante 30 minutos y de un solo uso.
- Formulario para establecer y confirmar la contraseña nueva.
- Revocación de todos los refresh tokens después de un cambio exitoso.
- Rate limiting diferenciado para solicitud y consumo del token.
- Contrato OpenAPI, cliente TypeScript generado y pruebas backend/frontend.

No incluye:

- Inicio de sesión automático después del cambio.
- Invalidación inmediata de JWT de acceso mediante denylist. Los JWT actuales vencen en un máximo de 15 minutos.
- Cambio de contraseña para usuarios autenticados.
- Modificación de la política general de contraseñas.
- Confirmación automática del correo.
- Persistencia propia de tokens o una migración de base de datos.
- Branding avanzado, cola de correo o reintentos SMTP automáticos.

## 3. Decisiones

| Tema | Decisión |
|---|---|
| Vigencia | 30 minutos |
| Proveedor | Proveedor de tokens exclusivo para restablecimiento, separado del de confirmación de correo |
| Uso | Un restablecimiento exitoso invalida el token usado y todos los enlaces anteriores |
| Enumeración | La solicitud siempre devuelve el mismo resultado, exista o no la cuenta |
| SMTP | Los fallos se ocultan al cliente y se registran sin PII ni tokens |
| Sesiones | Se revocan todos los refresh tokens del usuario después del cambio |
| JWT existentes | Permanecen válidos hasta su expiración actual, como máximo 15 minutos |
| Correo no confirmado | Puede recuperar la contraseña, pero continúa sin confirmar |
| Contraseña | Se reutiliza la política actual de Identity: mínimo 8 caracteres |
| Navegación final | Confirmación de éxito y acceso explícito al login; sin login automático |

## 4. Flujos

### 4.1 Solicitar recuperación

1. Desde el login, el usuario abre `/olvide-password`.
2. Introduce su correo y envía el formulario.
3. La SPA llama a `POST /api/auth/forgot-password` con `{ email }`.
4. El backend busca la cuenta sin revelar el resultado.
5. Si existe una cuenta apta, genera un token de restablecimiento y trata de enviar el correo.
6. El endpoint devuelve `204 No Content` en todos estos casos:
   - la cuenta existe y se envió el correo;
   - la cuenta no existe;
   - la cuenta no tiene un correo utilizable;
   - SMTP falló.
7. La UI muestra: «Si existe una cuenta asociada a ese correo, recibirás un enlace para restablecer tu contraseña.»

El endpoint tiene una ventana fija de 3 solicitudes por hora y por IP. Al superar el límite devuelve `429 Too Many Requests`.

### 4.2 Abrir el enlace

El correo contiene una URL con esta forma:

```text
/restablecer-password#usuarioId=<id-opaco>&token=<token-codificado>
```

El fragmento no se transmite al servidor HTTP. La pantalla React:

1. Lee `usuarioId` y `token` desde `window.location.hash`.
2. Conserva ambos únicamente en memoria durante el formulario.
3. Limpia inmediatamente el fragmento de la barra del navegador mediante `history.replaceState`.
4. Si falta algún valor, muestra el mismo estado genérico usado para un enlace inválido o caducado.

### 4.3 Restablecer contraseña

1. El usuario introduce la contraseña nueva y su confirmación.
2. La UI valida un mínimo de 8 caracteres y que ambos campos coincidan.
3. La SPA llama a `POST /api/auth/reset-password` con `{ usuarioId, token, password }`.
4. El backend valida el token y aplica la política real mediante ASP.NET Core Identity.
5. Si el cambio es exitoso, revoca todos los refresh tokens del usuario.
6. La cuenta conserva el valor previo de `EmailConfirmed`.
7. La UI muestra: «Tu contraseña se restableció correctamente. Ya puedes iniciar sesión.» y un botón hacia `/login`.

El endpoint tiene una ventana fija de 10 intentos cada 15 minutos y por IP. Al superar el límite devuelve `429 Too Many Requests`.

## 5. Contrato HTTP

### `POST /api/auth/forgot-password`

Solicitud:

```json
{ "email": "usuario@example.com" }
```

Respuestas:

- `204`: respuesta uniforme, sin indicar existencia ni resultado del correo.
- `429`: límite por IP superado.

El formato del correo se valida sin devolver diferencias observables relacionadas con la existencia de la cuenta.

### `POST /api/auth/reset-password`

Solicitud:

```json
{
  "usuarioId": "00000000-0000-0000-0000-000000000000",
  "token": "token-opaco",
  "password": "contraseña-nueva"
}
```

Respuestas:

- `204`: contraseña cambiada y refresh tokens revocados.
- `400`: enlace inválido o caducado, usuario inexistente o contraseña rechazada. La respuesta no revela cuál condición interna ocurrió; los errores de política que ya muestra la UI pueden representarse de manera segura sin incluir valores recibidos.
- `429`: límite por IP superado.

## 6. Diseño backend

### Token

- Registrar un `DataProtectorTokenProvider<ApplicationUser>` específico para recuperación.
- Configurar `IdentityOptions.Tokens.PasswordResetTokenProvider` con ese proveedor.
- Configurar su `TokenLifespan` en 30 minutos sin alterar la vigencia de confirmación de correo ni otros proveedores.
- Generar y consumir tokens mediante `UserManager.GeneratePasswordResetTokenAsync` y `UserManager.ResetPasswordAsync`.
- No persistir tokens ni hashes adicionales.
- El restablecimiento actualiza el sello de seguridad de Identity; por ello, cualquier token previo deja de ser válido después del primer cambio exitoso.

### Orquestación y capas

- Mantener el comportamiento de recuperación dentro del bounded context Identity.
- Application define los casos de uso y puertos necesarios para correo, construcción del enlace, operaciones de usuario y revocación de sesiones.
- Infrastructure adapta ASP.NET Core Identity, SMTP y la persistencia de refresh tokens.
- Host expone los contratos HTTP, configura los proveedores de token y aplica las políticas de rate limiting.
- No introducir dependencias hacia otros bounded contexts.

### Revocación de sesiones

- Ampliar el servicio de refresh tokens con una operación que revoque todos los tokens activos de un usuario en una actualización de base de datos.
- Ejecutar la revocación únicamente después de que `ResetPasswordAsync` tenga éxito.
- No registrar tokens, identificadores de token ni información del usuario.

### Correo

- Reutilizar `IServicioCorreo`, `IPlantillaCorreo` y la URL pública configurada de la aplicación.
- Añadir asunto y cuerpo específicos para recuperación.
- El correo no incluye contraseñas ni datos personales innecesarios.
- El manejador absorbe el fallo SMTP y emite solo un evento de log técnico genérico, sin destinatario, usuario, enlace ni token.

## 7. Diseño frontend

### Login

- Añadir «¿Olvidaste tu contraseña?» con destino `/olvide-password`.

### Solicitud

- Campo de correo y botón de envío.
- Mostrar siempre el texto anti-enumeración después de un `204`.
- Tratar `429` como exceso de intentos con un mensaje en español que no exponga datos.

### Restablecimiento

- Campos «Nueva contraseña» y «Confirmar contraseña».
- Ojos independientes con etiquetas accesibles para mostrar u ocultar cada valor.
- No mostrar el error de coincidencia antes de que el usuario interactúe con la confirmación.
- Deshabilitar el envío mientras las contraseñas sean diferentes o la petición esté en curso.
- Mantener token e identificador únicamente en memoria y no incorporarlos a errores, telemetría o estado persistente.
- Usar un único mensaje para token ausente, inválido, usado o caducado.

## 8. Seguridad y anti-PII

- Nunca registrar correos, contraseñas, tokens, URLs de recuperación, cuerpos de petición ni respuesta, identificadores vinculables o contenido del correo.
- No almacenar el token en `localStorage`, `sessionStorage`, cookies ni base de datos propia.
- La API recibe el token únicamente en el cuerpo HTTPS del restablecimiento.
- El fragmento evita que el token llegue a logs HTTP, proxies y métricas de navegación del servidor.
- La respuesta uniforme evita enumeración directa de cuentas.
- El rate limiting anónimo se particiona por IP y no por correo.
- No diferenciar públicamente usuario inexistente, token inválido, token usado o token caducado.
- Las pruebas usan direcciones ficticias y tokens sintéticos; nunca credenciales reales.

## 9. Errores y estados

| Escenario | Resultado público |
|---|---|
| Correo existente | `204` + mensaje genérico |
| Correo inexistente | `204` + el mismo mensaje |
| SMTP no disponible | `204` + el mismo mensaje |
| Límite de solicitud superado | `429` + mensaje genérico de espera |
| Enlace incompleto | Estado genérico de enlace inválido o caducado |
| Token inválido, usado o expirado | `400` + estado genérico de enlace inválido o caducado |
| Contraseña rechazada | `400`, sin eco de la contraseña ni datos sensibles |
| Restablecimiento exitoso | `204`, confirmación y acceso al login |
| Límite de consumo superado | `429` + mensaje genérico de espera |

## 10. Pruebas

### Backend unitario

- La solicitud para un usuario existente genera enlace y correo.
- Usuario inexistente y SMTP fallido mantienen el mismo resultado público.
- La plantilla coloca usuario y token codificados en el fragmento.
- El proveedor específico vence a los 30 minutos sin cambiar otros tokens.
- La revocación afecta todos los refresh tokens activos del usuario y no los de otros usuarios.

### Backend integración

- Solicitud existente e inexistente devuelven el mismo estado.
- Token válido permite cambiar la contraseña.
- Token inválido o expirado devuelve el error genérico.
- El token no puede reutilizarse y un cambio invalida enlaces anteriores.
- Una cuenta sin confirmar continúa sin confirmar.
- La contraseña anterior deja de funcionar y la nueva permite login.
- Los refresh tokens anteriores dejan de renovar sesión.
- Se verifican ambos límites de frecuencia sin usar PII como partición.

### Frontend

- El login enlaza a la solicitud.
- La solicitud muestra el mensaje anti-enumeración.
- La pantalla obtiene el fragmento y limpia la URL.
- Los ojos funcionan de forma independiente.
- La confirmación desigual bloquea el envío y muestra el error en el momento adecuado.
- El payload no contiene el campo de confirmación.
- Éxito, enlace inválido y `429` muestran sus estados correspondientes.

## 11. Criterios de aceptación

1. Un usuario puede completar el flujo desde el login hasta volver a iniciar sesión con la contraseña nueva.
2. Los tokens vencen a los 30 minutos y dejan de funcionar después de un cambio exitoso.
3. La solicitud no revela si la cuenta existe ni si el correo pudo enviarse.
4. Todos los refresh tokens previos quedan revocados tras el cambio.
5. El correo no confirmado permanece sin confirmar.
6. Los límites acordados se aplican por IP: 3/hora para solicitar y 10/15 minutos para restablecer.
7. No se registra ni persiste PII, contraseñas o tokens.
8. OpenAPI y el cliente TypeScript representan ambos endpoints.
9. Build, tests, formato, lint y typecheck aplicables quedan verdes.

## 12. Trabajo diferido

- Invalidación inmediata de access tokens mediante versión de sesión o denylist.
- Cola/outbox y reintentos automáticos de correo.
- Cambio de contraseña para una sesión autenticada.
- Revisión futura de una política de contraseñas más fuerte para toda la aplicación.
