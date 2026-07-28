import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Box,
  Button,
  Divider,
  ListItemButton,
  ListItemText,
  Menu,
  MenuItem,
  MenuList,
  Typography,
} from '@mui/material';

import { Link as RouterLink } from 'react-router-dom';
import { listarNotificaciones, marcarLeida, marcarTodasLeidas } from '../api/notificaciones';

export interface NotificacionesDropdownProps {
  open: boolean;
  anchorEl: HTMLElement | null;
  onClose: () => void;
}

export function NotificacionesDropdown({ open, anchorEl, onClose }: NotificacionesDropdownProps) {
  const cliente = useQueryClient();
  const { data, isLoading } = useQuery({
    queryKey: ['notificaciones', 'recientes'],
    queryFn: () => listarNotificaciones(false, 1, 5),
    refetchInterval: 60_000,
  });

  const marcarLeidaMutation = useMutation({
    mutationFn: marcarLeida,
    onSuccess: async () => {
      await cliente.invalidateQueries({ queryKey: ['notificaciones'] });
    },
  });

  const marcarTodasMutation = useMutation({
    mutationFn: marcarTodasLeidas,
    onSuccess: async () => {
      await cliente.invalidateQueries({ queryKey: ['notificaciones'] });
    },
  });

  const notificaciones = data?.items ?? [];

  return (
    <Menu
      open={open}
      anchorEl={anchorEl}
      onClose={onClose}
      slotProps={{ paper: { sx: { width: 360 } } }}
    >
      <Box sx={{ px: 2, py: 1, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="subtitle1">Notificaciones</Typography>
        <Button
          size="small"
          onClick={() => marcarTodasMutation.mutate()}
          disabled={marcarTodasMutation.isPending || notificaciones.length === 0}
        >
          Marcar todas
        </Button>
      </Box>
      <Divider />
      {isLoading ? (
        <MenuItem disabled>
          <ListItemText primary="Cargando…" />
        </MenuItem>
      ) : notificaciones.length === 0 ? (
        <MenuItem disabled>
          <ListItemText primary="No tienes notificaciones" />
        </MenuItem>
      ) : (
        <MenuList dense>
          {notificaciones.map((notificacion) => (
            <ListItemButton
              key={notificacion.id}
              component={RouterLink}
              to="/notificaciones"
              onClick={() => {
                if (!notificacion.leida) {
                  marcarLeidaMutation.mutate(notificacion.id);
                }
                onClose();
              }}
            >
              <ListItemText
                primary={
                  <Typography sx={{ fontWeight: notificacion.leida ? 'normal' : 'bold' }}>
                    {notificacion.titulo}
                  </Typography>
                }
                secondary={notificacion.mensaje}
              />
            </ListItemButton>
          ))}
        </MenuList>
      )}
      <Divider />
      <MenuItem component={RouterLink} to="/notificaciones" onClick={onClose}>
        <ListItemText primary="Ver todas" sx={{ textAlign: 'center' }} />
      </MenuItem>
    </Menu>
  );
}
