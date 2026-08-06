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
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { enviarKyc, obtenerEstadoKyc } from '../api/kyc';
import { HttpError } from '../api/http';
import { esMovilConCamara } from '../kyc/captura/plataforma';
import { FlujoCapturaKyc } from '../kyc/captura/FlujoCapturaKyc';

const DEPARTAMENTOS = [
  ['LaPaz', 'La Paz'],
  ['Cochabamba', 'Cochabamba'],
  ['SantaCruz', 'Santa Cruz'],
  ['Chuquisaca', 'Chuquisaca'],
  ['Oruro', 'Oruro'],
  ['Potosi', 'Potosí'],
  ['Tarija', 'Tarija'],
  ['Beni', 'Beni'],
  ['Pando', 'Pando'],
] as const;

export function KycPage() {
  const queryClient = useQueryClient();
  const esDispositivoMovil = esMovilConCamara();
  const {
    data: estado,
    isLoading,
    isError,
  } = useQuery({
    queryKey: ['kyc', 'estado'],
    queryFn: obtenerEstadoKyc,
  });

  const [documento, setDocumento] = useState<File | null>(null);
  const [selfie, setSelfie] = useState<File | null>(null);
  const [numeroCi, setNumeroCi] = useState('');
  const [complementoCi, setComplementoCi] = useState('');
  const [departamento, setDepartamento] = useState('');

  const mutacion = useMutation({
    mutationFn: () =>
      enviarKyc({
        numeroCi,
        complementoCi: complementoCi || undefined,
        departamentoExpedicion: departamento,
        documento: documento as File,
        selfie: selfie as File,
      }),
    onSuccess: () => {
      setDocumento(null);
      setSelfie(null);
      return queryClient.invalidateQueries({ queryKey: ['kyc', 'estado'] });
    },
    onError: (error) => {
      if (error instanceof HttpError && error.status === 409) {
        setDocumento(null);
        setSelfie(null);
        queryClient.invalidateQueries({ queryKey: ['kyc', 'estado'] });
      }
    },
  });

  const onEnviar = () => {
    if (!documento || !selfie || !/^\d{5,12}$/.test(numeroCi) || !departamento) return;
    mutacion.mutate();
  };

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError) {
    return (
      <Container maxWidth="sm" sx={{ py: 4 }}>
        <Alert severity="error">
          No se pudo cargar tu estado de verificación. Inténtalo más tarde.
        </Alert>
      </Container>
    );
  }

  const es409 = mutacion.error instanceof HttpError && mutacion.error.status === 409;
  const es422 = mutacion.error instanceof HttpError && mutacion.error.status === 422;
  const es503 = mutacion.error instanceof HttpError && mutacion.error.status === 503;
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
          Tu solicitud está pendiente de revisión humana. Te avisaremos cuando haya una decisión.
        </Alert>
      )}

      {estado?.estado === 'Rechazada' && (
        <Alert severity="error" sx={{ my: 2 }}>
          Tu solicitud fue rechazada. Motivo: {estado.motivoRechazo ?? 'sin especificar'}. Puedes
          volver a enviar los documentos.
        </Alert>
      )}

      {mostrarFormulario && !esDispositivoMovil && (
        <Alert severity="info" sx={{ mt: 2 }}>
          La fotografía del CI debe realizarse desde un teléfono o una tableta con cámara. Abre
          Caserito en Chrome o Safari desde tu dispositivo móvil para continuar.
        </Alert>
      )}

      {mostrarFormulario && esDispositivoMovil && (
        <Stack spacing={3} sx={{ mt: 2 }}>
          <Typography variant="body2" color="text.secondary">
            Toma una fotografía del frontal de tu CI y una selfie. Un revisor autorizado comprobará
            manualmente ambas imágenes. Cada fotografía debe ocupar como máximo 5 MB.
          </Typography>

          <TextField
            label="Número de CI"
            value={numeroCi}
            onChange={(e) => setNumeroCi(e.target.value)}
            required
            error={numeroCi.length > 0 && !/^\d{5,12}$/.test(numeroCi)}
            helperText="Solo números, entre 5 y 12 dígitos"
          />
          <TextField
            label="Complemento (opcional)"
            value={complementoCi}
            onChange={(e) => setComplementoCi(e.target.value)}
          />
          <TextField
            select
            label="Departamento de expedición"
            value={departamento}
            onChange={(e) => setDepartamento(e.target.value)}
            required
          >
            {DEPARTAMENTOS.map(([valor, etiqueta]) => (
              <MenuItem key={valor} value={valor}>
                {etiqueta}
              </MenuItem>
            ))}
          </TextField>

          <FlujoCapturaKyc
            documento={documento}
            selfie={selfie}
            onDocumento={setDocumento}
            onSelfie={setSelfie}
          />

          {mutacion.isError && es503 && (
            <Alert severity="warning">
              El servicio de verificación no está disponible en este momento. Inténtalo más tarde.
            </Alert>
          )}
          {mutacion.isError && es422 && (
            <Alert severity="warning">
              No detectamos un rostro en alguna de las fotos. Asegúrate de que el rostro del CI y
              tu selfie se vean frontales, nítidos y con buena luz, e inténtalo de nuevo.
            </Alert>
          )}
          {mutacion.isError && es409 && (
            <Alert severity="info">
              Tu solicitud ya no se puede enviar en este estado; actualizamos tu estado de
              verificación.
            </Alert>
          )}
          {mutacion.isError && !es409 && !es422 && !es503 && (
            <Alert severity="error">
              No se pudo enviar la solicitud. Verifica los archivos e inténtalo de nuevo.
            </Alert>
          )}

          <Button
            variant="contained"
            onClick={onEnviar}
            disabled={
              mutacion.isPending ||
              !/^\d{5,12}$/.test(numeroCi) ||
              !departamento ||
              !documento ||
              !selfie
            }
          >
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
