# Procesamiento automático de fotos de avisos

## Objetivo

Permitir que cámara y galería acepten fotos habituales de teléfonos sin exigir
compresión manual. Antes del preview y de la subida, el navegador normaliza las
imágenes. El backend repite la normalización defensivamente y almacena solamente
el resultado final.

## No objetivos

- No aumentar el máximo de cinco fotos por aviso.
- No conservar originales, EXIF, GPS ni nombres aportados por el usuario.
- No crear todavía miniaturas o variantes para listados.
- No cambiar la autorización ni hacer públicas fotos de avisos no visibles.

## Política de imagen

- Entrada absoluta: JPEG o PNG de hasta 25 MiB por archivo.
- Salida: JPEG, proporción conservada y lado mayor máximo de 1600 px.
- Transparencia: composición sobre fondo blanco antes de codificar JPEG.
- Calidad inicial: 82 %.
- Objetivo: máximo 1 MiB. Se reduce primero la calidad y después las dimensiones
  hasta alcanzar el objetivo, sin superar nunca 1600 px.
- Orientación: se respeta la orientación declarada por la imagen antes de
  escalar; la salida queda físicamente orientada y sin esa metadata.
- Un archivo que no pueda decodificarse, exceda el límite absoluto o no sea
  JPEG/PNG produce un error genérico y no se almacena.

## Flujo frontend

`FormAviso` envía los archivos elegidos por cámara y galería a una utilidad del
dominio de avisos. Se procesan secuencialmente para acotar memoria. Durante el
trabajo se muestra «Preparando fotos…» y se deshabilitan selección y envío.

El preview y la subida usan únicamente los `File` JPEG normalizados. En una
selección múltiple se conservan las fotos válidas; se informa de forma genérica
si una o más no pudieron prepararse. Los object URLs se revocan al quitar una
foto o desmontar el formulario.

## Flujo backend

El endpoint rechaza antes de copiar archivos vacíos o superiores a 25 MiB. La
aplicación verifica MIME y magic bytes, invoca un puerto de normalización y solo
entrega al almacén el JPEG normalizado de hasta 1 MiB. Infrastructure implementa
la decodificación, orientación, escalado, fondo blanco, codificación y eliminación
de metadata. El navegador nunca es una frontera de confianza.

## Persistencia y contenedores

La relación se mantiene en SQL mediante `FotoAviso` (`Id`, `AvisoId`, `Clave`,
`ContentType`, `Orden`). El usuario se obtiene a través del aviso; no se incorpora
PII ni IDs de usuario a rutas de disco.

Los blobs usan claves aleatorias opacas. Los nuevos archivos se escriben como
JPEG de forma atómica y se distribuyen en directorios derivados del prefijo de
la clave para evitar un único directorio creciente. La lectura y el borrado
mantienen compatibilidad con los actuales `{clave}.bin` y `{clave}.meta`.

Producción conserva el bind mount existente:

`/var/apps/caseritoapp/fotos-avisos:/data/fotos-avisos`

Desarrollo añade un volumen nombrado para `/data/fotos-avisos`, evitando perder
fotos al recrear el contenedor de API.

## Seguridad y errores

- No registrar bytes, metadata, nombres originales, rutas aportadas, claves de
  blobs, títulos, descripciones ni otros contenidos privados.
- Los errores visibles y de API son genéricos.
- No persistir el archivo original ni temporales después de la operación.
- La escritura final es atómica y la limpieza ante fallos es best-effort.

## Pruebas

- Frontend unitario: JPEG, PNG, transparencia, orientación, imagen grande,
  reducción de peso, formato inválido, error de decodificación y varias fotos.
- Formulario: cámara y galería pasan por preparación, muestra del estado,
  errores parciales, máximo cinco y bloqueo durante el proceso.
- Backend unitario: validación absoluta, normalización JPEG/PNG, orientación,
  transparencia, dimensiones, peso, metadata eliminada y errores.
- Almacenamiento: patrón opaco/particionado, escritura, lectura, borrado y
  compatibilidad con blobs antiguos.
- Integración: subida almacena y sirve el JPEG normalizado.

## Criterios de aceptación

1. Una foto habitual de móvil se prepara y sube sin compresión manual.
2. Toda foto nueva almacenada es JPEG, mide como máximo 1600 px por lado y pesa
   como máximo 1 MiB.
3. La salida no contiene EXIF/GPS y la transparencia se ve sobre blanco.
4. Cámara y galería tienen el mismo comportamiento y feedback visible.
5. Siguen admitiéndose como máximo cinco fotos.
6. Las fotos sobreviven a la recreación de contenedores en desarrollo y VPS.

## Trabajo diferido

Miniaturas de 400–480 px y variantes responsive quedan para una segunda fase:
requieren contrato de variantes, estrategia de regeneración, almacenamiento y
selección de URL en listados.
