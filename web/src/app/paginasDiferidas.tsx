import { lazy } from 'react';

export const ExplorarPage = lazy(() =>
  import('../routes/ExplorarPage').then((modulo) => ({ default: modulo.ExplorarPage })),
);
export const DetalleAvisoPage = lazy(() =>
  import('../routes/DetalleAvisoPage').then((modulo) => ({ default: modulo.DetalleAvisoPage })),
);
export const CrearAvisoPage = lazy(() =>
  import('../routes/CrearAvisoPage').then((modulo) => ({ default: modulo.CrearAvisoPage })),
);
export const EditarAvisoPage = lazy(() =>
  import('../routes/EditarAvisoPage').then((modulo) => ({ default: modulo.EditarAvisoPage })),
);
export const MisAvisosPage = lazy(() =>
  import('../routes/MisAvisosPage').then((modulo) => ({ default: modulo.MisAvisosPage })),
);
export const LoginPage = lazy(() =>
  import('../routes/LoginPage').then((modulo) => ({ default: modulo.LoginPage })),
);
export const RegistroPage = lazy(() =>
  import('../routes/RegistroPage').then((modulo) => ({ default: modulo.RegistroPage })),
);
export const ConfirmarEmailPage = lazy(() =>
  import('../routes/ConfirmarEmailPage').then((modulo) => ({ default: modulo.ConfirmarEmailPage })),
);
export const PerfilPage = lazy(() =>
  import('../routes/PerfilPage').then((modulo) => ({ default: modulo.PerfilPage })),
);
export const NotFoundPage = lazy(() =>
  import('../routes/NotFoundPage').then((modulo) => ({ default: modulo.NotFoundPage })),
);
export const KycPage = lazy(() =>
  import('../routes/KycPage').then((modulo) => ({ default: modulo.KycPage })),
);
export const AdminKycPage = lazy(() =>
  import('../routes/AdminKycPage').then((modulo) => ({ default: modulo.AdminKycPage })),
);
export const AdminModeracionPage = lazy(() =>
  import('../routes/AdminModeracionPage').then((modulo) => ({
    default: modulo.AdminModeracionPage,
  })),
);
export const AdminModeracionChatPage = lazy(() =>
  import('../routes/AdminModeracionChatPage').then((modulo) => ({
    default: modulo.AdminModeracionChatPage,
  })),
);
export const ConversacionesPage = lazy(() =>
  import('../routes/ConversacionesPage').then((modulo) => ({ default: modulo.ConversacionesPage })),
);
export const ConversacionPage = lazy(() =>
  import('../routes/ConversacionPage').then((modulo) => ({ default: modulo.ConversacionPage })),
);
export const MisAcuerdosPage = lazy(() =>
  import('../routes/MisAcuerdosPage').then((modulo) => ({ default: modulo.MisAcuerdosPage })),
);
export const DetalleAcuerdoPage = lazy(() =>
  import('../routes/DetalleAcuerdoPage').then((modulo) => ({ default: modulo.DetalleAcuerdoPage })),
);
export const PerfilPublicoPage = lazy(() =>
  import('../routes/PerfilPublicoPage').then((modulo) => ({ default: modulo.PerfilPublicoPage })),
);
export const NotificacionesPage = lazy(() =>
  import('../routes/NotificacionesPage').then((modulo) => ({ default: modulo.NotificacionesPage })),
);
export const BusquedasGuardadasPage = lazy(() =>
  import('../routes/BusquedasGuardadasPage').then((modulo) => ({
    default: modulo.BusquedasGuardadasPage,
  })),
);
export const OlvidePasswordPage = lazy(() =>
  import('../routes/OlvidePasswordPage').then((modulo) => ({ default: modulo.OlvidePasswordPage })),
);
export const RestablecerPasswordPage = lazy(() =>
  import('../routes/RestablecerPasswordPage').then((modulo) => ({
    default: modulo.RestablecerPasswordPage,
  })),
);
export const AuthExternaCallbackPage = lazy(() =>
  import('../routes/AuthExternaCallbackPage').then((modulo) => ({
    default: modulo.AuthExternaCallbackPage,
  })),
);
export const AuthExternaSimuladorPage = lazy(() =>
  import('../routes/AuthExternaSimuladorPage').then((modulo) => ({
    default: modulo.AuthExternaSimuladorPage,
  })),
);
export const CompletarRegistroExternoPage = lazy(() =>
  import('../routes/CompletarRegistroExternoPage').then((modulo) => ({
    default: modulo.CompletarRegistroExternoPage,
  })),
);

const cargarPaginasLegales = () => import('../routes/LegalPages');
export const ContactoPage = lazy(() =>
  cargarPaginasLegales().then((modulo) => ({ default: modulo.ContactoPage })),
);
export const CookiesPage = lazy(() =>
  cargarPaginasLegales().then((modulo) => ({ default: modulo.CookiesPage })),
);
export const EliminacionDatosPage = lazy(() =>
  cargarPaginasLegales().then((modulo) => ({ default: modulo.EliminacionDatosPage })),
);
export const PrivacidadPage = lazy(() =>
  cargarPaginasLegales().then((modulo) => ({ default: modulo.PrivacidadPage })),
);
export const TerminosPage = lazy(() =>
  cargarPaginasLegales().then((modulo) => ({ default: modulo.TerminosPage })),
);
