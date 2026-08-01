import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useLocation, useNavigate, Link as RouterLink } from 'react-router-dom';
import { Button, Container, Stack, TextField, Typography, Alert, Link } from '@mui/material';
import { useAuth } from '../auth/AuthContext';

const esquema = z.object({
  email: z.string().email('Email inválido'),
  password: z.string().min(1, 'La contraseña es obligatoria'),
});
type Datos = z.infer<typeof esquema>;

export function LoginPage() {
  const { iniciarSesion } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null);
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
      navigate(typeof solicitado === 'string' && solicitado.startsWith('/') && !solicitado.startsWith('//') ? solicitado : '/perfil');
    } catch {
      setErrorGeneral('Credenciales inválidas');
    }
  };

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Iniciar sesión
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
          <Button type="submit" variant="contained" disabled={isSubmitting}>
            Entrar
          </Button>
          <Link component={RouterLink} to="/olvide-password">
            ¿Olvidaste tu contraseña?
          </Link>
          <Link component={RouterLink} to="/registro">
            ¿No tienes cuenta? Regístrate
          </Link>
        </Stack>
      </form>
    </Container>
  );
}
