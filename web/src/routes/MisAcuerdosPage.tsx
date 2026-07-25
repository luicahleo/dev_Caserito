import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Alert, Box, Chip, CircularProgress, Container, List, ListItemButton,
  ListItemText, Tab, Tabs, Typography,
} from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import { listarOrdenes, type RolOrden } from '../api/orders';
import { formatearBob } from '../lib/formato';

function estadoVisible(estado: string) {
  return estado === 'Agreed' ? 'Acordado' : 'Solicitado';
}

export function MisAcuerdosPage() {
  const [rol, setRol] = useState<RolOrden>('comprador');
  const { data, isLoading, error } = useQuery({
    queryKey: ['orders', rol],
    queryFn: () => listarOrdenes(rol),
  });

  return (
    <Container maxWidth="md" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>Mis acuerdos</Typography>
      <Tabs value={rol} onChange={(_, valor: RolOrden) => setRol(valor)} sx={{ mb: 2 }}>
        <Tab value="comprador" label="Compras" />
        <Tab value="vendedor" label="Ventas" />
      </Tabs>
      {isLoading && <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}><CircularProgress /></Box>}
      {error && <Alert severity="error">No se pudieron cargar tus acuerdos.</Alert>}
      {!isLoading && !error && data?.items.length === 0 && (
        <Typography>No tienes acuerdos en esta sección.</Typography>
      )}
      <List disablePadding>
        {data?.items.map((orden) => (
          <ListItemButton
            key={orden.id}
            component={RouterLink}
            to={`/acuerdos/${orden.id}`}
            divider
          >
            <ListItemText
              primary={formatearBob(Number(orden.montoAcordado))}
              secondary={`Actualizado ${new Date(orden.actualizadaEn).toLocaleString('es-BO')}`}
            />
            <Chip label={estadoVisible(orden.estado)} />
          </ListItemButton>
        ))}
      </List>
    </Container>
  );
}
