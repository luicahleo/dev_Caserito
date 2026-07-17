import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link as RouterLink } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Container,
  Link,
  Stack,
  Typography,
} from '@mui/material';
import { enviarKyc, obtenerEstadoKyc } from '../api/kyc';

const MIME_PERMITIDOS = ['image/jpeg', 'image/png'];
const LIMITE_BYTES = 5 * 1024 * 1024;

// Valida un archivo contra los límites del backend. Devuelve mensaje de error o null.
function validarArchivo(archivo: File | null): string | null {
  if (!archivo) return 'Selecciona un archivo';
  if (!MIME_PERMITIDOS.includes(archivo.type)) return 'Formato no permitido (usa JPG o PNG)';
  if (archivo.size > LIMITE_BYTES) return 'El archivo supera el máximo de 5 MB';
  return null;
}

export function KycPage() {
  const queryClient = useQueryClient();
  const { data: estado, isLoading } = useQuery({
    queryKey: ['kyc', 'estado'],
    queryFn: obtenerEstadoKyc,
  });

  const [documento, setDocumento] = useState<File | null>(null);
  const [selfie, setSelfie] = useState<File | null>(null);
  const [errorDoc, setErrorDoc] = useState<string | null>(null);
  const [errorSelfie, setErrorSelfie] = useState<string | null>(null);

  const mutacion = useMutation({
    mutationFn: () => enviarKyc(documento as File, selfie as File),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['kyc', 'estado'] }),
  });

  const onEnviar = () => {
    const eDoc = validarArchivo(documento);
    const eSelfie = validarArchivo(selfie);
    setErrorDoc(eDoc);
    setErrorSelfie(eSelfie);
    if (eDoc || eSelfie) return;
    mutacion.mutate();
  };

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  const mostrarFormulario = estado?.estado === 'NoIniciado' || estado?.estado === 'Rechazada';

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Verificación de identidad
      </Typography>

      {estado?.estado === 'Aprobada' && (
        <Chip color="success" label="Identidad verificada" sx={{ my: 2 }} />
      )}

      {estado?.estado === 'Pendiente' && (
        <Alert severity="info" sx={{ my: 2 }}>
          Tu solicitud está en revisión. Te avisaremos cuando haya una decisión.
        </Alert>
      )}

      {estado?.estado === 'Rechazada' && (
        <Alert severity="error" sx={{ my: 2 }}>
          Tu solicitud fue rechazada. Motivo: {estado.motivoRechazo ?? 'sin especificar'}. Puedes
          volver a enviar los documentos.
        </Alert>
      )}

      {mostrarFormulario && (
        <Stack spacing={3} sx={{ mt: 2 }}>
          <Typography variant="body2" color="text.secondary">
            Sube una foto de tu documento de identidad y una selfie. Formatos JPG o PNG, máximo 5 MB
            cada uno.
          </Typography>

          <Box>
            <Button component="label" variant="outlined">
              Documento
              <input
                type="file"
                hidden
                aria-label="documento"
                accept="image/jpeg,image/png"
                onChange={(e) => {
                  const f = e.target.files?.[0] ?? null;
                  setDocumento(f);
                  setErrorDoc(validarArchivo(f));
                }}
              />
            </Button>
            {documento && <Typography variant="caption" sx={{ ml: 2 }}>{documento.name}</Typography>}
            {errorDoc && <Alert severity="warning" sx={{ mt: 1 }}>{errorDoc}</Alert>}
          </Box>

          <Box>
            <Button component="label" variant="outlined">
              Selfie
              <input
                type="file"
                hidden
                aria-label="selfie"
                accept="image/jpeg,image/png"
                onChange={(e) => {
                  const f = e.target.files?.[0] ?? null;
                  setSelfie(f);
                  setErrorSelfie(validarArchivo(f));
                }}
              />
            </Button>
            {selfie && <Typography variant="caption" sx={{ ml: 2 }}>{selfie.name}</Typography>}
            {errorSelfie && <Alert severity="warning" sx={{ mt: 1 }}>{errorSelfie}</Alert>}
          </Box>

          {mutacion.isError && (
            <Alert severity="error">
              No se pudo enviar la solicitud. Verifica los archivos e inténtalo de nuevo.
            </Alert>
          )}

          <Button variant="contained" onClick={onEnviar} disabled={mutacion.isPending}>
            Enviar
          </Button>
        </Stack>
      )}

      <Box sx={{ mt: 4 }}>
        <Link component={RouterLink} to="/perfil">
          Volver a mi perfil
        </Link>
      </Box>
    </Container>
  );
}
