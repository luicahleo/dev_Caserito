import { test } from 'node:test';
import assert from 'node:assert/strict';
import { calcularScore, compararConBaseline } from '../check-mutation.mjs';

function mutante(status) {
  return { status };
}

test('calcula el score sobre mutantes matados vs. total elegible', () => {
  const datos = {
    files: {
      'Archivo.cs': {
        mutants: [mutante('Killed'), mutante('Survived'), mutante('Timeout'), mutante('NoCoverage')],
      },
    },
  };
  const { matados, total, score } = calcularScore(datos);
  assert.equal(matados, 2);
  assert.equal(total, 4);
  assert.equal(score, 50);
});

test('excluye Ignored y CompileError del total', () => {
  const datos = {
    files: {
      'Archivo.cs': {
        mutants: [mutante('Killed'), mutante('Ignored'), mutante('CompileError')],
      },
    },
  };
  const { total, score } = calcularScore(datos);
  assert.equal(total, 1);
  assert.equal(score, 100);
});

test('devuelve score 0 cuando no hay mutantes elegibles', () => {
  const datos = { files: { 'Archivo.cs': { mutants: [mutante('Ignored')] } } };
  const { score, total } = calcularScore(datos);
  assert.equal(total, 0);
  assert.equal(score, 0);
});

test('suma mutantes de varios archivos', () => {
  const datos = {
    files: {
      'A.cs': { mutants: [mutante('Killed')] },
      'B.cs': { mutants: [mutante('Survived')] },
    },
  };
  const { matados, total } = calcularScore(datos);
  assert.equal(matados, 1);
  assert.equal(total, 2);
});

test('pasa cuando el score se mantiene igual a la baseline', () => {
  const r = compararConBaseline(80, { score: 80, tolerancia_pp: 1.0 });
  assert.equal(r.ok, true);
  assert.equal(r.caida, 0);
});

test('pasa cuando el score sube', () => {
  const r = compararConBaseline(85, { score: 80, tolerancia_pp: 1.0 });
  assert.equal(r.ok, true);
});

test('pasa cuando la caída está dentro de la tolerancia', () => {
  const r = compararConBaseline(79.5, { score: 80, tolerancia_pp: 1.0 });
  assert.equal(r.ok, true);
});

test('falla cuando la caída excede la tolerancia', () => {
  const r = compararConBaseline(70, { score: 80, tolerancia_pp: 1.0 });
  assert.equal(r.ok, false);
  assert.equal(r.caida, 10);
});
