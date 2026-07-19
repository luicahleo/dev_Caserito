import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { listarCategorias, listarCiudades } from '../api/catalogo';
import { type FotoAvisoDto, borrarFotoAviso, subirFotoAviso } from '../api/avisos';

export interface ValoresAviso {
  titulo: string;
  descripcion: string;
  monto: string;
  condicion: string;
  categoriaId: string;
  ciudadId: string;
}

const CONDICIONES = ['Nuevo', 'Usado'];
const MAX_FOTOS = 5;
const MAX_BYTES = 5 * 1024 * 1024;

const VACIO: ValoresAviso = {
  titulo: '',
  descripcion: '',
  monto: '',
  condicion: 'Usado',
  categoriaId: '',
  ciudadId: '',
};

// Valida las reglas de dominio del aviso en cliente. Devuelve errores por campo.
function validar(v: ValoresAviso): Partial<Record<keyof ValoresAviso, string>> {
  const e: Partial<Record<keyof ValoresAviso, string>> = {};
  if (!v.titulo.trim()) e.titulo = 'El título es obligatorio';
  else if (v.titulo.length > 120) e.titulo = 'Máximo 120 caracteres';
  if (!v.descripcion.trim()) e.descripcion = 'La descripción es obligatoria';
  else if (v.descripcion.length > 2000) e.descripcion = 'Máximo 2000 caracteres';
  const monto = Number(v.monto);
  if (!v.monto || Number.isNaN(monto) || monto <= 0) e.monto = 'El precio debe ser mayor a 0';
  if (!v.categoriaId) e.categoriaId = 'Elige una categoría';
  if (!v.ciudadId) e.ciudadId = 'Elige una ciudad';
  return e;
}

export function FormAviso({
  inicial,
  enviando,
  textoBoton,
  onSubmit,
  avisoId,
  fotosIniciales = [],
  onFotasLocalesChange,
}: {
  inicial?: Partial<ValoresAviso>;
  enviando: boolean;
  textoBoton: string;
  onSubmit: (valores: ValoresAviso) => void;
  /** Id del aviso existente (solo en editar). Si se proporciona, las fotos se suben/borran inmediatamente. */
  avisoId?: string;
  /** Fotos ya guardadas del aviso (solo en editar). */
  fotosIniciales?: FotoAvisoDto[];
  /** Callback invocado cuando cambian los archivos locales pendientes (solo en crear). */
  onFotasLocalesChange?: (archivos: File[]) => void;
}) {
  const [valores, setValores] = useState<ValoresAviso>({ ...VACIO, ...inicial });
  const [errores, setErrores] = useState<Partial<Record<keyof ValoresAviso, string>>>({});

  const [fotosGuardadas, setFotosGuardadas] = useState<FotoAvisoDto[]>(fotosIniciales);
  const [fotasLocales, setFotasLocales] = useState<{ preview: string; archivo: File }[]>([]);
  const [subiendo, setSubiendo] = useState(false);
  const [errorFoto, setErrorFoto] = useState<string | null>(null);

  const totalFotos = fotosGuardadas.length + fotasLocales.length;

  const categorias = useQuery({ queryKey: ['categorias'], queryFn: listarCategorias });
  const ciudades = useQuery({ queryKey: ['ciudades'], queryFn: listarCiudades });

  const set = (campo: keyof ValoresAviso) => (e: { target: { value: string } }) =>
    setValores((v) => ({ ...v, [campo]: e.target.value }));

  const enviar = () => {
    const e = validar(valores);
    setErrores(e);
    if (Object.keys(e).length === 0) onSubmit(valores);
  };

  const handleArchivos = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const archivos = Array.from(e.target.files ?? []);
    e.target.value = '';
    setErrorFoto(null);

    const disponibles = MAX_FOTOS - totalFotos;
    if (archivos.length > disponibles) {
      setErrorFoto(`Solo podés agregar ${disponibles} foto${disponibles !== 1 ? 's' : ''} más (máx. ${MAX_FOTOS}).`);
      return;
    }
    for (const archivo of archivos) {
      if (archivo.size > MAX_BYTES) {
        setErrorFoto('Cada foto debe pesar menos de 5 MiB.');
        return;
      }
    }

    if (avisoId) {
      setSubiendo(true);
      try {
        for (const archivo of archivos) {
          const { id } = await subirFotoAviso(avisoId, archivo);
          const url = URL.createObjectURL(archivo);
          setFotosGuardadas((prev) => [
            ...prev,
            { id, url, orden: prev.length === 0 ? 0 : Math.max(...prev.map((f) => Number(f.orden))) + 1 },
          ]);
        }
      } catch {
        setErrorFoto('No se pudo subir la foto. Inténtalo de nuevo.');
      } finally {
        setSubiendo(false);
      }
    } else {
      const nuevas = archivos.map((a) => ({ preview: URL.createObjectURL(a), archivo: a }));
      setFotasLocales((prev) => {
        const actualizadas = [...prev, ...nuevas];
        onFotasLocalesChange?.(actualizadas.map((f) => f.archivo));
        return actualizadas;
      });
    }
  };

  const handleBorrarGuardada = async (fotoId: string) => {
    if (!avisoId) return;
    setErrorFoto(null);
    try {
      await borrarFotoAviso(avisoId, fotoId);
      setFotosGuardadas((prev) => prev.filter((f) => f.id !== fotoId));
    } catch {
      setErrorFoto('No se pudo borrar la foto. Inténtalo de nuevo.');
    }
  };

  const handleBorrarLocal = (idx: number) => {
    setFotasLocales((prev) => {
      URL.revokeObjectURL(prev[idx].preview);
      const actualizadas = prev.filter((_, i) => i !== idx);
      onFotasLocalesChange?.(actualizadas.map((f) => f.archivo));
      return actualizadas;
    });
  };

  return (
    <Stack spacing={3}>
      <TextField
        label="Título"
        value={valores.titulo}
        onChange={set('titulo')}
        error={!!errores.titulo}
        helperText={errores.titulo}
        slotProps={{ htmlInput: { maxLength: 120 } }}
      />
      <TextField
        label="Descripción"
        multiline
        minRows={4}
        value={valores.descripcion}
        onChange={set('descripcion')}
        error={!!errores.descripcion}
        helperText={errores.descripcion}
        slotProps={{ htmlInput: { maxLength: 2000 } }}
      />
      <TextField
        label="Precio (BOB)"
        type="number"
        value={valores.monto}
        onChange={set('monto')}
        error={!!errores.monto}
        helperText={errores.monto}
      />
      <TextField
        select
        label="Categoría"
        value={valores.categoriaId}
        onChange={set('categoriaId')}
        error={!!errores.categoriaId}
        helperText={errores.categoriaId}
      >
        <MenuItem value="" />
        {(categorias.data ?? []).map((c) => (
          <MenuItem key={c.id} value={c.id}>
            {c.nombre}
          </MenuItem>
        ))}
      </TextField>
      <TextField
        select
        label="Ciudad"
        value={valores.ciudadId}
        onChange={set('ciudadId')}
        error={!!errores.ciudadId}
        helperText={errores.ciudadId}
      >
        <MenuItem value="" />
        {(ciudades.data ?? []).map((c) => (
          <MenuItem key={c.id} value={c.id}>
            {c.nombre}
          </MenuItem>
        ))}
      </TextField>
      <TextField select label="Condición" value={valores.condicion} onChange={set('condicion')}>
        {CONDICIONES.map((c) => (
          <MenuItem key={c} value={c}>
            {c}
          </MenuItem>
        ))}
      </TextField>
      {(categorias.isError || ciudades.isError) && (
        <Alert severity="error">No se pudieron cargar las opciones. Recarga la página.</Alert>
      )}

      {/* ── Sección de fotos ── */}
      <Box>
        <Typography variant="subtitle1" gutterBottom>
          Fotos ({totalFotos}/{MAX_FOTOS})
        </Typography>

        {fotosGuardadas.length > 0 && (
          <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', mb: 1 }}>
            {fotosGuardadas.map((f) => (
              <Box key={f.id} sx={{ position: 'relative' }}>
                <Box
                  component="img"
                  src={f.url}
                  alt="foto del aviso"
                  sx={{ width: 72, height: 72, objectFit: 'cover', borderRadius: 1 }}
                />
                <Button
                  size="small"
                  onClick={() => handleBorrarGuardada(f.id)}
                  sx={{
                    position: 'absolute', top: 0, right: 0, minWidth: 0,
                    p: 0.25, bgcolor: 'rgba(0,0,0,0.5)', color: 'white',
                    '&:hover': { bgcolor: 'rgba(0,0,0,0.75)' },
                  }}
                  aria-label="Borrar foto"
                >
                  ✕
                </Button>
              </Box>
            ))}
          </Stack>
        )}

        {fotasLocales.length > 0 && (
          <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', mb: 1 }}>
            {fotasLocales.map((f, i) => (
              <Box key={f.preview} sx={{ position: 'relative' }}>
                <Box
                  component="img"
                  src={f.preview}
                  alt="previsualización"
                  sx={{ width: 72, height: 72, objectFit: 'cover', borderRadius: 1 }}
                />
                <Button
                  size="small"
                  onClick={() => handleBorrarLocal(i)}
                  sx={{
                    position: 'absolute', top: 0, right: 0, minWidth: 0,
                    p: 0.25, bgcolor: 'rgba(0,0,0,0.5)', color: 'white',
                    '&:hover': { bgcolor: 'rgba(0,0,0,0.75)' },
                  }}
                  aria-label="Quitar foto"
                >
                  ✕
                </Button>
              </Box>
            ))}
          </Stack>
        )}

        <Button
          component="label"
          variant="outlined"
          disabled={totalFotos >= MAX_FOTOS || subiendo}
          size="small"
        >
          {subiendo ? <CircularProgress size={16} sx={{ mr: 1 }} /> : null}
          Agregar fotos
          <input
            type="file"
            accept="image/jpeg,image/png"
            multiple
            hidden
            onChange={handleArchivos}
          />
        </Button>

        {errorFoto && <Alert severity="error" sx={{ mt: 1 }}>{errorFoto}</Alert>}
      </Box>

      <Box>
        <Button variant="contained" onClick={enviar} disabled={enviando || subiendo}>
          {textoBoton}
        </Button>
      </Box>
    </Stack>
  );
}
