# Diseño responsive de Mis avisos

## Objetivo

Hacer que `/mis-avisos` sea usable sin desbordamiento horizontal desde 320 px
hasta escritorio, manteniendo clara la información y las acciones de cada
aviso.

## No objetivos

- No cambiar contratos, permisos, estados ni operaciones de los avisos.
- No rediseñar otras rutas en este cambio.
- No añadir una librería de tablas o layout.

## Diseño

La página usará dos representaciones del mismo listado mediante breakpoints de
MUI:

- En `xs`, cada aviso se mostrará como una tarjeta vertical con miniatura
  opcional, título, precio, chips de estado y acciones adaptables al ancho.
- Desde `sm`, se conservará la tabla, envuelta en `TableContainer` y `Paper`.

Solo una representación estará visible en cada breakpoint. Ambas consumirán el
mismo resultado remoto y ejecutarán las mismas funciones de editar, pausar,
reactivar y eliminar.

El encabezado se apilará en móvil y el botón `Publicar aviso` ocupará el ancho
disponible. La paginación se configurará con botones vecinos reducidos para
evitar desbordamiento en 320 px.

## Estados y accesibilidad

- Carga, error, vacío y confirmación conservarán sus mensajes actuales.
- Las miniaturas tendrán texto alternativo asociado al título; si no hay foto,
  se reservará una superficie estable con `Sin foto`.
- Los avisos vendidos no mostrarán acciones de mutación en ninguna variante.
- Los controles no dependerán de hover y conservarán nombres accesibles.
- La confirmación de eliminación seguirá siendo común a tarjetas y tabla.

## Seguridad y privacidad

No se añaden datos, telemetría ni registros. Las imágenes y los identificadores
se usan únicamente para renderizar o ejecutar los flujos existentes.

## Pruebas

- La estructura móvil contiene una tarjeta y la de escritorio una tabla, con
  visibilidad responsive declarada.
- Ambas variantes muestran título, precio y estado.
- Las acciones mantienen pausar y confirmar eliminación.
- Un aviso vendido no ofrece mutaciones.
- Un título largo y varios chips no fuerzan un ancho mínimo superior al
  viewport.

## Criterios de aceptación

1. `/mis-avisos` no provoca scroll horizontal de página desde 320 px.
2. En móvil se presentan tarjetas; en escritorio, tabla.
3. Encabezado, acciones, estados y paginación se adaptan al ancho disponible.
4. Todas las operaciones actuales conservan su comportamiento.
5. Tests, typecheck, lint, formato y puerta de calidad aplicables quedan verdes.
