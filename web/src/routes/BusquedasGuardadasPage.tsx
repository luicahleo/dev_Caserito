import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Box,
  Button,
  Card,
  CardActions,
  CardContent,
  CircularProgress,
  Container,
  TextField,
  Typography,
} from '@mui/material';
import {
  crearBusquedaGuardada,
  eliminarBusquedaGuardada,
  listarBusquedasGuardadas,
} from '../api/busquedasGuardadas';

export function BusquedasGuardadasPage() {
  const cliente = useQueryClient();
  const [palabraClave, setPalabraClave] = useState('');
  const [categoria, setCategoria] = useState('');
  const [ciudad, setCiudad] = useState('');
  const [precioMinimo, setPrecioMinimo] = useState('');
  const [precioMaximo, setPrecioMaximo] = useState('');
  const [estado, setEstado] = useState('');
  const [errorValidacion, setErrorValidacion] = useState<string | null>(null);

  const { data, isLoading, error } = useQuery({
    queryKey: ['busquedas-guardadas'],
    queryFn: listarBusquedasGuardadas,
  });

  const crear = useMutation({
    mutationFn: crearBusquedaGuardada,
    onSuccess: async () => {
      await cliente.invalidateQueries({ queryKey: ['busquedas-guardadas'] });
      setPalabraClave('');
      setCategoria('');
      setCiudad('');
      setPrecioMinimo('');
      setPrecioMaximo('');
      setEstado('');
      setErrorValidacion(null);
    },
  });

  const eliminar = useMutation({
    mutationFn: eliminarBusquedaGuardada,
    onSuccess: async () => {
      await cliente.invalidateQueries({ queryKey: ['busquedas-guardadas'] });
    },
  });

  const handleSubmit = (evento: React.FormEvent) => {
    evento.preventDefault();

    if (!palabraClave.trim() && !categoria.trim() && !ciudad.trim()) {
      setErrorValidacion('Ingresa al menos una palabra clave, categoría o ciudad.');
      return;
    }

    const precioMin = precioMinimo ? Number(precioMinimo) : null;
    const precioMax = precioMaximo ? Number(precioMaximo) : null;

    if (precioMinimo && Number.isNaN(precioMin)) {
      setErrorValidacion('El precio mínimo no es válido.');
      return;
    }

    if (precioMaximo && Number.isNaN(precioMax)) {
      setErrorValidacion('El precio máximo no es válido.');
      return;
    }

    if (precioMin != null && precioMax != null && precioMin > precioMax) {
      setErrorValidacion('El precio mínimo no puede ser mayor que el máximo.');
      return;
    }

    crear.mutate({
      palabraClave: palabraClave.trim() || null,
      categoria: categoria.trim() || null,
      ciudad: ciudad.trim() || null,
      precioMinimo: precioMin,
      precioMaximo: precioMax,
      estado: estado.trim() || null,
    });
  };

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error) {
    return (
      <Container maxWidth="md" sx={{ py: 4 }}>
        <Alert severity="error">No se pudieron cargar las búsquedas guardadas.</Alert>
      </Container>
    );
  }

  const busquedas = data ?? [];

  return (
    <Container maxWidth="md" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Alertas de búsqueda
      </Typography>

      <Card component="form" onSubmit={handleSubmit} sx={{ mb: 4 }}>
        <CardContent>
          <Typography variant="h6" gutterBottom>
            Nueva alerta
          </Typography>
          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { sm: '1fr 1fr' } }}>
            <TextField
              label="Palabra clave"
              value={palabraClave}
              onChange={(e) => setPalabraClave(e.target.value)}
            />
            <TextField
              label="Categoría"
              value={categoria}
              onChange={(e) => setCategoria(e.target.value)}
            />
            <TextField
              label="Ciudad"
              value={ciudad}
              onChange={(e) => setCiudad(e.target.value)}
            />
            <TextField
              label="Estado del producto"
              value={estado}
              onChange={(e) => setEstado(e.target.value)}
            />
            <TextField
              label="Precio mínimo"
              type="number"
              value={precioMinimo}
              onChange={(e) => setPrecioMinimo(e.target.value)}
            />
            <TextField
              label="Precio máximo"
              type="number"
              value={precioMaximo}
              onChange={(e) => setPrecioMaximo(e.target.value)}
            />
          </Box>
          {(errorValidacion || crear.isError) && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {errorValidacion ?? 'No se pudo guardar la alerta.'}
            </Alert>
          )}
        </CardContent>
        <CardActions>
          <Button
            type="submit"
            variant="contained"
            disabled={crear.isPending}
          >
            {crear.isPending ? 'Guardando…' : 'Guardar alerta'}
          </Button>
        </CardActions>
      </Card>

      {busquedas.length === 0 ? (
        <Alert severity="info">No tienes alertas de búsqueda guardadas.</Alert>
      ) : (
        <Box sx={{ display: 'grid', gap: 2 }}>
          {busquedas.map((busqueda) => (
            <Card key={busqueda.id}>
              <CardContent>
                <Typography variant="subtitle1">
                  {[
                    busqueda.palabraClave,
                    busqueda.categoria,
                    busqueda.ciudad,
                    busqueda.estadoProducto,
                  ]
                    .filter(Boolean)
                    .join(' · ') || 'Alerta general'}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {busqueda.precioMinimo != null && `Desde ${busqueda.precioMinimo} `}
                  {busqueda.precioMaximo != null && `hasta ${busqueda.precioMaximo}`}
                </Typography>
              </CardContent>
              <CardActions>
                <Button
                  color="error"
                  onClick={() => eliminar.mutate(busqueda.id)}
                  disabled={eliminar.isPending}
                >
                  Eliminar
                </Button>
              </CardActions>
            </Card>
          ))}
        </Box>
      )}
    </Container>
  );
}
