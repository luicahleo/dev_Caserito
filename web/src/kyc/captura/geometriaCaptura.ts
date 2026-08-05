export interface Rectangulo {
  x: number;
  y: number;
  ancho: number;
  alto: number;
}

function dimensionesValidas(...valores: number[]): boolean {
  return valores.every((valor) => Number.isFinite(valor) && valor > 0);
}

export function calcularRecorteCover(
  video: { ancho: number; alto: number },
  visor: Rectangulo,
  guia: Rectangulo,
): Rectangulo {
  if (
    !dimensionesValidas(
      video.ancho,
      video.alto,
      visor.ancho,
      visor.alto,
      guia.ancho,
      guia.alto,
    )
  ) {
    throw new Error('Dimensiones de captura inválidas');
  }

  const escala = Math.max(visor.ancho / video.ancho, visor.alto / video.alto);
  const desplazamientoX = (video.ancho * escala - visor.ancho) / 2;
  const desplazamientoY = (video.alto * escala - visor.alto) / 2;
  const x = Math.max(0, (guia.x - visor.x + desplazamientoX) / escala);
  const y = Math.max(0, (guia.y - visor.y + desplazamientoY) / escala);
  const derecha = Math.min(video.ancho, (guia.x - visor.x + guia.ancho + desplazamientoX) / escala);
  const abajo = Math.min(video.alto, (guia.y - visor.y + guia.alto + desplazamientoY) / escala);

  return {
    x: Math.round(x),
    y: Math.round(y),
    ancho: Math.round(Math.max(1, derecha - x)),
    alto: Math.round(Math.max(1, abajo - y)),
  };
}
