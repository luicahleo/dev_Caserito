import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Link as RouterLink } from 'react-router-dom';
import { Button, Container, Stack, TextField, Typography, Alert, Link } from '@mui/material';
import { useAuth } from '../auth/AuthContext';

const esquema = z.object({
  email: z.string().email('Email inválido'),
  password: z.string().min(8, 'Mínimo 8 caracteres'),
  nombre: z.string().min(1, 'El nombre es obligatorio'),
  ciudad: z.string().min(1, 'La ciudad es obligatoria'),
});
type Datos = z.infer<typeof esquema>;

export function RegistroPage() {
  const { registrar } = useAuth();
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null);
  const [registrado, setRegistrado] = useState(false);
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
      await registrar(datos);
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
            type="password"
            {...register('password')}
            error={!!errors.password}
            helperText={errors.password?.message}
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
          <Button type="submit" variant="contained" disabled={isSubmitting}>
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
