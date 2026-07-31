import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useNavigate } from 'react-router-dom';
import { Alert, Box, Button, Chip, Container, Link, Snackbar, Stack, TextField, Typography, CircularProgress } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { actualizarPerfil, type Perfil } from '../api/perfil';

const esquema = z.object({
  nombre: z.string().min(1, 'El nombre es obligatorio'),
  ciudad: z.string().min(1, 'La ciudad es obligatoria'),
});
type Datos = z.infer<typeof esquema>;

export function PerfilPage() {
  const { usuario, cerrarSesion, verificado, identidadHabilitada, tienePermiso } = useAuth();
  const navigate = useNavigate();
  const [perfilGuardado, setPerfilGuardado] = useState<Perfil | null>(null);
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null);
  const [guardado, setGuardado] = useState(false);
  const perfil = perfilGuardado ?? usuario;
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<Datos>({
    resolver: zodResolver(esquema),
    values: perfil ? { nombre: perfil.nombre, ciudad: perfil.ciudad } : undefined,
  });

  const onSubmit = async (datos: Datos) => {
    setErrorGeneral(null);
    try {
      await actualizarPerfil(datos);
      setPerfilGuardado(perfil ? { ...perfil, ...datos } : null);
      reset(datos);
      setGuardado(true);
    } catch {
      setErrorGeneral('No se pudo actualizar el perfil');
    }
  };

  const onCerrarSesion = async () => {
    await cerrarSesion();
    navigate('/login');
  };

  if (!perfil) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Mi perfil
      </Typography>
      <Stack spacing={2}>
        <TextField
          label="Email"
          value={perfil.email}
          disabled
          slotProps={{ input: { readOnly: true } }}
        />
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Chip
            color={verificado ? 'success' : identidadHabilitada ? 'info' : 'default'}
            label={verificado ? 'Verificado' : identidadHabilitada ? 'Exento por rol de administrador' : 'Sin verificar'}
          />
          <Link component={RouterLink} to="/kyc">
            {verificado ? 'Ver estado' : identidadHabilitada ? 'Consultar rol' : 'Verificar identidad'}
          </Link>
        </Box>
        {tienePermiso('kyc.revisar') && (
          <Link component={RouterLink} to="/admin/kyc">
            Revisar verificaciones (admin)
          </Link>
        )}
        {errorGeneral && <Alert severity="error">{errorGeneral}</Alert>}
        <form onSubmit={handleSubmit(onSubmit)} noValidate>
          <Stack spacing={2}>
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
              Guardar
            </Button>
          </Stack>
        </form>
        <Button variant="outlined" color="secondary" onClick={onCerrarSesion}>
          Cerrar sesión
        </Button>
      </Stack>
      <Snackbar
        open={guardado}
        autoHideDuration={3000}
        onClose={() => setGuardado(false)}
        message="Perfil actualizado"
      />
    </Container>
  );
}
