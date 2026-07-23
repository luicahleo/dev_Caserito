import { isValidElement, type ReactNode } from 'react';
import { describe, expect, it } from 'vitest';
import { ProtectedRoute } from '../auth/ProtectedRoute';
import { RequierePermiso } from '../auth/RequierePermiso';
import { router } from './router';

describe('router', () => {
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
