import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Box, Button, Chip, CircularProgress, Container, Dialog, DialogActions,
  DialogContent, DialogTitle, List, ListItemButton, ListItemText, Tab, Tabs,
  Typography,
} from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import {
  cancelarOrden, listarOrdenes, marcarOrdenVendida, type OrdenResumen, type RolOrden,
} from '../api/orders';
import { formatearBob } from '../lib/formato';

function estadoVisible(estado: string) {
  if (estado === 'Agreed') return 'Acordado';
  if (estado === 'Cancelled') return 'Cancelado';
  if (estado === 'MarkedAsSold') return 'Marcado como vendido';
  if (estado === 'Completed') return 'Completado';
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
  const [aVender, setAVender] = useState<OrdenResumen | null>(null);
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
  const vender = useMutation({
    mutationFn: (id: string) => marcarOrdenVendida(id),
    onSuccess: async () => {
      const avisoId = aVender?.avisoId;
      setAVender(null);
      await Promise.all([
        qc.invalidateQueries({ queryKey: ['orders'] }),
        qc.invalidateQueries({ queryKey: ['mis-avisos'] }),
        qc.invalidateQueries({ queryKey: ['avisos-publicos'] }),
        qc.invalidateQueries({ queryKey: ['aviso-publico', avisoId] }),
      ]);
    },
  });

  const cerrarDialogo = () => {
    if (!cancelar.isPending) {
      setACancelar(null);
    }
  };

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
                  {rol === 'vendedor' && orden.estado === 'Agreed' && (
                    <Button size="small" variant="contained" onClick={() => setAVender(orden)}>
                      Marcar como vendido
                    </Button>
                  )}
                </Box>
              )}
            </Box>
          );
        })}
      </List>

      {cancelar.isError && (
        <Alert severity="error" sx={{ mt: 2 }}>No se pudo procesar la cancelación.</Alert>
      )}

      <Dialog open={aCancelar !== null} onClose={cerrarDialogo}>
        <DialogTitle>{aCancelar ? etiquetaAccion(aCancelar.estado, rol) : ''}</DialogTitle>
        <DialogContent>
          <Typography>
            Esta acción cierra el acuerdo y no implica ningún pago ni penalización.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button disabled={cancelar.isPending} onClick={cerrarDialogo}>Cancelar</Button>
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
      <Dialog open={aVender !== null} onClose={() => !vender.isPending && setAVender(null)}>
        <DialogTitle>Marcar como vendido</DialogTitle>
        <DialogContent>
          <Typography>
            Esta acción es irreversible y cancelará las demás solicitudes y acuerdos del aviso.
            Caserito no verifica el pago ni la entrega.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button disabled={vender.isPending} onClick={() => setAVender(null)}>Cancelar</Button>
          <Button
            variant="contained"
            disabled={vender.isPending}
            onClick={() => aVender && vender.mutate(aVender.id)}
          >
            Confirmar
          </Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
}
