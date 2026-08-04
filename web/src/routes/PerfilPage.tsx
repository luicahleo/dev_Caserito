import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useNavigate } from 'react-router-dom';
import { Alert, Box, Button, Chip, Container, Link, Snackbar, Stack, TextField, Typography, CircularProgress } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { actualizarPerfil, type Perfil } from '../api/perfil';
import { SelectorCiudad } from '../perfil/SelectorCiudad';

const esquema = z.object({
  nombres: z.string().min(1, 'Los nombres son obligatorios'),
  apellidos: z.string().min(1, 'Los apellidos son obligatorios'),
  ciudadId: z.string().min(1, 'Selecciona una ciudad'),
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
    control,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<Datos>({
    resolver: zodResolver(esquema),
    values: perfil ? {
      nombres: perfil.nombres,
      apellidos: perfil.apellidos,
      ciudadId: perfil.ciudadId,
    } : undefined,
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
              label="Nombres"
              {...register('nombres')}
              error={!!errors.nombres}
              helperText={errors.nombres?.message}
            />
            <TextField
              label="Apellidos"
              {...register('apellidos')}
              error={!!errors.apellidos}
              helperText={errors.apellidos?.message}
            />
            <Controller name="ciudadId" control={control} render={({ field }) =>
              <SelectorCiudad value={field.value} onChange={field.onChange}
                error={!!errors.ciudadId} helperText={errors.ciudadId?.message} />} />
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
