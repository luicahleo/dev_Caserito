import { useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Link as RouterLink } from 'react-router-dom';
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
import { restablecerPassword } from '../api/auth';
import { HttpError } from '../api/http';

const esquema = z
  .object({
    password: z.string().min(8, 'Mínimo 8 caracteres'),
    confirmarPassword: z.string(),
  })
  .refine((datos) => datos.password === datos.confirmarPassword, {
    message: 'Las contraseñas no coinciden',
    path: ['confirmarPassword'],
  });
type Datos = z.infer<typeof esquema>;

interface DatosEnlace {
  usuarioId: string;
  token: string;
}

function consumirFragmento(): DatosEnlace | null {
  const parametros = new URLSearchParams(window.location.hash.slice(1));
  const usuarioId = parametros.get('usuarioId');
  const token = parametros.get('token');
  window.history.replaceState(null, '', `${window.location.pathname}${window.location.search}`);
  return usuarioId && token ? { usuarioId, token } : null;
}

const mensajeEnlaceInvalido = 'El enlace no es válido o ha caducado.';

export function RestablecerPasswordPage() {
  const [enlace] = useState(consumirFragmento);
  const [estado, setEstado] = useState<'formulario' | 'exito' | 'enlace-invalido'>(
    enlace ? 'formulario' : 'enlace-invalido',
  );
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null);
  const [mostrarPassword, setMostrarPassword] = useState(false);
  const [mostrarConfirmacion, setMostrarConfirmacion] = useState(false);
  const {
    register,
    handleSubmit,
    control,
    formState: { dirtyFields, errors, isSubmitting },
  } = useForm<Datos>({
    resolver: zodResolver(esquema),
    mode: 'onChange',
    defaultValues: { password: '', confirmarPassword: '' },
  });
  const [password, confirmarPassword] = useWatch({
    control,
    name: ['password', 'confirmarPassword'],
  });
  const confirmacionNoCoincide =
    dirtyFields.confirmarPassword && password !== confirmarPassword;

  const onSubmit = async (datos: Datos) => {
    if (!enlace) return;
    setErrorGeneral(null);
    try {
      await restablecerPassword(enlace.usuarioId, enlace.token, datos.password);
      setEstado('exito');
    } catch (error) {
      if (error instanceof HttpError && error.status === 400) {
        setEstado('enlace-invalido');
      } else {
        setErrorGeneral(
          error instanceof HttpError && error.status === 429
            ? 'Has realizado demasiados intentos. Espera antes de volver a intentarlo.'
            : 'No se pudo restablecer la contraseña. Inténtalo de nuevo más tarde.',
        );
      }
    }
  };

  if (estado === 'exito') {
    return (
      <Container maxWidth="sm" sx={{ py: 4 }}>
        <Stack spacing={2}>
          <Alert severity="success">
            Tu contraseña se restableció correctamente. Ya puedes iniciar sesión.
          </Alert>
          <Link component={RouterLink} to="/login">
            Iniciar sesión
          </Link>
        </Stack>
      </Container>
    );
  }

  if (estado === 'enlace-invalido') {
    return (
      <Container maxWidth="sm" sx={{ py: 4 }}>
        <Alert severity="error">{mensajeEnlaceInvalido}</Alert>
      </Container>
    );
  }

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Nueva contraseña
      </Typography>
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <Stack spacing={2}>
          {errorGeneral && <Alert severity="error">{errorGeneral}</Alert>}
          <TextField
            label="Nueva contraseña"
            type={mostrarPassword ? 'text' : 'password'}
            {...register('password')}
            error={!!errors.password}
            helperText={errors.password?.message}
            slotProps={{
              input: {
                endAdornment: (
                  <InputAdornment position="end">
                    <IconButton
                      aria-label={
                        mostrarPassword ? 'Ocultar nueva contraseña' : 'Mostrar nueva contraseña'
                      }
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
          <TextField
            label="Confirmar contraseña"
            type={mostrarConfirmacion ? 'text' : 'password'}
            {...register('confirmarPassword')}
            error={confirmacionNoCoincide || !!errors.confirmarPassword}
            helperText={
              confirmacionNoCoincide
                ? 'Las contraseñas no coinciden'
                : dirtyFields.confirmarPassword
                  ? errors.confirmarPassword?.message
                  : undefined
            }
            slotProps={{
              input: {
                endAdornment: (
                  <InputAdornment position="end">
                    <IconButton
                      aria-label={
                        mostrarConfirmacion
                          ? 'Ocultar confirmación de contraseña'
                          : 'Mostrar confirmación de contraseña'
                      }
                      edge="end"
                      onClick={() => setMostrarConfirmacion((visible) => !visible)}
                    >
                      {mostrarConfirmacion ? <VisibilityOffIcon /> : <VisibilityIcon />}
                    </IconButton>
                  </InputAdornment>
                ),
              },
            }}
          />
          <Button
            type="submit"
            variant="contained"
            disabled={isSubmitting || password.length < 8 || password !== confirmarPassword}
          >
            Restablecer contraseña
          </Button>
        </Stack>
      </form>
    </Container>
  );
}
