# Entorno local de PC2 accesible desde móvil

## Objetivo

Permitir que PC2, después de actualizar el repositorio, levante Caserito completo
con un único script y lo exponga por HTTPS al móvil conectado a su misma red
Wi-Fi. El entorno debe ser inequívocamente de desarrollo, usar almacenamiento
local y permitir probar el flujo de autenticación social sin contactar a Google
o Facebook.

## No objetivos

- No configurar todavía el futuro subdominio de pruebas ni su túnel desde la VPS.
- No reutilizar bases de datos, secretos, cuentas o servicios de producción.
- No imitar las pantallas visuales exactas de Google o Facebook.
- No convertir el simulador social en un mecanismo disponible en Production.

## Topología

`docker-compose.dev.yml` seguirá siendo la base y contendrá SQL Server, API y
Vite. El entorno móvil añadirá un reverse proxy TLS en el puerto 443 que expone
la web como un único origen y reenvía a Vite; Vite conserva sus proxies de API,
health y SignalR hacia la API. La extensión de ARGOS se levantará cuando su
imagen/configuración local esté disponible.

El script de PC2:

1. valida Docker y la configuración local;
2. detecta la IPv4 del adaptador que tiene la ruta por defecto, permitiendo una
   IP explícita para resolver ambigüedades;
3. crea o renueva una autoridad certificadora y un certificado con SAN para esa
   IP y `localhost`, sin versionar claves privadas;
4. construye y levanta los contenedores;
5. espera los healthchecks y muestra la URL HTTPS, estado y ruta del certificado
   público que debe instalarse en el móvil.

Habrá comandos documentados para iniciar, detener, consultar logs y borrar
volúmenes locales de forma explícita.

## HTTPS local y confianza del móvil

Los certificados y claves se guardarán en una ruta ignorada por Git. La clave de
la CA y la clave del servidor no se imprimirán ni copiarán al móvil. Solo el
certificado público de la CA se instalará como autoridad de confianza en el
dispositivo de pruebas. El certificado se regenerará si cambia la IP de PC2.

El script no abrirá puertos en el firewall automáticamente: informará una
instrucción acotada si Windows bloquea el acceso. El servicio se vinculará al
puerto HTTPS necesario para la LAN; SQL Server y la API no necesitan exposición
directa al móvil.

## Simulador de autenticación social

Una opción `Authentication:Simulador:Habilitado` estará desactivada por defecto.
Solo podrá habilitarse en `Development`; el host fallará al iniciar si se intenta
activar en otro entorno.

Cuando esté activo:

- `/api/auth/external/providers` ofrecerá Google y Facebook aunque no existan
  credenciales reales;
- iniciar un proveedor abrirá una pantalla local claramente rotulada como
  simulador, nunca una réplica engañosa del proveedor;
- se podrá aprobar o cancelar usando exclusivamente perfiles sintéticos
  predefinidos;
- los escenarios cubrirán alta nueva, identidad ya vinculada y correo existente
  que requiere vinculación;
- Google podrá entregar email verificado y Facebook email no verificado;
- al aprobar se creará el principal externo temporal y se continuará por el
  callback, onboarding, vinculación y emisión de sesión reales de Caserito.

Los identificadores sintéticos serán estables para poder repetir el acceso. Los
endpoints del simulador no aceptarán secretos, tokens ni PII arbitraria, no se
incluirán en OpenAPI y devolverán 404 cuando estén deshabilitados. Producción
seguirá usando únicamente los handlers oficiales configurados.

## Indicador visual de Development

El layout global mostrará en todas las rutas una franja persistente y accesible:

> Entorno de desarrollo — usa únicamente datos de prueba

La señal se activará con una variable explícita de build/runtime del frontend en
el compose local. Fuera de Development no se renderizará y no ocupará espacio.
Debe adaptarse desde 320 px, tener contraste suficiente y no tapar navegación ni
acciones.

## Datos y servicios externos

SQL Server usará exclusivamente el volumen local existente. Las cuentas de
bootstrap y los escenarios sociales emplearán valores sintéticos. ARGOS se
mantendrá como extensión local. Correo, pagos u otras integraciones se conectarán
solo a dobles locales o sandboxes en trabajos posteriores si hoy no existe uno;
el entorno no ganará acceso implícito a producción.

## Errores y seguridad

- Los scripts deben fallar con mensajes accionables si falta Docker, `.env`, una
  IP válida, generación TLS o un contenedor saludable.
- No registrar documentos, imágenes, tokens, credenciales, contenido privado ni
  valores del `.env`.
- No versionar `.env`, certificados privados, bases locales ni material de CA.
- El indicador visual no sustituye los bloqueos técnicos por entorno.
- El simulador debe respetar la normalización de `returnUrl` existente.

## Pruebas

- Tests backend de habilitación, rechazo fuera de Development, proveedores,
  aprobación/cancelación y continuación de escenarios por el flujo real.
- Tests frontend del indicador visible en Development y ausente fuera de él.
- Tests del formulario/pantalla del simulador si reside en React; si lo sirve el
  host, tests de integración HTTP y contenido accesible.
- Validación estática de compose y pruebas dirigidas de los scripts donde sea
  viable, más arranque real de contenedores y healthchecks.
- Puertas completas backend/frontend y `verify.ps1 -Full` antes del merge.

## Criterios de aceptación

1. En PC2, tras `git pull` y configurar `.env`, un único script levanta el
   entorno y muestra una URL `https://<ip-wifi>` utilizable desde el móvil.
2. El móvil confía en la CA pública instalada y puede usar cámara/carga de fotos,
   PWA, API y SignalR sin errores de contenido mixto.
3. Ninguna petición de la SPA local usa producción.
4. Google y Facebook simulados ejercitan onboarding, sesión, cancelación y
   vinculación sin contactar servicios externos.
5. El simulador devuelve 404 deshabilitado y no puede activarse fuera de
   Development.
6. Todas las rutas muestran la leyenda de Development en el entorno PC2 y nunca
   la muestran en el build de producción.
7. Detener y volver a iniciar conserva datos; el borrado requiere una acción
   explícita y documentada.
8. La puerta de calidad completa queda verde.
