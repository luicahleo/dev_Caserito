import { Box, Typography } from '@mui/material';
import { esEntornoDesarrollo } from '../lib/entorno';

const mensaje = 'Entorno de desarrollo — usa únicamente datos de prueba';

export function IndicadorEntorno() {
  if (!esEntornoDesarrollo()) return null;

  return (
    <Box
      aria-label={mensaje}
      role="status"
      sx={{
        bgcolor: 'warning.main',
        color: 'warning.contrastText',
        px: 2,
        py: 0.75,
        textAlign: 'center',
      }}
    >
      <Typography component="span" variant="body2" sx={{ fontWeight: 700 }}>
        {mensaje}
      </Typography>
    </Box>
  );
}
