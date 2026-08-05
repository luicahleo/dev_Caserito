# Diseño — Captura guiada de CI y selfie en la aplicación móvil

**Fecha:** 2026-08-05
**Estado:** aprobado por directriz del usuario (aplicar las recomendaciones sin
preguntas adicionales)
**Contexto:** extiende el KYC manual existente sin cambiar su contrato HTTP ni su
revisión administrativa.

## 1. Objetivo

Reemplazar en `/kyc` la subida de imágenes desde archivos por una captura guiada
del frontal del CI y de una selfie. La cámara solo estará disponible cuando la
SPA se ejecute dentro de la aplicación instalada mediante Capacitor. En un
navegador, tanto móvil como de escritorio, el usuario no podrá capturar ni subir
imágenes y verá cómo continuar desde la aplicación móvil.

## 2. Alcance

Dentro de este bloque:

- distinguir de forma explícita la ejecución nativa de Capacitor del navegador;
- integrar la plataforma Android necesaria para ejecutar y verificar el flujo;
- solicitar acceso a la cámara únicamente tras una acción del usuario;
- capturar el frontal del CI con la cámara trasera y un marco rectangular;
- capturar la selfie con la cámara frontal y una guía ovalada;
- permitir revisar, repetir y confirmar cada captura;
- recortar la imagen al área visible de la guía del CI;
- convertir cada captura confirmada en un `File` en memoria y reutilizar
  `enviarKyc` y el endpoint multipart actuales;
- liberar cámara, object URLs, canvas y referencias al abandonar o finalizar;
- cubrir permisos, compatibilidad, errores y accesibilidad.

## 3. Fuera de alcance

- selección desde galería, explorador de archivos o drag-and-drop;
- captura desde navegador móvil o de escritorio;
- reverso del CI;
- OCR, lectura automática del número de CI o detección de bordes;
- reconocimiento/comparación facial, prueba de vida o decisión automática;
- publicación en Play Store o soporte iOS en este bloque;
- cambios al dominio, persistencia, revisión administrativa o contrato backend;
- conservación de capturas tras recargar o cerrar la vista.

La revisión KYC sigue siendo humana. La UI no afirmará que el sistema compara
rostros automáticamente.

## 4. Decisiones

### 4.1 Plataforma admitida

La capacidad se habilita solo cuando Capacitor informa una plataforma nativa y
la plataforma es Android. No se inferirá por ancho de pantalla, user-agent,
pantalla táctil ni presencia de webcam.

En navegador se conserva la consulta del estado KYC, pero para los estados
`NoIniciado` y `Rechazada` se sustituye el formulario por un aviso:

> Para proteger tu identidad, toma las fotografías desde la aplicación móvil de
> Caserito.

No se renderizará ningún `<input type="file">` como alternativa.

### 4.2 Acceso a cámara

La vista usará el flujo de vídeo del dispositivo dentro del WebView de
Capacitor. El permiso se solicitará después de pulsar una acción explícita, no al
cargar la página. Android declarará y gestionará el permiso de cámara requerido.

- CI: `facingMode: environment` como preferencia.
- Selfie: `facingMode: user` como preferencia.
- Audio: siempre desactivado.
- `playsInline`: obligatorio para mantener el flujo dentro de la vista.

La ausencia de la lente preferida no causa un fallo inmediato: se usa otra
cámara disponible y se informa al usuario cuando el encuadre recomendado no sea
posible.

### 4.3 Captura y recorte

Un componente de cámara reutilizable mostrará `<video>` y una superposición
visual. La guía no modifica el vídeo ni se incluye en la fotografía.

Al capturar:

1. se calcula la transformación real entre las dimensiones del vídeo y su
   representación con `object-fit: cover`;
2. para el CI se traduce el rectángulo visible de la guía a coordenadas del
   frame original;
3. se dibuja únicamente esa región en un canvas fuera de pantalla;
4. para la selfie se conserva un encuadre rectangular alrededor de la guía
   ovalada; no se recorta una imagen ovalada;
5. se exporta JPEG con calidad controlada y dimensiones máximas acotadas para
   permanecer bajo el límite de 5 MiB;
6. se crea un `File` con nombre técnico neutro, sin número de CI ni otros datos
   personales.

La orientación y el espejo de la previsualización de selfie se resolverán de
forma que el archivo enviado quede correctamente orientado y no dependa de
metadatos EXIF.

### 4.4 Flujo de usuario

Los datos textuales actuales (`número de CI`, complemento y departamento) se
mantienen. Las imágenes siguen esta secuencia:

1. instrucciones breves de iluminación, reflejos y visibilidad de bordes;
2. `Tomar foto del CI`;
3. permiso y visor con marco rectangular;
4. captura, vista previa y acciones `Repetir` / `Usar esta foto`;
5. `Tomar selfie`;
6. visor frontal con guía ovalada;
7. captura, vista previa y acciones `Repetir` / `Usar esta foto`;
8. resumen de ambas capturas sin nombres de archivos;
9. envío con prevención de doble acción;
10. limpieza inmediata de recursos tras éxito.

El usuario podrá volver atrás entre los dos pasos antes de enviar. Volver a
capturar reemplaza y libera la imagen anterior.

## 5. Estados y errores

El componente de captura tendrá estados explícitos:

- `inactivo`: todavía no se abrió la cámara;
- `solicitandoPermiso`;
- `vistaPreviaEnVivo`;
- `capturando`;
- `revisandoCaptura`;
- `error`;
- `noCompatible`.

Mensajes genéricos y accionables:

- permiso denegado: explicar cómo habilitarlo en ajustes y permitir reintento;
- cámara ocupada o no disponible: cerrar otras aplicaciones y reintentar;
- dispositivo sin cámara: indicar que no puede completarse desde ese equipo;
- contexto o API no compatible: indicar incompatibilidad sin detalles técnicos;
- captura fallida: permitir repetir;
- imagen demasiado grande: recomprimir una vez y, si persiste, pedir repetir;
- pérdida de visibilidad o salida de ruta: detener inmediatamente todos los
  `MediaStreamTrack`.

Los estados KYC `Pendiente` y `Aprobada` no inicializan ni solicitan la cámara.
Un rechazo permite repetir todo el flujo con capturas nuevas.

## 6. Arquitectura frontend

La ruta seguirá coordinando el estado remoto, los datos del formulario y la
mutación. La responsabilidad de cámara se separará por comportamiento:

- utilidad de plataforma: decide si la ejecución es Android nativa;
- hook de cámara: apertura, cambio de lente, cierre y errores normalizados;
- componente de captura guiada: vídeo, overlay, controles y accesibilidad;
- utilidad pura de geometría/canvas: coordenadas, recorte y conversión a `File`;
- orquestador KYC: secuencia CI → selfie y entrega los dos archivos confirmados.

No se añadirá una librería de UI, estado ni cámara salvo que una limitación
demostrada del WebView lo haga imprescindible. MUI seguirá siendo la base visual.

## 7. Seguridad y PII

- Nunca registrar imágenes, blobs, object URLs, `File`, streams, número de CI,
  complemento ni errores del dispositivo que incorporen datos sensibles.
- No usar `localStorage`, `sessionStorage`, IndexedDB, Cache API ni service worker
  para las capturas.
- No enviar nada al backend hasta que el usuario pulse `Enviar`.
- No incluir datos personales en nombres de archivo ni telemetría.
- Revocar object URLs y detener tracks al repetir, enviar, desmontar, cambiar de
  ruta o quedar la aplicación en segundo plano.
- Mantener la validación backend de tamaño, tipo y magic bytes como autoridad.
- Los errores visibles serán genéricos; los tests no usarán imágenes reales ni
  documentos identificables.

## 8. Accesibilidad y presentación

- Controles con nombre accesible y foco visible.
- Instrucciones textuales además del marco visual; no depender solo de color.
- Relación de aspecto estable para evitar saltos de layout.
- Soporte desde 320 px y ambas orientaciones, favoreciendo vertical.
- La acción de captura debe poder activarse mediante el control accesible del
  WebView; el vídeo tendrá descripción textual adecuada.
- Transiciones mínimas y respeto de `prefers-reduced-motion`.

## 9. Pruebas

### Unitarias

- detección de Android nativo frente a navegador, Android simulado e iOS;
- cálculo de recorte para distintas proporciones y `object-fit: cover`;
- normalización de errores de permisos/dispositivo;
- liberación idempotente de tracks y object URLs.

### Componentes

- navegador muestra el bloqueo y no contiene input de archivo ni abre cámara;
- Android nativo permite iniciar la captura tras acción explícita;
- permiso denegado, cámara ausente y reintento;
- secuencia CI → confirmar → selfie → confirmar;
- repetir reemplaza la captura y libera recursos;
- desmontar y segundo plano detienen la cámara;
- solo habilita `Enviar` con datos válidos y ambas capturas;
- conserva los estados `Pendiente`, `Aprobada`, `Rechazada` y errores 409/503.

### Integración manual en dispositivo Android

- permiso inicial, denegación y permiso previamente concedido;
- cámara trasera/frontal, orientación y vuelta desde segundo plano;
- encuadre y recorte del CI sin incluir el overlay;
- selfie enviada sin orientación o espejo incorrectos;
- envío real bajo el límite vigente y ausencia de acceso a galería;
- navegador de escritorio y navegador móvil bloqueados.

## 10. Criterios de aceptación

1. En cualquier navegador no existe forma de seleccionar, subir o capturar una
   imagen para KYC.
2. En la aplicación Android instalada se capturan CI y selfie mediante cámara
   en vivo con sus guías correspondientes.
3. Cada captura se puede revisar, repetir y confirmar antes del envío.
4. El CI enviado corresponde al área del marco y el overlay no forma parte de la
   imagen.
5. La API recibe los mismos campos multipart actuales y no requiere cambios.
6. La cámara y las URLs temporales se liberan en todos los caminos de salida.
7. No se persiste ni registra PII en el cliente.
8. Los estados y errores relevantes tienen UI y pruebas observables.
9. Typecheck, lint, tests, format y build del frontend terminan correctamente.

## 11. Trabajo diferido

- reverso del CI;
- indicadores automáticos de desenfoque, reflejo y bordes;
- OCR, comparación facial y liveness con evaluación legal específica;
- plataforma iOS y publicación en tiendas;
- analítica de fallos de cámara diseñada con categorías estrictamente sin PII.
