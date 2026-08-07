#!/usr/bin/env node
// Puerta de calidad de CaseritoApp.
//   node quality/verify.mjs           -> nivel rápido, sin Docker
//   node quality/verify.mjs --full    -> nivel completo, requiere Docker
// Se detiene en el primer gate que falla.

import { fileURLToPath } from 'node:url';
import { join } from 'node:path';
import { ejecutar } from './lib/ejecutar.mjs';
import { titulo, exito, fallo } from './lib/salida.mjs';

const completo = process.argv.includes('--full');
// fileURLToPath, no .pathname: en Windows .pathname produce "/C:/..." y rompe spawn.
const raiz = fileURLToPath(new URL('..', import.meta.url));
const backend = join(raiz, 'CaseritoApp');
const frontend = join(raiz, 'web');

// Cada gate: { nombre, comando, args, cwd, soloCompleto }
const gates = [
  {
    nombre: 'Formato .NET',
    comando: 'dotnet',
    args: ['format', 'CaseritoApp.sln', '--verify-no-changes'],
    cwd: backend,
  },
  {
    nombre: 'Build Release (warnings as errors)',
    comando: 'dotnet',
    args: ['build', 'CaseritoApp.sln', '--configuration', 'Release'],
    cwd: backend,
  },
  {
    nombre: 'Unit tests',
    comando: 'dotnet',
    args: ['test', 'tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj',
           '--no-build', '--configuration', 'Release'],
    cwd: backend,
  },
  {
    nombre: 'Tests de arquitectura',
    comando: 'dotnet',
    args: ['test', 'tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj',
           '--no-build', '--configuration', 'Release'],
    cwd: backend,
  },
  { nombre: 'Lint web', comando: 'npm', args: ['run', 'lint'], cwd: frontend },
  { nombre: 'Typecheck web', comando: 'npm', args: ['run', 'typecheck'], cwd: frontend },
  { nombre: 'Tests web', comando: 'npm', args: ['run', 'test'], cwd: frontend },
  {
    nombre: 'Tests de integración (Docker)',
    comando: 'dotnet',
    args: ['test', 'tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj',
           '--no-build', '--configuration', 'Release'],
    cwd: backend,
    soloCompleto: true,
  },
];

let fallidos = 0;

for (const gate of gates) {
  if (gate.soloCompleto && !completo) continue;

  console.log(titulo(gate.nombre));
  const { codigo, duracionMs } = ejecutar(gate.comando, gate.args, { cwd: gate.cwd });

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
                           : 'Puerta de calidad rápida: verde.'));
