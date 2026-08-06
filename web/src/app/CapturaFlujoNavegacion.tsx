import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import { registrarEventoFlujo, sanitizarRuta } from '../lib/sesionDiagnostico';

// Registra cada cambio de ruta en el buffer de flujo (solo con modo
// diagnóstico activo; registrarEventoFlujo ya filtra). No renderiza nada.
export function CapturaFlujoNavegacion() {
  const location = useLocation();

  useEffect(() => {
    registrarEventoFlujo({
      eventName: 'flow.navigation',
      detail: sanitizarRuta(location.pathname),
    });
  }, [location.pathname]);

  return null;
}
