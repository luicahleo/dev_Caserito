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
