import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Alert, Box, Button, MenuItem, Stack, TextField } from '@mui/material';
import { listarCategorias, listarCiudades } from '../api/catalogo';

export interface ValoresAviso {
  titulo: string;
  descripcion: string;
  monto: string;
  condicion: string;
  categoriaId: string;
  ciudadId: string;
}

const CONDICIONES = ['Nuevo', 'Usado'];

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
}: {
  inicial?: Partial<ValoresAviso>;
  enviando: boolean;
  textoBoton: string;
  onSubmit: (valores: ValoresAviso) => void;
}) {
  const [valores, setValores] = useState<ValoresAviso>({ ...VACIO, ...inicial });
  const [errores, setErrores] = useState<Partial<Record<keyof ValoresAviso, string>>>({});

  const categorias = useQuery({ queryKey: ['categorias'], queryFn: listarCategorias });
  const ciudades = useQuery({ queryKey: ['ciudades'], queryFn: listarCiudades });

  const set = (campo: keyof ValoresAviso) => (e: { target: { value: string } }) =>
    setValores((v) => ({ ...v, [campo]: e.target.value }));

  const enviar = () => {
    const e = validar(valores);
    setErrores(e);
    if (Object.keys(e).length === 0) onSubmit(valores);
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
      <Box>
        <Button variant="contained" onClick={enviar} disabled={enviando}>
          {textoBoton}
        </Button>
      </Box>
    </Stack>
  );
}
