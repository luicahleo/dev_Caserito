#!/usr/bin/env node
// Compara el mutation score de Stryker contra quality/mutation-baseline.json.
// Se ejecuta en el job nocturno, no en el gate de commit.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { exito, fallo } from './lib/salida.mjs';

const ESTADOS_EXCLUIDOS = new Set(['Ignored', 'CompileError']);
const ESTADOS_MATADO = new Set(['Killed', 'Timeout']);

export function buscarReporte(directorio) {
  for (const entrada of readdirSync(directorio)) {
    const ruta = join(directorio, entrada);
    if (statSync(ruta).isDirectory()) {
      const hallado = buscarReporte(ruta);
      if (hallado) return hallado;
    } else if (entrada === 'mutation-report.json') {
      return ruta;
    }
  }
  return null;
}

export function calcularScore(datos) {
  let matados = 0;
  let total = 0;

  for (const archivo of Object.values(datos.files ?? {})) {
    for (const mutante of archivo.mutants ?? []) {
      if (ESTADOS_EXCLUIDOS.has(mutante.status)) continue;
      total += 1;
      if (ESTADOS_MATADO.has(mutante.status)) matados += 1;
    }
  }

  const score = total === 0 ? 0 : Number(((matados / total) * 100).toFixed(2));
  return { matados, total, score };
}

export function compararConBaseline(score, baseline) {
  const caida = Number((baseline.score - score).toFixed(2));
  return { ok: caida <= baseline.tolerancia_pp, caida };
}

if (process.argv[1] && process.argv[1].endsWith('check-mutation.mjs')) {
  const reporte = buscarReporte('StrykerOutput');
  if (!reporte) {
    console.log(fallo('No se encontró mutation-report.json'));
    process.exit(1);
  }

  const datos = JSON.parse(readFileSync(reporte, 'utf8'));
  const { score } = calcularScore(datos);
  const baseline = JSON.parse(
    readFileSync(new URL('./mutation-baseline.json', import.meta.url), 'utf8'),
  );
  const { ok, caida } = compararConBaseline(score, baseline);

  if (!ok) {
    console.log(fallo(
      'Mutation score en regresión',
      `${baseline.score}% -> ${score}% (caída de ${caida} pp). ` +
      `Hay tests que dejaron de aseverar comportamiento.`,
    ));
    process.exit(1);
  }

  console.log(exito(`Mutation score: ${score}% (baseline ${baseline.score}%)`));
}
