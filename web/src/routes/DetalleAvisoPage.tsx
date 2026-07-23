import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Container,
  Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, TextField,
  Stack,
  Typography,
} from '@mui/material';
import { obtenerAvisoPublico, obtenerMiAviso, reportarAviso } from '../api/avisos';
import { iniciarConversacion } from '../api/chat';
import { useAuth } from '../auth/AuthContext';
import { formatearBob } from '../lib/formato';
import { HttpError } from '../api/http';

export function DetalleAvisoPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const { estaAutenticado } = useAuth();
  const [selectedIdx, setSelectedIdx] = useState(0);
  const [reporteAbierto, setReporteAbierto] = useState(false);
  const [motivo, setMotivo] = useState('EstafaOEngano');
  const [detalle, setDetalle] = useState('');
  const reporte = useMutation({ mutationFn: () => reportarAviso(id, { motivo, detalle: detalle.trim() || null }), onSuccess: () => setReporteAbierto(false) });
  const contacto = useMutation({
    mutationFn: () => iniciarConversacion(id),
    onSuccess: (conversacion) => navigate(`/mensajes/${conversacion.id}`),
  });
  const propiedad = useQuery({
    queryKey: ['mi-aviso', id],
    queryFn: () => obtenerMiAviso(id),
    enabled: estaAutenticado,
    retry: false,
  });
  const { data, isLoading, error } = useQuery({
    queryKey: ['aviso-publico', id],
    queryFn: () => obtenerAvisoPublico(id),
  });
  const esAvisoAjeno =
    estaAutenticado &&
    propiedad.error instanceof HttpError &&
    propiedad.error.status === 404;

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error || !data) {
    const noDisponible = error instanceof HttpError && error.status === 404;
    return (
      <Container maxWidth="sm" sx={{ py: 4 }}>
        <Alert severity={noDisponible ? 'info' : 'error'}>
          {noDisponible
            ? 'Este aviso no está disponible.'
            : 'No se pudo cargar el aviso. Inténtalo más tarde.'}
        </Alert>
        <Button component={RouterLink} to="/" sx={{ mt: 2 }}>
          Volver a explorar
        </Button>
      </Container>
    );
  }

  return (
    <Container maxWidth="md" sx={{ py: 4 }}>
      <Button component={RouterLink} to="/" sx={{ mb: 2 }}>
        ← Volver a explorar
      </Button>
      {/* Galería de fotos */}
      {data.fotos && data.fotos.length > 0 ? (
        <Box sx={{ mb: 3 }}>
          <Box
            component="img"
            src={data.fotos[selectedIdx]?.url ?? data.fotos[0].url}
            alt={data.titulo}
            sx={{ width: '100%', maxHeight: 320, objectFit: 'cover', borderRadius: 1 }}
          />
          {data.fotos.length > 1 && (
            <Stack direction="row" spacing={1} sx={{ mt: 1, flexWrap: 'wrap' }}>
              {data.fotos.map((f, i) => (
                <Box
                  key={f.id}
                  component="img"
                  src={f.url}
                  alt={`Foto ${i + 1}`}
                  onClick={() => setSelectedIdx(i)}
                  sx={{
                    width: 64, height: 64, objectFit: 'cover', borderRadius: 0.5,
                    cursor: 'pointer',
                    border: i === selectedIdx ? '2px solid' : '2px solid transparent',
                    borderColor: i === selectedIdx ? 'primary.main' : 'transparent',
                  }}
                />
              ))}
            </Stack>
          )}
        </Box>
      ) : (
        <Box
          sx={{
            height: 260, bgcolor: 'grey.200', mb: 3,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
          }}
        >
          <Typography variant="caption" color="text.disabled">Sin fotos</Typography>
        </Box>
      )}
      <Typography variant="h4" component="h1" gutterBottom>
        {data.titulo}
      </Typography>
      <Typography variant="h5" color="primary" gutterBottom>
        {formatearBob(data.monto)}
      </Typography>
      <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
        <Chip label={data.nombreCategoria} />
        <Chip label={data.nombreCiudad} />
        <Chip label={data.condicion} />
      </Stack>
      <Typography variant="body1" sx={{ whiteSpace: 'pre-wrap' }}>
        {data.descripcion}
      </Typography>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mt: 3 }}>
        {esAvisoAjeno && (
          <Button
            variant="contained"
            onClick={() => contacto.mutate()}
            disabled={contacto.isPending}
          >
            {contacto.isPending ? 'Abriendo conversación…' : 'Contactar al vendedor'}
          </Button>
        )}
        {!estaAutenticado && (
          <Button component={RouterLink} to="/login" state={{ from: `/avisos/${id}` }}>
            Inicia sesión para contactar
          </Button>
        )}
      </Stack>
      {contacto.isError && (
        <Alert severity="error" sx={{ mt: 2 }}>
          No se pudo abrir la conversación. Inténtalo nuevamente.
        </Alert>
      )}
      {estaAutenticado ? <Button color="error" onClick={() => setReporteAbierto(true)} sx={{ mt: 3 }}>Reportar aviso</Button> : <Button component={RouterLink} to="/login" state={{ from: `/avisos/${id}` }} sx={{ mt: 3 }}>Inicia sesión para reportar</Button>}
      <Dialog open={reporteAbierto} onClose={() => setReporteAbierto(false)} fullWidth>
        <DialogTitle>Reportar aviso</DialogTitle>
        <DialogContent>
          <TextField select fullWidth label="Motivo" value={motivo} onChange={(e) => setMotivo(e.target.value)} sx={{ mt: 1 }}>
            <MenuItem value="EstafaOEngano">Estafa o engaño</MenuItem><MenuItem value="ProductoProhibido">Producto prohibido</MenuItem><MenuItem value="ContenidoInapropiado">Contenido inapropiado</MenuItem><MenuItem value="DuplicadoOSpam">Duplicado o spam</MenuItem><MenuItem value="Otro">Otro</MenuItem>
          </TextField>
          <TextField fullWidth multiline label="Detalle opcional" value={detalle} onChange={(e) => setDetalle(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }} sx={{ mt: 2 }} />
          {reporte.isError && <Alert severity="error" sx={{ mt: 2 }}>No se pudo enviar el reporte.</Alert>}
        </DialogContent>
        <DialogActions><Button onClick={() => setReporteAbierto(false)}>Cancelar</Button><Button variant="contained" onClick={() => reporte.mutate()} disabled={reporte.isPending}>Enviar</Button></DialogActions>
      </Dialog>
    </Container>
  );
}
