import { test } from 'node:test';
import assert from 'node:assert/strict';
import { contarViolaciones, compararComplejidad } from '../check-complexity.mjs';

const SALIDA_BUILD = `
/repo/src/Catalog/Aviso.cs(42,17): warning CA1502: 'Validar' tiene complejidad ciclomatica de '12'
/repo/src/Orders/Orden.cs(88,9): warning CA1502: 'Cerrar' tiene complejidad ciclomatica de '9'
/repo/src/Chat/Sala.cs(10,5): warning CA1505: 'Sala' tiene indice de mantenibilidad bajo
/repo/src/Chat/Sala.cs(10,5): warning CS8618: campo no anulable
`;

test('cuenta las violaciones por regla e ignora otros warnings', () => {
  const conteo = contarViolaciones(SALIDA_BUILD);
  assert.equal(conteo.CA1502, 2);
  assert.equal(conteo.CA1505, 1);
  assert.equal(conteo.CA1506, undefined);
  assert.equal(conteo.CS8618, undefined);
});

test('no cuenta dos veces la misma linea repetida por multi-target', () => {
  const conteo = contarViolaciones(SALIDA_BUILD + SALIDA_BUILD);
  assert.equal(conteo.CA1502, 2);
});

test('pasa cuando el conteo se mantiene', () => {
  const r = compararComplejidad({ CA1502: 2, CA1505: 1 }, { CA1502: 2, CA1505: 1 });
  assert.equal(r.ok, true);
});

test('pasa cuando el conteo baja', () => {
  const r = compararComplejidad({ CA1502: 0 }, { CA1502: 2 });
  assert.equal(r.ok, true);
});

test('falla cuando el conteo sube', () => {
  const r = compararComplejidad({ CA1502: 5 }, { CA1502: 2 });
  assert.equal(r.ok, false);
  assert.equal(r.aumentos[0].regla, 'CA1502');
  assert.equal(r.aumentos[0].actual, 5);
});

test('falla cuando aparece una regla que no estaba en la baseline', () => {
  const r = compararComplejidad({ CA1506: 1 }, { CA1502: 2 });
  assert.equal(r.ok, false);
});
