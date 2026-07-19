import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, CircularProgress, Container, MenuItem, Stack, TextField, Typography } from '@mui/material';
import { eliminarAvisoModeracion, listarAvisosReportados, ocultarAviso, restaurarAviso, type EstadoReporte } from '../api/moderacion';

export function AdminModeracionPage() {
  const [estado, setEstado] = useState<EstadoReporte>('Pendiente');
  const cliente = useQueryClient();
  const consulta = useQuery({ queryKey: ['admin', 'moderacion', estado], queryFn: () => listarAvisosReportados(estado) });
  const accion = useMutation({
    mutationFn: ({ id, tipo }: { id: string; tipo: 'ocultar' | 'restaurar' | 'eliminar' }) =>
      tipo === 'ocultar' ? ocultarAviso(id) : tipo === 'restaurar' ? restaurarAviso(id) : eliminarAvisoModeracion(id),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['admin', 'moderacion'] }),
  });
  return <Container maxWidth="md" sx={{ py: 4 }}>
    <Typography variant="h4" component="h1">Moderación de avisos</Typography>
    <TextField select label="Estado del reporte" value={estado} onChange={(e) => setEstado(e.target.value as EstadoReporte)} sx={{ my: 2, minWidth: 220 }}>
      {(['Pendiente', 'Atendido', 'Descartado'] as const).map((valor) => <MenuItem key={valor} value={valor}>{valor}</MenuItem>)}
    </TextField>
    {consulta.isLoading && <CircularProgress />}
    {consulta.isError && <Alert severity="error">No se pudo cargar la cola.</Alert>}
    {consulta.data?.items.map((aviso) => <Stack key={aviso.avisoId} direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ py: 2, borderBottom: 1, borderColor: 'divider' }}>
      <Typography sx={{ flexGrow: 1 }}>{aviso.titulo} · {Number(aviso.cantidadReportes)} reportes · {aviso.estadoModeracion}</Typography>
      <Button onClick={() => accion.mutate({ id: aviso.avisoId, tipo: 'ocultar' })}>Ocultar</Button>
      <Button onClick={() => accion.mutate({ id: aviso.avisoId, tipo: 'restaurar' })}>Restaurar</Button>
      <Button color="error" onClick={() => accion.mutate({ id: aviso.avisoId, tipo: 'eliminar' })}>Eliminar</Button>
    </Stack>)}
    {consulta.data?.items.length === 0 && <Typography>No hay avisos en este estado.</Typography>}
  </Container>;
}
