import { useEffect, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link as RouterLink, useParams } from 'react-router-dom';
import {
  Alert, Box, Button, Chip, CircularProgress, Container, Dialog, DialogActions,
  DialogContent, DialogTitle, MenuItem, Paper, Stack, TextField, Typography,
} from '@mui/material';
import {
  bloquearParticipante, cerrarConversacion, crearReporteChat, desbloquearParticipante,
  enviarMensaje, listarConversaciones, marcarLectura, obtenerMensajes,
  reabrirConversacion, recuperarMensajes, type CrearReporteChatRequest,
} from '../api/chat';
import type { MensajeChat } from '../chat/sincronizacionMensajes';
import { crearClienteTiempoReal } from '../chat/tiempoReal';
import { getAccessToken } from '../auth/session';
import { useAuth } from '../auth/AuthContext';

function combinar(actuales: readonly MensajeChat[], nuevos: readonly MensajeChat[]) {
  const mapa = new Map(actuales.map((mensaje) => [mensaje.id, mensaje]));
  nuevos.forEach((mensaje) => mapa.set(mensaje.id, mensaje));
  return [...mapa.values()].sort((a, b) => a.secuencia - b.secuencia);
}

const categorias = [
  [1, 'Acoso'], [2, 'Estafa'], [3, 'Contenido prohibido'], [4, 'Spam'],
  [5, 'Suplantación'], [6, 'Salida de la plataforma'], [7, 'Otro'],
] as const;

async function recuperarTodo(
  conversacionId: string,
  recibir: (mensajes: MensajeChat[]) => void,
  desde = 0,
): Promise<void> {
  const lote = await recuperarMensajes(conversacionId, desde, 100);
  recibir(lote);
  const ultima = lote.at(-1)?.secuencia;
  if (lote.length === 100 && ultima !== undefined) {
    await recuperarTodo(conversacionId, recibir, ultima);
  }
}

export function ConversacionPage() {
  const { id = '' } = useParams();
  const { usuario } = useAuth();
  const queryClient = useQueryClient();
  const [mensajesVivos, setMensajesVivos] = useState<MensajeChat[]>([]);
  const [mensajesAnteriores, setMensajesAnteriores] = useState<MensajeChat[]>([]);
  const [cursorCargado, setCursorCargado] = useState<string | null | undefined>();
  const [texto, setTexto] = useState('');
  const claveRef = useRef(crypto.randomUUID());
  const [errorTiempoReal, setErrorTiempoReal] = useState(false);
  const [reporte, setReporte] = useState<{ tipo: 1 | 2 | 3; mensajeId?: string } | null>(null);
  const [categoria, setCategoria] = useState(1);
  const [detalle, setDetalle] = useState('');

  const conversaciones = useQuery({
    queryKey: ['chat-conversaciones', 50],
    queryFn: () => listarConversaciones(undefined, 50),
  });
  const conversacion = conversaciones.data?.items.find((item) => item.id === id);
  const historial = useQuery({
    queryKey: ['chat-mensajes', id],
    queryFn: () => obtenerMensajes(id),
    enabled: Boolean(conversacion),
  });

  const mensajes = combinar(
    combinar(historial.data?.items ?? [], mensajesAnteriores),
    mensajesVivos,
  );
  const cursor = cursorCargado === undefined ? historial.data?.siguienteCursor : cursorCargado;
  const ultimaSecuencia = mensajes.at(-1)?.secuencia ?? 0;
  useEffect(() => {
    if (!conversacion || ultimaSecuencia === 0) return;
    void marcarLectura(id, ultimaSecuencia)
      .then(async () => {
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: ['chat-conversaciones'] }),
          queryClient.invalidateQueries({ queryKey: ['chat-bandeja'] }),
        ]);
      })
      .catch(() => undefined);
  }, [conversacion, id, ultimaSecuencia, queryClient]);

  const cliente = useMemo(() => crearClienteTiempoReal({
    getAccessToken,
    alReconectar: async () => {
      await recuperarTodo(id, (lote) => {
        setMensajesVivos((actuales) => combinar(actuales, lote));
      });
      setErrorTiempoReal(false);
    },
  }), [id]);

  useEffect(() => {
    if (!conversacion || !conversacion.puedeEnviar) return;
    cliente.alRecibirMensaje((mensaje) => {
      setMensajesVivos((actuales) => combinar(actuales, [mensaje]));
      void queryClient.invalidateQueries({ queryKey: ['chat-conversaciones'] });
      void queryClient.invalidateQueries({ queryKey: ['chat-bandeja'] });
    });
    void cliente.suscribir(id).then(() => setErrorTiempoReal(false)).catch(() => setErrorTiempoReal(true));
    return () => { void cliente.desuscribir(id).finally(() => cliente.detener()); };
  }, [cliente, conversacion, id, queryClient]);

  const enviar = useMutation({
    mutationFn: () => enviarMensaje(id, claveRef.current, texto),
    onSuccess: (mensaje) => {
      setMensajesVivos((actuales) => combinar(actuales, [mensaje]));
      setTexto('');
      claveRef.current = crypto.randomUUID();
      void queryClient.invalidateQueries({ queryKey: ['chat-conversaciones'] });
      void queryClient.invalidateQueries({ queryKey: ['chat-bandeja'] });
    },
  });
  const accion = useMutation({
    mutationFn: async (tipo: 'cerrar' | 'reabrir' | 'bloquear' | 'desbloquear') => {
      if (tipo === 'cerrar') await cerrarConversacion(id);
      if (tipo === 'reabrir') await reabrirConversacion(id);
      if (tipo === 'bloquear') await bloquearParticipante(id);
      if (tipo === 'desbloquear') await desbloquearParticipante(id);
    },
    onSuccess: () => {
      cliente.revocar(id);
      void queryClient.invalidateQueries({ queryKey: ['chat-conversaciones'] });
      void queryClient.invalidateQueries({ queryKey: ['chat-bandeja'] });
    },
  });
  const reportar = useMutation({
    mutationFn: () => {
      if (!reporte) return Promise.resolve();
      const body: CrearReporteChatRequest = {
        tipoObjetivo: reporte.tipo,
        mensajeId: reporte.mensajeId ?? null,
        categoria,
        detalle: detalle.trim() || null,
      };
      return crearReporteChat(id, body);
    },
    onSuccess: () => { setReporte(null); setDetalle(''); },
  });

  if (conversaciones.isLoading || (conversacion && historial.isLoading)) {
    return <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}><CircularProgress /></Box>;
  }
  if (conversaciones.error || historial.error || !conversacion) {
    return <Container maxWidth="sm" sx={{ py: 4 }}><Alert severity="error">No se pudo cargar la conversación.</Alert><Button component={RouterLink} to="/mensajes" sx={{ mt: 2 }}>Volver a mensajes</Button></Container>;
  }

  const puedeEnviar = conversacion.puedeEnviar;
  return (
    <Container maxWidth="md" sx={{ py: { xs: 1, sm: 3 } }}>
      <Stack direction="row" sx={{ mb: 2, gap: 1, justifyContent: 'space-between', alignItems: 'center' }}>
        <Box>
          <Button component={RouterLink} to="/mensajes">← Mensajes</Button>
          <Typography variant="h5" component="h1">{conversacion.rol === 'Comprador' ? 'Conversación con el vendedor' : 'Conversación con el comprador'}</Typography>
        </Box>
        <Chip label={puedeEnviar ? 'Activa' : 'Envío no disponible'} color={puedeEnviar ? 'success' : 'default'} />
      </Stack>
      {errorTiempoReal && <Alert severity="warning" sx={{ mb: 2 }}>Conexión en tiempo real interrumpida. Recuperaremos los mensajes al reconectar.</Alert>}
      {!puedeEnviar && <Alert severity="info" sx={{ mb: 2 }}>Esta conversación no admite nuevos mensajes.</Alert>}
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mb: 2 }}>
        {cursor && <Button onClick={() => void obtenerMensajes(id, cursor).then((pagina) => { setMensajesAnteriores((actuales) => combinar(pagina.items, actuales)); setCursorCargado(pagina.siguienteCursor); })}>Cargar mensajes anteriores</Button>}
        {conversacion.estado === 1 ? <Button onClick={() => accion.mutate('reabrir')}>Reabrir conversación</Button> : <Button onClick={() => accion.mutate('cerrar')} disabled={conversacion.estado === 2}>Cerrar conversación</Button>}
        <Button color="warning" onClick={() => accion.mutate('bloquear')}>Bloquear contraparte</Button>
        <Button onClick={() => accion.mutate('desbloquear')}>Desbloquear contraparte</Button>
        <Button color="error" onClick={() => setReporte({ tipo: 1 })}>Reportar conversación</Button>
        <Button color="error" onClick={() => setReporte({ tipo: 3 })}>Reportar contraparte</Button>
      </Stack>
      {accion.isError && <Alert severity="error">No se pudo completar la acción.</Alert>}
      <Paper variant="outlined" sx={{ p: { xs: 1, sm: 2 }, minHeight: 280, maxHeight: '55vh', overflowY: 'auto' }}>
        {mensajes.length === 0 && <Typography color="text.secondary">Aún no hay mensajes.</Typography>}
        <Stack spacing={1}>
          {mensajes.map((mensaje) => {
            const propio = mensaje.remitenteId === usuario?.id;
            return (
              <Box key={mensaje.id} sx={{ alignSelf: propio ? 'flex-end' : 'flex-start', maxWidth: '85%' }}>
                <Paper sx={{ px: 1.5, py: 1, bgcolor: propio ? 'primary.light' : 'grey.100' }}>
                  <Typography variant="caption">{propio ? 'Tú' : 'Contraparte'}</Typography>
                  <Typography sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{mensaje.texto}</Typography>
                </Paper>
                <Button size="small" color="error" onClick={() => setReporte({ tipo: 2, mensajeId: mensaje.id })}>Reportar mensaje</Button>
              </Box>
            );
          })}
        </Stack>
      </Paper>
      <Stack component="form" direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mt: 2 }} onSubmit={(evento) => { evento.preventDefault(); if (texto.trim()) enviar.mutate(); }}>
        <TextField fullWidth multiline maxRows={4} label="Mensaje" value={texto} onChange={(evento) => setTexto(evento.target.value)} disabled={!puedeEnviar || enviar.isPending} slotProps={{ htmlInput: { maxLength: 2000 } }} />
        <Button type="submit" variant="contained" disabled={!puedeEnviar || enviar.isPending || !texto.trim()}>Enviar</Button>
      </Stack>
      {enviar.isError && <Alert severity="error" sx={{ mt: 1 }}>No se pudo enviar. Puedes intentarlo nuevamente.</Alert>}
      <Dialog open={reporte !== null} onClose={() => setReporte(null)} fullWidth>
        <DialogTitle>Enviar reporte</DialogTitle>
        <DialogContent>
          <TextField select fullWidth label="Categoría" value={categoria} onChange={(evento) => setCategoria(Number(evento.target.value))} sx={{ mt: 1 }}>
            {categorias.map(([valor, etiqueta]) => <MenuItem key={valor} value={valor}>{etiqueta}</MenuItem>)}
          </TextField>
          <TextField fullWidth multiline label="Detalle opcional" value={detalle} onChange={(evento) => setDetalle(evento.target.value)} slotProps={{ htmlInput: { maxLength: 1000 } }} sx={{ mt: 2 }} />
          {reportar.isError && <Alert severity="error" sx={{ mt: 2 }}>No se pudo enviar el reporte.</Alert>}
        </DialogContent>
        <DialogActions><Button onClick={() => setReporte(null)}>Cancelar</Button><Button variant="contained" onClick={() => reportar.mutate()} disabled={reportar.isPending}>Enviar reporte</Button></DialogActions>
      </Dialog>
    </Container>
  );
}
