import { createBrowserRouter, Navigate } from 'react-router-dom';
import { LoginPage } from '../routes/LoginPage';
import { RegistroPage } from '../routes/RegistroPage';
import { PerfilPage } from '../routes/PerfilPage';
import { NotFoundPage } from '../routes/NotFoundPage';
import { ProtectedRoute } from '../auth/ProtectedRoute';

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
  { path: '*', element: <NotFoundPage /> },
]);
