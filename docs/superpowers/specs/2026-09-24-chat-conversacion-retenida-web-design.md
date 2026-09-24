# Diseño — UI web de la conversación retenida

Fecha: 2026-09-24
Serie: «verificación al contactar»
Depende de: `docs/superpowers/specs/2026-09-21-chat-verificacion-contacto-design.md`

## 1. Contexto y objetivo

El bloque anterior implementó la retención en el backend: la conversación de un
comprador sin identidad habilitada nace `RetenidaPorVerificacion`, admite sus
mensajes y permanece invisible para el vendedor hasta que el comprador se
verifica. La web todavía no sabe nada de ese estado: `ConversacionPage` y
`ConversacionesPage` comparan `conversacion.estado` contra los literales `1` y
`2`, de modo que el valor `3` cae en la rama por defecto y se presenta como una
conversación activa cualquiera.

El objetivo de este bloque es cerrar esa brecha: que el comprador entienda que
su mensaje está guardado pero no entregado, sepa qué hacer para que llegue, y
tenga una salida explícita cuando ya se ha verificado.

### Serie completa (contexto, no alcance)

1. Resolución automática de KYC — cerrado.
2. Conversación retenida (backend) — cerrado.
3. Aviso al administrador de solicitudes en espera — pendiente.
4. Distintivo de verificado en listado, detalle y chat — pendiente.

Este spec es la UI web del bloque 2, diferida explícitamente en §8 de su spec.

## 2. No objetivos

- El distintivo de verificado (bloque 4).
- El aviso al administrador (bloque 3).
- Cualquier gate en `DetalleAvisoPage`: el botón «Contactar al vendedor» sigue
  sin condición. La decisión del backend fue retener, no rechazar, y avisar
  antes obligaría a consultar el estado real de verificación desde la página
  pública del aviso.
- Cualquier cambio de backend, de contrato OpenAPI o del cliente generado.
- Superficie de vendedor: no existe, porque el servidor nunca le entrega una
  conversación retenida.

## 3. Decisiones

1. **El aviso vive dentro de la conversación, no antes.** Una sola superficie
   que mantener y ningún riesgo de mensajes contradictorios entre pantallas.
2. **Los valores del enum se centralizan en un módulo con test de contrato.**
   `EstadoConversacion` viaja como número (§11.2 del spec anterior) y hoy los
   literales están repartidos. Un módulo único con un test que fija los cuatro
   valores convierte un reorden del enum en un fallo visible. Se descartó
   serializar el enum como cadena: arregla la causa, pero cambia el contrato y
   obliga a revisar todos los consumidores, lo que es alcance de backend dentro
   de un bloque de UI.
3. **El comprador puede seguir escribiendo.** Coherente con «retiene, no
   rechaza»: los mensajes se acumulan y el vendedor recibe el hilo completo al
   liberarse.
4. **Un botón dispara la red de seguridad perezosa.** La liberación depende del
   evento `UserVerified`; la red perezosa solo actúa en
   `IniciarConversacionCommand`. El botón «Ya verifiqué mi identidad» llama a
   `iniciarConversacion(avisoId)`, que reutiliza la conversación existente y la
   libera si corresponde. Da al usuario una salida para el hueco que el spec
   anterior documentó, sin backend nuevo.
5. **El indicador de entrega por mensaje no cambia.** En una conversación
   retenida `obtenerEstadoMensaje` devuelve siempre `'enviado'`, lo cual es
   cierto: el mensaje está guardado. El aviso persistente explica por qué no
   avanza. Añadir un cuarto estado obligaría a pasar el estado de la
   conversación a un helper que hoy solo conoce secuencias.

## 4. Comportamiento

### 4.1 Conversación retenida, vista del comprador

Cuando `conversacion.estado === EstadoConversacion.RetenidaPorVerificacion`:

- Se monta un `Alert severity="info"` persistente sobre el hilo, con la
  explicación y dos acciones:
  - **«Verificar mi identidad»** — enlace a `/kyc`.
  - **«Ya verifiqué mi identidad»** — reintento (§4.2).
- El compositor permanece habilitado; `puedeEnviar` es `true`.
- Los mensajes conservan su check «Enviado».
- El `Alert` genérico «Esta conversación no admite nuevos mensajes»
  (`ConversacionPage.tsx:262`) no aparece, porque depende de `!puedeEnviar`.

### 4.2 Reintento de liberación

`iniciarConversacion(conversacion.avisoId)` y, según la respuesta:

| Respuesta | Efecto |
|---|---|
| `estado` distinto de `3` | El componente invoca `alLiberar()`; `ConversacionPage` invalida `['chat-conversacion', id]` y `['chat-bandeja']`, y el aviso desaparece sin recargar la página |
| `estado === 3` | El aviso permanece y se muestra un texto secundario: la verificación aún no está aprobada. No es un error y no se presenta como tal |
| Error de red o HTTP | `Alert severity="error"` genérico, sin detalle técnico |

### 4.3 Listado de conversaciones

El chip de estado muestra **«En espera de verificación»** para el valor `3`. No
cambian el orden, el contador de no leídos ni la navegación.

### 4.4 Otros estados

`Activa`, `Cerrada` y `CerradaPorModeracion` se comportan exactamente como hoy.
La migración de los literales a constantes es sustitución de valor idéntico.

## 5. Componentes

### `web/src/chat/estadoConversacion.ts` (nuevo)

- `EstadoConversacion`: objeto constante con `Activa: 0`, `Cerrada: 1`,
  `CerradaPorModeracion: 2`, `RetenidaPorVerificacion: 3`. Refleja
  `CaseritoApp.Chat.Domain.Conversaciones.EstadoConversacion`, que EF y el
  serializador exponen como `int`.
- `esRetenida(estado: number): boolean`.
- `etiquetaEstado(conversacion)`: absorbe la función homónima de
  `ConversacionesPage.tsx:20` y añade la etiqueta del estado retenido.

### `web/src/chat/AvisoConversacionRetenida.tsx` (nuevo)

Presentación y mutación del aviso. Props: `avisoId: string` y
`alLiberar: () => void`. El componente es dueño de la llamada a
`iniciarConversacion` y de sus tres desenlaces (§4.2); **no** conoce el
`QueryClient`. La invalidación es responsabilidad de `ConversacionPage`, que
implementa `alLiberar`. Esa frontera mantiene el componente testeable sin montar
un `QueryClientProvider`.

### `web/src/routes/ConversacionPage.tsx`

Monta el aviso cuando la conversación está retenida, le pasa
`conversacion.avisoId` y un `alLiberar` que invalida `['chat-conversacion', id]`
y `['chat-bandeja']`, y migra los literales de las líneas 280-284 a las
constantes del módulo.

### `web/src/routes/ConversacionesPage.tsx`

Importa `etiquetaEstado` y elimina la copia local.

## 6. Contrato

Sin cambios. No se regenera el cliente OpenAPI: el estado ya viaja en
`ConversacionDto` y `ConversacionResumenDto`, y su tipo sigue siendo `number`.

## 7. Seguridad y PII

- El aviso no menciona al vendedor, ni el aviso comercial, ni dato alguno de la
  solicitud de KYC. El motivo se expresa sobre la propia identidad del comprador.
- No se registra nada en consola.
- Los tests no asertan sobre texto de mensajes ni ids técnicos, en línea con
  `ConversacionesPage.test.tsx:23`.

## 8. Pruebas

### Tests existentes afectados

Inventario previo, para no repetir la sorpresa del bloque 2:

- `ConversacionesPage.test.tsx:23` y `:64` — fixture con `estado: 0` y
  `puedeEnviar: true`; la etiqueta sigue siendo «Activa». No se rompe.
- `ConversacionPage.test.tsx:32` — fixture con `estado: 0` y
  `puedeEnviar: false`; sigue mostrando el `Alert` genérico con el compositor
  deshabilitado, y el aviso de retención no se monta. No se rompe.

Ningún test existente depende de la regla que cambia.

### `estadoConversacion.test.ts` (nuevo)

- Los cuatro valores, escritos explícitamente, con comentario que remite a
  `EstadoConversacion.cs`.
- `esRetenida` distingue `3` de los otros tres valores.
- `etiquetaEstado` cubre las cuatro etiquetas y la rama `!puedeEnviar`.

### `AvisoConversacionRetenida.test.tsx` (nuevo)

1. Renderiza la explicación y el CTA apuntando a `/kyc`.
2. El reintento llama a `iniciarConversacion` con el `avisoId`; si vuelve
   `estado: 0`, invoca `alLiberar`.
3. Si vuelve `estado: 3`, no invoca `alLiberar` y muestra el texto de
   verificación aún no aprobada.
4. Si la llamada rechaza, muestra el error genérico y no invoca `alLiberar`.

### `ConversacionPage.test.tsx` (caso nuevo)

Con `estado: 3` y `puedeEnviar: true`: aparece el aviso y el compositor sigue
habilitado.

### `ConversacionesPage.test.tsx` (caso nuevo)

Con `estado: 3`: el chip muestra «En espera de verificación».

## 9. Criterios de aceptación

1. Un comprador con conversación retenida ve el aviso, con CTA a `/kyc`, y
   puede seguir escribiendo.
2. Sus mensajes se muestran con el check «Enviado».
3. El listado muestra «En espera de verificación» para esa conversación.
4. Tras verificarse, «Ya verifiqué mi identidad» libera la conversación y el
   aviso desaparece sin recargar la página.
5. Sin verificarse, ese mismo botón deja la conversación retenida y lo informa
   sin tratarlo como error.
6. Una conversación activa, cerrada o cerrada por moderación se comporta
   exactamente como hoy.
7. `estadoConversacion.test.ts` falla si alguien reordena el enum del backend.

## 10. Riesgos y diferidos

- **El test de contrato es una réplica, no un vínculo.** Detecta un reorden del
  enum solo cuando se ejecute la suite web. Es lo máximo alcanzable sin
  serializar el enum como cadena.
- **El reintento consume el rate limit `chat-iniciar`.** Es una acción manual y
  puntual, no un sondeo; el agotamiento se presenta con el error genérico.
- **La liberación por evento no se refleja sola** si el comprador ya tiene la
  página abierta: no hay notificación hacia él en ese momento. El botón es la
  salida explícita; un refresco automático queda fuera de alcance.
- **Copy sin revisión de producto.** Los textos son propuesta técnica.
