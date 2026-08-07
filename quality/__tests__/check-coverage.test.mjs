import { test } from 'node:test';
import assert from 'node:assert/strict';
import { compararCobertura } from '../check-coverage.mjs';

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
