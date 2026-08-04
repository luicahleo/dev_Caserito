import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Container,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import {
  listarSolicitudesKyc,
  obtenerDetalleKyc,
  obtenerImagenKyc,
  aprobarKyc,
  rechazarKyc,
  type EstadoKyc,
  type SolicitudKycResumen,
} from '../api/kyc';

const ESTADOS: EstadoKyc[] = ['Pendiente', 'Aprobada', 'Rechazada'];
const SISTEMA_ID = '00000000-0000-0000-0000-000000000001';

function formatearScore(score: string | number | null | undefined): string {
  if (score === null || score === undefined) return '-';
  const n = typeof score === 'string' ? Number.parseFloat(score) : score;
  if (Number.isNaN(n)) return '-';
  return n.toFixed(1);
}

function formatearResolutor(resueltaPor: string | null | undefined): string {
  if (!resueltaPor) return '-';
  return resueltaPor === SISTEMA_ID ? 'Sistema' : resueltaPor;
}

// Panel de revisión: previsualiza documento y selfie de una solicitud.
function PanelRevision({
  solicitud,
  onCerrar,
}: {
  solicitud: SolicitudKycResumen;
  onCerrar: () => void;
}) {
  const [urlDoc, setUrlDoc] = useState<string | null>(null);
  const [urlSelfie, setUrlSelfie] = useState<string | null>(null);
  const [errorImg, setErrorImg] = useState(false);
  const [motivo, setMotivo] = useState('');
  const queryClient = useQueryClient();
  const detalle = useQuery({
    queryKey: ['admin', 'kyc', solicitud.solicitudId, 'detalle'],
    queryFn: () => obtenerDetalleKyc(solicitud.solicitudId),
  });
  const resolver = useMutation({
    mutationFn: (accion: 'aprobar' | 'rechazar') => accion === 'aprobar'
      ? aprobarKyc(solicitud.solicitudId)
      : rechazarKyc(solicitud.solicitudId, motivo),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin', 'kyc'] });
      onCerrar();
    },
  });

  useEffect(() => {
    let doc: string | null = null;
    let self: string | null = null;
    let activo = true;
    (async () => {
      try {
        const d = await obtenerImagenKyc(solicitud.solicitudId, 'documento');
        if (!activo) {
          URL.revokeObjectURL(d);
          return;
        }
        doc = d;
        setUrlDoc(d);
        const s = await obtenerImagenKyc(solicitud.solicitudId, 'selfie');
        if (!activo) {
          URL.revokeObjectURL(s);
          return;
        }
        self = s;
        setUrlSelfie(s);
      } catch {
        if (activo) setErrorImg(true);
      }
    })();
    return () => {
      activo = false;
      if (doc) URL.revokeObjectURL(doc);
      if (self) URL.revokeObjectURL(self);
    };
  }, [solicitud.solicitudId]);

  return (
    <Dialog open onClose={onCerrar} maxWidth="md" fullWidth>
      <DialogTitle>Revisar solicitud</DialogTitle>
      <DialogContent>
        {detalle.isError && <Alert severity="error">No se pudo cargar el detalle documental.</Alert>}
        {detalle.data && <Stack spacing={0.5} sx={{ mb: 2 }}>
          <Typography><strong>Persona:</strong> {detalle.data.nombres} {detalle.data.apellidos}</Typography>
          <Typography><strong>CI:</strong> {detalle.data.numeroCi}{detalle.data.complementoCi ? `-${detalle.data.complementoCi}` : ''}</Typography>
          <Typography><strong>Expedido en:</strong> {detalle.data.departamentoExpedicion}</Typography>
        </Stack>}
        {errorImg && <Alert severity="error">No se pudieron cargar las imágenes.</Alert>}
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ my: 1 }}>
          <Box sx={{ flex: 1 }}>
            <Typography variant="subtitle2">Documento</Typography>
            {urlDoc ? (
              <Box component="img" src={urlDoc} alt="Documento" sx={{ maxWidth: '100%' }} />
            ) : (
              <CircularProgress size={24} />
            )}
          </Box>
          <Box sx={{ flex: 1 }}>
            <Typography variant="subtitle2">Selfie</Typography>
            {urlSelfie ? (
              <Box component="img" src={urlSelfie} alt="Selfie" sx={{ maxWidth: '100%' }} />
            ) : (
              <CircularProgress size={24} />
            )}
          </Box>
        </Stack>

      </DialogContent>
      <DialogActions>
        <Button onClick={onCerrar}>Cerrar</Button>
        {solicitud.estado === 'Pendiente' ? (
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
            <TextField size="small" label="Motivo de rechazo" value={motivo}
              onChange={(e) => setMotivo(e.target.value)} />
            <Button color="error" disabled={!motivo || resolver.isPending}
              onClick={() => resolver.mutate('rechazar')}>Rechazar</Button>
            <Button variant="contained" disabled={resolver.isPending}
              onClick={() => resolver.mutate('aprobar')}>Aprobar</Button>
          </Stack>
        ) : (
          <Typography variant="body2" color="text.secondary" sx={{ mr: 2 }}>
            Esta solicitud ya fue resuelta.
          </Typography>
        )}
      </DialogActions>
    </Dialog>
  );
}

export function AdminKycPage() {
  const [filtro, setFiltro] = useState<EstadoKyc>('Pendiente');
  const [seleccion, setSeleccion] = useState<SolicitudKycResumen | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'kyc', filtro],
    queryFn: () => listarSolicitudesKyc(filtro),
  });

  return (
    <Container maxWidth="md" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Verificaciones de identidad
      </Typography>

      <TextField
        select
        label="Estado"
        value={filtro}
        onChange={(e) => setFiltro(e.target.value as EstadoKyc)}
        sx={{ minWidth: 200, my: 2 }}
      >
        {ESTADOS.map((e) => (
          <MenuItem key={e} value={e}>
            {e}
          </MenuItem>
        ))}
      </TextField>

      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
          <CircularProgress />
        </Box>
      ) : (data?.items.length ?? 0) === 0 ? (
        <Typography color="text.secondary">No hay solicitudes en este estado.</Typography>
      ) : (
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Usuario</TableCell>
              <TableCell>Documento</TableCell>
              <TableCell>Enviada</TableCell>
              <TableCell>Estado</TableCell>
              <TableCell>Score</TableCell>
              <TableCell>Resolutor</TableCell>
              <TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {data?.items.map((s) => (
              <TableRow key={s.solicitudId}>
                <TableCell>{s.usuarioId}</TableCell>
                <TableCell>{s.tipoDocumento}</TableCell>
                <TableCell>{new Date(s.enviadaEn).toLocaleDateString()}</TableCell>
                <TableCell>{s.estado}</TableCell>
                <TableCell>{formatearScore(s.scoreSimilitud)}</TableCell>
                <TableCell>{formatearResolutor(s.resueltaPor)}</TableCell>
                <TableCell>
                  <Button size="small" onClick={() => setSeleccion(s)}>
                    Revisar
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      {seleccion && (
        <PanelRevision
          solicitud={seleccion}
          onCerrar={() => setSeleccion(null)}
        />
      )}
    </Container>
  );
}
