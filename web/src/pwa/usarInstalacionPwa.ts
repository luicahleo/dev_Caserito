import { useEffect, useState } from 'react';

interface SolicitudInstalacionPwa extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

let solicitud: SolicitudInstalacionPwa | null = null;
const EVENTO_DISPONIBLE = 'caserito:pwa-instalable';

export function inicializarInstalacionPwa(): void {
  window.addEventListener('beforeinstallprompt', (evento) => {
    evento.preventDefault();
    solicitud = evento as SolicitudInstalacionPwa;
    window.dispatchEvent(new Event(EVENTO_DISPONIBLE));
  });
  window.addEventListener('appinstalled', () => {
    solicitud = null;
    window.dispatchEvent(new Event(EVENTO_DISPONIBLE));
  });
}

export function useInstalacionPwa() {
  const [puedeInstalar, setPuedeInstalar] = useState(solicitud !== null);

  useEffect(() => {
    const actualizar = () => setPuedeInstalar(solicitud !== null);
    window.addEventListener(EVENTO_DISPONIBLE, actualizar);
    actualizar();
    return () => window.removeEventListener(EVENTO_DISPONIBLE, actualizar);
  }, []);

  const instalar = async () => {
    const actual = solicitud;
    if (!actual) return;
    await actual.prompt();
    const eleccion = await actual.userChoice;
    if (eleccion.outcome === 'accepted') {
      solicitud = null;
      setPuedeInstalar(false);
    }
  };

  return { puedeInstalar, instalar };
}
