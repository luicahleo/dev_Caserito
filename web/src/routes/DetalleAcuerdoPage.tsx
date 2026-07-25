import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Container,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { Link as RouterLink, useParams } from 'react-router-dom';
import { aceptarOrden, confirmarCierreOrden, obtenerOrden } from '../api/orders';
import { obtenerAvisoPublico } from '../api/avisos';
import { formatearBob } from '../lib/formato';
import { crearResena, obtenerEstadoResena } from '../api/reputation';

const esquemaResena = z.object({
  puntuacion: z.number().int().min(1, 'Elige una puntuación').max(5),
  comentario: z
    .string()
    .trim()
    .min(10, 'Escribe al menos 10 caracteres')
    .max(500, 'Escribe como máximo 500 caracteres'),
});

type FormularioResena = z.infer<typeof esquemaResena>;

export function DetalleAcuerdoPage() {
  const { id = '' } = useParams();
  const [confirmando, setConfirmando] = useState(false);
  const [confirmandoResena, setConfirmandoResena] = useState(false);
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
  const estadoResena = useQuery({
    queryKey: ['reputation-order', id],
    queryFn: () => obtenerEstadoResena(id),
    enabled: orden.data?.estado === 'Completed',
    retry: false,
  });
  const formulario = useForm<FormularioResena>({
    resolver: zodResolver(esquemaResena),
    defaultValues: { puntuacion: 1, comentario: '' },
  });
  const enviarResena = useMutation({
    mutationFn: (valores: FormularioResena) =>
      crearResena(id, valores.puntuacion, valores.comentario.trim()),
    onSuccess: async () => {
      setConfirmandoResena(false);
      formulario.reset();
      await cliente.invalidateQueries({ queryKey: ['reputation-order', id] });
      if (estadoResena.data?.contraparteId) {
        await cliente.invalidateQueries({
          queryKey: ['public-profile', estadoResena.data.contraparteId],
        });
        await cliente.invalidateQueries({
          queryKey: ['public-reviews', estadoResena.data.contraparteId],
        });
      }
    },
  });

  if (orden.isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }
  if (orden.error || !orden.data) {
    return (
      <Container maxWidth="sm" sx={{ py: 4 }}>
        <Alert severity="error">El acuerdo no está disponible.</Alert>
      </Container>
    );
  }

  const data = orden.data;
  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Button component={RouterLink} to="/acuerdos" sx={{ mb: 2 }}>
        ← Mis acuerdos
      </Button>
      <Typography variant="h4" component="h1" gutterBottom>
        {aviso.data?.titulo ?? 'Acuerdo de compra'}
      </Typography>
      <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
        <Chip
          label={
            data.estado === 'Agreed'
              ? 'Acordado'
              : data.estado === 'Cancelled'
                ? 'Cancelado'
                : data.estado === 'MarkedAsSold'
                  ? 'Marcado como vendido'
                  : data.estado === 'Completed'
                    ? 'Completado'
                    : 'Solicitado'
          }
          color="primary"
        />
        <Chip label={data.rol === 'vendedor' ? 'Venta' : 'Compra'} variant="outlined" />
      </Stack>
      <Typography variant="h5">{formatearBob(Number(data.montoAcordado))}</Typography>
      <Typography sx={{ mt: 2 }}>
        Creado: {new Date(data.creadaEn).toLocaleString('es-BO')}
      </Typography>
      <Typography>Actualizado: {new Date(data.actualizadaEn).toLocaleString('es-BO')}</Typography>
      {data.marcadaVendidaEn && (
        <Typography>
          Marcado como vendido: {new Date(data.marcadaVendidaEn).toLocaleString('es-BO')}
        </Typography>
      )}
      {data.compradorConfirmoEn && (
        <Typography>
          Confirmado por comprador: {new Date(data.compradorConfirmoEn).toLocaleString('es-BO')}
        </Typography>
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
      {aceptar.isError && (
        <Alert severity="error" sx={{ mt: 2 }}>
          No se pudo aceptar el acuerdo.
        </Alert>
      )}
      {data.estado === 'MarkedAsSold' && data.rol === 'vendedor' && (
        <Alert severity="info" sx={{ mt: 3 }}>
          Esperando la confirmación del comprador.
        </Alert>
      )}
      {data.estado === 'MarkedAsSold' && data.rol === 'comprador' && (
        <Button variant="contained" sx={{ mt: 3 }} onClick={() => setConfirmando(true)}>
          Confirmar cierre
        </Button>
      )}
      {data.estado === 'Completed' && (
        <Alert severity="success" sx={{ mt: 3 }}>
          Completado por ambas partes.
        </Alert>
      )}
      {data.estado === 'Completed' && estadoResena.data && (
        <Box sx={{ mt: 3 }}>
          <Button
            component={RouterLink}
            to={`/usuarios/${estadoResena.data.contraparteId}`}
            sx={{ mb: 2 }}
          >
            Ver perfil de la contraparte
          </Button>
          {!estadoResena.data.autorYaCalifico ? (
            <Stack
              component="form"
              spacing={2}
              onSubmit={formulario.handleSubmit(() => setConfirmandoResena(true))}
            >
              <Typography variant="h6">Calificar acuerdo</Typography>
              <Controller
                name="puntuacion"
                control={formulario.control}
                render={({ field, fieldState }) => (
                  <TextField
                    {...field}
                    onChange={(evento) => field.onChange(Number(evento.target.value))}
                    select
                    label="Puntuación"
                    error={Boolean(fieldState.error)}
                    helperText={fieldState.error?.message}
                  >
                    {[1, 2, 3, 4, 5].map((valor) => (
                      <MenuItem key={valor} value={valor}>
                        {valor} de 5
                      </MenuItem>
                    ))}
                  </TextField>
                )}
              />
              <Controller
                name="comentario"
                control={formulario.control}
                render={({ field, fieldState }) => (
                  <TextField
                    {...field}
                    label="Comentario"
                    multiline
                    minRows={3}
                    error={Boolean(fieldState.error)}
                    helperText={fieldState.error?.message}
                    slotProps={{ htmlInput: { maxLength: 500 } }}
                  />
                )}
              />
              <Button type="submit" variant="contained">
                Enviar reseña
              </Button>
            </Stack>
          ) : (
            <Alert severity={estadoResena.data.reveladas ? 'success' : 'info'}>
              {estadoResena.data.reveladas
                ? 'Las reseñas de ambas partes ya están reveladas.'
                : 'Tu reseña fue enviada y espera la calificación de la contraparte.'}
            </Alert>
          )}
        </Box>
      )}
      {data.estado === 'Completed' && (estadoResena.isError || enviarResena.isError) && (
        <Alert severity="error" sx={{ mt: 2 }}>
          No se pudo completar la operación. Inténtalo nuevamente.
        </Alert>
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
            Confirma que das por finalizado el acuerdo. Esta acción es irreversible. Caserito no
            verifica el pago ni la entrega.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button disabled={confirmar.isPending} onClick={() => setConfirmando(false)}>
            Cancelar
          </Button>
          <Button
            variant="contained"
            disabled={confirmar.isPending}
            onClick={() => confirmar.mutate()}
          >
            Confirmar
          </Button>
        </DialogActions>
      </Dialog>
      <Dialog
        open={confirmandoResena}
        onClose={() => !enviarResena.isPending && setConfirmandoResena(false)}
      >
        <DialogTitle>Confirmar reseña</DialogTitle>
        <DialogContent>
          <Typography>
            La reseña no puede editarse ni eliminarse. Permanece oculta hasta que la contraparte
            también califique.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button disabled={enviarResena.isPending} onClick={() => setConfirmandoResena(false)}>
            Cancelar
          </Button>
          <Button
            variant="contained"
            disabled={enviarResena.isPending}
            onClick={() => enviarResena.mutate(formulario.getValues())}
          >
            Confirmar envío
          </Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
}
