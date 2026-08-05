# Recuperación ante chunks obsoletos

## Objetivo

Evitar que una pestaña abierta durante un despliegue termine en la pantalla de
error predeterminada de React Router al solicitar un módulo dinámico de la
versión anterior.

## Alcance

- Conservar en cada imagen los assets de la versión inmediatamente anterior.
- Ante un error compatible con la carga de un módulo dinámico, recargar una sola
  vez la URL actual para obtener el frontend vigente.
- Si la recuperación falla o el error es distinto, mostrar una pantalla genérica
  en español con acciones para reintentar o volver al inicio.

## No objetivos

- Cambiar autenticación, cookies o el `401` esperado de `/api/auth/refresh`.
- Consultar o modificar datos de producción.
- Registrar detalles técnicos, URLs, tokens ni otros datos sensibles.

## Decisiones

La recuperación se implementa en el `errorElement` raíz de React Router. Se
reconocen únicamente los mensajes habituales de importación dinámica y carga de
chunks. Una clave temporal en `sessionStorage` limita la recarga automática a un
intento por ruta durante cinco minutos: evita bucles y permite recuperaciones
futuras sin depender de que el árbol anterior termine de renderizar.

Durante el despliegue se crea un contexto de construcción temporal. Allí se
copian, sin sobrescribir, los archivos de `wwwroot/assets` del release indicado
por `current-release`. Los releases permanecen inmutables, los archivos actuales
prevalecen y los hashes hacen segura la convivencia. Si no existe release
anterior, el despliegue continúa normalmente.

## UI, seguridad y accesibilidad

La pantalla usa componentes MUI, un encabezado claro, texto accionable y botones
accesibles. No presenta el objeto de error ni lo escribe en consola.

## Pruebas y aceptación

- Un error de importación dinámica provoca exactamente una recarga automática.
- El mismo error repetido muestra la pantalla de recuperación sin bucle.
- Un error no relacionado muestra directamente esa pantalla.
- El router raíz utiliza el `errorElement` propio.
- El despliegue mezcla los assets anteriores sin sobrescribir los nuevos.
- Typecheck, lint, tests, formato y build frontend quedan verdes.
