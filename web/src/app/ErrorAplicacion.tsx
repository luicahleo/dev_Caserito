import { useEffect } from 'react';
import RefreshRoundedIcon from '@mui/icons-material/RefreshRounded';
import HomeRoundedIcon from '@mui/icons-material/HomeRounded';
import { Alert, Button, Container, Stack, Typography } from '@mui/material';
import { useRouteError } from 'react-router-dom';
import { intentarRecuperarCarga } from './recuperacionCarga';

export function ErrorAplicacion() {
  const error = useRouteError();

  useEffect(() => {
    intentarRecuperarCarga(error, window.location.pathname);
  }, [error]);

  return (
    <Container component="main" maxWidth="sm" sx={{ py: { xs: 6, md: 10 } }}>
      <Stack spacing={3} sx={{ alignItems: 'flex-start' }}>
        <Typography component="h1" variant="h4">
          No pudimos mostrar esta página
        </Typography>
        <Alert severity="warning" sx={{ width: '100%' }}>
          La aplicación pudo actualizarse mientras navegabas. Intenta cargarla nuevamente.
        </Alert>
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
