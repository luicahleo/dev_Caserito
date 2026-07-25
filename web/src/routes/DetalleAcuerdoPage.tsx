import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Box, Button, Chip, CircularProgress, Container, Stack, Typography,
} from '@mui/material';
import { Link as RouterLink, useParams } from 'react-router-dom';
import { aceptarOrden, obtenerOrden } from '../api/orders';
import { obtenerAvisoPublico } from '../api/avisos';
import { formatearBob } from '../lib/formato';

export function DetalleAcuerdoPage() {
  const { id = '' } = useParams();
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
          label={data.estado === 'Agreed' ? 'Acordado' : data.estado === 'Cancelled' ? 'Cancelado' : 'Solicitado'}
          color="primary"
        />
        <Chip label={data.rol === 'vendedor' ? 'Venta' : 'Compra'} variant="outlined" />
      </Stack>
      <Typography variant="h5">{formatearBob(Number(data.montoAcordado))}</Typography>
      <Typography sx={{ mt: 2 }}>Creado: {new Date(data.creadaEn).toLocaleString('es-BO')}</Typography>
      <Typography>Actualizado: {new Date(data.actualizadaEn).toLocaleString('es-BO')}</Typography>
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
      <Alert severity="info" sx={{ mt: 3 }}>
        Este acuerdo no realiza ningún pago ni coordina la entrega.
      </Alert>
    </Container>
  );
}
