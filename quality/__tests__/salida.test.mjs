import { test } from 'node:test';
import assert from 'node:assert/strict';
import { exito, fallo, aviso } from '../lib/salida.mjs';

test('exito incluye el texto y una marca visible', () => {
  const linea = exito('Formato correcto');
  assert.match(linea, /Formato correcto/);
  assert.match(linea, /OK/);
});

test('fallo incluye el detalle cuando se proporciona', () => {
  const linea = fallo('Cobertura insuficiente', 'Identity.Domain: 80,1% < 84,3%');
  assert.match(linea, /Cobertura insuficiente/);
  assert.match(linea, /Identity\.Domain/);
});

test('aviso se distingue de un fallo', () => {
  assert.notEqual(aviso('Exención en uso'), fallo('Exención en uso'));
});
