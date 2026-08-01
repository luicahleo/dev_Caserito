import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Link as RouterLink } from 'react-router-dom';
import { Alert, Button, Container, Link, Stack, TextField, Typography } from '@mui/material';
import { solicitarRestablecimientoPassword } from '../api/auth';
import { HttpError } from '../api/http';

const esquema = z.object({
  email: z.string().email('Email inválido'),
});
type Datos = z.infer<typeof esquema>;

const mensajeUniforme =
  'Si existe una cuenta asociada a ese correo, recibirás un enlace para restablecer tu contraseña.';

export function OlvidePasswordPage() {
  const [resultado, setResultado] = useState<string | null>(null);
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<Datos>({ resolver: zodResolver(esquema) });

  const onSubmit = async ({ email }: Datos) => {
    setResultado(null);
    setErrorGeneral(null);
    try {
      await solicitarRestablecimientoPassword(email);
      setResultado(mensajeUniforme);
    } catch (error) {
      setErrorGeneral(
        error instanceof HttpError && error.status === 429
          ? 'Has realizado demasiados intentos. Espera antes de volver a intentarlo.'
          : 'No se pudo procesar la solicitud. Inténtalo de nuevo más tarde.',
      );
    }
  };

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Restablecer contraseña
      </Typography>
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <Stack spacing={2}>
          {resultado && <Alert severity="info">{resultado}</Alert>}
          {errorGeneral && <Alert severity="error">{errorGeneral}</Alert>}
          <TextField
            label="Email"
            type="email"
            {...register('email')}
            error={!!errors.email}
            helperText={errors.email?.message}
          />
          <Button type="submit" variant="contained" disabled={isSubmitting}>
            Enviar enlace
          </Button>
          <Link component={RouterLink} to="/login">
            Volver a iniciar sesión
          </Link>
        </Stack>
      </form>
    </Container>
  );
}
