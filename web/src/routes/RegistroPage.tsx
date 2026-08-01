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
import { useAuth } from '../auth/AuthContext';

const esquema = z
  .object({
    email: z.string().email('Email inválido'),
    password: z.string().min(8, 'Mínimo 8 caracteres'),
    confirmarPassword: z.string(),
    nombre: z.string().min(1, 'El nombre es obligatorio'),
    ciudad: z.string().min(1, 'La ciudad es obligatoria'),
  })
  .refine((datos) => datos.password === datos.confirmarPassword, {
    message: 'Las contraseñas no coinciden',
    path: ['confirmarPassword'],
  });
type Datos = z.infer<typeof esquema>;

export function RegistroPage() {
  const { registrar } = useAuth();
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null);
  const [registrado, setRegistrado] = useState(false);
  const [mostrarPassword, setMostrarPassword] = useState(false);
  const [mostrarConfirmacion, setMostrarConfirmacion] = useState(false);
  const {
    register,
    handleSubmit,
    control,
    formState: { errors, isSubmitting },
  } = useForm<Datos>({
    resolver: zodResolver(esquema),
    mode: 'onChange',
  });
  const [password = '', confirmarPassword = ''] = useWatch({
    control,
    name: ['password', 'confirmarPassword'],
  });

  const onSubmit = async (datos: Datos) => {
    setErrorGeneral(null);
    try {
      const datosRegistro = {
        email: datos.email,
        password: datos.password,
        nombre: datos.nombre,
        ciudad: datos.ciudad,
      };
      await registrar(datosRegistro);
      setRegistrado(true);
    } catch {
      setErrorGeneral('No se pudo registrar');
    }
  };

  if (registrado) {
    return (
      <Container maxWidth="sm" sx={{ py: 4 }}>
        <Stack spacing={2}>
          <Typography variant="h4" component="h1">
            Revisa tu correo
          </Typography>
          <Alert severity="info">
            Te enviamos un correo de confirmación. Revisa tu bandeja de entrada y haz clic en el
            enlace para activar tu cuenta.
          </Alert>
          <Link component={RouterLink} to="/perfil">
            Continuar a mi perfil
          </Link>
        </Stack>
      </Container>
    );
  }

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Crear cuenta
      </Typography>
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <Stack spacing={2}>
          {errorGeneral && <Alert severity="error">{errorGeneral}</Alert>}
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
          <TextField
            label="Confirmar contraseña"
            type={mostrarConfirmacion ? 'text' : 'password'}
            {...register('confirmarPassword')}
            error={!!errors.confirmarPassword}
            helperText={errors.confirmarPassword?.message}
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
          <TextField
            label="Nombre"
            {...register('nombre')}
            error={!!errors.nombre}
            helperText={errors.nombre?.message}
          />
          <TextField
            label="Ciudad"
            {...register('ciudad')}
            error={!!errors.ciudad}
            helperText={errors.ciudad?.message}
          />
          <Button
            type="submit"
            variant="contained"
            disabled={isSubmitting || password !== confirmarPassword}
          >
            Registrarme
          </Button>
          <Link component={RouterLink} to="/login">
            ¿Ya tienes cuenta? Inicia sesión
          </Link>
        </Stack>
      </form>
    </Container>
  );
}
