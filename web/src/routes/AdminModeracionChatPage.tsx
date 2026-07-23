import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Checkbox,
  CircularProgress,
  Container,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Stack,
  Typography,
} from '@mui/material';
import {
  atenderReporteChat,
  cerrarConversacionModeracion,
  descartarReporteChat,
  listarReportesChat,
  liberarReporteChat,
  obtenerEvidenciaReporteChat,
  reabrirConversacionModeracion,
  tomarReporteChat,
  type ReporteChatCola,
} from '../api/moderacionChat';

const categorias: Record<number, string> = {
  1: 'Acoso',
  2: 'Estafa',
  3: 'Contenido prohibido',
  4: 'Spam',
  5: 'Suplantación',
  6: 'Salida de la plataforma',
  7: 'Otro',
};

const estados: Record<number, string> = {
  1: 'Pendiente',
  2: 'En revisión',
  3: 'Atendido',
  4: 'Descartado',
};

export function AdminModeracionChatPage() {
  const [reporteSeleccionado, setReporteSeleccionado] = useState<ReporteChatCola | null>(null);
  const [cerrarAlAtender, setCerrarAlAtender] = useState(false);
  const queryClient = useQueryClient();
  const cola = useQuery({
    queryKey: ['admin', 'moderacion-chat', 'cola'],
    queryFn: () => listarReportesChat({ limite: 50 }),
  });
  const evidencia = useQuery({
    queryKey: ['admin', 'moderacion-chat', 'evidencia', reporteSeleccionado?.id],
    queryFn: () => obtenerEvidenciaReporteChat(reporteSeleccionado?.id ?? ''),
    enabled: reporteSeleccionado !== null,
  });
  const tomar = useMutation({
    mutationFn: (reporteId: string) => tomarReporteChat(reporteId),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['admin', 'moderacion-chat', 'cola'] }),
  });
  const liberar = useMutation({
    mutationFn: (reporteId: string) => liberarReporteChat(reporteId),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['admin', 'moderacion-chat', 'cola'] }),
  });
  const atender = useMutation({
    mutationFn: (reporteId: string) =>
      atenderReporteChat(reporteId, { cerrarConversacion: cerrarAlAtender }),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['admin', 'moderacion-chat', 'cola'] }),
  });
  const descartar = useMutation({
    mutationFn: (reporteId: string) => descartarReporteChat(reporteId),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['admin', 'moderacion-chat', 'cola'] }),
  });
  const cerrarConversacion = useMutation({
    mutationFn: (reporteId: string) => cerrarConversacionModeracion(reporteId),
  });
  const reabrirConversacion = useMutation({
    mutationFn: (reporteId: string) => reabrirConversacionModeracion(reporteId),
  });
  const cerrarExpediente = () => {
    if (reporteSeleccionado !== null) {
      const queryKey = ['admin', 'moderacion-chat', 'evidencia', reporteSeleccionado.id];
      void queryClient.cancelQueries({ queryKey });
      queryClient.removeQueries({ queryKey, exact: true });
    }
    setCerrarAlAtender(false);
    setReporteSeleccionado(null);
  };

  return (
    <Container maxWidth="md" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1">
        Moderación de chat
      </Typography>
      {cola.isLoading && <CircularProgress />}
      {cola.isError && <Alert severity="error">No se pudo cargar la cola.</Alert>}
      {cola.data?.map((reporte) => (
        <Stack
          key={reporte.id}
          direction={{ xs: 'column', sm: 'row' }}
          spacing={2}
          sx={{ py: 2, borderBottom: 1, borderColor: 'divider', alignItems: 'center' }}
        >
          <Stack sx={{ flexGrow: 1 }}>
            <Typography>{categorias[reporte.categoria] ?? 'Categoría desconocida'}</Typography>
            <Typography color="text.secondary">
              {estados[reporte.estado] ?? 'Estado desconocido'} ·{' '}
              {new Date(reporte.creadoEn).toLocaleDateString('es-ES')}
            </Typography>
          </Stack>
          <Button onClick={() => setReporteSeleccionado(reporte)}>Revisar expediente</Button>
        </Stack>
      ))}
      {cola.data?.length === 0 && <Typography>No hay reportes en la cola.</Typography>}
      <Dialog
        open={reporteSeleccionado !== null}
        onClose={cerrarExpediente}
        fullWidth
      >
        <DialogTitle>Expediente de moderación</DialogTitle>
        <DialogContent>
          {evidencia.isLoading && <CircularProgress />}
          {evidencia.isError && (
            <Alert severity="error">No se pudo cargar la evidencia.</Alert>
          )}
          {evidencia.data?.detalle && (
            <Typography sx={{ whiteSpace: 'pre-wrap' }}>{evidencia.data.detalle}</Typography>
          )}
          {evidencia.data && (
            <Stack spacing={2} sx={{ mt: 2 }}>
              <Typography>Reportante: {evidencia.data.rolReportante}</Typography>
              <Typography>Objetivo: {evidencia.data.rolObjetivo}</Typography>
              {evidencia.data.mensajes.map((mensaje) => (
                <Stack
                  key={mensaje.id}
                  sx={{ p: 2, border: 1, borderColor: 'divider', borderRadius: 1 }}
                >
                  <Typography variant="body2" color="text.secondary">
                    {mensaje.autorRol}
                    {mensaje.esObjetivo ? ' · Mensaje señalado' : ''}
                  </Typography>
                  <Typography sx={{ whiteSpace: 'pre-wrap' }}>{mensaje.texto}</Typography>
                </Stack>
              ))}
            </Stack>
          )}
          {reporteSeleccionado?.estado === 2 && (
            <FormControlLabel
              control={
                <Checkbox
                  checked={cerrarAlAtender}
                  onChange={(event) => setCerrarAlAtender(event.target.checked)}
                />
              }
              label="Cerrar la conversación al atender"
            />
          )}
        </DialogContent>
        <DialogActions>
          {reporteSeleccionado?.estado === 1 && (
            <Button
              onClick={() => tomar.mutate(reporteSeleccionado.id)}
              disabled={tomar.isPending}
            >
              Tomar reporte
            </Button>
          )}
          {reporteSeleccionado?.estado === 2 && (
            <>
              <Button
                onClick={() => liberar.mutate(reporteSeleccionado.id)}
                disabled={liberar.isPending}
              >
                Liberar reporte
              </Button>
              <Button
                onClick={() => atender.mutate(reporteSeleccionado.id)}
                disabled={atender.isPending}
              >
                Atender reporte
              </Button>
              <Button
                color="error"
                onClick={() => descartar.mutate(reporteSeleccionado.id)}
                disabled={descartar.isPending}
              >
                Descartar reporte
              </Button>
            </>
          )}
          {reporteSeleccionado !== null && (
            <>
              <Button
                onClick={() => cerrarConversacion.mutate(reporteSeleccionado.id)}
                disabled={cerrarConversacion.isPending}
              >
                Cerrar conversación
              </Button>
              <Button
                onClick={() => reabrirConversacion.mutate(reporteSeleccionado.id)}
                disabled={reabrirConversacion.isPending}
              >
                Reabrir conversación
              </Button>
            </>
          )}
          <Button onClick={cerrarExpediente}>Cerrar</Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
}
