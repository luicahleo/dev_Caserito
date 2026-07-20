import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
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
  Pagination,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import {
  eliminarAviso,
  listarMisAvisos,
  pausarAviso,
  reactivarAviso,
  type AvisoResumen,
} from '../api/avisos';
import { formatearBob } from '../lib/formato';

const TAMANO = 20;

export function MisAvisosPage() {
  const qc = useQueryClient();
  const navigate = useNavigate();
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
      <Stack direction="row" sx={{ mb: 2, justifyContent: 'space-between', alignItems: 'center' }}>
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
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Título</TableCell>
                <TableCell>Precio</TableCell>
                <TableCell>Estado</TableCell>
                <TableCell align="right">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data?.items.map((a) => (
                <TableRow key={a.id}>
                  <TableCell>{a.titulo}</TableCell>
                  <TableCell>{formatearBob(a.monto)}</TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      label={a.estado}
                      color={a.estado === 'Activo' ? 'success' : 'default'}
                    />
                    {a.estadoModeracion !== 'Visible' && (
                      <Chip
                        size="small"
                        label={
                          a.estadoModeracion === 'Oculto'
                            ? 'Oculto por moderación'
                            : 'Eliminado por moderación'
                        }
                        color="warning"
                        sx={{ ml: 1 }}
                      />
                    )}
                  </TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end' }}>
                      <Button size="small" onClick={() => navigate(`/mis-avisos/${a.id}/editar`)}>
                        Editar
                      </Button>
                      {a.estado === 'Activo' ? (
                        <Button
                          size="small"
                          onClick={() => pausar.mutate(a.id)}
                          disabled={pausar.isPending}
                        >
                          Pausar
                        </Button>
                      ) : (
                        <Button
                          size="small"
                          onClick={() => reactivar.mutate(a.id)}
                          disabled={reactivar.isPending}
                        >
                          Reactivar
                        </Button>
                      )}
                      <Button size="small" color="error" onClick={() => setAEliminar(a)}>
                        Eliminar
                      </Button>
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
            <Pagination count={totalPaginas} page={pagina} onChange={(_, p) => setPagina(p)} />
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
