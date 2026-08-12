# Carrusel de fotos en el detalle del aviso

## Objetivo

Permitir que quien consulta `/avisos/:id` recorra las fotos del aviso desde la
imagen ampliada, sin depender de seleccionar cada miniatura.

## No objetivos

- No abrir una vista a pantalla completa ni añadir zoom.
- No cambiar la carga, el almacenamiento ni el orden de las fotos.
- No incorporar reproducción automática, gestos táctiles ni una librería de
  carrusel.

## Diseño

La galería conservará la imagen ampliada y las miniaturas existentes. Cuando el
aviso tenga al menos dos fotos, mostrará dos botones superpuestos sobre los
laterales de la imagen ampliada:

- `Imagen anterior`, en el lateral izquierdo.
- `Imagen siguiente`, en el lateral derecho.

La navegación será circular: avanzar desde la última foto seleccionará la
primera y retroceder desde la primera seleccionará la última. La miniatura
resaltada y la imagen ampliada permanecerán sincronizadas. Con cero o una foto
no aparecerán flechas.

Los controles usarán `IconButton` e iconos de MUI, tendrán nombre accesible y
serán visibles sin depender de `hover`. Se colocarán dentro de un contenedor con
posición relativa para adaptarse al ancho disponible desde 320 px.

## Datos, seguridad y errores

El cambio usa únicamente el estado local efímero del índice seleccionado. No
modifica contratos, permisos, consultas ni persistencia. No se registrarán URLs,
imágenes ni otros datos del aviso.

## Pruebas

Las pruebas observables verificarán que:

- las flechas aparecen con varias fotos y cambian la imagen ampliada;
- la navegación siguiente y anterior es circular;
- no hay flechas con una sola foto;
- seleccionar una miniatura sigue actualizando la imagen ampliada.

## Criterios de aceptación

1. En `/avisos/:id`, un aviso con varias fotos permite avanzar y retroceder
   mediante flechas sobre la imagen ampliada.
2. La navegación envuelve en ambos extremos.
3. Las flechas no aparecen cuando no son necesarias.
4. Las miniaturas conservan su comportamiento y reflejan la selección actual.
5. Los controles tienen nombres accesibles en español y el frontend supera sus
   verificaciones aplicables y la puerta de calidad de cambios.
