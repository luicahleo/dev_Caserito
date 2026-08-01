import { useEffect, useState, type FormEvent } from 'react';
import { Alert, Button, CircularProgress, Container, Link, Stack, TextField, Typography } from '@mui/material';
import { Link as RouterLink, useNavigate, useSearchParams } from 'react-router-dom';
import * as auth from '../api/auth';
import { useAuth } from '../auth/AuthContext';

export function CompletarRegistroExternoPage() {
  const [pendiente, setPendiente] = useState<auth.LoginExternoPendiente | null>(null);
  const [error, setError] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const { restaurarSesion } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const retorno = auth.normalizarRetorno(params.get('returnUrl'));
  useEffect(() => { auth.obtenerLoginExternoPendiente().then(setPendiente).catch(() => setError(true)); }, []);

  async function completar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault(); setEnviando(true); setError(false);
    const datos = new FormData(evento.currentTarget);
    try {
      await auth.completarLoginExterno({
        email: pendiente?.requiereEmail ? String(datos.get('email') ?? '') : null,
        nombre: pendiente?.requiereNombre ? String(datos.get('nombre') ?? '') : null,
        ciudad: pendiente?.requiereCiudad ? String(datos.get('ciudad') ?? '') : null,
      });
      await restaurarSesion(); navigate(retorno, { replace: true });
    } catch { setError(true); } finally { setEnviando(false); }
  }

  if (error) return <Container maxWidth="sm" sx={{ py: 4 }}><Alert severity="error">No pudimos continuar el acceso externo. Inténtalo de nuevo.</Alert></Container>;
  if (!pendiente) return <CircularProgress aria-label="Cargando datos pendientes" />;
  if (pendiente.requiereVinculacion) return <Container maxWidth="sm" sx={{ py: 4 }}><Stack spacing={2}>
    <Typography variant="h4" component="h1">Verifica tu cuenta</Typography>
    <Alert severity="info">Para continuar, verifica tu cuenta existente. No vincularemos cuentas automáticamente.</Alert>
    <Button component={RouterLink} to="/login" state={{ from: retorno, vincularExterno: true }}>Iniciar sesión</Button>
    <Link component={RouterLink} to="/olvide-password">Recuperar acceso</Link>
  </Stack></Container>;
  return <Container maxWidth="sm" sx={{ py: 4 }}><Typography variant="h4" component="h1" gutterBottom>Completa tu registro</Typography>
    <form onSubmit={completar}><Stack spacing={2}>
      {pendiente.requiereEmail && <TextField required name="email" label="Email" type="email" />}
      {pendiente.requiereNombre && <TextField required name="nombre" label="Nombre" defaultValue={pendiente.nombreVisible ?? ''} />}
      {pendiente.requiereCiudad && <TextField required name="ciudad" label="Ciudad" />}
      <Button type="submit" variant="contained" disabled={enviando}>Continuar</Button>
    </Stack></form>
  </Container>;
}
