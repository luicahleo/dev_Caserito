import { useEffect, useRef, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
} from '@mui/material';
import { calcularRecorteCover } from './geometriaCaptura';
import { crearCaptura } from './crearCaptura';
import { useCamara } from './usarCamara';
import type { TipoErrorCamara } from './erroresCamara';

interface CapturadorGuiadoProps {
  tipo: 'documento' | 'selfie';
  onConfirmar(archivo: File): void;
  onCancelar(): void;
}

const MENSAJES_ERROR: Record<TipoErrorCamara, string> = {
  permisoDenegado: 'No se concedió acceso a la cámara. Revisa el permiso e inténtalo de nuevo.',
  solicitarEnAjustes: 'Habilita el permiso de cámara en los ajustes de Caserito.',
  sinCamara: 'No encontramos una cámara disponible en este dispositivo.',
  noDisponible:
    'La cámara está ocupada o no está disponible. Cierra otras aplicaciones e inténtalo de nuevo.',
  desconocido: 'No se pudo abrir la cámara. Inténtalo de nuevo.',
};

export function CapturadorGuiado({ tipo, onConfirmar, onCancelar }: CapturadorGuiadoProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const visorRef = useRef<HTMLDivElement>(null);
  const guiaRef = useRef<HTMLDivElement>(null);
  const { estado, stream, error, abrir, cerrar } = useCamara();
  const [captura, setCaptura] = useState<File | null>(null);
  const [urlCaptura, setUrlCaptura] = useState<string | null>(null);
  const [capturando, setCapturando] = useState(false);
  const [errorCaptura, setErrorCaptura] = useState(false);

  useEffect(() => {
    const video = videoRef.current;
    if (!video || !stream) return;
    video.srcObject = stream;
    void video.play();
  }, [stream]);

  useEffect(() => {
    return () => {
      if (urlCaptura) URL.revokeObjectURL(urlCaptura);
    };
  }, [urlCaptura]);

  const tomarFoto = async () => {
    const video = videoRef.current;
    const visor = visorRef.current;
    const guia = guiaRef.current;
    if (!video || !visor || !guia || video.videoWidth === 0 || video.videoHeight === 0) return;
    setCapturando(true);
    setErrorCaptura(false);
    try {
      const visorRect = visor.getBoundingClientRect();
      const guiaRect = guia.getBoundingClientRect();
      const recorte = calcularRecorteCover(
        { ancho: video.videoWidth, alto: video.videoHeight },
        { x: visorRect.x, y: visorRect.y, ancho: visorRect.width, alto: visorRect.height },
        { x: guiaRect.x, y: guiaRect.y, ancho: guiaRect.width, alto: guiaRect.height },
      );
      const archivo = await crearCaptura(
        video,
        recorte,
        tipo === 'documento' ? 'documento-ci.jpg' : 'selfie.jpg',
      );
      setCaptura(archivo);
      setUrlCaptura(URL.createObjectURL(archivo));
      cerrar();
    } catch {
      setErrorCaptura(true);
    } finally {
      setCapturando(false);
    }
  };

  const repetir = () => {
    if (urlCaptura) URL.revokeObjectURL(urlCaptura);
    setUrlCaptura(null);
    setCaptura(null);
    void abrir(tipo === 'documento' ? 'trasera' : 'frontal');
  };

  const cancelar = () => {
    cerrar();
    onCancelar();
  };

  return (
    <Dialog open fullScreen onClose={cancelar} aria-labelledby="titulo-captura">
      <DialogTitle id="titulo-captura">
        {tipo === 'documento' ? 'Fotografía del CI' : 'Selfie de verificación'}
      </DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ height: '100%' }}>
          <Typography variant="body2">
            {tipo === 'documento'
              ? 'Coloca el frontal completo del CI dentro del marco, sin reflejos.'
              : 'Coloca tu rostro dentro de la guía y mira de frente.'}
          </Typography>

          {!stream && !captura && estado === 'inactiva' && (
            <Button
              variant="contained"
              onClick={() => void abrir(tipo === 'documento' ? 'trasera' : 'frontal')}
            >
              Abrir cámara
            </Button>
          )}

          {estado === 'solicitandoPermiso' && (
            <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
              <CircularProgress aria-label="Abriendo cámara" />
            </Box>
          )}

          {error && (
            <Alert
              severity="error"
              action={
                <Button
                  color="inherit"
                  onClick={() => void abrir(tipo === 'documento' ? 'trasera' : 'frontal')}
                >
                  Reintentar
                </Button>
              }
            >
              {MENSAJES_ERROR[error.tipo]}
            </Alert>
          )}

          {(stream || urlCaptura) && (
            <Box
              ref={visorRef}
              sx={{
                position: 'relative',
                flex: 1,
                minHeight: 280,
                overflow: 'hidden',
                bgcolor: 'common.black',
                borderRadius: '16px',
              }}
            >
              {urlCaptura ? (
                <Box
                  component="img"
                  src={urlCaptura}
                  alt={tipo === 'documento' ? 'Vista previa del CI' : 'Vista previa de la selfie'}
                  sx={{ width: '100%', height: '100%', objectFit: 'contain' }}
                />
              ) : (
                <Box
                  component="video"
                  ref={videoRef}
                  autoPlay
                  playsInline
                  muted
                  aria-label={
                    tipo === 'documento'
                      ? 'Cámara para fotografiar el CI'
                      : 'Cámara para tomar la selfie'
                  }
                  sx={{
                    width: '100%',
                    height: '100%',
                    objectFit: 'cover',
                    transform: tipo === 'selfie' ? 'scaleX(-1)' : undefined,
                  }}
                />
              )}
              {!urlCaptura && (
                <Box
                  ref={guiaRef}
                  aria-hidden="true"
                  sx={{
                    position: 'absolute',
                    left: tipo === 'documento' ? '6%' : '19%',
                    top: tipo === 'documento' ? '32%' : '12%',
                    width: tipo === 'documento' ? '88%' : '62%',
                    aspectRatio: tipo === 'documento' ? '1.586 / 1' : '3 / 4',
                    border: '3px solid',
                    borderColor: 'common.white',
                    borderRadius: tipo === 'documento' ? '12px' : '50%',
                    boxShadow: '0 0 0 9999px rgba(0, 0, 0, 0.48)',
                    pointerEvents: 'none',
                  }}
                />
              )}
            </Box>
          )}

          {errorCaptura && (
            <Alert severity="error">No se pudo crear la fotografía. Inténtalo de nuevo.</Alert>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={cancelar}>Cancelar</Button>
        {stream && (
          <Button variant="contained" onClick={() => void tomarFoto()} disabled={capturando}>
            Tomar foto
          </Button>
        )}
        {captura && (
          <>
            <Button onClick={repetir}>Repetir</Button>
            <Button variant="contained" onClick={() => onConfirmar(captura)}>
              Usar esta foto
            </Button>
          </>
        )}
      </DialogActions>
    </Dialog>
  );
}
