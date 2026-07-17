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
  aprobarKyc,
  listarSolicitudesKyc,
  obtenerImagenKyc,
  rechazarKyc,
  type EstadoKyc,
  type SolicitudKycResumen,
} from '../api/kyc';

const ESTADOS: EstadoKyc[] = ['Pendiente', 'Aprobada', 'Rechazada'];

// Panel de revisión: previsualiza documento y selfie de una solicitud y permite resolver.
function PanelRevision({
  solicitud,
  onCerrar,
  onResuelta,
}: {
  solicitud: SolicitudKycResumen;
  onCerrar: () => void;
  onResuelta: () => void;
}) {
  const [urlDoc, setUrlDoc] = useState<string | null>(null);
  const [urlSelfie, setUrlSelfie] = useState<string | null>(null);
  const [errorImg, setErrorImg] = useState(false);
  const [rechazando, setRechazando] = useState(false);
  const [motivo, setMotivo] = useState('');

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

  const aprobar = useMutation({
    mutationFn: () => aprobarKyc(solicitud.solicitudId),
    onSuccess: () => {
      onResuelta();
      onCerrar();
    },
  });

  const rechazar = useMutation({
    mutationFn: () => rechazarKyc(solicitud.solicitudId, motivo),
    onSuccess: () => {
      onResuelta();
      onCerrar();
    },
  });

  return (
    <Dialog open onClose={onCerrar} maxWidth="md" fullWidth>
      <DialogTitle>Revisar solicitud</DialogTitle>
      <DialogContent>
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

        {rechazando && (
          <TextField
            label="Motivo"
            fullWidth
            multiline
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
            sx={{ mt: 2 }}
          />
        )}
        {(aprobar.isError || rechazar.isError) && (
          <Alert severity="error" sx={{ mt: 2 }}>
            No se pudo resolver la solicitud. Inténtalo de nuevo.
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onCerrar}>Cerrar</Button>
        {!rechazando ? (
          <>
            <Button color="error" onClick={() => setRechazando(true)}>
              Rechazar
            </Button>
            <Button
              variant="contained"
              color="success"
              onClick={() => aprobar.mutate()}
              disabled={aprobar.isPending}
            >
              Aprobar
            </Button>
          </>
        ) : (
          <Button
            variant="contained"
            color="error"
            onClick={() => rechazar.mutate()}
            disabled={motivo.trim().length === 0 || rechazar.isPending}
          >
            Confirmar rechazo
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
}

export function AdminKycPage() {
  const queryClient = useQueryClient();
  const [filtro, setFiltro] = useState<EstadoKyc>('Pendiente');
  const [seleccion, setSeleccion] = useState<SolicitudKycResumen | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'kyc', filtro],
    queryFn: () => listarSolicitudesKyc(filtro),
  });

  const refrescar = () => queryClient.invalidateQueries({ queryKey: ['admin', 'kyc'] });

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
          onResuelta={refrescar}
        />
      )}
    </Container>
  );
}
