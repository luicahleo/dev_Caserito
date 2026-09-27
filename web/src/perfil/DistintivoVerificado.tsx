import VerifiedRoundedIcon from '@mui/icons-material/VerifiedRounded';
import { Chip, Tooltip } from '@mui/material';

/**
 * Sello de identidad verificada. Nunca marca lo contrario: si el usuario no está
 * verificado no se pinta nada, porque la ausencia del sello no es una acusación.
 */
export function DistintivoVerificado({
  verificado,
  compacto = false,
}: {
  verificado: boolean;
  compacto?: boolean;
}) {
  if (!verificado) return null;

  if (compacto) {
    return (
      <Tooltip title="Usuario verificado" enterTouchDelay={0}>
        <VerifiedRoundedIcon
          fontSize="small"
          aria-label="Usuario verificado"
          sx={{ color: 'success.main' }}
        />
      </Tooltip>
    );
  }

  return <Chip color="success" size="small" label="Usuario verificado" />;
}
