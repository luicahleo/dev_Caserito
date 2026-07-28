import { Badge, IconButton } from '@mui/material';
import NotificationsIcon from '@mui/icons-material/Notifications';
import { useQuery } from '@tanstack/react-query';
import { contarNoLeidas } from '../api/notificaciones';

export function NotificacionesBadge({
  onClick,
}: {
  onClick: (evento: React.MouseEvent<HTMLButtonElement>) => void;
}) {
  const { data: total = 0 } = useQuery({
    queryKey: ['notificaciones', 'no-leidas'],
    queryFn: contarNoLeidas,
    refetchInterval: 60_000,
  });

  return (
    <IconButton color="inherit" onClick={onClick} aria-label="Notificaciones">
      <Badge badgeContent={total} color="error" max={99}>
        <NotificationsIcon />
      </Badge>
    </IconButton>
  );
}
