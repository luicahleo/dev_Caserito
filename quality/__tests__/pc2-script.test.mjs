import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const script = readFileSync(new URL('../../iniciar-pc2.ps1', import.meta.url), 'utf8');

test('las advertencias de docker info no detienen el inicio de PC2', () => {
  assert.match(script, /\$ErrorActionPreference = 'Continue'[\s\S]*docker info \*> \$null/);
  assert.match(script, /\$codigoDocker = \$LASTEXITCODE/);
  assert.match(script, /finally[\s\S]*\$ErrorActionPreference = \$preferenciaErrores/);
  assert.match(script, /if \(\$codigoDocker -ne 0\)/);
});

test('los fallos transitorios de curl reintentan el sondeo HTTPS', () => {
  assert.match(
    script,
    /for \(\$intento = 0; \$intento -lt 30[\s\S]*\$ErrorActionPreference = 'Continue'[\s\S]*curl\.exe/,
  );
  assert.match(script, /\$codigoSalud = \$LASTEXITCODE/);
  assert.match(script, /\$saludable = \$codigoSalud -eq 0/);
  assert.match(script, /if \(-not \$saludable\) \{ Start-Sleep -Seconds 1 \}/);
});

test('la recreación de datos exige confirmación y elimina volúmenes antes de iniciar', () => {
  assert.match(script, /\[switch\]\$RecrearDatos/);
  assert.match(script, /\[switch\]\$ConfirmarBorradoDatos/);
  assert.match(
    script,
    /if \(\$RecrearDatos -and -not \$ConfirmarBorradoDatos\)[\s\S]*throw/,
  );
  assert.match(
    script,
    /if \(\$RecrearDatos\)[\s\S]*docker compose @archivosCompose down --volumes[\s\S]*docker compose @archivosCompose up/,
  );
});

test('el entorno PC2 siempre incluye y espera a ARGOS', () => {
  assert.doesNotMatch(script, /\[switch\]\$Argos/);
  assert.match(script, /'docker-compose\.argos\.yml'/);
  assert.match(script, /Falta ARGOS en \.\.\/dev\/ARGOS/);
  assert.match(script, /docker inspect --format '\{\{\.State\.Health\.Status\}\}' caserito-argos/);
  assert.match(script, /ARGOS no alcanzó un estado saludable/);
});
