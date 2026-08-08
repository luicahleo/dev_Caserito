import {
  Alert,
  Button,
  Container,
  FormControl,
  FormControlLabel,
  Radio,
  RadioGroup,
  Stack,
  Typography,
} from '@mui/material';
import { Navigate, useSearchParams } from 'react-router-dom';
import { normalizarRetorno, type ProveedorExterno } from '../api/auth';

const escenarios = [
  { valor: 'nuevo', etiqueta: 'Usuario nuevo (completa el registro)' },
  {
    valor: 'reutilizable',
    etiqueta: 'Identidad estable (los siguientes accesos reutilizan la cuenta)',
  },
  { valor: 'vinculacion', etiqueta: 'Correo existente (requiere vincular la cuenta)' },
] as const;

export function AuthExternaSimuladorPage() {
  const [params] = useSearchParams();
  const provider = params.get('provider');
  if (provider !== 'google' && provider !== 'facebook') return <Navigate to="/login" replace />;

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Stack spacing={3}>
        <Typography component="h1" variant="h4">
          Simulador local de {nombreProveedor(provider)}
        </Typography>
        <Alert severity="warning">
          Esta pantalla pertenece al entorno de desarrollo. No contacta a{' '}
          {nombreProveedor(provider)}.
        </Alert>
        <BoxFormulario provider={provider} returnUrl={normalizarRetorno(params.get('returnUrl'))} />
      </Stack>
    </Container>
  );
}

function BoxFormulario({ provider, returnUrl }: { provider: ProveedorExterno; returnUrl: string }) {
  return (
    <form method="post" action="/api/auth/external/simulator/authorize">
      <input type="hidden" name="provider" value={provider} />
      <input type="hidden" name="returnUrl" value={returnUrl} />
      <Stack spacing={3}>
        <FormControl>
          <Typography component="legend" variant="h6">
            Escenario sintético
          </Typography>
          <RadioGroup name="scenario" defaultValue="nuevo">
            {escenarios.map((escenario) => (
              <FormControlLabel
                key={escenario.valor}
                value={escenario.valor}
                control={<Radio />}
                label={escenario.etiqueta}
              />
            ))}
          </RadioGroup>
        </FormControl>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
          <Button type="submit" name="action" value="aprobar" variant="contained">
            Aprobar acceso simulado
          </Button>
          <Button type="submit" name="action" value="cancelar" variant="outlined">
            Cancelar
          </Button>
        </Stack>
      </Stack>
    </form>
  );
}

function nombreProveedor(provider: ProveedorExterno): string {
  return provider === 'google' ? 'Google' : 'Facebook';
}
