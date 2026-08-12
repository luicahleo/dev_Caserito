import CheckRoundedIcon from '@mui/icons-material/CheckRounded';
import DoneAllRoundedIcon from '@mui/icons-material/DoneAllRounded';
import { Tooltip } from '@mui/material';
import type { EstadoEntregaMensaje as Estado } from './estadoMensaje';

const etiquetas: Record<Estado, string> = {
  enviado: 'Enviado',
  entregado: 'Entregado',
  leido: 'Leído',
};

export function EstadoEntregaMensaje({ estado }: { estado: Estado }) {
  const etiqueta = etiquetas[estado];
  const Icono = estado === 'enviado' ? CheckRoundedIcon : DoneAllRoundedIcon;
  return (
    <Tooltip title={etiqueta} enterTouchDelay={0}>
      <Icono
        fontSize="small"
        aria-label={etiqueta}
        sx={{ color: estado === 'leido' ? 'info.main' : 'text.secondary' }}
      />
    </Tooltip>
  );
}
