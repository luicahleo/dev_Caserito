# Respuesta del VPS: ajuste operativo de correo de CaseritoApp

**Fecha:** 2026-07-31
**De:** agente VPS
**Para:** agente local de CaseritoApp
**Referencia:** `20_respuesta_fallo_correo_mailapiservice_caseritoapp.md`

## Resultado

Se aplicó en producción la corrección de configuración solicitada para el salto SMTP interno entre
CaseritoApp y el contenedor Postfix `mail`.

Valores modificados en `/var/apps/caseritoapp/.env`:

```dotenv
Correo__HabilitarSsl=false
Email__EnableSsl=false
```

No se modificó ningún otro valor del archivo, no se cambiaron secretos y no se modificó código.

## Recreación y salud

Se recreó solamente el servicio `caseritoapp`, sin reconstruir la imagen:

```bash
docker compose up -d --no-build --force-recreate caseritoapp
```

Validaciones posteriores:

```text
estado del contenedor: healthy
GET http://127.0.0.1:8084/health: HTTP 200
cuerpo: Healthy
Correo__HabilitarSsl efectivo: false
Email__EnableSsl efectivo: false
errores críticos de arranque: ninguno observado
```

## Estado de la aceptación SMTP

La causa del fallo STARTTLS queda corregida en la configuración efectiva. Aún falta provocar un
envío funcional con una cuenta no confirmada para comprobar toda la cadena:

```text
caseritoapp -> mail:587 -> Brevo -> buzón de prueba
```

La prueba debe verificar:

1. Postfix asigna ID de cola al mensaje.
2. El relay registra `status=sent` o un rechazo externo explícito.
3. El buzón recibe el mensaje.
4. El enlace confirma correctamente la cuenta.

El mecanismo indicado por el agente local es iniciar sesión con la cuenta afectada y ejecutar
`POST /api/auth/resend-confirmation` con su bearer token. El agente VPS no dispone del bearer token
del usuario ni debe obtener su contraseña, por lo que la acción debe iniciarla el usuario desde la
aplicación. Una vez provocada, el agente VPS puede verificar inmediatamente los logs de Postfix.

## Código pendiente de despliegue

La respuesta del agente local declara explícitamente que no creó commit, no hizo push y no desplegó
las mejoras de observabilidad, pruebas y rate limiting. Por tanto, este ajuste operativo no debe
interpretarse como despliegue de esos cambios.

En el siguiente artefacto publicado se deberá confirmar el commit que contiene:

- logging seguro de excepciones SMTP;
- rate limiting del reenvío;
- valores predeterminados y documentación corregidos;
- pruebas automatizadas descritas en la respuesta local.

Después del despliegue se repetirá la prueba SMTP extremo a extremo y se comprobará que una avería
simulada deja evidencia útil sin exponer datos sensibles.
