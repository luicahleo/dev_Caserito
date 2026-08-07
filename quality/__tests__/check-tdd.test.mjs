import { test } from 'node:test';
import assert from 'node:assert/strict';
import { evaluarTdd, archivosDelDiff, mensajesDelRango } from '../check-tdd.mjs';

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

test('archivosDelDiff falla explicitamente cuando git falla, en vez de reportar "sin cambios"', () => {
  const ejecutarFalso = () => ({ codigo: 128, salida: 'fatal: no se pudo resolver origin/master' });
  const r = archivosDelDiff(ejecutarFalso);
  assert.equal(r.ok, false);
  assert.match(r.motivo, /origin\/master/);
  // Anti-PII: el motivo no debe filtrar la salida cruda de git.
  assert.doesNotMatch(r.motivo, /fatal:/);
});

test('archivosDelDiff devuelve la lista de archivos cuando git responde bien', () => {
  const ejecutarFalso = () => ({ codigo: 0, salida: 'a.cs\nb.cs\n' });
  const r = archivosDelDiff(ejecutarFalso);
  assert.equal(r.ok, true);
  assert.deepEqual(r.archivos, ['a.cs', 'b.cs']);
});

test('mensajesDelRango falla explicitamente cuando git falla, en vez de dejar el mensaje vacio', () => {
  const ejecutarFalso = () => ({ codigo: 128, salida: 'fatal: rango invalido' });
  const r = mensajesDelRango(ejecutarFalso);
  assert.equal(r.ok, false);
  assert.match(r.motivo, /origin\/master/);
  assert.doesNotMatch(r.motivo, /fatal:/);
});

test('mensajesDelRango concatena los mensajes de todos los commits del rango, no solo el ultimo', () => {
  const ejecutarFalso = () => ({
    codigo: 0,
    salida: 'feat: primero sin exencion\nfix: segundo [sin-test] motivo largo suficiente aqui\n',
  });
  const r = mensajesDelRango(ejecutarFalso);
  assert.equal(r.ok, true);
  assert.match(r.mensaje, /\[sin-test\]/);
});
