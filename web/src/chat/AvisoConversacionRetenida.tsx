import { useState } from 'react';
import { Alert, Button, Stack, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import { iniciarConversacion } from '../api/chat';
import { esRetenida } from './estadoConversacion';

type Resultado = 'inicial' | 'sigueRetenida' | 'error';

export function AvisoConversacionRetenida({
  avisoId,
  alLiberar,
}: {
  avisoId: string;
  alLiberar: () => void;
}) {
  const [resultado, setResultado] = useState<Resultado>('inicial');
  const [comprobando, setComprobando] = useState(false);

  async function comprobar() {
    setComprobando(true);
    setResultado('inicial');
    try {
      const conversacion = await iniciarConversacion(avisoId);
      if (esRetenida(conversacion.estado)) {
        setResultado('sigueRetenida');
        return;
      }
      alLiberar();
    } catch {
      setResultado('error');
    } finally {
      setComprobando(false);
    }
  }

  return (
    <Alert severity="info" sx={{ mb: 2 }}>
      <Typography>
        Tu mensaje está guardado, pero todavía no se ha entregado. Para que llegue, verifica tu
        identidad.
      </Typography>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mt: 1 }}>
        <Button component={RouterLink} to="/kyc" variant="contained" size="small">
          Verificar mi identidad
        </Button>
        <Button onClick={() => void comprobar()} disabled={comprobando} size="small">
          Ya verifiqué mi identidad
        </Button>
      </Stack>
      {resultado === 'sigueRetenida' && (
        <Typography variant="body2" sx={{ mt: 1 }}>
          Tu verificación todavía no está aprobada.
        </Typography>
      )}
      {resultado === 'error' && (
        <Typography variant="body2" color="error" sx={{ mt: 1 }}>
          No se pudo comprobar tu verificación. Inténtalo nuevamente.
        </Typography>
      )}
    </Alert>
  );
}
