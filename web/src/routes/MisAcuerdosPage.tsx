import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Box, Button, Chip, CircularProgress, Container, Dialog, DialogActions,
  DialogContent, DialogTitle, List, ListItemButton, ListItemText, Tab, Tabs,
  Typography,
} from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import { cancelarOrden, listarOrdenes, type OrdenResumen, type RolOrden } from '../api/orders';
import { formatearBob } from '../lib/formato';

function estadoVisible(estado: string) {
  if (estado === 'Agreed') return 'Acordado';
  if (estado === 'Cancelled') return 'Cancelado';
  return 'Solicitado';
}

function etiquetaAccion(estado: string, rol: RolOrden): string | null {
  if (estado === 'Requested') return rol === 'comprador' ? 'Cancelar solicitud' : 'Rechazar solicitud';
  if (estado === 'Agreed') return 'Cancelar acuerdo';
  return null;
}

export function MisAcuerdosPage() {
  const [rol, setRol] = useState<RolOrden>('comprador');
  const [aCancelar, setACancelar] = useState<OrdenResumen | null>(null);
  const qc = useQueryClient();
  const { data, isLoading, error } = useQuery({
    queryKey: ['orders', rol],
    queryFn: () => listarOrdenes(rol),
  });

  const cancelar = useMutation({
    mutationFn: (id: string) => cancelarOrden(id),
    onSuccess: async () => {
      setACancelar(null);
      await qc.invalidateQueries({ queryKey: ['orders'] });
    },
  });

  return (
    <Container maxWidth="md" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>Mis acuerdos</Typography>
      <Tabs value={rol} onChange={(_, valor: RolOrden) => setRol(valor)} sx={{ mb: 2 }}>
        <Tab value="comprador" label="Compras" />
        <Tab value="vendedor" label="Ventas" />
      </Tabs>
      {isLoading && <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}><CircularProgress /></Box>}
      {error && <Alert severity="error">No se pudieron cargar tus acuerdos.</Alert>}
      {!isLoading && !error && data?.items.length === 0 && (
        <Typography>No tienes acuerdos en esta sección.</Typography>
      )}
      <List disablePadding>
        {data?.items.map((orden) => {
          const etiqueta = etiquetaAccion(orden.estado, rol);
          return (
            <Box key={orden.id} sx={{ borderBottom: '1px solid', borderColor: 'divider' }}>
              <ListItemButton component={RouterLink} to={`/acuerdos/${orden.id}`}>
                <ListItemText
                  primary={formatearBob(Number(orden.montoAcordado))}
                  secondary={`Actualizado ${new Date(orden.actualizadaEn).toLocaleString('es-BO')}`}
                />
                <Chip label={estadoVisible(orden.estado)} />
              </ListItemButton>
              {etiqueta && (
                <Box sx={{ display: 'flex', justifyContent: 'flex-end', px: 2, pb: 1 }}>
                  <Button size="small" onClick={() => setACancelar(orden)}>
                    {etiqueta}
                  </Button>
                </Box>
              )}
            </Box>
          );
        })}
      </List>

      {cancelar.isError && (
        <Alert severity="error" sx={{ mt: 2 }}>No se pudo procesar la cancelación.</Alert>
      )}

      <Dialog open={aCancelar !== null} onClose={() => setACancelar(null)}>
        <DialogTitle>{aCancelar ? etiquetaAccion(aCancelar.estado, rol) : ''}</DialogTitle>
        <DialogContent>
          <Typography>
            Esta acción cierra el acuerdo y no implica ningún pago ni penalización.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setACancelar(null)}>Cancelar</Button>
          <Button
            color="error"
            variant="contained"
            disabled={cancelar.isPending}
            onClick={() => aCancelar && cancelar.mutate(aCancelar.id)}
          >
            Confirmar
          </Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
}
