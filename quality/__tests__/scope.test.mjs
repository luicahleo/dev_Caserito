import test from 'node:test';
import assert from 'node:assert/strict';
import {
  detectarCambiosPendientes,
  determinarAlcance,
  gateAplicaAlAlcance,
  validarModo,
} from '../scope.mjs';

test('clasifica cambios exclusivos del frontend', () => {
  assert.deepEqual(determinarAlcance(['web/src/routes/LoginPage.tsx']), {
    backend: false,
    frontend: true,
  });
});

test('clasifica cambios exclusivos del backend', () => {
  assert.deepEqual(determinarAlcance(['CaseritoApp/src/Auth/Login.cs']), {
    backend: true,
    frontend: false,
  });
});

test('combina ambos alcances cuando cambian ambos árboles', () => {
  assert.deepEqual(
    determinarAlcance([
      'web/src/api/schema.d.ts',
      'CaseritoApp/src/Host/OpenApi.cs',
    ]),
    { backend: true, frontend: true },
  );
});

test('trata la infraestructura de calidad como transversal', () => {
  assert.deepEqual(determinarAlcance(['quality/verify.mjs']), {
    backend: true,
    frontend: true,
  });
});

test('no asigna stack a cambios exclusivamente documentales', () => {
  assert.deepEqual(determinarAlcance(['docs/ai/PUERTA_CALIDAD.md']), {
    backend: false,
    frontend: false,
  });
});

test('reúne cambios versionados y archivos no versionados sin duplicados', () => {
  const llamadas = [];
  const ejecutar = (_comando, args) => {
    llamadas.push(args);
    if (args[0] === 'diff') {
      return { codigo: 0, salida: 'web/src/App.tsx\nquality/nuevo.mjs\n' };
    }
    return { codigo: 0, salida: 'quality/nuevo.mjs\ndocs/nuevo.md\n' };
  };

  assert.deepEqual(detectarCambiosPendientes(ejecutar), {
    ok: true,
    archivos: ['docs/nuevo.md', 'quality/nuevo.mjs', 'web/src/App.tsx'],
  });
  assert.deepEqual(llamadas, [
    ['diff', '--name-only', 'HEAD'],
    ['ls-files', '--others', '--exclude-standard'],
  ]);
});

test('falla si Git no puede determinar los cambios', () => {
  const resultado = detectarCambiosPendientes(() => ({
    codigo: 1,
    salida: '',
  }));
  assert.equal(resultado.ok, false);
  assert.match(resultado.motivo, /Git/);
});

test('rechaza combinar los modos completo y por alcance', () => {
  assert.deepEqual(validarModo({ completo: true, porAlcance: true }), {
    ok: false,
    motivo: 'Los modos --full y --changed no se pueden combinar.',
  });
});

test('selecciona únicamente gates comunes y frontend', () => {
  const alcance = { backend: false, frontend: true };

  assert.equal(gateAplicaAlAlcance({}, alcance), true);
  assert.equal(gateAplicaAlAlcance({ grupo: 'frontend' }, alcance), true);
  assert.equal(gateAplicaAlAlcance({ grupo: 'backend' }, alcance), false);
});
