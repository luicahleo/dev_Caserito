import { useEffect, useRef, useState } from 'react';
import RefreshRoundedIcon from '@mui/icons-material/RefreshRounded';
import HomeRoundedIcon from '@mui/icons-material/HomeRounded';
import ContentCopyRoundedIcon from '@mui/icons-material/ContentCopyRounded';
import { Alert, Button, Container, Stack, Typography } from '@mui/material';
import { useRouteError } from 'react-router-dom';
import { esErrorCargaDiferida, intentarRecuperarCarga } from './recuperacionCarga';
import { crearErrorId, reportarDiagnostico } from '../lib/diagnosticos';

export function ErrorAplicacion() {
  const error = useRouteError();
  const [errorId] = useState(crearErrorId);
  const [copiado, setCopiado] = useState(false);
  const reportado = useRef(false);

  useEffect(() => {
    if (!reportado.current) {
      reportado.current = true;
      const esCarga = esErrorCargaDiferida(error);
      void reportarDiagnostico({
        errorId,
        eventName: esCarga ? 'chunk.load_failed' : 'router.unexpected',
        category: esCarga ? 'chunk' : 'unexpected',
        source: 'router',
      });
    }
    intentarRecuperarCarga(error, window.location.pathname);
  }, [error, errorId]);

  const copiarCodigo = async () => {
    try {
      if (!navigator.clipboard) return;
      await navigator.clipboard.writeText(errorId);
      setCopiado(true);
    } catch {
      // El código permanece visible para poder copiarlo manualmente.
    }
  };

  return (
    <Container component="main" maxWidth="sm" sx={{ py: { xs: 6, md: 10 } }}>
      <Stack spacing={3} sx={{ alignItems: 'flex-start' }}>
        <Typography component="h1" variant="h4">
          No pudimos mostrar esta página
        </Typography>
        <Alert severity="warning" sx={{ width: '100%' }}>
          La aplicación pudo actualizarse mientras navegabas. Intenta cargarla nuevamente.
        </Alert>
        <Stack spacing={1} sx={{ width: '100%' }}>
          <Typography variant="body2" color="text.secondary">
            Código de diagnóstico
          </Typography>
          <Typography component="code" sx={{ fontFamily: 'monospace', fontWeight: 700 }}>
            {errorId}
          </Typography>
          <Button
            variant="text"
            startIcon={<ContentCopyRoundedIcon />}
            onClick={() => void copiarCodigo()}
            sx={{ alignSelf: 'flex-start' }}
          >
            Copiar código de diagnóstico
          </Button>
          {copiado && (
            <Typography role="status" variant="body2" color="success.main">
              Código copiado
            </Typography>
          )}
        </Stack>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ width: '100%' }}>
          <Button
            variant="contained"
            startIcon={<RefreshRoundedIcon />}
            onClick={() => window.location.reload()}
          >
            Intentar nuevamente
          </Button>
          <Button variant="outlined" startIcon={<HomeRoundedIcon />} href="/">
            Volver al inicio
          </Button>
        </Stack>
      </Stack>
    </Container>
  );
}
