import { useEffect, useState } from 'react';
import { useSearchParams, Link as RouterLink } from 'react-router-dom';
import { Alert, Button, CircularProgress, Container, Stack, Typography } from '@mui/material';
import { confirmarEmail } from '../api/auth';

type Estado = 'cargando' | 'exito' | 'error';

// Destino del enlace que llega por correo: /confirmar-email?userId=...&token=...
export function ConfirmarEmailPage() {
  const [searchParams] = useSearchParams();
  const usuarioId = searchParams.get('userId');
  const token = searchParams.get('token');
  const [resultado, setResultado] = useState<Estado>('cargando');
  const estado: Estado = !usuarioId || !token ? 'error' : resultado;

  useEffect(() => {
    if (!usuarioId || !token) {
      return;
    }

    let activo = true;
    confirmarEmail(usuarioId, token)
      .then(() => activo && setResultado('exito'))
      .catch(() => activo && setResultado('error'));
    return () => {
      activo = false;
    };
  }, [usuarioId, token]);

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Stack spacing={2}>
        <Typography variant="h4" component="h1">
          Confirmación de email
        </Typography>
        {estado === 'cargando' && <CircularProgress aria-label="Confirmando email" />}
        {estado === 'exito' && (
          <>
            <Alert severity="success">Tu email ha sido confirmado. Ya puedes usar Caserito.</Alert>
            <Button component={RouterLink} to="/perfil" variant="contained">
              Ir a mi perfil
            </Button>
          </>
        )}
        {estado === 'error' && (
          <Alert severity="error">
            El enlace no es válido o ha expirado. Inicia sesión y solicita un correo nuevo desde tu
            perfil.
          </Alert>
        )}
      </Stack>
    </Container>
  );
}
