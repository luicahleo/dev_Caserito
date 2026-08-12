#!/usr/bin/env node
// Puerta de calidad de CaseritoApp.
//   node quality/verify.mjs           -> nivel rápido, sin Docker
//   node quality/verify.mjs --changed -> nivel rápido según cambios pendientes
//   node quality/verify.mjs --full    -> nivel completo, requiere Docker
// Se detiene en el primer gate que falla.

import { fileURLToPath } from 'node:url';
import { join } from 'node:path';
import { writeFileSync } from 'node:fs';
import { ejecutar } from './lib/ejecutar.mjs';
import { titulo, exito, fallo } from './lib/salida.mjs';
import {
  detectarCambiosPendientes,
  determinarAlcance,
  gateAplicaAlAlcance,
  validarModo,
} from './scope.mjs';

const completo = process.argv.includes('--full');
const porAlcance = process.argv.includes('--changed');
const modoValido = validarModo({ completo, porAlcance });
if (!modoValido.ok) {
  console.log(fallo('Modo de verificación inválido', modoValido.motivo));
  process.exit(1);
}

let alcance = { backend: true, frontend: true };
if (porAlcance) {
  const cambios = detectarCambiosPendientes();
  if (!cambios.ok) {
    console.log(fallo('No se pudo determinar el alcance', cambios.motivo));
    process.exit(1);
  }
  if (cambios.archivos.length === 0) {
    console.log(fallo('No hay cambios pendientes', 'Usa verify sin --changed para revisar todo.'));
    process.exit(1);
  }
  alcance = determinarAlcance(cambios.archivos);
  const grupos = [alcance.backend && 'backend', alcance.frontend && 'frontend'].filter(Boolean);
  console.log(titulo(`Alcance: ${grupos.join(' + ') || 'documentación'}`));
}
// fileURLToPath, no .pathname: en Windows .pathname produce "/C:/..." y rompe spawn.
const raiz = fileURLToPath(new URL('..', import.meta.url));
const backend = join(raiz, 'CaseritoApp');
const frontend = join(raiz, 'web');

// Cada gate: { nombre, comando, args, cwd, soloCompleto, soloRapido }
const gates = [
  {
    nombre: 'Gate TDD',
    comando: 'node',
    args: ['quality/check-tdd.mjs'],
    cwd: raiz,
  },
  {
    nombre: 'Formato .NET',
    grupo: 'backend',
    comando: 'dotnet',
    // Se excluyen los analizadores de mantenibilidad: se miden con el gate de
    // complejidad, no con dotnet format, que fallaría solo por reportarlos
    // aunque no tengan una corrección automática disponible.
    args: ['format', 'CaseritoApp.sln', '--verify-no-changes',
           '--exclude-diagnostics', 'CA1502', 'CA1505', 'CA1506'],
    cwd: backend,
  },
  {
    nombre: 'Build Release (warnings as errors)',
    grupo: 'backend',
    comando: 'dotnet',
    args: ['build', 'CaseritoApp.sln', '--configuration', 'Release'],
    cwd: backend,
  },
  {
    nombre: 'Complejidad contra baseline',
    grupo: 'backend',
    comando: 'node',
    args: ['quality/check-complexity.mjs', 'artifacts-build.log'],
    cwd: raiz,
  },
  {
    // Solo en el nivel rápido: el nivel completo ejecuta la suite entera
    // (unit + arquitectura + integración) de una sola vez vía 'Tests con
    // cobertura', más abajo. Repetirla aquí duplicaría toda la ejecución.
    nombre: 'Unit tests',
    grupo: 'backend',
    comando: 'dotnet',
    args: ['test', 'tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj',
           '--no-build', '--configuration', 'Release'],
    cwd: backend,
    soloRapido: true,
  },
  {
    nombre: 'Tests de arquitectura',
    grupo: 'backend',
    comando: 'dotnet',
    args: ['test', 'tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj',
           '--no-build', '--configuration', 'Release'],
    cwd: backend,
    soloRapido: true,
  },
  { nombre: 'Lint web', grupo: 'frontend', comando: 'npm', args: ['run', 'lint'], cwd: frontend },
  { nombre: 'Typecheck web', grupo: 'frontend', comando: 'npm', args: ['run', 'typecheck'], cwd: frontend },
  { nombre: 'Tests web', grupo: 'frontend', comando: 'npm', args: ['run', 'test'], cwd: frontend },
  {
    // Sustituye, en el nivel completo, a las ejecuciones de tests por
    // separado: corre unit + arquitectura + integración de una sola vez y
    // con recolección de cobertura, para alimentar el gate de abajo.
    nombre: 'Tests con cobertura',
    grupo: 'backend',
    comando: 'dotnet',
    args: ['test', 'CaseritoApp.sln', '--configuration', 'Release',
           '--collect:"XPlat Code Coverage"',
           '--results-directory', 'artifacts/coverage'],
    cwd: backend,
    soloCompleto: true,
  },
  {
    nombre: 'Cobertura contra baseline',
    grupo: 'backend',
    comando: 'node',
    args: ['quality/check-coverage.mjs', 'CaseritoApp/artifacts/coverage'],
    cwd: raiz,
    soloCompleto: true,
  },
];

let fallidos = 0;

for (const gate of gates) {
  if (gate.soloCompleto && !completo) continue;
  if (gate.soloRapido && completo) continue;
  if (porAlcance && !gateAplicaAlAlcance(gate, alcance)) continue;

  console.log(titulo(gate.nombre));
  const resultado = ejecutar(gate.comando, gate.args, { cwd: gate.cwd });
  const { codigo, duracionMs } = resultado;

  if (gate.nombre === 'Build Release (warnings as errors)') {
    writeFileSync(join(raiz, 'artifacts-build.log'), resultado.salida, 'utf8');
  }

  if (codigo !== 0) {
    console.log(fallo(`${gate.nombre} falló`, `código ${codigo}`));
    fallidos += 1;
    break; // retroalimentación rápida: no seguir tras el primer fallo
  }
  console.log(exito(`${gate.nombre} (${duracionMs} ms)`));
}

if (fallidos > 0) {
  console.log(fallo('La puerta de calidad no pasó. Arregla el código, no el gate.'));
  process.exit(1);
}

console.log(exito(completo ? 'Puerta de calidad completa: verde.'
                           : porAlcance ? 'Puerta de calidad por alcance: verde.'
                                        : 'Puerta de calidad rápida: verde.'));
