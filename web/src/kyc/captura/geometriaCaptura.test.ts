import { describe, expect, it } from 'vitest';
import { calcularRecorteCover } from './geometriaCaptura';

describe('calcularRecorteCover', () => {
  it('traduce una guía cuando el vídeo se recorta horizontalmente', () => {
    expect(
      calcularRecorteCover(
        { ancho: 1920, alto: 1080 },
        { x: 0, y: 0, ancho: 360, alto: 640 },
        { x: 30, y: 220, ancho: 300, alto: 190 },
      ),
    ).toEqual({ x: 707, y: 371, ancho: 506, alto: 321 });
  });

  it('limita la guía a los bordes del frame', () => {
    expect(
      calcularRecorteCover(
        { ancho: 100, alto: 100 },
        { x: 10, y: 20, ancho: 100, alto: 100 },
        { x: 0, y: 0, ancho: 150, alto: 150 },
      ),
    ).toEqual({ x: 0, y: 0, ancho: 100, alto: 100 });
  });

  it('rechaza dimensiones inválidas', () => {
    expect(() =>
      calcularRecorteCover(
        { ancho: 0, alto: 100 },
        { x: 0, y: 0, ancho: 100, alto: 100 },
        { x: 0, y: 0, ancho: 50, alto: 50 },
      ),
    ).toThrow('Dimensiones de captura inválidas');
  });
});
