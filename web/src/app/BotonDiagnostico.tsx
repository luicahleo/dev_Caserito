import { Fab } from '@mui/material';
import DownloadRoundedIcon from '@mui/icons-material/DownloadRounded';
import { exportarDiagnostico, modoDiagnosticoActivo } from '../lib/sesionDiagnostico';

// Acceso discreto a la exportación del flujo de diagnóstico; solo existe con
// el modo diagnóstico activo (?debug=1), nunca para usuarios normales.
export function BotonDiagnostico() {
  if (!modoDiagnosticoActivo()) {
    return null;
  }

  return (
    <Fab
      aria-label="Descargar diagnóstico"
      color="secondary"
      size="small"
      onClick={() => exportarDiagnostico()}
      sx={{ position: 'fixed', right: 16, bottom: 16, zIndex: (tema) => tema.zIndex.snackbar }}
    >
      <DownloadRoundedIcon />
    </Fab>
  );
}
