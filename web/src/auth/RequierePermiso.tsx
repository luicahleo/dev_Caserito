import { type ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from './AuthContext';

// Guard de ruta por permiso. Debe usarse dentro de ProtectedRoute (sesión ya garantizada).
export function RequierePermiso({ permiso, children }: { permiso: string; children: ReactNode }) {
  const { tienePermiso } = useAuth();
  if (!tienePermiso(permiso)) {
    return <Navigate to="/perfil" replace />;
  }
  return <>{children}</>;
}
