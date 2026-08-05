import { describe, expect, it, vi } from 'vitest';
import { intentarRecuperarCarga } from './recuperacionCarga';

function crearAlmacenamiento() {
  const valores = new Map<string, string>();
  return {
    getItem: (clave: string) => valores.get(clave) ?? null,
    setItem: (clave: string, valor: string) => valores.set(clave, valor),
  };
}

describe('intentarRecuperarCarga', () => {
  it('recarga una vez ante un módulo dinámico obsoleto', () => {
    const almacenamiento = crearAlmacenamiento();
    const recargar = vi.fn();
    const error = new TypeError('Failed to fetch dynamically imported module: /assets/Perfil.js');

    expect(intentarRecuperarCarga(error, '/perfil', almacenamiento, recargar, () => 1_000)).toBe(
      true,
    );
    expect(recargar).toHaveBeenCalledOnce();

    expect(intentarRecuperarCarga(error, '/perfil', almacenamiento, recargar, () => 1_001)).toBe(
      false,
    );
    expect(recargar).toHaveBeenCalledOnce();
  });

  it('no recarga para un error de aplicación no relacionado', () => {
    const recargar = vi.fn();

    expect(
      intentarRecuperarCarga(
        new Error('Error de negocio'),
        '/perfil',
        crearAlmacenamiento(),
        recargar,
      ),
    ).toBe(false);
    expect(recargar).not.toHaveBeenCalled();
  });

  it('no recarga si no puede guardar la protección contra bucles', () => {
    const recargar = vi.fn();
    const almacenamiento = {
      getItem: () => null,
      setItem: () => {
        throw new Error('Almacenamiento deshabilitado');
      },
    };

    expect(
      intentarRecuperarCarga(
        new TypeError('Importing a module script failed.'),
        '/',
        almacenamiento,
        recargar,
      ),
    ).toBe(false);
    expect(recargar).not.toHaveBeenCalled();
  });
});
