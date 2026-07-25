import { createBrowserRouter } from 'react-router-dom';
import { AppLayout } from './AppLayout';
import { ExplorarPage } from '../routes/ExplorarPage';
import { DetalleAvisoPage } from '../routes/DetalleAvisoPage';
import { CrearAvisoPage } from '../routes/CrearAvisoPage';
import { EditarAvisoPage } from '../routes/EditarAvisoPage';
import { MisAvisosPage } from '../routes/MisAvisosPage';
import { LoginPage } from '../routes/LoginPage';
import { RegistroPage } from '../routes/RegistroPage';
import { PerfilPage } from '../routes/PerfilPage';
import { NotFoundPage } from '../routes/NotFoundPage';
import { KycPage } from '../routes/KycPage';
import { AdminKycPage } from '../routes/AdminKycPage';
import { AdminModeracionPage } from '../routes/AdminModeracionPage';
import { AdminModeracionChatPage } from '../routes/AdminModeracionChatPage';
import { ProtectedRoute } from '../auth/ProtectedRoute';
import { RequierePermiso } from '../auth/RequierePermiso';
import { ConversacionesPage } from '../routes/ConversacionesPage';
import { ConversacionPage } from '../routes/ConversacionPage';
import { MisAcuerdosPage } from '../routes/MisAcuerdosPage';
import { DetalleAcuerdoPage } from '../routes/DetalleAcuerdoPage';

export const router = createBrowserRouter([
  {
    element: <AppLayout />,
    children: [
      { path: '/', element: <ExplorarPage /> },
      { path: '/avisos/:id', element: <DetalleAvisoPage /> },
      { path: '/login', element: <LoginPage /> },
      { path: '/registro', element: <RegistroPage /> },
      {
        path: '/mensajes',
        element: <ProtectedRoute><ConversacionesPage /></ProtectedRoute>,
      },
      {
        path: '/mensajes/:id',
        element: <ProtectedRoute><ConversacionPage /></ProtectedRoute>,
      },
      {
        path: '/acuerdos',
        element: <ProtectedRoute><MisAcuerdosPage /></ProtectedRoute>,
      },
      {
        path: '/acuerdos/:id',
        element: <ProtectedRoute><DetalleAcuerdoPage /></ProtectedRoute>,
      },
      {
        path: '/publicar',
        element: (
          <ProtectedRoute>
            <CrearAvisoPage />
          </ProtectedRoute>
        ),
      },
      {
        path: '/mis-avisos',
        element: (
          <ProtectedRoute>
            <MisAvisosPage />
          </ProtectedRoute>
        ),
      },
      {
        path: '/mis-avisos/:id/editar',
        element: (
          <ProtectedRoute>
            <EditarAvisoPage />
          </ProtectedRoute>
        ),
      },
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
        path: '/admin/moderacion',
        element: (
          <ProtectedRoute>
            <RequierePermiso permiso="publicaciones.moderar">
              <AdminModeracionPage />
            </RequierePermiso>
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
      {
        path: '/admin/moderacion-chat',
        element: (
          <ProtectedRoute>
            <RequierePermiso permiso="chat.moderar">
              <AdminModeracionChatPage />
            </RequierePermiso>
          </ProtectedRoute>
        ),
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
