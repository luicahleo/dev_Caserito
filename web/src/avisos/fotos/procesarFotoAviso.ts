export const LIMITE_ENTRADA_FOTO = 25 * 1024 * 1024;
export const LIMITE_SALIDA_FOTO = 1024 * 1024;

const LADO_MAXIMO = 1600;
const LADO_MINIMO_REDUCCION = 480;
const CALIDADES = [0.82, 0.74, 0.66, 0.58, 0.5, 0.42] as const;
const TIPOS_PERMITIDOS = new Set(['image/jpeg', 'image/png']);
const MENSAJE_ERROR = 'No se pudo preparar la foto.';

function exportar(canvas: HTMLCanvasElement, calidad: number): Promise<Blob> {
  return new Promise((resolve, reject) => {
    canvas.toBlob(
      (blob) => (blob ? resolve(blob) : reject(new Error(MENSAJE_ERROR))),
      'image/jpeg',
      calidad,
    );
  });
}

function dimensionesIniciales(ancho: number, alto: number) {
  const escala = Math.min(1, LADO_MAXIMO / Math.max(ancho, alto));
  return {
    ancho: Math.max(1, Math.round(ancho * escala)),
    alto: Math.max(1, Math.round(alto * escala)),
  };
}

/** Normaliza una foto de aviso antes de mostrarla o subirla. */
export async function procesarFotoAviso(archivo: File): Promise<File> {
  if (
    archivo.size === 0 ||
    archivo.size > LIMITE_ENTRADA_FOTO ||
    !TIPOS_PERMITIDOS.has(archivo.type)
  ) {
    throw new Error(MENSAJE_ERROR);
  }

  let imagen: ImageBitmap | undefined;
  try {
    imagen = await createImageBitmap(archivo, { imageOrientation: 'from-image' });
    let { ancho, alto } = dimensionesIniciales(imagen.width, imagen.height);
    const canvas = document.createElement('canvas');
    const contexto = canvas.getContext('2d', { alpha: false });
    if (!contexto) throw new Error(MENSAJE_ERROR);

    while (true) {
      canvas.width = ancho;
      canvas.height = alto;
      contexto.fillStyle = '#ffffff';
      contexto.fillRect(0, 0, ancho, alto);
      contexto.drawImage(imagen, 0, 0, ancho, alto);

      for (const calidad of CALIDADES) {
        const blob = await exportar(canvas, calidad);
        if (blob.size <= LIMITE_SALIDA_FOTO) {
          return new File([blob], 'foto-aviso.jpg', {
            type: 'image/jpeg',
            lastModified: Date.now(),
          });
        }
      }

      if (Math.max(ancho, alto) <= LADO_MINIMO_REDUCCION) throw new Error(MENSAJE_ERROR);
      const escala = Math.max(0.5, LADO_MINIMO_REDUCCION / Math.max(ancho, alto));
      ancho = Math.max(1, Math.round(ancho * escala));
      alto = Math.max(1, Math.round(alto * escala));
    }
  } catch {
    throw new Error(MENSAJE_ERROR);
  } finally {
    imagen?.close();
  }
}
