#!/usr/bin/env node
// Gate de complejidad: cuenta las violaciones de los analizadores de
// mantenibilidad y falla si el número sube respecto a la baseline.
// La deuda solo puede decrecer.

import { readFileSync } from 'node:fs';
import { exito, fallo } from './lib/salida.mjs';

const REGLAS = ['CA1502', 'CA1505', 'CA1506'];

export function contarViolaciones(salidaBuild) {
  const vistas = new Set();

  for (const linea of salidaBuild.split('\n')) {
    const coincidencia = linea.match(/^(.+?)\((\d+),(\d+)\):\s+warning\s+(CA\d+):/);
    if (!coincidencia) continue;

    const [, archivo, fila, columna, regla] = coincidencia;
    if (!REGLAS.includes(regla)) continue;

    // MSBuild repite el mismo warning una vez por target; deduplicar.
    vistas.add(`${regla}|${archivo}|${fila}|${columna}`);
  }

  const conteo = {};
  for (const clave of vistas) {
    const regla = clave.split('|')[0];
    conteo[regla] = (conteo[regla] ?? 0) + 1;
  }
  return conteo;
}

export function compararComplejidad(actual, baseline) {
  const aumentos = [];

  for (const regla of new Set([...Object.keys(actual), ...Object.keys(baseline)])) {
    const previo = baseline[regla] ?? 0;
    const ahora = actual[regla] ?? 0;
    if (ahora > previo) {
      aumentos.push({ regla, baseline: previo, actual: ahora });
    }
  }

  return { ok: aumentos.length === 0, aumentos };
}

if (process.argv[1] && process.argv[1].endsWith('check-complexity.mjs')) {
  const rutaLog = process.argv[2];
  if (!rutaLog) {
    console.log(fallo('Uso: node quality/check-complexity.mjs <ruta-log-build>'));
    process.exit(1);
  }

  const actual = contarViolaciones(readFileSync(rutaLog, 'utf8'));
  const baseline = JSON.parse(
    readFileSync(new URL('./complexity-baseline.json', import.meta.url), 'utf8'),
  ).reglas;

  const resultado = compararComplejidad(actual, baseline);

  if (!resultado.ok) {
    const detalle = resultado.aumentos
      .map((a) => `${a.regla}: ${a.baseline} -> ${a.actual} violaciones`)
      .join('\n       ');
    console.log(fallo('Gate de complejidad', detalle +
      '\n       Simplifica el código. No subas la baseline.'));
    process.exit(1);
  }

  const total = Object.values(actual).reduce((a, b) => a + b, 0);
  console.log(exito(`Gate de complejidad (${total} violaciones, sin aumentos)`));
}
