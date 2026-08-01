import { useEffect, useState } from 'react';
import { Alert, CircularProgress, Container, Stack, Typography } from '@mui/material';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { normalizarRetorno } from '../api/auth';

export function AuthExternaCallbackPage() {
  const { restaurarSesion } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const [error, setError] = useState(false);

  useEffect(() => {
    restaurarSesion()
      .then(() => navigate(normalizarRetorno(params.get('returnUrl')), { replace: true }))
      .catch(() => setError(true));
  }, [navigate, params, restaurarSesion]);

  return <Container maxWidth="sm" sx={{ py: 4 }}><Stack spacing={2} sx={{ alignItems: 'center' }}>
    {error ? <Alert severity="error">No pudimos completar el acceso. Vuelve a intentarlo.</Alert> : <>
      <CircularProgress aria-label="Completando el acceso" />
      <Typography>Completando el acceso…</Typography>
    </>}
  </Stack></Container>;
}
