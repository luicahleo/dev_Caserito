#!/usr/bin/env node
// Gate TDD: todo cambio de comportamiento en src/ debe venir acompañado de
// cambios en tests/. Ver docs/ai/PUERTA_CALIDAD.md para el procedimiento.

import { ejecutar } from './lib/ejecutar.mjs';
import { exito, fallo, aviso } from './lib/salida.mjs';

const PREFIJO_SRC = 'CaseritoApp/src/';
const PREFIJO_TESTS = 'CaseritoApp/tests/';
const MOTIVO_MINIMO = 20;

// Rutas de src/ que no exigen test por no contener comportamiento propio.
const EXCLUIDOS = [
  /\/Migrations\//,
  /\/Program\.cs$/,
  /\.Designer\.cs$/,
  /\.csproj$/,
];

function exigeTest(ruta) {
  if (!ruta.startsWith(PREFIJO_SRC)) return false;
  if (!ruta.endsWith('.cs')) return false;
  return !EXCLUIDOS.some((patron) => patron.test(ruta));
}

export function evaluarTdd({ archivosCambiados, mensajeCommit }) {
  const sinTest = archivosCambiados.filter(exigeTest);

  if (sinTest.length === 0) {
    return { ok: true, motivo: 'No hay cambios de comportamiento en src/.', exencion: false };
  }

  const hayTests = archivosCambiados.some((ruta) => ruta.startsWith(PREFIJO_TESTS));
  if (hayTests) {
    return { ok: true, motivo: 'Los cambios en src/ vienen con cambios en tests/.', exencion: false };
  }

  const etiqueta = mensajeCommit.indexOf('[sin-test]');
  if (etiqueta === -1) {
    return {
      ok: false,
      exencion: false,
      motivo:
        `Se modificaron ${sinTest.length} archivo(s) de src/ sin tocar tests/:\n       ` +
        sinTest.join('\n       ') +
        '\n       Escribe el test, o justifica con "[sin-test] <motivo>" en el commit.',
    };
  }

  const razon = mensajeCommit.slice(etiqueta + '[sin-test]'.length).trim();
  if (razon.length < MOTIVO_MINIMO) {
    return {
      ok: false,
      exencion: false,
      motivo: `La exención [sin-test] exige un motivo de al menos ${MOTIVO_MINIMO} caracteres. Recibido: ${razon.length}.`,
    };
  }

  return { ok: true, exencion: true, motivo: razon };
}

function archivosDelDiff() {
  const { salida } = ejecutar('git', ['diff', '--name-only', 'origin/master...HEAD'], { silencioso: true });
  return salida.split('\n').map((l) => l.trim()).filter(Boolean);
}

function ultimoMensaje() {
  const { salida } = ejecutar('git', ['log', '-1', '--pretty=%B'], { silencioso: true });
  return salida.trim();
}

// Punto de entrada CLI. Al importarse como módulo (tests) no se ejecuta.
if (process.argv[1] && process.argv[1].endsWith('check-tdd.mjs')) {
  const resultado = evaluarTdd({
    archivosCambiados: archivosDelDiff(),
    mensajeCommit: ultimoMensaje(),
  });

  if (!resultado.ok) {
    console.log(fallo('Gate TDD', resultado.motivo));
    process.exit(1);
  }
  if (resultado.exencion) {
    console.log(aviso(`Gate TDD eximido con [sin-test]: ${resultado.motivo}`));
  } else {
    console.log(exito('Gate TDD'));
  }
}
