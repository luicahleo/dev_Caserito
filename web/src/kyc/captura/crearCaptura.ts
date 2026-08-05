import type { Rectangulo } from './geometriaCaptura';

const LIMITE_BYTES = 5 * 1024 * 1024;

function exportar(canvas: HTMLCanvasElement, calidad: number): Promise<Blob> {
  return new Promise((resolve, reject) => {
    canvas.toBlob(
      (blob) => (blob ? resolve(blob) : reject(new Error('No se pudo crear la captura'))),
      'image/jpeg',
      calidad,
    );
  });
}

export async function crearCaptura(
  video: HTMLVideoElement,
  recorte: Rectangulo,
  nombre: 'documento-ci.jpg' | 'selfie.jpg',
): Promise<File> {
  const canvas = document.createElement('canvas');
  const escala = Math.min(1, 1920 / Math.max(recorte.ancho, recorte.alto));
  canvas.width = Math.max(1, Math.round(recorte.ancho * escala));
  canvas.height = Math.max(1, Math.round(recorte.alto * escala));
  const contexto = canvas.getContext('2d');
  if (!contexto) throw new Error('No se pudo crear la captura');

  contexto.drawImage(
    video,
    recorte.x,
    recorte.y,
    recorte.ancho,
    recorte.alto,
    0,
    0,
    canvas.width,
    canvas.height,
  );
  let blob = await exportar(canvas, 0.88);

  if (blob.size > LIMITE_BYTES) {
    const reduccion = Math.min(1, 1600 / Math.max(canvas.width, canvas.height));
    const auxiliar = document.createElement('canvas');
    auxiliar.width = Math.max(1, Math.round(canvas.width * reduccion));
    auxiliar.height = Math.max(1, Math.round(canvas.height * reduccion));
    const contextoAuxiliar = auxiliar.getContext('2d');
    if (!contextoAuxiliar) throw new Error('No se pudo crear la captura');
    contextoAuxiliar.drawImage(canvas, 0, 0, auxiliar.width, auxiliar.height);
    blob = await exportar(auxiliar, 0.75);
  }

  if (blob.size > LIMITE_BYTES) throw new Error('La captura supera el tamaño permitido');
  return new File([blob], nombre, { type: 'image/jpeg' });
}
