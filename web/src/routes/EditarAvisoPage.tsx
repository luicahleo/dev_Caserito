import { useMutation, useQuery } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router-dom';
import { Alert, Box, Button, CircularProgress, Container, Typography } from '@mui/material';
import { FormAviso, type ValoresAviso } from './FormAviso';
import { editarAviso, obtenerMiAviso } from '../api/avisos';

export function EditarAvisoPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();

  const { data, isLoading, isError } = useQuery({
    queryKey: ['mi-aviso', id],
    queryFn: () => obtenerMiAviso(id),
  });

  const mutacion = useMutation({
    mutationFn: (v: ValoresAviso) =>
      editarAviso(id, {
        titulo: v.titulo,
        descripcion: v.descripcion,
        monto: Number(v.monto),
        condicion: v.condicion,
        categoriaId: v.categoriaId,
        ciudadId: v.ciudadId,
      }),
    onSuccess: () => navigate('/mis-avisos'),
  });

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError || !data) {
    return (
      <Container maxWidth="sm" sx={{ py: 4 }}>
        <Alert severity="error">No se encontró el aviso o no te pertenece.</Alert>
        <Button onClick={() => navigate('/mis-avisos')} sx={{ mt: 2 }}>
          Volver a mis avisos
        </Button>
      </Container>
    );
  }

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Editar aviso
      </Typography>
      {mutacion.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          No se pudo guardar el aviso. Revisa los datos e inténtalo de nuevo.
        </Alert>
      )}
      <FormAviso
        inicial={{
          titulo: data.titulo,
          descripcion: data.descripcion,
          monto: String(data.monto),
          condicion: data.condicion,
          categoriaId: data.categoriaId,
          ciudadId: data.ciudadId,
        }}
        enviando={mutacion.isPending}
        textoBoton="Guardar cambios"
        onSubmit={mutacion.mutate}
        avisoId={id}
        fotosIniciales={data.fotos}
      />
    </Container>
  );
}
