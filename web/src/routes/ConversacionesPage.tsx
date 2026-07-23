import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import {
  Alert, Box, Button, Chip, CircularProgress, Container, List, ListItemButton,
  ListItemText, Stack, Typography,
} from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import { listarConversaciones, type ConversacionResumen } from '../api/chat';
import { obtenerAvisoPublico } from '../api/avisos';

function TituloAviso({ avisoId }: { avisoId: string }) {
  const { data, isLoading } = useQuery({
    queryKey: ['aviso-publico', avisoId],
    queryFn: () => obtenerAvisoPublico(avisoId),
    retry: false,
  });
  if (isLoading) return <>Cargando aviso…</>;
  return <>{data?.titulo ?? 'Aviso no disponible'}</>;
}

function etiquetaEstado(conversacion: ConversacionResumen) {
  if (conversacion.estado === 2) return 'Cerrada por moderación';
  if (conversacion.estado === 1) return 'Cerrada';
  if (!conversacion.puedeEnviar) return 'Envío no disponible';
  return 'Activa';
}

export function ConversacionesPage() {
  const { data, isLoading, error, hasNextPage, fetchNextPage, isFetchingNextPage } = useInfiniteQuery({
    queryKey: ['chat-bandeja'],
    queryFn: ({ pageParam }) => listarConversaciones(pageParam, 20),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (pagina) => pagina.siguienteCursor ?? undefined,
  });
  const conversaciones = data?.pages.flatMap((pagina) => pagina.items) ?? [];

  if (isLoading) return <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}><CircularProgress /></Box>;

  return (
    <Container maxWidth="md" sx={{ py: { xs: 2, sm: 4 } }}>
      <Typography variant="h4" component="h1" gutterBottom>Mensajes</Typography>
      {error && <Alert severity="error">No se pudieron cargar tus conversaciones.</Alert>}
      {!error && conversaciones.length === 0 && (
        <Stack spacing={2} sx={{ alignItems: 'flex-start' }}>
          <Typography>No tienes conversaciones todavía.</Typography>
          <Button component={RouterLink} to="/">Explorar avisos</Button>
        </Stack>
      )}
      <List disablePadding>
        {conversaciones.map((conversacion) => (
          <ListItemButton
            key={conversacion.id}
            component={RouterLink}
            to={`/mensajes/${conversacion.id}`}
            divider
            sx={{ px: { xs: 1, sm: 2 }, alignItems: 'flex-start' }}
          >
            <ListItemText
              primary={<TituloAviso avisoId={conversacion.avisoId} />}
              secondary={`${conversacion.rol === 'Comprador' ? 'Hablas con el vendedor' : 'Hablas con el comprador'} · ${new Date(conversacion.ultimaActividadEn).toLocaleString('es-BO')}`}
            />
            <Stack spacing={1} sx={{ ml: 1, alignItems: 'flex-end' }}>
              {conversacion.noLeidos > 0 && <Chip color="primary" size="small" label={`${conversacion.noLeidos} sin leer`} />}
              <Chip size="small" variant="outlined" label={etiquetaEstado(conversacion)} />
            </Stack>
          </ListItemButton>
        ))}
      </List>
      {hasNextPage && (
        <Button onClick={() => void fetchNextPage()} disabled={isFetchingNextPage} sx={{ mt: 2 }}>
          {isFetchingNextPage ? 'Cargando…' : 'Cargar más conversaciones'}
        </Button>
      )}
    </Container>
  );
}
