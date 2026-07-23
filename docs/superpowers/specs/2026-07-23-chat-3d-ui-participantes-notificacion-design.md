# Chat 3D — UI de participantes y notificación básica

Fecha: 2026-07-23  
Estado: aprobado

## Objetivo

Permitir que un comprador autenticado contacte al vendedor desde un aviso ajeno y
que ambos usen dentro de CaseritoApp el chat persistente y en tiempo real construido
en 3A, 3B y 3C.

El bloque entrega bandeja, conversación, lectura, acciones de seguridad y un contador
global básico. HTTP continúa siendo la fuente de verdad y SignalR reduce la latencia.

## No objetivos

- presencia e indicador de escritura;
- adjuntos, edición, eliminación o búsqueda;
- nombres, perfiles o identificadores técnicos de la contraparte;
- push, correo, preferencias o persistencia completa de Notifications;
- órdenes, QR, pagos y envíos;
- cambios en los contratos backend salvo que una prueba demuestre una carencia
  imprescindible.

## Decisiones funcionales

### Inicio desde un aviso

El detalle de un aviso muestra `Contactar al vendedor` únicamente a un usuario
autenticado que no sea su propietario. La comparación usa el ID del perfil
autenticado y una consulta protegida existente; el detalle público no expone al
vendedor.

La acción llama a `POST /api/chat/conversaciones`. Tanto creación como reutilización
idempotente navegan a `/mensajes/{conversacionId}`. Un fallo muestra un texto
genérico. Para una persona anónima se ofrece iniciar sesión conservando el retorno al
aviso.

### Bandeja

La ruta protegida `/mensajes` lista conversaciones propias por cursor. Cada elemento
muestra:

- título del aviso, obtenido del catálogo público;
- rol relativo de la contraparte: `Comprador` o `Vendedor`;
- fecha de última actividad;
- cantidad de mensajes no leídos;
- estado operativo.

No muestra IDs ni nombres de usuario. Si el aviso ya no es público usa `Aviso no
disponible`. La bandeja tiene estados de carga, vacío, error y paginación.

### Conversación e historial

La ruta protegida `/mensajes/{id}` obtiene la conversación desde la bandeja propia y
carga los mensajes recientes. La paginación histórica usa el cursor opaco del
backend; cargar anteriores antepone resultados y deduplica por ID.

Los mensajes se presentan como propios o de la contraparte comparando el remitente
con el ID del perfil. El texto se renderiza siempre como texto plano y conserva
saltos de línea. No se muestran IDs ni metadatos técnicos.

Después de renderizar la secuencia más alta recibida se marca lectura por HTTP. La
operación es monotónica y los fallos no bloquean la lectura del historial.

### Envío

Cada intento lógico genera un UUID de idempotencia. Si la petición falla, el texto y
la misma clave se conservan para que el usuario pueda reintentar sin duplicar. Tras
éxito se incorpora el DTO retornado, se limpia el borrador y se genera una clave nueva
para el siguiente mensaje.

El compositor queda deshabilitado cuando `puedeEnviar` es falso, durante el envío o
si el texto normalizado está vacío. La UI explica de forma genérica que la
conversación no admite mensajes, sin revelar dirección de bloqueos.

### Tiempo real y recuperación

La pantalla se suscribe únicamente a su conversación. Combina historial HTTP y
`MensajeCreado` mediante `SincronizadorMensajes`, deduplica por ID y ordena por
secuencia.

Ante un hueco recupera por `despuesDeSecuencia`. Tras reconectar vuelve a suscribirse
y recupera desde la última secuencia aplicada. El estado desconectado se muestra sin
afirmar pérdida de mensajes. Cierre o bloqueo revoca la suscripción; reapertura o
desbloqueo vuelve a suscribir explícitamente.

### Seguridad y reportes

Un menú ofrece, según estado:

- cerrar o reabrir la conversación;
- bloquear o desbloquear a la contraparte;
- reportar la conversación;
- reportar a la contraparte;
- reportar un mensaje concreto desde ese mensaje.

El diálogo de reporte exige categoría, acepta detalle opcional y nunca acepta un ID
de usuario. Los errores son genéricos. La UI no registra mensajes, reportes, tokens,
usuarios ni argumentos.

### Notificación básica

La navegación autenticada muestra un contador global de no leídos. Se calcula desde
la primera página de hasta 50 conversaciones, se refresca periódicamente y se
invalida al iniciar, enviar, recibir o marcar lectura.

Es una aproximación deliberada de MVP: no persiste una notificación separada, no
suscribe globalmente todas las conversaciones y no promete exactitud para más de 50
conversaciones. La bandeja conserva el conteo autoritativo por conversación.

## Arquitectura web

- `src/api/chat.ts` completa todos los adaptadores HTTP usando tipos OpenAPI.
- `src/chat/` conserva la sincronización y añade un hook/controlador de conversación
  que coordina HTTP y SignalR sin estado global sensible.
- `src/routes/ConversacionesPage.tsx` implementa la bandeja.
- `src/routes/ConversacionPage.tsx` implementa historial, compositor y acciones.
- `src/chat/ContadorChat.tsx` consulta el contador básico para `AppLayout`.
- `DetalleAvisoPage`, router y layout reciben únicamente integración visual.

TanStack Query administra estado remoto e invalidaciones. El estado efímero del
borrador y la clave idempotente permanece local a la pantalla.

## Errores, responsive y accesibilidad

Todas las superficies tienen carga, vacío y error genérico. En móvil la bandeja y la
conversación ocupan el ancho disponible, los controles se apilan y el compositor
permanece utilizable sin desbordamientos. Acciones tienen nombres accesibles y los
estados no dependen solo del color.

## Seguridad y PII

- no registrar payloads, texto, reportes, tokens, usuarios o IDs;
- no mostrar identificadores técnicos;
- texto siempre como contenido React, nunca HTML;
- errores genéricos sin reflejar entradas;
- perfiles y nombres de contraparte quedan fuera del contrato;
- cachés contienen solo los DTO autorizados durante la sesión normal.

## Pruebas

- adaptadores: inicio, historial con cursor, envío, lectura y conversiones numéricas;
- detalle: botón solo en aviso ajeno autenticado, reutilización y errores;
- bandeja: carga, vacío, no leídos, aviso no disponible y paginación;
- conversación: historial, anteriores, envío/reintento, lectura y `puedeEnviar`;
- tiempo real: deduplicación, orden, huecos, reconexión, revocación y recuperación;
- acciones: cierre, reapertura, bloqueo, desbloqueo y tres objetivos de reporte;
- layout/router: rutas protegidas y contador;
- responsive mediante estructura y estilos verificables;
- flujo comprador-vendedor en entorno reconstruido.

## Criterios de aceptación

1. Un comprador autenticado inicia o reutiliza chat desde un aviso ajeno.
2. Ambos participantes encuentran la conversación y leen historial paginado.
3. Envíos repetidos con la misma intención no duplican mensajes.
4. Los mensajes vivos se deduplican, ordenan y recuperan tras desconexión.
5. La lectura actualiza los contadores de bandeja y navegación.
6. `puedeEnviar=false` impide usar el compositor y explica el estado.
7. Cierre, reapertura, bloqueo, desbloqueo y los tres reportes son ejercibles.
8. La UI cubre carga, vacío, desconexión y errores genéricos en móvil y escritorio.
9. No se muestran IDs ni se registran datos sensibles.
10. Push, correo, preferencias y persistencia completa de Notifications siguen
    diferidos.
