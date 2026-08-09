import { useRef } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import { Alert, Button, Container, Typography } from '@mui/material';
import { FormAviso, type ValoresAviso } from './FormAviso';
import { crearAviso, subirFotoAviso } from '../api/avisos';
import { useAuth } from '../auth/AuthContext';
import { HttpError } from '../api/http';

class ErrorSubidaFotos extends Error {}

export function CrearAvisoPage() {
  const { identidadHabilitada } = useAuth();
  const navigate = useNavigate();
  const fotasLocalesRef = useRef<File[]>([]);

  const mutacion = useMutation({
    mutationFn: async (v: ValoresAviso) => {
      const { id } = await crearAviso({
        titulo: v.titulo,
        descripcion: v.descripcion,
        monto: Number(v.monto),
        condicion: v.condicion,
        categoriaId: v.categoriaId,
        ciudadId: v.ciudadId,
      });
      try {
        for (const archivo of fotasLocalesRef.current) {
          await subirFotoAviso(id, archivo);
        }
      } catch {
        throw new ErrorSubidaFotos();
      }
      return id;
    },
    onSuccess: () => navigate('/mis-avisos'),
  });

  const es403 = mutacion.error instanceof HttpError && mutacion.error.status === 403;
  const falloFotos = mutacion.error instanceof ErrorSubidaFotos;

  if (!identidadHabilitada) {
    return (
      <Container maxWidth="sm" sx={{ py: 4 }}>
        <Typography variant="h4" component="h1" gutterBottom>
          Publicar un aviso
        </Typography>
        <Alert severity="info" sx={{ mb: 2 }}>
          Necesitas verificar tu identidad antes de publicar un aviso.
        </Alert>
        <Button component={RouterLink} to="/kyc" variant="contained">
          Verificar identidad
        </Button>
      </Container>
    );
  }

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Publicar un aviso
      </Typography>
      {mutacion.isError && es403 && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          Necesitas verificar tu identidad. <RouterLink to="/kyc">Verificar ahora</RouterLink>
        </Alert>
      )}
      {falloFotos && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          El aviso se creó, pero no se pudieron subir todas las fotos. Puedes agregarlas desde “Mis
          avisos”.
        </Alert>
      )}
      {mutacion.isError && !es403 && !falloFotos && (
        <Alert severity="error" sx={{ mb: 2 }}>
          No se pudo publicar el aviso. Revisa los datos e inténtalo de nuevo.
        </Alert>
      )}
      <FormAviso
        enviando={mutacion.isPending}
        textoBoton="Publicar"
        onSubmit={mutacion.mutate}
        onFotasLocalesChange={(archivos) => {
          fotasLocalesRef.current = archivos;
        }}
      />
    </Container>
  );
}
