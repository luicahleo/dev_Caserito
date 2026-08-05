import { isValidElement, type ReactNode } from 'react';
import { describe, expect, it } from 'vitest';
import { ProtectedRoute } from '../auth/ProtectedRoute';
import { RequierePermiso } from '../auth/RequierePermiso';
import { router } from './router';
import { ErrorAplicacion } from './ErrorAplicacion';

describe('router', () => {
  it('usa una pantalla de error propia en la ruta raíz', () => {
    const errorElement = router.routes[0].errorElement;

    expect(isValidElement(errorElement)).toBe(true);
    expect(isValidElement(errorElement) ? errorElement.type : null).toBe(ErrorAplicacion);
  });

  it.each(['/privacidad', '/terminos', '/cookies', '/contacto', '/eliminacion-de-datos'])(
    'expone la página pública %s sin guard de sesión',
    (path) => {
      const ruta = router.routes[0].children?.find((candidata) => candidata.path === path);
      expect(ruta).toBeDefined();
      expect(isValidElement(ruta?.element)).toBe(true);
      expect(ruta?.element && isValidElement(ruta.element) ? ruta.element.type : null).not.toBe(
        ProtectedRoute,
      );
    },
  );

  it('expone el perfil público sin guard de sesión', () => {
    const ruta = router.routes[0].children?.find((candidata) => candidata.path === '/usuarios/:id');
    expect(ruta).toBeDefined();
    expect(isValidElement(ruta?.element)).toBe(true);
    expect(ruta?.element && isValidElement(ruta.element) ? ruta.element.type : null).not.toBe(
      ProtectedRoute,
    );
  });

  it('protege la moderación de chat con chat.moderar', () => {
    const ruta = router.routes[0].children?.find(
      (candidata) => candidata.path === '/admin/moderacion-chat',
    );
    expect(ruta).toBeDefined();
    expect(isValidElement(ruta?.element)).toBe(true);

    const protegida = ruta?.element;
    if (!isValidElement<{ children: ReactNode }>(protegida)) {
      throw new Error('La ruta administrativa debe tener un guard de sesión.');
    }
    expect(protegida.type).toBe(ProtectedRoute);

    const permiso = protegida.props.children;
    if (!isValidElement<{ permiso: string }>(permiso)) {
      throw new Error('La ruta administrativa debe tener un guard de permiso.');
    }
    expect(permiso.type).toBe(RequierePermiso);
    expect(permiso.props.permiso).toBe('chat.moderar');
  });
});
