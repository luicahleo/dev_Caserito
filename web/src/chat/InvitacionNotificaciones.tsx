import { useState } from 'react';
import { Alert, Button, Stack } from '@mui/material';
import { activarNotificacionesPush, soportaWebPush } from '../pwa/push';

const claveDescartada = 'caserito-push-invitacion-descartada';

export function InvitacionNotificaciones() {
  const [visible, setVisible] = useState(
    () =>
      soportaWebPush() &&
      Notification.permission === 'default' &&
      localStorage.getItem(claveDescartada) !== 'si',
  );
  const [estado, setEstado] = useState<'reposo' | 'activada' | 'denegada' | 'error'>('reposo');
  if (!soportaWebPush()) return null;
  if (!visible && estado === 'reposo') return null;

  const activar = async () => {
    try {
      setEstado(await activarNotificacionesPush());
      setVisible(false);
    } catch {
      setEstado('error');
    }
  };

  if (estado === 'activada') return <Alert severity="success">Notificaciones activadas.</Alert>;
  if (estado === 'denegada') {
    return <Alert severity="info">Puedes habilitarlas más adelante desde el navegador.</Alert>;
  }
  if (estado === 'error') {
    return <Alert severity="error">No fue posible activar las notificaciones.</Alert>;
  }
  return (
    <Alert severity="info" sx={{ mb: 2 }}>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ alignItems: 'center' }}>
        Recibe un aviso cuando te escriban.
        <Button variant="contained" size="small" onClick={() => void activar()}>
          Activar notificaciones
        </Button>
        <Button
          size="small"
          onClick={() => {
            localStorage.setItem(claveDescartada, 'si');
            setVisible(false);
          }}
        >
          Ahora no
        </Button>
      </Stack>
    </Alert>
  );
}
