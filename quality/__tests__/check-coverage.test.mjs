import { test } from 'node:test';
import assert from 'node:assert/strict';
import { compararCobertura, combinarCobertura, resolverTolerancia, TOLERANCIA_PP } from '../check-coverage.mjs';

const TOLERANCIA = 0.5;

test('pasa cuando la cobertura se mantiene', () => {
  const r = compararCobertura(
    { 'CaseritoApp.Catalog.Domain': { linea: 84.3, rama: 70.0 } },
    { 'CaseritoApp.Catalog.Domain': { linea: 84.3, rama: 70.0 } },
    TOLERANCIA,
  );
  assert.equal(r.ok, true);
});

test('pasa cuando la cobertura sube', () => {
  const r = compararCobertura(
    { 'CaseritoApp.Catalog.Domain': { linea: 90.0, rama: 75.0 } },
    { 'CaseritoApp.Catalog.Domain': { linea: 84.3, rama: 70.0 } },
    TOLERANCIA,
  );
  assert.equal(r.ok, true);
});

test('tolera una caida dentro del margen de ruido', () => {
  const r = compararCobertura(
    { 'CaseritoApp.Catalog.Domain': { linea: 84.0, rama: 70.0 } },
    { 'CaseritoApp.Catalog.Domain': { linea: 84.3, rama: 70.0 } },
    TOLERANCIA,
  );
  assert.equal(r.ok, true);
});

test('falla cuando la cobertura de linea cae mas que la tolerancia', () => {
  const r = compararCobertura(
    { 'CaseritoApp.Catalog.Domain': { linea: 80.0, rama: 70.0 } },
    { 'CaseritoApp.Catalog.Domain': { linea: 84.3, rama: 70.0 } },
    TOLERANCIA,
  );
  assert.equal(r.ok, false);
  assert.equal(r.regresiones.length, 1);
  assert.equal(r.regresiones[0].proyecto, 'CaseritoApp.Catalog.Domain');
});

test('falla cuando la cobertura de rama cae mas que la tolerancia', () => {
  const r = compararCobertura(
    { 'CaseritoApp.Catalog.Domain': { linea: 84.3, rama: 60.0 } },
    { 'CaseritoApp.Catalog.Domain': { linea: 84.3, rama: 70.0 } },
    TOLERANCIA,
  );
  assert.equal(r.ok, false);
});

test('un proyecto nuevo sin baseline no hace fallar el gate', () => {
  const r = compararCobertura(
    { 'CaseritoApp.Pagos.Domain': { linea: 10.0, rama: 5.0 } },
    { 'CaseritoApp.Catalog.Domain': { linea: 84.3, rama: 70.0 } },
    TOLERANCIA,
  );
  assert.equal(r.ok, true);
});

test('un proyecto que desaparece del reporte hace fallar el gate', () => {
  const r = compararCobertura(
    {},
    { 'CaseritoApp.Catalog.Domain': { linea: 84.3, rama: 70.0 } },
    TOLERANCIA,
  );
  assert.equal(r.ok, false);
  assert.match(r.regresiones[0].motivo, /no aparece/);
});

test('combinarCobertura conserva un proyecto que solo aparece en un reporte', () => {
  const actual = { 'CaseritoApp.Catalog.Domain': { linea: 84.3, rama: 70.0 } };
  const r = combinarCobertura(actual, { 'CaseritoApp.Chat.Domain': { linea: 90.0, rama: 80.0 } });
  assert.deepEqual(r, {
    'CaseritoApp.Catalog.Domain': { linea: 84.3, rama: 70.0 },
    'CaseritoApp.Chat.Domain': { linea: 90.0, rama: 80.0 },
  });
});

test('combinarCobertura se queda con el maximo por metrica cuando el proyecto aparece en dos reportes', () => {
  const actual = { 'CaseritoApp.Catalog.Domain': { linea: 84.3, rama: 93.1 } };
  const r = combinarCobertura(actual, { 'CaseritoApp.Catalog.Domain': { linea: 0, rama: 100 } });
  assert.deepEqual(r, { 'CaseritoApp.Catalog.Domain': { linea: 84.3, rama: 100 } });
});

test('combinarCobertura muta y devuelve el mismo acumulador recibido', () => {
  const actual = {};
  const r = combinarCobertura(actual, { 'CaseritoApp.Catalog.Domain': { linea: 50, rama: 50 } });
  assert.equal(r, actual);
});

test('resolverTolerancia usa el valor de la baseline cuando esta presente', () => {
  assert.equal(resolverTolerancia({ tolerancia_pp: 1.5 }), 1.5);
});

test('resolverTolerancia cae a TOLERANCIA_PP cuando la baseline no trae el campo', () => {
  assert.equal(resolverTolerancia({}), TOLERANCIA_PP);
});
