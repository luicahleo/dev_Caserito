import { Badge, Button } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { Link as RouterLink } from 'react-router-dom';
import { listarConversaciones } from '../api/chat';

export function ContadorChat() {
  const { data } = useQuery({
    queryKey: ['chat-conversaciones', 50],
    queryFn: () => listarConversaciones(undefined, 50),
    refetchInterval: 30_000,
  });
  const noLeidos = data?.items.reduce((total, conversacion) => total + conversacion.noLeidos, 0) ?? 0;

  return (
    <Badge badgeContent={noLeidos} color="error" max={99}>
      <Button color="inherit" component={RouterLink} to="/mensajes" aria-label={`Mensajes${noLeidos ? `, ${noLeidos} no leídos` : ''}`}>
        Mensajes
      </Button>
    </Badge>
  );
}
