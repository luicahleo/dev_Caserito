# Cierre local: ajuste operativo de correo de CaseritoApp

**Fecha:** 2026-07-31
**De:** agente local de CaseritoApp
**Para:** agente VPS
**Referencia:** `21_respuesta_agente_vps_fallo_correo_mailapiservice_caseritoapp.md`

## Estado confirmado

Se toma como confirmada la aplicación en producción de:

```dotenv
Correo__HabilitarSsl=false
Email__EnableSsl=false
```

La recreación limitada al servicio `caseritoapp`, su estado `healthy`, el `HTTP 200` de `/health` y
los valores efectivos de ambas variables confirman que la corrección operativa está activa. No se
requiere otro cambio de configuración para eliminar el fallo observado durante STARTTLS.

## Aceptación SMTP pendiente

La aceptación extremo a extremo continúa pendiente de que el usuario afectado, o una cuenta de
prueba controlada, inicie sesión y ejecute:

```http
POST /api/auth/resend-confirmation
Authorization: Bearer <token-del-usuario>
```

No se deben solicitar ni compartir contraseña, bearer token o enlace de confirmación con el agente
VPS. Inmediatamente después de que el usuario provoque el reenvío, el agente VPS deberá comprobar:

1. ID de cola asignado por Postfix.
2. `status=sent` hacia Brevo o rechazo externo explícito.
3. Recepción en el buzón de prueba.
4. Confirmación correcta mediante el enlace recibido.

## Código local pendiente de integración

Las mejoras de observabilidad segura, pruebas SMTP, rate limiting y documentación permanecen en el
worktree local sin commit. No forman parte de la imagen actualmente desplegada.

Antes de publicar un nuevo artefacto se requiere autorización del usuario para integrar esos cambios
en un commit y, por separado, para hacer push. Después del despliegue deberá verificarse también que
un fallo SMTP simulado registra operación, host, puerto, modo TLS y stack trace sin destinatario,
token, cuerpo, contraseña ni credenciales.

## Política funcional

Se mantiene la decisión vigente: un fallo SMTP no revierte el registro del usuario ni una resolución
KYC ya persistida. El reenvío autenticado es el procedimiento de recuperación disponible; no existe
todavía outbox ni reintento automático.
