import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CardMedia,
  Chip,
  CircularProgress,
  Container,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Pagination,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
  useMediaQuery,
} from '@mui/material';
import { useTheme } from '@mui/material/styles';
import {
  eliminarAviso,
  listarMisAvisos,
  pausarAviso,
  reactivarAviso,
  type AvisoResumen,
} from '../api/avisos';
import { formatearBob } from '../lib/formato';

const TAMANO = 20;

function EstadosAviso({ aviso }: { aviso: AvisoResumen }) {
  return (
    <Stack direction="row" sx={{ flexWrap: 'wrap', gap: 1 }}>
      <Chip
        size="small"
        label={aviso.estado === 'Vendido' ? 'Vendido' : aviso.estado}
        color={aviso.estado === 'Activo' ? 'success' : 'default'}
      />
      {aviso.estadoModeracion !== 'Visible' && (
        <Chip
          size="small"
          label={
            aviso.estadoModeracion === 'Oculto'
              ? 'Oculto por moderación'
              : 'Eliminado por moderación'
          }
          color="warning"
        />
      )}
    </Stack>
  );
}

function AccionesAviso({
  aviso,
  alEditar,
  alPausar,
  alReactivar,
  alEliminar,
  pausando,
  reactivando,
}: {
  aviso: AvisoResumen;
  alEditar: () => void;
  alPausar: () => void;
  alReactivar: () => void;
  alEliminar: () => void;
  pausando: boolean;
  reactivando: boolean;
}) {
  if (aviso.estado === 'Vendido') return null;

  return (
    <Stack direction="row" sx={{ justifyContent: 'flex-end', flexWrap: 'wrap', gap: 1 }}>
      <Button size="small" onClick={alEditar} sx={{ flex: { xs: '1 1 88px', sm: '0 0 auto' } }}>
        Editar
      </Button>
      {aviso.estado === 'Activo' ? (
        <Button
          size="small"
          onClick={alPausar}
          disabled={pausando}
          sx={{ flex: { xs: '1 1 88px', sm: '0 0 auto' } }}
        >
          Pausar
        </Button>
      ) : (
        <Button
          size="small"
          onClick={alReactivar}
          disabled={reactivando}
          sx={{ flex: { xs: '1 1 88px', sm: '0 0 auto' } }}
        >
          Reactivar
        </Button>
      )}
      <Button
        size="small"
        color="error"
        onClick={alEliminar}
        sx={{ flex: { xs: '1 1 88px', sm: '0 0 auto' } }}
      >
        Eliminar
      </Button>
    </Stack>
  );
}

export function MisAvisosPage() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const theme = useTheme();
  const esMovil = useMediaQuery(theme.breakpoints.down('sm'));
  const [pagina, setPagina] = useState(1);
  const [aEliminar, setAEliminar] = useState<AvisoResumen | null>(null);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['mis-avisos', pagina],
    queryFn: () => listarMisAvisos(pagina, TAMANO),
  });

  const refrescar = () => qc.invalidateQueries({ queryKey: ['mis-avisos'] });

  const pausar = useMutation({ mutationFn: (id: string) => pausarAviso(id), onSuccess: refrescar });
  const reactivar = useMutation({
    mutationFn: (id: string) => reactivarAviso(id),
    onSuccess: refrescar,
  });
  const eliminar = useMutation({
    mutationFn: (id: string) => eliminarAviso(id),
    onSuccess: () => {
      setAEliminar(null);
      refrescar();
    },
  });

  // total viene del contrato como number | string (double de OpenAPI); se coacciona antes de dividir.
  const totalPaginas = data ? Math.max(1, Math.ceil(Number(data.total) / TAMANO)) : 1;

  return (
    <Container maxWidth="md" sx={{ py: 4 }}>
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={2}
        sx={{ mb: 3, justifyContent: 'space-between', alignItems: { xs: 'stretch', sm: 'center' } }}
      >
        <Typography variant="h4" component="h1">
          Mis avisos
        </Typography>
        <Button component={RouterLink} to="/publicar" variant="contained">
          Publicar aviso
        </Button>
      </Stack>

      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
          <CircularProgress />
        </Box>
      ) : isError ? (
        <Alert severity="error">No se pudieron cargar tus avisos. Inténtalo más tarde.</Alert>
      ) : (data?.items.length ?? 0) === 0 ? (
        <Typography color="text.secondary">
          Todavía no publicaste avisos. <RouterLink to="/publicar">Publica el primero</RouterLink>.
        </Typography>
      ) : (
        <>
          {esMovil ? (
            <Stack component="ul" aria-label="Mis avisos" spacing={2} sx={{ p: 0, m: 0 }}>
              {data?.items.map((aviso) => (
                <Card component="li" key={aviso.id} variant="outlined" sx={{ listStyle: 'none' }}>
                  {aviso.fotos[0] ? (
                    <CardMedia
                      component="img"
                      image={aviso.fotos[0].url}
                      alt={aviso.titulo}
                      sx={{ height: 160, objectFit: 'cover' }}
                    />
                  ) : (
                    <Box
                      sx={{
                        height: 140,
                        bgcolor: 'grey.200',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                      }}
                    >
                      <Typography variant="caption" color="text.disabled">
                        Sin foto
                      </Typography>
                    </Box>
                  )}
                  <CardContent>
                    <Typography
                      variant="h6"
                      component="h2"
                      sx={{ overflowWrap: 'anywhere', mb: 0.5 }}
                    >
                      {aviso.titulo}
                    </Typography>
                    <Typography variant="h6" color="primary.main" sx={{ mb: 1.5 }}>
                      {formatearBob(aviso.monto)}
                    </Typography>
                    <EstadosAviso aviso={aviso} />
                    <Box sx={{ mt: 2 }}>
                      <AccionesAviso
                        aviso={aviso}
                        alEditar={() => navigate(`/mis-avisos/${aviso.id}/editar`)}
                        alPausar={() => pausar.mutate(aviso.id)}
                        alReactivar={() => reactivar.mutate(aviso.id)}
                        alEliminar={() => setAEliminar(aviso)}
                        pausando={pausar.isPending}
                        reactivando={reactivar.isPending}
                      />
                    </Box>
                  </CardContent>
                </Card>
              ))}
            </Stack>
          ) : (
            <TableContainer component={Paper} variant="outlined">
              <Table aria-label="Mis avisos">
                <TableHead>
                  <TableRow>
                    <TableCell>Título</TableCell>
                    <TableCell>Precio</TableCell>
                    <TableCell>Estado</TableCell>
                    <TableCell align="right">Acciones</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {data?.items.map((aviso) => (
                    <TableRow key={aviso.id}>
                      <TableCell sx={{ overflowWrap: 'anywhere' }}>{aviso.titulo}</TableCell>
                      <TableCell>{formatearBob(aviso.monto)}</TableCell>
                      <TableCell>
                        <EstadosAviso aviso={aviso} />
                      </TableCell>
                      <TableCell align="right">
                        <AccionesAviso
                          aviso={aviso}
                          alEditar={() => navigate(`/mis-avisos/${aviso.id}/editar`)}
                          alPausar={() => pausar.mutate(aviso.id)}
                          alReactivar={() => reactivar.mutate(aviso.id)}
                          alEliminar={() => setAEliminar(aviso)}
                          pausando={pausar.isPending}
                          reactivando={reactivar.isPending}
                        />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}

          <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
            <Pagination
              count={totalPaginas}
              page={pagina}
              siblingCount={0}
              boundaryCount={1}
              size={esMovil ? 'small' : 'medium'}
              onChange={(_, p) => setPagina(p)}
            />
          </Box>
        </>
      )}

      <Dialog open={aEliminar !== null} onClose={() => setAEliminar(null)}>
        <DialogTitle>Eliminar aviso</DialogTitle>
        <DialogContent>
          <Typography>
            ¿Seguro que quieres eliminar «{aEliminar?.titulo}»? Esta acción no se puede deshacer.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAEliminar(null)}>Cancelar</Button>
          <Button
            color="error"
            variant="contained"
            disabled={eliminar.isPending}
            onClick={() => aEliminar && eliminar.mutate(aEliminar.id)}
          >
            Confirmar
          </Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
}
