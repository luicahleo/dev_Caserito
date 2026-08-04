import { Box, Stack, Typography } from '@mui/material';

interface MarcaCaseritoProps {
  invertida?: boolean;
  mostrarNombre?: boolean;
  tamano?: number;
}

export function MarcaCaserito({
  invertida = false,
  mostrarNombre = true,
  tamano = 40,
}: MarcaCaseritoProps) {
  return (
    <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
      <Box
        component="img"
        src="/brand/caserito-symbol.png"
        alt=""
        aria-hidden="true"
        sx={{ display: 'block', height: tamano, width: tamano }}
      />
      {mostrarNombre && (
        <Typography
          variant="h6"
          component="span"
          sx={{ color: invertida ? 'primary.contrastText' : 'text.primary', lineHeight: 1 }}
        >
          Caserito
        </Typography>
      )}
    </Stack>
  );
}
