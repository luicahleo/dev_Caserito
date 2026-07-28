import { useQuery } from '@tanstack/react-query';
import { Alert, Box, Chip, CircularProgress, Typography } from '@mui/material';
import { listarPuntosEncuentro } from '../api/puntosEncuentro';

interface PuntosEncuentroListaProps {
  ciudad: string;
}

export function PuntosEncuentroLista({ ciudad }: PuntosEncuentroListaProps) {
  const { data, isLoading, error } = useQuery({
    queryKey: ['puntos-encuentro', ciudad],
    queryFn: () => listarPuntosEncuentro(ciudad),
    enabled: ciudad.trim().length > 0,
  });

  if (ciudad.trim().length === 0) {
    return null;
  }

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 2 }}>
        <CircularProgress size={24} />
      </Box>
    );
  }

  if (error) {
    return (
      <Alert severity="warning" sx={{ mt: 2 }}>
        No se pudieron cargar los puntos de encuentro seguros.
      </Alert>
    );
  }

  const puntos = data ?? [];

  if (puntos.length === 0) {
    return (
      <Alert severity="info" sx={{ mt: 2 }}>
        No hay puntos de encuentro seguros registrados en {ciudad}.
      </Alert>
    );
  }

  return (
    <Box sx={{ mt: 3 }}>
      <Typography variant="h6" gutterBottom>
        Puntos de encuentro seguros en {ciudad}
      </Typography>
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
        {puntos.map((punto) => (
          <Box
            key={punto.id}
            sx={{
              p: 2,
              border: 1,
              borderColor: 'divider',
              borderRadius: 1,
            }}
          >
            <Typography variant="subtitle1">{punto.nombre}</Typography>
            <Typography variant="body2" color="text.secondary">
              {punto.direccion}
            </Typography>
            <Chip label={punto.ciudad} size="small" sx={{ mt: 1 }} />
          </Box>
        ))}
      </Box>
    </Box>
  );
}
