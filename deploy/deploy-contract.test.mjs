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
  assert.match(
    contenido,
    /curl -fsS -H 'Host: caserito\.app' http:\/\/localhost:8080\/health/,
  );
});

test('el workflow serializa y restringe el despliegue de producción', async () => {
  const contenido = await leer('.github/workflows/deploy.yml');

  assert.match(contenido, /workflow_run:/);
  assert.match(contenido, /workflows:\s*\[CI\]/);
  assert.match(contenido, /types:\s*\[completed\]/);
  assert.doesNotMatch(contenido, /workflow_dispatch:/);
  assert.match(contenido, /github\.event\.workflow_run\.conclusion == 'success'/);
  assert.match(contenido, /github\.event\.workflow_run\.head_branch == 'master'/);
  assert.match(contenido, /permissions:\s*\r?\n\s+contents:\s*read/);
  assert.match(contenido, /concurrency:\s*\r?\n\s+group:\s*caseritoapp-production/);
  assert.match(contenido, /cancel-in-progress:\s*false/);
  assert.match(contenido, /environment:\s*production/);
  assert.match(contenido, /timeout-minutes:/);
  assert.match(contenido, /RELEASE_SHA:\s*\$\{\{ github\.event\.workflow_run\.head_sha \}\}/);

  const actions = [...contenido.matchAll(/^\s*-?\s*uses:\s*([^\s#]+)(?:\s*#.*)?$/gm)];
  assert.ok(actions.length >= 4);
  for (const [, action] of actions) {
    assert.match(action, /^[^@]+@[0-9a-f]{40}$/);
  }
});

test('el workflow usa releases aislados y rollback verificable', async () => {
  const contenido = await leer('.github/workflows/deploy.yml');

  assert.match(contenido, /deploy\/.dockerignore/);
  assert.match(contenido, /releases\/\$\{RELEASE_SHA\}/);
  assert.match(contenido, /CaseritoApp\.Host\.dll/);
  assert.match(contenido, /wwwroot\/index\.html/);
  assert.match(contenido, /docker build[^\n]+caseritoapp:/);
  assert.match(contenido, /imagen_anterior=/);
  assert.match(contenido, /rollback/);
  assert.match(contenido, /docker image tag[^\n]+caseritoapp:latest/);
  assert.match(contenido, /http:\/\/127\.0\.0\.1:8084\/health/);
  assert.match(
    contenido,
    /-H 'Host: caserito\.app'[^\n]*http:\/\/127\.0\.0\.1:8084\/health/,
  );
  assert.match(contenido, /https:\/\/caserito\.app\/health/);
  assert.match(contenido, /set -Eeuo pipefail/);
  assert.doesNotMatch(contenido, /\|\|\s*true/);
  assert.doesNotMatch(contenido, /docker system prune/);

  assert.ok(
    contenido.indexOf('install -m 0644 "$release/.dockerignore"') <
      contenido.indexOf('docker build --file "$release/Dockerfile.web"'),
  );
  assert.ok(
    contenido.indexOf('https://caserito.app/health') <
      contenido.indexOf('docker image tag "caseritoapp:$sha" caseritoapp:latest'),
  );
});

test('la imagen final supera un smoke Linux antes de usar SSH', async () => {
  const contenido = await leer('.github/workflows/deploy.yml');
  const dockerfile = await leer('Dockerfile.web');

  assert.match(dockerfile, /^HEALTHCHECK\s/m);
  assert.match(contenido, /docker build[^\n]+caseritoapp:\$\{RELEASE_SHA\}-runner/);
  assert.match(contenido, /docker run[^\n]+--restart=no/);
  assert.match(contenido, /ASPNETCORE_ENVIRONMENT=Testing/);
  assert.match(contenido, /docker inspect[^\n]+\.State/);
  assert.match(contenido, /docker logs --tail 200/);
  assert.ok(
    contenido.indexOf('Smoke de la imagen Linux final') <
      contenido.indexOf('Configurar agente SSH'),
  );
});

test('el VPS valida un candidato separado antes de promover', async () => {
  const contenido = await leer('.github/workflows/deploy.yml');

  assert.match(contenido, /Kyc__ClaveHuellaCi/);
  assert.match(contenido, /caseritoapp-candidate-/);
  assert.match(contenido, /--restart=no/);
  assert.match(contenido, /127\.0\.0\.1:18084:8080/);
  assert.match(contenido, /docker inspect[^\n]+\.State/);
  assert.match(contenido, /docker logs --tail 200/);
  assert.doesNotMatch(contenido, /\.Config\.Env/);

  const candidato = contenido.indexOf('Validando candidato aislado');
  const promocion = contenido.indexOf('Promoviendo candidato');
  const externo = contenido.lastIndexOf('https://caserito.app/health');
  const latest = contenido.indexOf('caseritoapp:latest');
  assert.ok(candidato >= 0 && candidato < promocion);
  assert.ok(promocion < externo);
  assert.ok(externo < latest);
});

test('las migraciones conservan las columnas requeridas por un rollback', async () => {
  const migracionOriginal = await leer(
    'CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Migrations/20260804161339_IdentidadPerfilContract.cs',
  );
  const migracionCompensatoria = await leer(
    'CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Migrations/20260805090000_RestaurarPerfilLegacyParaRollback.cs',
  );

  assert.doesNotMatch(migracionOriginal, /DropColumn/);
  assert.match(migracionCompensatoria, /COL_LENGTH/);
  assert.match(migracionCompensatoria, /\[Ciudad\]/);
  assert.match(migracionCompensatoria, /\[Nombre\]/);
});
