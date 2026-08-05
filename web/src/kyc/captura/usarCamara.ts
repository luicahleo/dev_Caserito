import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { ErrorCamara } from './erroresCamara';
import { normalizarErrorCamara } from './erroresCamara';

export type LenteCamara = 'trasera' | 'frontal';
export type EstadoCamara = 'inactiva' | 'solicitandoPermiso' | 'activa' | 'error';

interface DependenciasCamara {
  obtenerMedios?: (restricciones: MediaStreamConstraints) => Promise<MediaStream>;
}

function detener(stream: MediaStream | null): void {
  stream?.getTracks().forEach((track) => track.stop());
}

export function useCamara(dependencias: DependenciasCamara = {}) {
  const [estado, setEstado] = useState<EstadoCamara>('inactiva');
  const [stream, setStream] = useState<MediaStream | null>(null);
  const [error, setError] = useState<ErrorCamara | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const operacionRef = useRef(0);
  const obtenerMedios = useMemo(
    () =>
      dependencias.obtenerMedios ??
      ((restricciones: MediaStreamConstraints) =>
        navigator.mediaDevices.getUserMedia(restricciones)),
    [dependencias.obtenerMedios],
  );

  const cerrar = useCallback(() => {
    operacionRef.current += 1;
    detener(streamRef.current);
    streamRef.current = null;
    setStream(null);
    setEstado('inactiva');
  }, []);

  const abrir = useCallback(
    async (lente: LenteCamara) => {
      const operacion = ++operacionRef.current;
      detener(streamRef.current);
      streamRef.current = null;
      setStream(null);
      setError(null);
      setEstado('solicitandoPermiso');

      try {
        const restricciones: MediaStreamConstraints = {
          audio: false,
          video: {
            facingMode: { ideal: lente === 'trasera' ? 'environment' : 'user' },
            width: { ideal: 1920 },
            height: { ideal: 1080 },
          },
        };
        let nuevo: MediaStream;
        try {
          nuevo = await obtenerMedios(restricciones);
        } catch (fallo) {
          if (!(fallo instanceof DOMException) || fallo.name !== 'OverconstrainedError')
            throw fallo;
          nuevo = await obtenerMedios({ audio: false, video: true });
        }
        if (operacion !== operacionRef.current) {
          detener(nuevo);
          return;
        }
        streamRef.current = nuevo;
        setStream(nuevo);
        setEstado('activa');
      } catch (fallo) {
        if (operacion !== operacionRef.current) return;
        setError(normalizarErrorCamara(fallo));
        setEstado('error');
      }
    },
    [obtenerMedios],
  );

  useEffect(() => {
    const alCambiarVisibilidad = () => {
      if (document.visibilityState === 'hidden') cerrar();
    };
    document.addEventListener('visibilitychange', alCambiarVisibilidad);
    return () => {
      document.removeEventListener('visibilitychange', alCambiarVisibilidad);
      cerrar();
    };
  }, [cerrar]);

  return { estado, stream, error, abrir, cerrar };
}
