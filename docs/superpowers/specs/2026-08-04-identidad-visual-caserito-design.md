# Identidad visual de Caserito

## Objetivo

Reemplazar los recursos visuales genéricos de la plantilla por una identidad propia que comunique intercambio, compra y venta directa entre personas y conserve legibilidad desde un favicon de 16 px hasta una imagen social.

## Dirección aprobada

El símbolo representa dos manos realizando un intercambio: una entrega un paquete y otra una moneda. El recorrido visual y el espacio negativo sugieren una «C» sin depender de texto. La geometría será compacta, plana y ligeramente artesanal, con verde pino y terracota de la interfaz actual.

## No objetivos

- No rediseñar pantallas, tipografías ni la paleta general.
- No crear una mascota ni usar carritos, flechas de reciclaje o folclore decorativo.
- No incorporar una biblioteca nueva de iconos.
- No incluir fotografías, PII ni texto pequeño dentro del símbolo.

## Sistema visual

- Símbolo maestro cuadrado, centrado y con margen seguro para recortes circulares.
- Silueta reconocible a 16, 32, 48, 192 y 512 px.
- Verde pino `#165C3B`, terracota `#D75A2D`, crema `#F8F6F1` y blanco.
- Sin degradados, brillos, sombras 3D, transparencias complejas ni detalles finos.
- La cabecera y el pie muestran el símbolo junto al nombre «Caserito»; los iconos de sistema usan solo el símbolo.

## Entregables

- Arte maestro del símbolo dentro de `web/public/brand/`.
- Favicon SVG, PNG de 32 px e ICO.
- Apple Touch Icon de 180 px.
- Iconos PWA de 192 y 512 px, más variantes `maskable`.
- Imagen Open Graph de 1200 × 630 px.
- Actualización de manifiesto, metadatos HTML, cabecera y pie.

## Accesibilidad y compatibilidad

- El logo enlazado conserva el nombre accesible «Caserito, inicio».
- Las imágenes decorativas no duplican texto para lectores de pantalla.
- Los recursos PWA declaran tamaños, tipos y propósito correctos.
- El favicon mantiene contraste en temas claros y oscuros del navegador.

## Pruebas y aceptación

- Todos los archivos referenciados existen y tienen las dimensiones declaradas.
- El manifiesto incluye iconos normales y `maskable`.
- `index.html` incluye favicon, Apple Touch Icon y metadatos Open Graph.
- La cabecera y el pie reutilizan el mismo componente de marca.
- Typecheck, lint, pruebas frontend y build pasan.
- Inspección visual confirma que el símbolo sigue siendo legible a 16 px y no parece un recurso de plantilla.
