import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const raiz = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const leer = (ruta) => readFile(resolve(raiz, ruta), 'utf8');

test('el contexto Docker niega todo salvo el payload requerido', async () => {
  const contenido = await leer('deploy/.dockerignore');

  assert.deepEqual(
    contenido.trim().split(/\r?\n/),
    ['**', '!web/**', '!Dockerfile.web', '!.dockerignore'],
  );
});

test('Compose despliega una imagen etiquetada sin construir en producción', async () => {
  const contenido = await leer('docker-compose.yml');

  assert.match(contenido, /image:\s*caseritoapp:\$\{CASERITOAPP_IMAGE_TAG:-latest\}/);
  assert.doesNotMatch(contenido, /^\s+build:/m);
});

test('el workflow serializa y restringe el despliegue de producción', async () => {
  const contenido = await leer('.github/workflows/deploy.yml');

  assert.match(contenido, /permissions:\s*\r?\n\s+contents:\s*read/);
  assert.match(contenido, /concurrency:\s*\r?\n\s+group:\s*caseritoapp-production/);
  assert.match(contenido, /cancel-in-progress:\s*false/);
  assert.match(contenido, /environment:\s*production/);
  assert.match(contenido, /timeout-minutes:/);
  assert.match(contenido, /github\.ref == 'refs\/heads\/master'/);

  const actions = [...contenido.matchAll(/^\s*-?\s*uses:\s*([^\s#]+)(?:\s*#.*)?$/gm)];
  assert.ok(actions.length >= 4);
  for (const [, action] of actions) {
    assert.match(action, /^[^@]+@[0-9a-f]{40}$/);
  }
});

test('el workflow usa releases aislados y rollback verificable', async () => {
  const contenido = await leer('.github/workflows/deploy.yml');

  assert.match(contenido, /deploy\/.dockerignore/);
  assert.match(contenido, /releases\/\$\{GITHUB_SHA\}/);
  assert.match(contenido, /CaseritoApp\.Host\.dll/);
  assert.match(contenido, /wwwroot\/index\.html/);
  assert.match(contenido, /docker build[^\n]+caseritoapp:/);
  assert.match(contenido, /imagen_anterior=/);
  assert.match(contenido, /rollback/);
  assert.match(contenido, /docker image tag[^\n]+caseritoapp:latest/);
  assert.match(contenido, /http:\/\/127\.0\.0\.1:8084\/health/);
  assert.match(contenido, /https:\/\/caserito\.app\/health/);
  assert.match(contenido, /set -Eeuo pipefail/);
  assert.doesNotMatch(contenido, /\|\|\s*true/);
  assert.doesNotMatch(contenido, /docker system prune/);

  assert.ok(
    contenido.indexOf('install -m 0644 "$release/.dockerignore"') <
      contenido.indexOf('docker build --file'),
  );
  assert.ok(
    contenido.indexOf('https://caserito.app/health') <
      contenido.indexOf('docker image tag "caseritoapp:$sha" caseritoapp:latest'),
  );
});
