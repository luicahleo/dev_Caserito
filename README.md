# CaseritoApp

Marketplace C2C para Bolivia (web + Android). Backend .NET (Clean Architecture,
bounded contexts) + frontend SPA React/TypeScript (PWA, envuelta con Capacitor
para tiendas).

- Alcance y visión: `CaseritoApp/Documentacion/marketplace-bolivia-mvp-brief.md`.
- Plan por fases: `CaseritoApp/Documentacion/plan-desarrollo-mvp-v1.md`.
- Convenciones y arquitectura: `CLAUDE.md`.
- Specs y planes de cada bloque: `docs/superpowers/{specs,plans}/`.

## Cómo levantar la app (desarrollo)

El entorno de desarrollo corre en contenedores: **SQL Server 2022 + API (.NET) +
Web (Vite/React)**.

### Requisitos (una sola vez)

- **Docker Desktop** corriendo.
- Un archivo **`.env`** en la raíz: copiá `.env.example` a `.env` y definí `SA_PASSWORD`.

### Levantar

Desde la raíz del repo (PowerShell):

```powershell
./rebuild.ps1            # construye y levanta los 3 contenedores en segundo plano
./rebuild.ps1 -Logs      # además sigue los logs
```

Equivalente directo (o `rebuild.sh` en Bash):

```bash
docker compose -f docker-compose.dev.yml up -d --build --renew-anon-volumes
```

El **primer** build tarda (compila la imagen .NET y la de la web); los siguientes
usan caché y son rápidos. Al arrancar en Development la base de datos se **migra
sola**.

### URLs

- Web → **http://localhost:5173**
- API → http://localhost:8080 (health: `http://localhost:8080/health`)
- SQL Server → `localhost:1433` (usuario `sa`, contraseña = `SA_PASSWORD` del `.env`)

### Probar desde un móvil en PC2

PC2 puede exponer este mismo entorno por HTTPS a cualquier móvil conectado a su
misma Wi-Fi. No se conecta a producción y el login de Google/Facebook se simula
localmente.

1. Actualiza el repositorio, inicia Docker Desktop y copia `.env.example` como
   `.env` si todavía no existe. Completa `SA_PASSWORD` y usa solo cuentas
   sintéticas.
2. Desde la raíz ejecuta:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\iniciar-pc2.ps1
   ```

   Si la detección automática elige una VPN o adaptador incorrecto:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\iniciar-pc2.ps1 -Ip 192.168.1.25
   ```

3. El script muestra `https://<IP-PC2>` y la ruta de `root.crt`. Copia **solo ese
   certificado público** al móvil e instálalo como autoridad de confianza para
   pruebas. Nunca copies archivos `.key` ni el resto de `.local/pc2`.
   - Android: Ajustes → Seguridad → Cifrado y credenciales → Instalar certificado
     de CA (la ruta puede variar por fabricante).
   - iPhone/iPad: instala el perfil desde el archivo y activa después la confianza
     total en Ajustes → General → Información → Ajustes de confianza.
4. Abre la URL mostrada. La franja “Entorno de desarrollo — usa únicamente datos
   de prueba” debe aparecer en todas las páginas.

Para incluir el reconocimiento KYC local, coloca ARGOS en `../dev/ARGOS` respecto
de este repositorio y usa `-Argos`. La primera ejecución puede tardar mientras
descarga el modelo:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\iniciar-pc2.ps1 -Argos
```

Operación habitual:

```powershell
.\estado-pc2.ps1                 # estado
.\estado-pc2.ps1 -Logs           # últimos 100 eventos por servicio
.\detener-pc2.ps1                # detiene y conserva datos
.\detener-pc2.ps1 -BorrarDatos -ConfirmarBorradoDatos  # elimina volúmenes locales
```

Si el móvil no conecta, verifica que ambos dispositivos estén en la misma Wi-Fi,
que el router no tenga aislamiento de clientes y permite TCP 80/443 para redes
privadas en Firewall de Windows. Si cambia la IP de PC2, vuelve a ejecutar el
script: Caddy renovará el certificado local. El futuro subdominio de pruebas
reemplazará esta CA local y permitirá configurar los proveedores sociales reales.

### Preparar cuentas para las pruebas manuales de fases 1–3

El bootstrap es exclusivo de `Development`, está deshabilitado por defecto y no
modifica usuarios existentes. No funciona en Production ni Testing.

1. Copiá `.env.example` como `.env`.
2. Definí una contraseña local fuerte para `SA_PASSWORD`.
3. Cambiá `CASERITO_BOOTSTRAP_ENABLED=true`.
4. Completá las cuatro variables de cada cuenta con datos sintéticos:
   administrador, vendedor y comprador.
5. Usá correos reservados para pruebas y contraseñas diferentes de cualquier
   cuenta real. No pegues esos valores en Git, logs, capturas ni reportes.

El administrador recibe únicamente `AdminPlataforma`; vendedor y comprador
reciben únicamente `Cliente`. El vendedor empieza sin verificar para ejercitar
el flujo KYC real. Si falta una variable, no se crea ninguna cuenta.

Prepará también tres imágenes sintéticas JPG o PNG menores de 5 MiB:

- documento ficticio, marcado de forma visible como `PRUEBA — SIN VALIDEZ`;
- selfie sintética que no represente una persona real;
- foto sintética del artículo.

No uses documentos, rostros, correos ni nombres reales.

### Verificar el entorno

Desde la raíz:

```powershell
.\rebuild.ps1
docker compose -f docker-compose.dev.yml ps
Invoke-WebRequest http://localhost:8080/health -UseBasicParsing
Invoke-WebRequest http://localhost:5173 -UseBasicParsing
```

Los tres contenedores deben estar levantados, SQL Server debe figurar saludable
y ambas peticiones HTTP deben responder `200`. El bootstrap es idempotente:
repetir `.\rebuild.ps1` no duplica ni altera las cuentas.

### Guion manual reproducible en PC1

Usá una ventana normal como **sesión A** y una ventana de incógnito u otro
navegador como **sesión B**. Consultá las credenciales únicamente en el `.env`
local. Cerrá sesión antes de cambiar de actor dentro de una misma ventana.

#### 1. Registro, login y perfil

1. En sesión B, registrá una cuarta cuenta sintética desde **Registrarse**.
2. Iniciá sesión con ella y abrí **Perfil**.
3. Editá nombre y ciudad con valores sintéticos, guardá y recargá la página.
4. Resultado esperado: los cambios persisten y la sesión se recupera tras recargar.
5. Cerrá sesión. Resultado esperado: Perfil vuelve a requerir autenticación.

#### 2. Solicitud y aprobación KYC

1. En sesión B, iniciá como vendedor y abrí **Verificar identidad**.
2. Subí el documento ficticio y la selfie sintética.
3. Resultado esperado: el estado pasa a `En revisión` y no permite otra solicitud.
4. En sesión A, iniciá como administrador y abrí **Revisar verificaciones**.
5. Abrí la solicitud pendiente, comprobá ambas imágenes y aprobala.
6. En sesión B, recargá o volvé a iniciar sesión.
7. Resultado esperado: Perfil muestra `Identidad verificada`.

#### 3. Publicación, búsqueda y detalle

1. Como vendedor en sesión B, abrí **Publicar aviso**.
2. Creá un aviso sintético con título distinguible, descripción sin PII, precio,
   categoría, condición y ciudad; añadí la foto sintética.
3. Resultado esperado: aparece en **Mis avisos** y en **Explorar**.
4. Buscalo por una palabra del título y aplicá filtros de categoría, ciudad,
   precio y condición.
5. Abrí el detalle y comprobá texto, precio, ubicación y foto.

#### 4. Contacto y mensajes en tiempo real

1. Dejá al vendedor autenticado en sesión B.
2. En sesión A, cerrá la sesión administrativa e iniciá como comprador.
3. Buscá el aviso, abrí el detalle y pulsá **Contactar al vendedor**.
4. Resultado esperado: se abre una conversación asociada al aviso.
5. Abrí la misma conversación como vendedor en sesión B.
6. Enviá mensajes sintéticos alternando ambas ventanas.
7. Resultado esperado: cada mensaje aparece en la otra ventana sin recargar y
   una repetición/reintento no crea duplicados.

#### 5. Lectura, no leídos y recuperación

1. Sacá una sesión de la conversación, por ejemplo navegando a **Explorar**.
2. Desde la otra sesión enviá dos mensajes.
3. Resultado esperado: aumenta el contador de no leídos de navegación y bandeja.
4. Abrí la conversación receptora.
5. Resultado esperado: los mensajes aparecen y el contador vuelve a cero tras
   marcarse la lectura.
6. En la conversación, abrí DevTools → Network y activá **Offline**.
7. Enviá desde la otra ventana uno o más mensajes y comprobá el indicador de
   desconexión.
8. Volvé a **Online**.
9. Resultado esperado: reconecta, recupera los mensajes faltantes, mantiene el
   orden y no duplica ninguno.

#### 6. Cierre, reapertura, bloqueo y desbloqueo

1. Cerrá la conversación desde un participante.
2. Resultado esperado: el historial sigue visible y el compositor queda inactivo.
3. Reabrila. Resultado esperado: el compositor vuelve a estar disponible tras
   la nueva suscripción.
4. Bloqueá a la contraparte.
5. Resultado esperado: no se puede enviar ni iniciar otra conversación entre
   ambos, pero el historial sigue legible.
6. Desbloqueá y comprobá que vuelve a ser posible enviar.

#### 7. Reportes

Usá únicamente detalles sintéticos y sin información personal:

1. Desde el detalle del aviso, reportá el aviso.
2. Desde el chat, creá por separado reportes de:
   - conversación;
   - un mensaje concreto;
   - contraparte.
3. Resultado esperado: cada reporte válido se confirma; repetir el mismo reporte
   pendiente produce un conflicto genérico y no crea un duplicado.

#### 8. Moderación

1. Cerrá sesión A como comprador e iniciá nuevamente como administrador.
2. En **Moderación de avisos**, localizá el reporte, tomalo, revisalo y aplicá la
   acción elegida. Comprobá en Explorar el efecto sobre el aviso.
3. En **Moderación de chat**, localizá cada reporte, tomalo y abrí la evidencia.
4. Comprobá que la evidencia muestra solo la ventana mínima autorizada y roles
   relativos, sin identidades técnicas.
5. Atendé o descartá reportes; en uno de ellos probá cerrar la conversación por
   moderación y luego reabrirla desde el expediente.
6. Resultado esperado: los estados de cola cambian correctamente y un
   participante no puede revertir por sí mismo un cierre de moderación.

Anotá por paso únicamente `correcto` o una descripción genérica del fallo. No
copies textos de mensajes/reportes, imágenes, tokens, credenciales ni IDs.
Estas pruebas no se consideran superadas hasta confirmar los resultados
observados en ambos navegadores.

### Cerrar o reiniciar la prueba

```powershell
docker compose -f docker-compose.dev.yml down
```

Esto conserva la base. Para empezar realmente desde cero, después de confirmar
que no necesitás los datos locales de prueba:

```powershell
docker compose -f docker-compose.dev.yml down -v
```

El segundo comando elimina el volumen local de SQL Server y no es reversible.

### Comandos útiles

```powershell
docker compose -f docker-compose.dev.yml ps            # estado de los contenedores
docker compose -f docker-compose.dev.yml logs -f web   # logs de la web (o api / sqlserver)
docker compose -f docker-compose.dev.yml down          # apagar (los datos de SQL persisten)
docker compose -f docker-compose.dev.yml down -v       # apagar y borrar datos (empezar de cero)
```

### Problemas frecuentes

- **La web da `504 (Outdated Optimize Dep)`**: es la caché de pre-bundle de Vite
  en el volumen de `node_modules`. Recreá solo la web con el volumen renovado:

  ```powershell
  docker compose -f docker-compose.dev.yml up -d --force-recreate --renew-anon-volumes web
  ```

- **`POST /api/auth/refresh` devuelve `401` en la consola al cargar**: es
  **esperado** cuando no hay sesión iniciada; la app lo maneja y continúa como
  usuario anónimo.

## Flujo híbrido (API en el host, SQL en contenedor)

Para depurar la API desde el IDE con hot-reload contra SQL en contenedor:

```bash
docker compose -f docker-compose.dev.yml up -d sqlserver
# cadena de conexión a localhost,1433 vía user-secrets del host
dotnet run --project CaseritoApp/src/Host/CaseritoApp.Host
cd web && npm run dev        # Vite proxya /api y /health al host
```

## Comandos de desarrollo

Backend (desde `CaseritoApp/`):

```bash
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes
```

Frontend (desde `web/`):

```bash
npm run dev | build | lint | typecheck | test
```
