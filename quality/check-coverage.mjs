#!/usr/bin/env node
// Gate de cobertura: no-regresion contra quality/coverage-baseline.json.
// No exige un minimo absoluto; solo prohibe empeorar.
// Ver docs/ai/PUERTA_CALIDAD.md para actualizar la baseline legitimamente.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { exito, fallo } from './lib/salida.mjs';

export const TOLERANCIA_PP = 0.5;

export function compararCobertura(actual, baseline, tolerancia) {
  const regresiones = [];
  // Reporte vacío del todo (colección/pipeline rota) es la única señal fiable
  // de "desapareció": un solo proyecto ausente en un reporte no vacío puede
  // deberse a un rename u otra causa legítima, y no debe hacer fallar el gate.
  const reporteVacio = Object.keys(actual).length === 0 && Object.keys(baseline).length > 0;

  for (const [proyecto, esperado] of Object.entries(baseline)) {
    const medido = actual[proyecto];

    if (!medido) {
      if (reporteVacio) {
        regresiones.push({
          proyecto,
          motivo: `El proyecto no aparece en el reporte de cobertura. ` +
                  `¿Se eliminó, o dejó de ejecutarse su suite?`,
        });
      }
      continue;
    }

    for (const metrica of ['linea', 'rama']) {
      const caida = esperado[metrica] - medido[metrica];
      if (caida > tolerancia) {
        regresiones.push({
          proyecto,
          metrica,
          baseline: esperado[metrica],
          actual: medido[metrica],
          caida: Number(caida.toFixed(2)),
          motivo: `Cobertura de ${metrica} cayó ${caida.toFixed(2)} pp ` +
                  `(${esperado[metrica]}% -> ${medido[metrica]}%).`,
        });
      }
    }
  }

  return { ok: regresiones.length === 0, regresiones };
}

// Parsea un cobertura.xml de coverlet y agrega por ensamblado.
export function parsearCobertura(xml) {
  const proyectos = {};
  const patron = /<package\s+name="([^"]+)"\s+line-rate="([^"]+)"\s+branch-rate="([^"]+)"/g;

  for (const [, nombre, linea, rama] of xml.matchAll(patron)) {
    proyectos[nombre] = {
      linea: Number((Number(linea) * 100).toFixed(2)),
      rama: Number((Number(rama) * 100).toFixed(2)),
    };
  }

  return proyectos;
}

// Combina la cobertura de varios reportes del mismo proyecto (uno por cada
// suite de test que lo ejercita) quedándose con el máximo por métrica.
// Object.assign directo sobrescribiría con el último reporte procesado
// -en orden no determinista, por nombre de carpeta GUID- perdiendo cobertura
// real medida por otra suite.
export function combinarCobertura(actual, nuevo) {
  for (const [proyecto, medido] of Object.entries(nuevo)) {
    const previo = actual[proyecto];
    actual[proyecto] = previo
      ? { linea: Math.max(previo.linea, medido.linea), rama: Math.max(previo.rama, medido.rama) }
      : medido;
  }
  return actual;
}

function buscarReportes(directorio) {
  const encontrados = [];
  for (const entrada of readdirSync(directorio)) {
    const ruta = join(directorio, entrada);
    if (statSync(ruta).isDirectory()) {
      encontrados.push(...buscarReportes(ruta));
    } else if (entrada === 'coverage.cobertura.xml') {
      encontrados.push(ruta);
    }
  }
  return encontrados;
}

if (process.argv[1] && process.argv[1].endsWith('check-coverage.mjs')) {
  const directorio = process.argv[2];
  if (!directorio) {
    console.log(fallo('Uso: node quality/check-coverage.mjs <directorio-de-resultados>'));
    process.exit(1);
  }

  const actual = {};
  for (const reporte of buscarReportes(directorio)) {
    combinarCobertura(actual, parsearCobertura(readFileSync(reporte, 'utf8')));
  }

  const baseline = JSON.parse(
    readFileSync(new URL('./coverage-baseline.json', import.meta.url), 'utf8'),
  );

  const resultado = compararCobertura(actual, baseline.proyectos, TOLERANCIA_PP);

  if (!resultado.ok) {
    const detalle = resultado.regresiones
      .map((r) => `${r.proyecto}: ${r.motivo}`)
      .join('\n       ');
    console.log(fallo('Gate de cobertura', detalle));
    process.exit(1);
  }

  console.log(exito(`Gate de cobertura (${Object.keys(actual).length} proyectos)`));
}
