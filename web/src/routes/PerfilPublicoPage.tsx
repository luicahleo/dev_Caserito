import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Container,
  Pagination,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import { useParams } from 'react-router-dom';
import { listarResenasPublicas, obtenerPerfilPublico } from '../api/reputation';

export function PerfilPublicoPage() {
  const { id = '' } = useParams();
  const [pagina, setPagina] = useState(1);
  const perfil = useQuery({
    queryKey: ['public-profile', id],
    queryFn: () => obtenerPerfilPublico(id),
    retry: false,
  });
  const resenas = useQuery({
    queryKey: ['public-reviews', id, pagina],
    queryFn: () => listarResenasPublicas(id, pagina, 10),
    enabled: perfil.isSuccess,
    retry: false,
  });

  if (perfil.isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }
  if (perfil.isError || !perfil.data) {
    return (
      <Container maxWidth="sm" sx={{ py: 4 }}>
        <Alert severity="info">Este perfil no está disponible.</Alert>
      </Container>
    );
  }

  const totalPaginas = Math.max(1, Math.ceil((resenas.data?.total ?? 0) / 10));
  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        {perfil.data.nombreVisible}
      </Typography>
      <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
        <Chip label={perfil.data.nombreCiudad} />
        {perfil.data.verificado && <Chip color="success" label="Usuario verificado" />}
      </Stack>
      <Typography variant="h6">
        {perfil.data.promedio === null
          ? 'Sin calificaciones reveladas'
          : `${perfil.data.promedio.toLocaleString('es-BO', {
              minimumFractionDigits: 1,
              maximumFractionDigits: 1,
            })} de 5`}
      </Typography>
      <Typography color="text.secondary" sx={{ mb: 3 }}>
        {perfil.data.totalResenas} reseñas reveladas
      </Typography>
      {resenas.isError && <Alert severity="error">No se pudieron cargar las reseñas.</Alert>}
      {resenas.data?.items.length === 0 && (
        <Alert severity="info">Todavía no hay reseñas públicas.</Alert>
      )}
      <Stack spacing={2}>
        {resenas.data?.items.map((resena) => (
          <Paper key={`${resena.creadaEn}-${resena.puntuacion}`} variant="outlined" sx={{ p: 2 }}>
            <Stack direction="row" spacing={1} sx={{ mb: 1 }}>
              <Chip label={`${resena.puntuacion} de 5`} color="primary" />
              <Chip
                label={resena.rolAutor === 'comprador' ? 'Comprador' : 'Vendedor'}
                variant="outlined"
              />
            </Stack>
            <Typography sx={{ whiteSpace: 'pre-wrap' }}>{resena.comentario}</Typography>
            <Typography variant="caption" color="text.secondary">
              {new Date(resena.creadaEn).toLocaleDateString('es-BO')}
            </Typography>
          </Paper>
        ))}
      </Stack>
      {totalPaginas > 1 && (
        <Pagination
          page={pagina}
          count={totalPaginas}
          onChange={(_, valor) => setPagina(valor)}
          sx={{ mt: 3 }}
        />
      )}
    </Container>
  );
}
