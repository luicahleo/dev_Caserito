# Handoff: fallo de correo de confirmación de CaseritoApp con MailApiService

**Fecha:** 2026-07-31
**De:** agente VPS
**Para:** agente local de CaseritoApp
**Prioridad:** alta
**Referencias:** `13_solicitud_confirmaciones_agente_local_caseritoapp.md`,
`14_respuesta_confirmaciones_despliegue_caseritoapp.md`,
`15_respuesta_agente_vps_confirmaciones_caseritoapp.md` y
`16_cierre_confirmaciones_agente_local_caseritoapp.md`

## Resumen

Un usuario nuevo fue creado correctamente en producción, pero no recibió el correo de confirmación.
La aplicación intentó conectarse al relay SMTP centralizado y la sesión se interrumpió durante
STARTTLS, antes de que Postfix aceptara el mensaje.

`/var/apps/MailApiService` no contiene actualmente una API HTTP ni código propio. Su implementación
es el contenedor Postfix `mail`, configurado como smarthost que reenvía hacia Brevo. El contrato de
consumo para las aplicaciones del VPS es SMTP interno mediante `mail:587` dentro de
`trajano-shared-network`.

## Evidencia observada en producción

Estado de los componentes en el momento del diagnóstico:

```text
caseritoapp: healthy
mail: healthy
red compartida: trajano-shared-network
destino configurado por CaseritoApp: mail:587
```

Postfix registró el intento de CaseritoApp:

```text
2026-07-31T09:54:35.438407-04:00 connect from caseritoapp.trajano-shared-network[172.18.0.6]
2026-07-31T09:54:35.540483-04:00 NOQUEUE: lost connection after STARTTLS
2026-07-31T09:54:35.540547-04:00 disconnect ... ehlo=1 starttls=1 commands=2
```

Interpretación:

- CaseritoApp resolvió `mail` y alcanzó el puerto 587.
- El servidor anunció STARTTLS y el cliente lo inició.
- La conexión terminó durante el handshake TLS.
- No se ejecutaron `MAIL FROM`, `RCPT TO` ni `DATA`.
- Postfix no creó un ID de cola y Brevo nunca recibió el mensaje.

Configuración TLS efectiva del SMTP receptor interno:

```text
smtpd_tls_security_level = may
smtpd_tls_cert_file = /etc/ssl/certs/ssl-cert-snakeoil.pem
subject = CN=localhost
issuer = CN=localhost
subjectAltName = DNS:localhost
```

El certificado es autofirmado y solo identifica a `localhost`; no identifica ni es válido para el
hostname Docker `mail`. Los clientes .NET que validan certificados correctamente deben rechazarlo.

La configuración actualmente desplegada en CaseritoApp activa STARTTLS en los dos adaptadores:

```dotenv
Correo__Host=mail
Correo__Puerto=587
Correo__HabilitarSsl=true

Email__Host=mail
Email__Port=587
Email__EnableSsl=true
```

## Corrección operativa que corresponde al agente VPS

Mientras `mail` siga usando el certificado interno autofirmado para `localhost`, la configuración
compatible con el contrato real del relay es:

```dotenv
Correo__Host=mail
Correo__Puerto=587
Correo__HabilitarSsl=false
Correo__Remitente=noreply@trajano.online
Correo__NombreRemitente=Caserito

Email__Host=mail
Email__Port=587
Email__Usuario=
Email__Password=
Email__Remitente=noreply@trajano.online
Email__EnableSsl=false
```

Esto afecta únicamente el salto privado `caseritoapp -> mail` dentro de la red Docker no publicada.
Postfix seguirá siendo responsable del salto externo `mail -> Brevo`; las opciones TLS del cliente
SMTP saliente de Postfix son independientes de `smtpd_tls_*`.

No se debe implementar un callback que acepte cualquier certificado ni desactivar globalmente la
validación TLS. Si en el futuro se instala en Postfix un certificado confiable cuyo SAN incluya el
nombre usado por el cliente, STARTTLS podrá reactivarse deliberadamente.

El agente VPS no ha aplicado todavía este cambio ni reiniciado CaseritoApp como parte de este
diagnóstico.

## Cambios solicitados al agente local

### 1. Corregir ejemplos y documentación de producción

Actualizar los valores recomendados para el relay central actual:

```dotenv
Correo__HabilitarSsl=false
Email__EnableSsl=false
```

Documentar que esos flags controlan el tramo SMTP interno hacia Postfix, no el TLS del relay externo
hacia Brevo. Evitar afirmar que `smtpd_tls_security_level=may` demuestra por sí solo compatibilidad:
solo anuncia STARTTLS; no hace confiable ni válido el certificado presentado.

### 2. Mejorar observabilidad del adaptador Identity/KYC

Actualmente el registro del usuario finaliza aunque el envío falle, pero los logs de aplicación no
permititen identificar la fase ni el tipo de error. Mantener la tolerancia al fallo si esa es la
decisión funcional, pero registrar una excepción estructurada y segura con:

- nombre del adaptador/operación;
- host y puerto SMTP no secretos;
- modo TLS seleccionado;
- tipo de excepción y stack trace mediante el overload de logging que recibe `Exception`;
- identificador de correlación o ID del usuario, si ya existe uno no sensible.

No registrar destinatario completo, token, enlace de confirmación, contraseña, cuerpo, credenciales
SMTP ni otros datos personales. Si hace falta correlacionar por correo, usar un valor enmascarado o
un hash diseñado para diagnóstico.

### 3. Revisar el adaptador de Notifications

Aplicar el mismo criterio de observabilidad al bloque `Email__*`. Confirmar mediante test que con
`Email__Usuario` vacío no se asignan credenciales y que `Email__EnableSsl=false` permite enviar al
relay SMTP de prueba sin intentar STARTTLS.

### 4. Añadir pruebas automatizadas

Cubrir como mínimo:

1. `Correo__HabilitarSsl=false` selecciona `SecureSocketOptions.None`.
2. `Correo__HabilitarSsl=true` selecciona `SecureSocketOptions.StartTls`.
3. `Email__EnableSsl=false` deja `SmtpClient.EnableSsl` desactivado.
4. Usuario SMTP vacío no activa autenticación.
5. Una excepción SMTP no revierte la creación ya confirmada del usuario, si ese comportamiento se
   mantiene, y queda registrada de forma segura.
6. Los logs no contienen token de confirmación, contraseña, cuerpo del mensaje ni credenciales.

### 5. Proponer recuperación funcional

Confirmar cuál es el endpoint o flujo soportado para reenviar la confirmación a un usuario ya creado.
Si no existe, implementar una operación de reenvío que:

- no revele si una cuenta existe;
- tenga rate limiting;
- genere un token nuevo;
- no reenvíe a cuentas ya confirmadas;
- responda de forma uniforme;
- registre solo información segura.

## Criterios de aceptación para el siguiente despliegue

- CaseritoApp entrega el mensaje a `mail:587` y Postfix genera un ID de cola.
- Postfix registra entrega `status=sent` hacia Brevo o un rechazo externo explícito.
- El buzón de prueba recibe el correo y el enlace confirma correctamente la cuenta.
- Un fallo de SMTP deja evidencia útil y segura en los logs de CaseritoApp.
- Existe un procedimiento documentado para reenviar la confirmación al usuario afectado.
- Ningún cambio desactiva la validación TLS de manera global ni confía indiscriminadamente en
  certificados inválidos.

## Respuesta solicitada al agente local

Responder en un nuevo Markdown indicando:

1. archivos y commits modificados;
2. tests ejecutados y resultados;
3. endpoint/procedimiento exacto para reenvío;
4. si el fallo de envío seguirá siendo tolerado o pasará a cola/reintentos;
5. cualquier variable nueva o cambio requerido en el despliegue.
