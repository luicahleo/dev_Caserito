import { createBrowserRouter, Navigate } from 'react-router-dom';
import { LoginPage } from '../routes/LoginPage';
import { RegistroPage } from '../routes/RegistroPage';
import { PerfilPage } from '../routes/PerfilPage';
import { NotFoundPage } from '../routes/NotFoundPage';
import { KycPage } from '../routes/KycPage';
import { AdminKycPage } from '../routes/AdminKycPage';
import { ProtectedRoute } from '../auth/ProtectedRoute';
import { RequierePermiso } from '../auth/RequierePermiso';

export const router = createBrowserRouter([
  { path: '/', element: <Navigate to="/perfil" replace /> },
  { path: '/login', element: <LoginPage /> },
  { path: '/registro', element: <RegistroPage /> },
  {
    path: '/perfil',
    element: (
      <ProtectedRoute>
        <PerfilPage />
      </ProtectedRoute>
    ),
  },
  {
    path: '/kyc',
    element: (
      <ProtectedRoute>
        <KycPage />
      </ProtectedRoute>
    ),
  },
  {
    path: '/admin/kyc',
    element: (
      <ProtectedRoute>
        <RequierePermiso permiso="kyc.revisar">
          <AdminKycPage />
        </RequierePermiso>
      </ProtectedRoute>
    ),
  },
  { path: '*', element: <NotFoundPage /> },
]);
