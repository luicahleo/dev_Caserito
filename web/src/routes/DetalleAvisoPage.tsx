import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link as RouterLink, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Container,
  Stack,
  Typography,
} from '@mui/material';
import { obtenerAvisoPublico } from '../api/avisos';
import { formatearBob } from '../lib/formato';
import { HttpError } from '../api/http';

export function DetalleAvisoPage() {
  const { id = '' } = useParams();
  const [selectedIdx, setSelectedIdx] = useState(0);
  const { data, isLoading, error } = useQuery({
    queryKey: ['aviso-publico', id],
    queryFn: () => obtenerAvisoPublico(id),
  });

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error || !data) {
    const noDisponible = error instanceof HttpError && error.status === 404;
    return (
      <Container maxWidth="sm" sx={{ py: 4 }}>
        <Alert severity={noDisponible ? 'info' : 'error'}>
          {noDisponible
            ? 'Este aviso no está disponible.'
            : 'No se pudo cargar el aviso. Inténtalo más tarde.'}
        </Alert>
        <Button component={RouterLink} to="/" sx={{ mt: 2 }}>
          Volver a explorar
        </Button>
      </Container>
    );
  }

  return (
    <Container maxWidth="md" sx={{ py: 4 }}>
      <Button component={RouterLink} to="/" sx={{ mb: 2 }}>
        ← Volver a explorar
      </Button>
      {/* Galería de fotos */}
      {data.fotos && data.fotos.length > 0 ? (
        <Box sx={{ mb: 3 }}>
          <Box
            component="img"
            src={data.fotos[selectedIdx]?.url ?? data.fotos[0].url}
            alt={data.titulo}
            sx={{ width: '100%', maxHeight: 320, objectFit: 'cover', borderRadius: 1 }}
          />
          {data.fotos.length > 1 && (
            <Stack direction="row" spacing={1} sx={{ mt: 1, flexWrap: 'wrap' }}>
              {data.fotos.map((f, i) => (
                <Box
                  key={f.id}
                  component="img"
                  src={f.url}
                  alt={`Foto ${i + 1}`}
                  onClick={() => setSelectedIdx(i)}
                  sx={{
                    width: 64, height: 64, objectFit: 'cover', borderRadius: 0.5,
                    cursor: 'pointer',
                    border: i === selectedIdx ? '2px solid' : '2px solid transparent',
                    borderColor: i === selectedIdx ? 'primary.main' : 'transparent',
                  }}
                />
              ))}
            </Stack>
          )}
        </Box>
      ) : (
        <Box
          sx={{
            height: 260, bgcolor: 'grey.200', mb: 3,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
          }}
        >
          <Typography variant="caption" color="text.disabled">Sin fotos</Typography>
        </Box>
      )}
      <Typography variant="h4" component="h1" gutterBottom>
        {data.titulo}
      </Typography>
      <Typography variant="h5" color="primary" gutterBottom>
        {formatearBob(data.monto)}
      </Typography>
      <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
        <Chip label={data.nombreCategoria} />
        <Chip label={data.nombreCiudad} />
        <Chip label={data.condicion} />
      </Stack>
      <Typography variant="body1" sx={{ whiteSpace: 'pre-wrap' }}>
        {data.descripcion}
      </Typography>
    </Container>
  );
}
