import { ejecutar as ejecutarProceso } from './lib/ejecutar.mjs';

function lineas(salida) {
  return salida
    .split('\n')
    .map((linea) => linea.trim())
    .filter(Boolean);
}

export function detectarCambiosPendientes(ejecutar = ejecutarProceso) {
  const versionados = ejecutar('git', ['diff', '--name-only', 'HEAD'], {
    silencioso: true,
  });
  if (versionados.codigo !== 0) {
    return {
      ok: false,
      motivo: 'Git no pudo leer los cambios versionados respecto de HEAD.',
    };
  }

  const nuevos = ejecutar(
    'git',
    ['ls-files', '--others', '--exclude-standard'],
    {
      silencioso: true,
    },
  );
  if (nuevos.codigo !== 0) {
    return {
      ok: false,
      motivo: 'Git no pudo leer los archivos no versionados.',
    };
  }

  const archivos = [
    ...new Set([...lineas(versionados.salida), ...lineas(nuevos.salida)]),
  ].sort();
  return { ok: true, archivos };
}

export function determinarAlcance(archivos) {
  let backend = false;
  let frontend = false;

  for (const ruta of archivos) {
    if (ruta.startsWith('web/')) {
      frontend = true;
    } else if (ruta.startsWith('CaseritoApp/')) {
      backend = true;
    } else if (!ruta.startsWith('docs/')) {
      backend = true;
      frontend = true;
    }
  }

  return { backend, frontend };
}

export function validarModo({ completo, porAlcance }) {
  if (completo && porAlcance) {
    return {
      ok: false,
      motivo: 'Los modos --full y --changed no se pueden combinar.',
    };
  }
  return { ok: true };
}

export function gateAplicaAlAlcance(gate, alcance) {
  if (!gate.grupo) return true;
  return alcance[gate.grupo];
}
