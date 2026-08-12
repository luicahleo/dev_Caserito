import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useLocation, useNavigate, useSearchParams, Link as RouterLink } from 'react-router-dom';
import VisibilityIcon from '@mui/icons-material/Visibility';
import VisibilityOffIcon from '@mui/icons-material/VisibilityOff';
import {
  Alert,
  Button,
  Container,
  IconButton,
  InputAdornment,
  Link,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useAuth } from '../auth/AuthContext';
import * as authApi from '../api/auth';

const esquema = z.object({
  email: z.string().email('Email inválido'),
  password: z.string().min(1, 'La contraseña es obligatoria'),
});
type Datos = z.infer<typeof esquema>;

export function LoginPage() {
  const { iniciarSesion, restaurarSesion } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [params] = useSearchParams();
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null);
  const [proveedores, setProveedores] = useState<authApi.ProveedorExterno[]>([]);
  const [mostrarPassword, setMostrarPassword] = useState(false);
  useEffect(() => {
    authApi
      .obtenerProveedores()
      .then(setProveedores)
      .catch(() => setProveedores([]));
  }, []);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<Datos>({
    resolver: zodResolver(esquema),
  });

  const onSubmit = async (datos: Datos) => {
    setErrorGeneral(null);
    try {
      await iniciarSesion(datos);
      const solicitado = (location.state as { from?: unknown } | null)?.from;
      const estado = location.state as { vincularExterno?: boolean } | null;
      if (estado?.vincularExterno) {
        await authApi.vincularLoginExterno();
        await restaurarSesion();
      }
      navigate(authApi.normalizarRetorno(solicitado));
    } catch {
      setErrorGeneral('Credenciales inválidas');
    }
  };

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Iniciar sesión
      </Typography>
      <Stack spacing={1.5} sx={{ mb: 2 }}>
        {proveedores.map((proveedor) => (
          <Button
            key={proveedor}
            component="a"
            variant="outlined"
            href={`/api/auth/external/${proveedor}/start?returnUrl=${encodeURIComponent(authApi.normalizarRetorno((location.state as { from?: unknown } | null)?.from))}`}
          >
            Continuar con {proveedor === 'facebook' ? 'Facebook' : 'Google'}
          </Button>
        ))}
        {proveedores.length > 0 && <Typography align="center">o</Typography>}
      </Stack>
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <Stack spacing={2}>
          {(errorGeneral || params.has('authExterna')) && (
            <Alert severity="error">
              {errorGeneral ??
                (params.get('authExterna') === 'cancelado'
                  ? 'El acceso externo fue cancelado. Puedes intentarlo de nuevo.'
                  : 'El proveedor no está disponible. Inténtalo de nuevo.')}
            </Alert>
          )}
          <TextField
            label="Email"
            type="email"
            {...register('email')}
            error={!!errors.email}
            helperText={errors.email?.message}
          />
          <TextField
            label="Contraseña"
            type={mostrarPassword ? 'text' : 'password'}
            {...register('password')}
            error={!!errors.password}
            helperText={errors.password?.message}
            slotProps={{
              input: {
                endAdornment: (
                  <InputAdornment position="end">
                    <IconButton
                      aria-label={mostrarPassword ? 'Ocultar contraseña' : 'Mostrar contraseña'}
                      edge="end"
                      onClick={() => setMostrarPassword((visible) => !visible)}
                    >
                      {mostrarPassword ? <VisibilityOffIcon /> : <VisibilityIcon />}
                    </IconButton>
                  </InputAdornment>
                ),
              },
            }}
          />
          <Button type="submit" variant="contained" disabled={isSubmitting}>
            Entrar
          </Button>
          <Link component={RouterLink} to="/olvide-password">
            ¿Olvidaste tu contraseña?
          </Link>
          <Link component={RouterLink} to="/registro">
            ¿No tienes cuenta? Regístrate
          </Link>
          <Typography variant="caption" color="text.secondary">
            Al continuar, aceptas los{' '}
            <Link component={RouterLink} to="/terminos">
              Términos
            </Link>{' '}
            y reconoces la{' '}
            <Link component={RouterLink} to="/privacidad">
              Política de privacidad
            </Link>
            .
          </Typography>
        </Stack>
      </form>
    </Container>
  );
}
