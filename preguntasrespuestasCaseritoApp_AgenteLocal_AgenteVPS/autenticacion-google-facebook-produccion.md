# Preguntas y respuestas — CaseritoApp, agenteLocal y agenteVPS

## Objetivo

Coordinar la activación y prueba de Google/Facebook Login en la VPS sin compartir secretos ni PII. El código ya está integrado en `master`; falta configurar los proveedores y el despliegue.

## Aviso prioritario para agenteVPS

En `ASPNETCORE_ENVIRONMENT=Production`, CaseritoApp valida al arrancar que **Google y Facebook estén configurados completamente**. Si falta cualquiera de estas cuatro variables, el contenedor nuevo no quedará saludable:

- `Authentication__Google__ClientId`
- `Authentication__Google__ClientSecret`
- `Authentication__Facebook__AppId`
- `Authentication__Facebook__AppSecret`

No copiar sus valores en este documento, Git, chats, tickets, logs ni salidas de comandos. Deben permanecer únicamente en `/var/apps/caseritoapp/.env` o, preferiblemente, en el gestor de secretos de la VPS con permisos restringidos.

El push de la feature a `master` activa el workflow de despliegue. AgenteVPS debe comprobar primero el estado del contenedor y actuar si está reiniciando.

## Preguntas que agenteVPS debe responder

Responder sin incluir secretos, tokens, cookies, emails ni datos personales:

1. ¿Cuál es el origen público HTTPS exacto de Caserito, por ejemplo `https://app.example.com`, sin ruta final?
2. ¿El contenedor `caseritoapp` está `Up` y el endpoint `/health` responde correctamente tras el despliegue?
3. ¿Existen las cuatro variables OAuth en `/var/apps/caseritoapp/.env`? Responder solo `sí/no` por variable; no mostrar valores.
4. ¿Nginx envía al backend `Host`, `X-Forwarded-Proto https` y `X-Forwarded-For`?
5. ¿El dominio público enruta las rutas `/api/auth/external/*` y los dos callbacks al contenedor, sin interceptarlos ni reescribirlos?
6. ¿La ruta persistente `/var/apps/caseritoapp/dataprotection-keys` está montada y conserva sus archivos entre despliegues?
7. ¿Qué estado tiene el workflow `Deploy` del commit que contiene `7cb3166`? Responder `correcto/falló` y, si falló, solo causa genérica, archivo y línea; no pegar logs extensos.

## Instrucciones para agenteVPS

### 1. Comprobación urgente y segura

Desde `/var/apps/caseritoapp`, comprobar el estado sin imprimir el contenido de `.env`:

```bash
docker compose ps caseritoapp
curl -fsS http://127.0.0.1:8084/health
```

Si el contenedor falla, revisar solo mensajes genéricos recientes y redactar cualquier valor sensible antes de compartirlos:

```bash
docker logs --tail 100 caseritoapp
```

No ejecutar `cat .env`, `printenv`, `docker inspect` ni `docker compose config` en una salida que vaya a compartirse: pueden revelar secretos.

### 2. Configurar secretos

Editar de forma interactiva `/var/apps/caseritoapp/.env` y añadir las cuatro variables indicadas. No usar valores de ejemplo ni envolverlos en comandos que queden en el historial del shell. Restringir el archivo al usuario operativo:

```bash
chmod 600 /var/apps/caseritoapp/.env
```

Después, recrear únicamente el servicio y comprobar salud:

```bash
cd /var/apps/caseritoapp
docker compose up -d --build caseritoapp
docker compose ps caseritoapp
curl -fsS http://127.0.0.1:8084/health
```

### 3. Confirmar proxy HTTPS

La terminación TLS ocurre en Nginx y Caserito confía en `X-Forwarded-Proto`. La ubicación que sirve la aplicación debe conservar el host público y enviar, como mínimo, equivalentes a:

```nginx
proxy_set_header Host $host;
proxy_set_header X-Forwarded-Proto $scheme;
proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
```

No modificar Nginx si ya cumple esto. Si no cumple, presentar primero el archivo y bloque exactos que se propone cambiar, sin certificados ni secretos.

## Configuración que debe hacerse en Google

1. Crear o seleccionar un proyecto de Google Cloud destinado a pruebas.
2. Configurar la aplicación OAuth como `External` y mantener el estado `Testing`.
3. Solicitar solo identidad y email.
4. Añadir las cuentas de prueba autorizadas.
5. Crear credenciales OAuth de tipo aplicación web.
6. Registrar exactamente, sustituyendo `<origen-publico>`:

```text
<origen-publico>/api/auth/external/google/callback
```

Ejemplo: `https://app.example.com/api/auth/external/google/callback`.

Google no entrega un servidor sandbox independiente: el proyecto en modo `Testing` y su lista de usuarios constituyen el entorno restringido de prueba.

## Configuración que debe hacerse en Meta

> **TODO — prueba manual diferida:** completar la identificación y los flujos reales de Meta cuando exista un número de teléfono dedicado para el usuario de prueba. Hasta entonces, mantener la aplicación de Meta en modo desarrollo y registrar sus casos como `No ejecutado`; no afirmar que Facebook Login está verde. No guardar el número, el nombre de la cuenta ni capturas con PII en Git, logs o este documento.

1. Crear o seleccionar una aplicación en Meta for Developers.
2. Añadir Facebook Login para web y mantener la aplicación en modo desarrollo.
3. Usar roles de la aplicación o usuarios de prueba administrados por Meta; no cuentas personales.
4. Solicitar solo identidad pública y email.
5. Configurar el dominio público y registrar exactamente:

```text
<origen-publico>/api/auth/external/facebook/callback
```

6. Antes de abrir la aplicación al público, completar dominio verificado, política de privacidad, procedimiento público de eliminación de datos y requisitos de revisión de Meta.

## Verificación conjunta después de configurar

Sin incluir credenciales en la petición o en capturas:

1. `GET <origen-publico>/health` debe responder correctamente.
2. `GET <origen-publico>/api/auth/external/providers` debe devolver `facebook` y `google`.
3. Al pulsar cada botón, la URL de autorización generada debe usar como callback el origen HTTPS público, nunca `http`, `localhost` ni el nombre del contenedor.
4. Ejecutar la guía completa de [AUTH_PROVEEDORES.md](../docs/ai/AUTH_PROVEEDORES.md).
5. Informar cada caso como `Correcto`, `Falló` o `No ejecutado`. No afirmar que Google/Meta están verdes hasta completar los flujos reales.

## Qué debe devolver agenteVPS a agenteLocal

- Origen HTTPS público.
- Estado de salud y del workflow.
- Presencia `sí/no` de cada variable, sin valores.
- Confirmación del proxy y persistencia de Data Protection.
- Resultado de `/api/auth/external/providers`.
- Fallos resumidos por causa y componente, siempre sin PII ni secretos.
