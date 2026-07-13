import { Container, Typography, Chip, Stack } from '@mui/material';
import { useHealth } from '../api/useHealth';

export function HomePage() {
  const { data, isLoading, isError } = useHealth();
  const estado = isLoading ? 'consultando…' : isError ? 'sin conexión' : (data?.estado ?? '—');

  return (
    <Container sx={{ py: 4 }}>
      <Stack spacing={2}>
        <Typography variant="h4" component="h1">
          CaseritoApp
        </Typography>
        <Chip label={`Backend: ${estado}`} color={isError ? 'error' : 'success'} />
      </Stack>
    </Container>
  );
}
