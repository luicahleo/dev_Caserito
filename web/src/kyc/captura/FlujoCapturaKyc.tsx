import { useState } from 'react';
import { Button, Chip, Stack, Typography } from '@mui/material';
import { CapturadorGuiado } from './CapturadorGuiado';

interface FlujoCapturaKycProps {
  documento: File | null;
  selfie: File | null;
  onDocumento(archivo: File | null): void;
  onSelfie(archivo: File | null): void;
}

export function FlujoCapturaKyc({
  documento,
  selfie,
  onDocumento,
  onSelfie,
}: FlujoCapturaKycProps) {
  const [capturando, setCapturando] = useState<'documento' | 'selfie' | null>(null);

  return (
    <Stack spacing={2}>
      <Typography variant="subtitle1">Fotografías para la revisión humana</Typography>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <Button variant="outlined" onClick={() => setCapturando('documento')}>
          {documento ? 'Repetir foto del CI' : 'Tomar foto del CI'}
        </Button>
        {documento && <Chip color="success" label="CI fotografiado" />}
      </Stack>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <Button variant="outlined" onClick={() => setCapturando('selfie')} disabled={!documento}>
          {selfie ? 'Repetir selfie' : 'Tomar selfie'}
        </Button>
        {selfie && <Chip color="success" label="Selfie fotografiada" />}
      </Stack>

      {capturando && (
        <CapturadorGuiado
          tipo={capturando}
          onCancelar={() => setCapturando(null)}
          onConfirmar={(archivo) => {
            if (capturando === 'documento') {
              onDocumento(archivo);
              onSelfie(null);
            } else {
              onSelfie(archivo);
            }
            setCapturando(null);
          }}
        />
      )}
    </Stack>
  );
}
