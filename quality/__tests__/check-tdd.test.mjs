import { test } from 'node:test';
import assert from 'node:assert/strict';
import { evaluarTdd } from '../check-tdd.mjs';

test('pasa cuando el cambio en src viene con cambio en tests', () => {
  const r = evaluarTdd({
    archivosCambiados: [
      'CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Aviso.cs',
      'CaseritoApp/tests/CaseritoApp.UnitTests/AvisoTests.cs',
    ],
    mensajeCommit: 'feat(catalog): valida titulo del aviso',
  });
  assert.equal(r.ok, true);
  assert.equal(r.exencion, false);
});

test('falla cuando hay cambio en src sin ningun cambio en tests', () => {
  const r = evaluarTdd({
    archivosCambiados: ['CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Aviso.cs'],
    mensajeCommit: 'feat(catalog): valida titulo del aviso',
  });
  assert.equal(r.ok, false);
  assert.match(r.motivo, /Aviso\.cs/);
});

test('ignora migraciones, Program.cs y archivos de proyecto', () => {
  const r = evaluarTdd({
    archivosCambiados: [
      'CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Migrations/20260101_X.cs',
      'CaseritoApp/src/Host/CaseritoApp.Host/Program.cs',
      'CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/CaseritoApp.Catalog.Domain.csproj',
    ],
    mensajeCommit: 'chore: migracion',
  });
  assert.equal(r.ok, true);
});

test('la exencion requiere un motivo de al menos 20 caracteres', () => {
  const corto = evaluarTdd({
    archivosCambiados: ['CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Aviso.cs'],
    mensajeCommit: 'refactor: renombra [sin-test] menor',
  });
  assert.equal(corto.ok, false);
  assert.match(corto.motivo, /20 caracteres/);
});

test('la exencion con motivo suficiente pasa y queda marcada', () => {
  const r = evaluarTdd({
    archivosCambiados: ['CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Aviso.cs'],
    mensajeCommit: 'refactor: [sin-test] renombrado puro sin cambio de comportamiento',
  });
  assert.equal(r.ok, true);
  assert.equal(r.exencion, true);
});

test('no exige tests cuando no hay cambios en src', () => {
  const r = evaluarTdd({
    archivosCambiados: ['docs/ai/WORKFLOW.md'],
    mensajeCommit: 'docs: actualiza workflow',
  });
  assert.equal(r.ok, true);
});
