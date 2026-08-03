import { createBrowserRouter } from 'react-router-dom';
import { AppLayout } from './AppLayout';
import { ProtectedRoute } from '../auth/ProtectedRoute';
import { RequierePermiso } from '../auth/RequierePermiso';
import {
  AdminKycPage,
  AdminModeracionChatPage,
  AdminModeracionPage,
  AuthExternaCallbackPage,
  BusquedasGuardadasPage,
  CompletarRegistroExternoPage,
  ConfirmarEmailPage,
  ContactoPage,
  ConversacionPage,
  ConversacionesPage,
  CookiesPage,
  CrearAvisoPage,
  DetalleAcuerdoPage,
  DetalleAvisoPage,
  EditarAvisoPage,
  EliminacionDatosPage,
  ExplorarPage,
  KycPage,
  LoginPage,
  MisAcuerdosPage,
  MisAvisosPage,
  NotFoundPage,
  NotificacionesPage,
  OlvidePasswordPage,
  PerfilPage,
  PerfilPublicoPage,
  PrivacidadPage,
  RegistroPage,
  RestablecerPasswordPage,
  TerminosPage,
} from './paginasDiferidas';

export const router = createBrowserRouter([
  {
    element: <AppLayout />,
    children: [
      { path: '/', element: <ExplorarPage /> },
      { path: '/avisos/:id', element: <DetalleAvisoPage /> },
      { path: '/usuarios/:id', element: <PerfilPublicoPage /> },
      { path: '/login', element: <LoginPage /> },
      { path: '/auth/external/completado', element: <AuthExternaCallbackPage /> },
      { path: '/auth/external/onboarding', element: <CompletarRegistroExternoPage /> },
      { path: '/registro', element: <RegistroPage /> },
      { path: '/olvide-password', element: <OlvidePasswordPage /> },
      { path: '/restablecer-password', element: <RestablecerPasswordPage /> },
      { path: '/confirmar-email', element: <ConfirmarEmailPage /> },
      { path: '/privacidad', element: <PrivacidadPage /> },
      { path: '/terminos', element: <TerminosPage /> },
      { path: '/cookies', element: <CookiesPage /> },
      { path: '/contacto', element: <ContactoPage /> },
      { path: '/eliminacion-de-datos', element: <EliminacionDatosPage /> },
      {
        path: '/mensajes',
        element: (
          <ProtectedRoute>
            <ConversacionesPage />
          </ProtectedRoute>
        ),
      },
      {
        path: '/mensajes/:id',
        element: (
          <ProtectedRoute>
            <ConversacionPage />
          </ProtectedRoute>
        ),
      },
      {
        path: '/acuerdos',
        element: (
          <ProtectedRoute>
            <MisAcuerdosPage />
          </ProtectedRoute>
        ),
      },
      {
        path: '/acuerdos/:id',
        element: (
          <ProtectedRoute>
            <DetalleAcuerdoPage />
          </ProtectedRoute>
        ),
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
        path: '/notificaciones',
        element: (
          <ProtectedRoute>
            <NotificacionesPage />
          </ProtectedRoute>
        ),
      },
      {
        path: '/busquedas-guardadas',
        element: (
          <ProtectedRoute>
            <BusquedasGuardadasPage />
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
