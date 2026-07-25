import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Box, Button, Chip, CircularProgress, Container, Dialog, DialogActions,
  DialogContent, DialogTitle, Stack, Typography,
} from '@mui/material';
import { Link as RouterLink, useParams } from 'react-router-dom';
import { aceptarOrden, confirmarCierreOrden, obtenerOrden } from '../api/orders';
import { obtenerAvisoPublico } from '../api/avisos';
import { formatearBob } from '../lib/formato';

export function DetalleAcuerdoPage() {
  const { id = '' } = useParams();
  const [confirmando, setConfirmando] = useState(false);
  const cliente = useQueryClient();
  const orden = useQuery({
    queryKey: ['order', id],
    queryFn: () => obtenerOrden(id),
    retry: false,
  });
  const aviso = useQuery({
    queryKey: ['aviso-publico', orden.data?.avisoId],
    queryFn: () => obtenerAvisoPublico(orden.data!.avisoId),
    enabled: Boolean(orden.data?.avisoId),
    retry: false,
  });
  const aceptar = useMutation({
    mutationFn: () => aceptarOrden(id),
    onSuccess: async () => {
      await cliente.invalidateQueries({ queryKey: ['order', id] });
      await cliente.invalidateQueries({ queryKey: ['orders'] });
    },
  });
  const confirmar = useMutation({
    mutationFn: () => confirmarCierreOrden(id),
    onSuccess: async () => {
      setConfirmando(false);
      await cliente.invalidateQueries({ queryKey: ['order', id] });
      await cliente.invalidateQueries({ queryKey: ['orders'] });
    },
  });

  if (orden.isLoading) {
    return <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}><CircularProgress /></Box>;
  }
  if (orden.error || !orden.data) {
    return <Container maxWidth="sm" sx={{ py: 4 }}><Alert severity="error">El acuerdo no está disponible.</Alert></Container>;
  }

  const data = orden.data;
  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Button component={RouterLink} to="/acuerdos" sx={{ mb: 2 }}>← Mis acuerdos</Button>
      <Typography variant="h4" component="h1" gutterBottom>
        {aviso.data?.titulo ?? 'Acuerdo de compra'}
      </Typography>
      <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
        <Chip
          label={data.estado === 'Agreed' ? 'Acordado'
            : data.estado === 'Cancelled' ? 'Cancelado'
              : data.estado === 'MarkedAsSold' ? 'Marcado como vendido'
                : data.estado === 'Completed' ? 'Completado' : 'Solicitado'}
          color="primary"
        />
        <Chip label={data.rol === 'vendedor' ? 'Venta' : 'Compra'} variant="outlined" />
      </Stack>
      <Typography variant="h5">{formatearBob(Number(data.montoAcordado))}</Typography>
      <Typography sx={{ mt: 2 }}>Creado: {new Date(data.creadaEn).toLocaleString('es-BO')}</Typography>
      <Typography>Actualizado: {new Date(data.actualizadaEn).toLocaleString('es-BO')}</Typography>
      {data.marcadaVendidaEn && (
        <Typography>Marcado como vendido: {new Date(data.marcadaVendidaEn).toLocaleString('es-BO')}</Typography>
      )}
      {data.compradorConfirmoEn && (
        <Typography>Confirmado por comprador: {new Date(data.compradorConfirmoEn).toLocaleString('es-BO')}</Typography>
      )}
      {data.completadaEn && (
        <Typography>Completado: {new Date(data.completadaEn).toLocaleString('es-BO')}</Typography>
      )}
      {data.rol === 'vendedor' && data.estado === 'Requested' && (
        <Button
          variant="contained"
          onClick={() => aceptar.mutate()}
          disabled={aceptar.isPending}
          sx={{ mt: 3 }}
        >
          {aceptar.isPending ? 'Aceptando…' : 'Aceptar acuerdo'}
        </Button>
      )}
      {aceptar.isError && <Alert severity="error" sx={{ mt: 2 }}>No se pudo aceptar el acuerdo.</Alert>}
      {data.estado === 'MarkedAsSold' && data.rol === 'vendedor' && (
        <Alert severity="info" sx={{ mt: 3 }}>Esperando la confirmación del comprador.</Alert>
      )}
      {data.estado === 'MarkedAsSold' && data.rol === 'comprador' && (
        <Button variant="contained" sx={{ mt: 3 }} onClick={() => setConfirmando(true)}>
          Confirmar cierre
        </Button>
      )}
      {data.estado === 'Completed' && (
        <Alert severity="success" sx={{ mt: 3 }}>Completado por ambas partes.</Alert>
      )}
      {confirmar.isError && (
        <Alert severity="error" sx={{ mt: 2 }}>
          No se pudo confirmar el cierre. Inténtalo nuevamente.
        </Alert>
      )}
      <Alert severity="info" sx={{ mt: 3 }}>
        Caserito no verifica el pago ni la entrega.
      </Alert>
      <Dialog open={confirmando} onClose={() => !confirmar.isPending && setConfirmando(false)}>
        <DialogTitle>Confirmar cierre</DialogTitle>
        <DialogContent>
          <Typography>
            Confirma que das por finalizado el acuerdo. Esta acción es irreversible.
            Caserito no verifica el pago ni la entrega.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button disabled={confirmar.isPending} onClick={() => setConfirmando(false)}>Cancelar</Button>
          <Button
            variant="contained"
            disabled={confirmar.isPending}
            onClick={() => confirmar.mutate()}
          >
            Confirmar
          </Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
}
