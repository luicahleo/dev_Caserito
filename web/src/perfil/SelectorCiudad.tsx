import { useQuery } from '@tanstack/react-query';
import { Alert, Button, MenuItem, Skeleton, Stack, TextField } from '@mui/material';
import { listarCiudades } from '../api/catalogo';

interface Props {
  value: string;
  onChange: (value: string) => void;
  error?: boolean;
  helperText?: string;
}

export function SelectorCiudad({ value, onChange, error, helperText }: Props) {
  const ciudades = useQuery({ queryKey: ['ciudades'], queryFn: listarCiudades });
  if (ciudades.isLoading) return <Skeleton variant="rounded" height={56} />;
  if (ciudades.isError) {
    return <Stack spacing={1}><Alert severity="error">No pudimos cargar las ciudades.</Alert>
      <Button onClick={() => ciudades.refetch()}>Reintentar</Button></Stack>;
  }
  return <TextField select label="Ciudad" value={value} onChange={(e) => onChange(e.target.value)}
    error={error} helperText={helperText}>
    {(ciudades.data ?? []).map((ciudad) => <MenuItem key={ciudad.id} value={ciudad.id}>{ciudad.nombre}</MenuItem>)}
  </TextField>;
}
