import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link as RouterLink, useSearchParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Card,
  CardActionArea,
  CardContent,
  CardMedia,
  CircularProgress,
  Container,
  Grid,
  MenuItem,
  Pagination,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { buscarAvisos, type FiltroBusqueda } from '../api/avisos';
import { listarCategorias, listarCiudades } from '../api/catalogo';
import { formatearBob } from '../lib/formato';

const TAMANO = 20;
const CONDICIONES = ['Nuevo', 'Usado'];

// Lee el filtro y la página desde los search params de la URL (fuente de verdad).
function leerFiltro(params: URLSearchParams): { filtro: FiltroBusqueda; pagina: number } {
  const num = (v: string | null) => (v ? Number(v) : undefined);
  return {
    filtro: {
      q: params.get('q') ?? undefined,
      categoriaId: params.get('categoriaId') ?? undefined,
      ciudadId: params.get('ciudadId') ?? undefined,
      precioMin: num(params.get('precioMin')),
      precioMax: num(params.get('precioMax')),
      condicion: params.get('condicion') ?? undefined,
    },
    pagina: Number(params.get('pagina') ?? '1'),
  };
}

export function ExplorarPage() {
  const [params, setParams] = useSearchParams();
  const { filtro, pagina } = leerFiltro(params);

  // Borrador editable de los campos; se vuelca a la URL al pulsar "Buscar".
  const [borrador, setBorrador] = useState(filtro);

  const categorias = useQuery({ queryKey: ['categorias'], queryFn: listarCategorias });
  const ciudades = useQuery({ queryKey: ['ciudades'], queryFn: listarCiudades });

  const { data, isLoading, isError } = useQuery({
    queryKey: ['avisos-publicos', filtro, pagina],
    queryFn: () => buscarAvisos(filtro, pagina, TAMANO),
  });

  const aplicar = () => {
    const nuevo = new URLSearchParams();
    if (borrador.q) nuevo.set('q', borrador.q);
    if (borrador.categoriaId) nuevo.set('categoriaId', borrador.categoriaId);
    if (borrador.ciudadId) nuevo.set('ciudadId', borrador.ciudadId);
    if (borrador.precioMin != null) nuevo.set('precioMin', String(borrador.precioMin));
    if (borrador.precioMax != null) nuevo.set('precioMax', String(borrador.precioMax));
    if (borrador.condicion) nuevo.set('condicion', borrador.condicion);
    nuevo.set('pagina', '1');
    setParams(nuevo);
  };

  const cambiarPagina = (p: number) => {
    const nuevo = new URLSearchParams(params);
    nuevo.set('pagina', String(p));
    setParams(nuevo);
  };

  const totalPaginas = data ? Math.max(1, Math.ceil(Number(data.total) / TAMANO)) : 1;

  return (
    <Container maxWidth="lg" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Explorar avisos
      </Typography>

      <Stack
        direction={{ xs: 'column', md: 'row' }}
        spacing={2}
        sx={{ mb: 3, flexWrap: 'wrap' }}
      >
        <TextField
          label="Buscar"
          value={borrador.q ?? ''}
          onChange={(e) => setBorrador((b) => ({ ...b, q: e.target.value }))}
          onKeyDown={(e) => e.key === 'Enter' && aplicar()}
        />
        <TextField
          select
          label="Categoría"
          value={borrador.categoriaId ?? ''}
          sx={{ minWidth: 160 }}
          onChange={(e) => setBorrador((b) => ({ ...b, categoriaId: e.target.value || undefined }))}
        >
          <MenuItem value="">Todas</MenuItem>
          {(categorias.data ?? []).map((c) => (
            <MenuItem key={c.id} value={c.id}>
              {c.nombre}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          select
          label="Ciudad"
          value={borrador.ciudadId ?? ''}
          sx={{ minWidth: 160 }}
          onChange={(e) => setBorrador((b) => ({ ...b, ciudadId: e.target.value || undefined }))}
        >
          <MenuItem value="">Todas</MenuItem>
          {(ciudades.data ?? []).map((c) => (
            <MenuItem key={c.id} value={c.id}>
              {c.nombre}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          select
          label="Condición"
          value={borrador.condicion ?? ''}
          sx={{ minWidth: 140 }}
          onChange={(e) => setBorrador((b) => ({ ...b, condicion: e.target.value || undefined }))}
        >
          <MenuItem value="">Cualquiera</MenuItem>
          {CONDICIONES.map((c) => (
            <MenuItem key={c} value={c}>
              {c}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          label="Precio mín."
          type="number"
          value={borrador.precioMin ?? ''}
          onChange={(e) =>
            setBorrador((b) => ({
              ...b,
              precioMin: e.target.value ? Number(e.target.value) : undefined,
            }))
          }
        />
        <TextField
          label="Precio máx."
          type="number"
          value={borrador.precioMax ?? ''}
          onChange={(e) =>
            setBorrador((b) => ({
              ...b,
              precioMax: e.target.value ? Number(e.target.value) : undefined,
            }))
          }
        />
        <Button variant="contained" onClick={aplicar}>
          Buscar
        </Button>
      </Stack>

      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
          <CircularProgress />
        </Box>
      ) : isError ? (
        <Alert severity="error">No se pudieron cargar los avisos. Inténtalo más tarde.</Alert>
      ) : (data?.items.length ?? 0) === 0 ? (
        <Typography color="text.secondary">No se encontraron avisos.</Typography>
      ) : (
        <>
          <Grid container spacing={2}>
            {data?.items.map((a) => (
              <Grid key={a.id} size={{ xs: 12, sm: 6, md: 4 }}>
                <Card>
                  <CardActionArea component={RouterLink} to={`/avisos/${a.id}`}>
                    {a.fotos && a.fotos.length > 0 ? (
                      <CardMedia
                        component="img"
                        height={140}
                        image={a.fotos[0].url}
                        alt={a.titulo}
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
                      <Typography variant="h6" noWrap>
                        {a.titulo}
                      </Typography>
                      <Typography variant="subtitle1" color="primary">
                        {formatearBob(a.monto)}
                      </Typography>
                      <Typography variant="body2" color="text.secondary">
                        {a.nombreCategoria} · {a.nombreCiudad} · {a.condicion}
                      </Typography>
                    </CardContent>
                  </CardActionArea>
                </Card>
              </Grid>
            ))}
          </Grid>

          <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
            <Pagination
              count={totalPaginas}
              page={pagina}
              onChange={(_, p) => cambiarPagina(p)}
            />
          </Box>
        </>
      )}
    </Container>
  );
}
