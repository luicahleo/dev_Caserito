import { Badge, Button } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';

export function ContadorChat({ noLeidos }: { noLeidos: number }) {
  return (
    <Badge badgeContent={noLeidos} color="error" max={99}>
      <Button
        color="inherit"
        component={RouterLink}
        to="/mensajes"
        aria-label={`Mensajes${noLeidos ? `, ${noLeidos} no leídos` : ''}`}
      >
        Mensajes
      </Button>
    </Badge>
  );
}
