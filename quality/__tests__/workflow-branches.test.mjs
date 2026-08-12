import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const leer = (ruta) => readFileSync(new URL(`../../${ruta}`, import.meta.url), 'utf8');

test('CI valida develop y master, nunca la rama obsoleta dev', () => {
  const workflow = leer('.github/workflows/ci.yml');

  assert.match(workflow, /branches:\s*\[\s*master,\s*develop\s*\]/);
  assert.doesNotMatch(workflow, /branches:\s*\[[^\]]*\bdev\b/);
});

test('producción requiere despacho manual desde master y CI verde del SHA', () => {
  const workflow = leer('.github/workflows/deploy.yml');

  assert.match(workflow, /workflow_dispatch:/);
  assert.doesNotMatch(workflow, /workflow_run:/);
  assert.match(workflow, /github\.ref == 'refs\/heads\/master'/);
  assert.match(workflow, /Verificar CI verde del commit/);
  assert.match(workflow, /conclusion.*success/);
});

test('las reglas reservan master para una promoción pedida por el usuario', () => {
  const reglas = leer('AGENTS.md');

  assert.match(
    reglas,
    /Nunca fusionar ni hacer push a\s+`master` sin pedido explícito del usuario/,
  );
  assert.match(reglas, /rama permanente `develop`/);
});

test('el trabajo cotidiano se realiza directamente en develop sin ramas obligatorias', () => {
  const reglas = leer('AGENTS.md');
  const workflow = leer('docs/ai/WORKFLOW.md');

  assert.match(reglas, /Trabajar directamente en la rama permanente `develop`/);
  assert.match(workflow, /No crear una rama de trabajo salvo pedido explícito/);
  assert.doesNotMatch(workflow, /crear una rama dedicada/);
  assert.doesNotMatch(workflow, /borrar la rama de trabajo/);
});
