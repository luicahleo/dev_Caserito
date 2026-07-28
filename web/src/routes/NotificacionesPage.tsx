import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Container,
  List,
  ListItem,
  ListItemButton,
  ListItemText,
  Pagination,
  Typography,
} from '@mui/material';
import { listarNotificaciones, marcarLeida, marcarTodasLeidas } from '../api/notificaciones';

const TAMANO_PAGINA = 20;

export function NotificacionesPage() {
  const [pagina, setPagina] = useState(1);
  const cliente = useQueryClient();

  const { data, isLoading, error } = useQuery({
    queryKey: ['notificaciones', 'bandeja', pagina],
    queryFn: () => listarNotificaciones(false, pagina, TAMANO_PAGINA),
  });

  const marcarLeidaMutation = useMutation({
    mutationFn: marcarLeida,
    onSuccess: async () => {
      await cliente.invalidateQueries({ queryKey: ['notificaciones'] });
    },
  });

  const marcarTodasMutation = useMutation({
    mutationFn: marcarTodasLeidas,
    onSuccess: async () => {
      await cliente.invalidateQueries({ queryKey: ['notificaciones'] });
    },
  });

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error) {
    return (
      <Container maxWidth="md" sx={{ py: 4 }}>
        <Alert severity="error">No se pudieron cargar las notificaciones.</Alert>
      </Container>
    );
  }

  const notificaciones = data?.items ?? [];
  const totalPaginas = data
    ? Math.ceil(Number(data.total) / Number(data.tamano))
    : 0;

  return (
    <Container maxWidth="md" sx={{ py: 4 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h4" component="h1">
          Notificaciones
        </Typography>
        <Button
          variant="outlined"
          onClick={() => marcarTodasMutation.mutate()}
          disabled={marcarTodasMutation.isPending || notificaciones.length === 0}
        >
          Marcar todas como leídas
        </Button>
      </Box>
      {notificaciones.length === 0 ? (
        <Alert severity="info">No tienes notificaciones.</Alert>
      ) : (
        <>
          <List>
            {notificaciones.map((notificacion) => (
              <ListItem key={notificacion.id} disablePadding>
                <ListItemButton
                  onClick={() => {
                    if (!notificacion.leida) {
                      marcarLeidaMutation.mutate(notificacion.id);
                    }
                  }}
                >
                  <ListItemText
                    primary={
                      <Typography sx={{ fontWeight: notificacion.leida ? 'normal' : 'bold' }}>
                        {notificacion.titulo}
                      </Typography>
                    }
                    secondary={
                      <Typography
                        sx={{
                          whiteSpace: 'nowrap',
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                        }}
                      >
                        {notificacion.mensaje}
                      </Typography>
                    }
                  />
                </ListItemButton>
              </ListItem>
            ))}
          </List>
          {totalPaginas > 1 && (
            <Box sx={{ display: 'flex', justifyContent: 'center', mt: 2 }}>
              <Pagination
                count={totalPaginas}
                page={pagina}
                onChange={(_, value) => setPagina(value)}
              />
            </Box>
          )}
        </>
      )}
    </Container>
  );
}
