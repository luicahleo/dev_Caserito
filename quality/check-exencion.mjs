#!/usr/bin/env node
// Un refactor trivial no toca quince archivos. Este gate impide que la exención
// [sin-test] se use para colar cambios grandes sin pruebas.

import { archivosDelDiff, mensajesDelRango } from './check-tdd.mjs';
import { exito, fallo } from './lib/salida.mjs';

export const LIMITE_ARCHIVOS = 5;

export function validarLimiteExencion({ archivosCambiados, mensajeCommit }) {
  if (!mensajeCommit.includes('[sin-test]')) {
    return { ok: true, motivo: 'Sin exención: el límite no aplica.' };
  }

  const enSrc = archivosCambiados.filter(
    (ruta) => ruta.startsWith('CaseritoApp/src/') && ruta.endsWith('.cs'),
  );

  if (enSrc.length > LIMITE_ARCHIVOS) {
    return {
      ok: false,
      motivo:
        `La exención [sin-test] modificó ${enSrc.length} archivos de src/, ` +
        `por encima del límite de ${LIMITE_ARCHIVOS}. ` +
        `Un cambio de este tamaño necesita pruebas.`,
    };
  }

  return { ok: true, motivo: `Exención dentro del límite (${enSrc.length}/${LIMITE_ARCHIVOS}).` };
}

// Punto de entrada CLI. Al importarse como módulo (tests) no se ejecuta.
// Reutiliza los helpers de check-tdd.mjs para que la exención tenga el mismo
// alcance que el gate TDD: toda la rama (origin/master...HEAD), no solo el
// último commit, y fallo explícito si git falla en vez de pasar en silencio.
if (process.argv[1] && process.argv[1].endsWith('check-exencion.mjs')) {
  const diff = archivosDelDiff();
  if (!diff.ok) {
    console.log(fallo('Límite de exención [sin-test]', diff.motivo));
    process.exit(1);
  }

  const rango = mensajesDelRango();
  if (!rango.ok) {
    console.log(fallo('Límite de exención [sin-test]', rango.motivo));
    process.exit(1);
  }

  const resultado = validarLimiteExencion({
    archivosCambiados: diff.archivos,
    mensajeCommit: rango.mensaje,
  });

  if (!resultado.ok) {
    console.log(fallo('Límite de exención [sin-test]', resultado.motivo));
    process.exit(1);
  }
  console.log(exito(resultado.motivo));
}
