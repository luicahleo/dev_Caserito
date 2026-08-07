# Puerta de calidad determinista — plan de implementación

> **Para agentes ejecutores:** SUB-SKILL REQUERIDA: usar
> `superpowers:subagent-driven-development` (recomendado) o
> `superpowers:executing-plans` para implementar este plan tarea por tarea.
> Los pasos usan casillas (`- [ ]`) para seguimiento.

**Objetivo:** construir una puerta de calidad determinista que permita mergear
cambios sin que un humano lea el código.

**Arquitectura:** un punto de entrada único (`verify.ps1` / `verify.sh`) que
delega en scripts Node bajo `quality/`. Cada gate compara contra una baseline
versionada y falla solo ante regresiones. Las reglas de arquitectura se expresan
como tests xUnit con NetArchTest. El mutation testing corre aparte, de noche.

**Stack:** .NET 10, xUnit, NetArchTest.Rules 1.3.2, coverlet, Stryker.NET,
Node 22 (runner `node:test` integrado), GitHub Actions, Husky.Net.

**Spec:** `docs/superpowers/specs/2026-08-07-puerta-calidad-design.md`

## Restricciones globales

- Todos los comandos .NET se ejecutan desde `CaseritoApp/`; los de npm desde `web/`.
- Textos de UI, mensajes de error de los scripts y comentarios en **español con
  acentos y UTF-8**. Nunca mojibake.
- Anti-PII: ningún script imprime contenido de archivos de datos, tokens,
  cadenas de conexión ni valores de configuración. Solo rutas y métricas.
- `TreatWarningsAsErrors` está activo. Cualquier analizador nuevo debe entrar
  vía `WarningsNotAsErrors` o el build se rompe.
- Los scripts Node son ESM (`.mjs`) y no añaden dependencias de npm: solo
  módulos integrados (`node:fs`, `node:child_process`, `node:test`, etc.).
- Bounded contexts existentes: `Identity`, `Catalog`, `Chat`, `Orders`,
  `Reputation`, `Notifications`. Más `BuildingBlocks` y `Host`.
- Convención verificada: los 76 handlers son `public sealed class …Handler`.
- Cada tarea termina con commit. No dejar la rama con trabajo a medias.

---

## Estructura de archivos

**Crear:**

| Archivo | Responsabilidad |
|---|---|
| `verify.ps1` | Wrapper PowerShell. Traduce `-Full` y delega en `quality/verify.mjs`. |
| `verify.sh` | Wrapper POSIX. Idéntico contrato. |
| `quality/verify.mjs` | Orquestador. Ejecuta los gates en orden de coste y para en el primer fallo. |
| `quality/lib/ejecutar.mjs` | Utilidad compartida: ejecutar un comando, capturar salida, medir duración. |
| `quality/lib/salida.mjs` | Utilidad compartida: formato de mensajes de éxito, fallo y aviso. |
| `quality/check-tdd.mjs` | Gate TDD: diff de `src/` sin diff de `tests/`. |
| `quality/check-coverage.mjs` | Compara cobertura contra baseline. |
| `quality/check-complexity.mjs` | Compara recuento de violaciones de complejidad contra baseline. |
| `quality/coverage-baseline.json` | Cobertura por proyecto. |
| `quality/complexity-baseline.json` | Violaciones por regla y proyecto. |
| `quality/mutation-baseline.json` | Score de mutación. |
| `quality/__tests__/check-tdd.test.mjs` | Tests del gate TDD. |
| `quality/__tests__/check-coverage.test.mjs` | Tests del gate de cobertura. |
| `quality/__tests__/check-complexity.test.mjs` | Tests del gate de complejidad. |
| `CaseritoApp/tests/CaseritoApp.ArchitectureTests/CapasTests.cs` | Reglas de dependencia entre capas. |
| `CaseritoApp/tests/CaseritoApp.ArchitectureTests/ContextosTests.cs` | Aislamiento entre bounded contexts. |
| `CaseritoApp/tests/CaseritoApp.ArchitectureTests/ConvencionesTests.cs` | Convenciones CQRS. |
| `CaseritoApp/tests/CaseritoApp.ArchitectureTests/Ensamblados.cs` | Helper compartido para resolver assemblies por nombre. |
| `CaseritoApp/stryker-config.json` | Configuración de mutation testing. |
| `.github/workflows/mutation.yml` | Job nocturno de mutación. |
| `docs/ai/PUERTA_CALIDAD.md` | Documentación de los gates para agentes. |

**Modificar:**

| Archivo | Cambio |
|---|---|
| `CaseritoApp/Directory.Packages.props` | Añadir `coverlet.collector`. |
| `CaseritoApp/tests/*/*.csproj` (×3) | Referenciar `coverlet.collector`. |
| `CaseritoApp/Directory.Build.props` | `WarningsNotAsErrors` para CA1502/CA1505/CA1506. |
| `CaseritoApp/.editorconfig` | Severidad `warning` para CA1502/CA1505/CA1506. |
| `.github/workflows/ci.yml` | Job de gates + límite de archivos con `[sin-test]`. |
| `AGENTS.md` | Sección "Puerta de calidad". |
| `docs/ai/WORKFLOW.md` | Cierre exige `verify --full`. |
| `docs/ai/README.md` | Enlace a `PUERTA_CALIDAD.md`. |

---

## Tarea 1: Utilidades compartidas y orquestador mínimo

Construye el andamiaje: los dos wrappers y un `verify.mjs` que de momento solo
encadena los gates que **ya existen hoy** (formato, build, tests, frontend). Las
tareas siguientes le enchufan gates nuevos.

**Archivos:**
- Crear: `quality/lib/ejecutar.mjs`, `quality/lib/salida.mjs`, `quality/verify.mjs`, `verify.ps1`, `verify.sh`
- Test: `quality/__tests__/salida.test.mjs`

**Interfaces:**
- Produce:
  - `ejecutar(comando, args, opciones) -> { codigo: number, salida: string, duracionMs: number }`
    donde `opciones = { cwd?: string, silencioso?: boolean }`.
  - `exito(texto)`, `fallo(texto, detalle?)`, `aviso(texto)`, `titulo(texto)` — todas
    devuelven `string` formateado, no imprimen.
  - `quality/verify.mjs` acepta el flag `--full` y sale con código 0 o 1.

- [ ] **Paso 1: escribir el test que falla**

`quality/__tests__/salida.test.mjs`:

```javascript
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { exito, fallo, aviso } from '../lib/salida.mjs';

test('exito incluye el texto y una marca visible', () => {
  const linea = exito('Formato correcto');
  assert.match(linea, /Formato correcto/);
  assert.match(linea, /OK/);
});

test('fallo incluye el detalle cuando se proporciona', () => {
  const linea = fallo('Cobertura insuficiente', 'Identity.Domain: 80,1% < 84,3%');
  assert.match(linea, /Cobertura insuficiente/);
  assert.match(linea, /Identity\.Domain/);
});

test('aviso se distingue de un fallo', () => {
  assert.notEqual(aviso('Exención en uso'), fallo('Exención en uso'));
});
```

- [ ] **Paso 2: ejecutar el test y ver el fallo correcto**

Ejecutar: `node --test quality/__tests__/salida.test.mjs`
Esperado: FALLO — `Cannot find module '../lib/salida.mjs'`

- [ ] **Paso 3: implementar las utilidades**

`quality/lib/salida.mjs`:

```javascript
// Formato uniforme para la salida de los gates de calidad.
// No imprime: devuelve cadenas, para que los tests puedan verificarlas.

const VERDE = '\x1b[32m';
const ROJO = '\x1b[31m';
const AMARILLO = '\x1b[33m';
const NEGRITA = '\x1b[1m';
const RESET = '\x1b[0m';

export function titulo(texto) {
  return `${NEGRITA}== ${texto} ==${RESET}`;
}

export function exito(texto) {
  return `${VERDE}[OK]${RESET} ${texto}`;
}

export function fallo(texto, detalle) {
  const cabecera = `${ROJO}[FALLO]${RESET} ${texto}`;
  return detalle ? `${cabecera}\n       ${detalle}` : cabecera;
}

export function aviso(texto) {
  return `${AMARILLO}[AVISO]${RESET} ${texto}`;
}
```

`quality/lib/ejecutar.mjs`:

```javascript
// Ejecuta un comando externo y devuelve su código, salida combinada y duración.
// Nunca lanza: el llamador decide qué hacer con un código distinto de cero.

import { spawnSync } from 'node:child_process';

export function ejecutar(comando, args, opciones = {}) {
  const inicio = process.hrtime.bigint();
  const resultado = spawnSync(comando, args, {
    cwd: opciones.cwd ?? process.cwd(),
    encoding: 'utf8',
    shell: process.platform === 'win32',
    maxBuffer: 32 * 1024 * 1024,
  });
  const duracionMs = Number((process.hrtime.bigint() - inicio) / 1_000_000n);
  const salida = `${resultado.stdout ?? ''}${resultado.stderr ?? ''}`;

  if (!opciones.silencioso) {
    process.stdout.write(salida);
  }

  return { codigo: resultado.status ?? 1, salida, duracionMs };
}
```

- [ ] **Paso 4: ejecutar el test y verlo pasar**

Ejecutar: `node --test quality/__tests__/salida.test.mjs`
Esperado: PASA — 3 tests.

- [ ] **Paso 5: implementar el orquestador y los wrappers**

`quality/verify.mjs`:

```javascript
#!/usr/bin/env node
// Puerta de calidad de CaseritoApp.
//   node quality/verify.mjs           -> nivel rápido, sin Docker
//   node quality/verify.mjs --full    -> nivel completo, requiere Docker
// Se detiene en el primer gate que falla.

import { fileURLToPath } from 'node:url';
import { join } from 'node:path';
import { ejecutar } from './lib/ejecutar.mjs';
import { titulo, exito, fallo } from './lib/salida.mjs';

const completo = process.argv.includes('--full');
// fileURLToPath, no .pathname: en Windows .pathname produce "/C:/..." y rompe spawn.
const raiz = fileURLToPath(new URL('..', import.meta.url));
const backend = join(raiz, 'CaseritoApp');
const frontend = join(raiz, 'web');

// Cada gate: { nombre, comando, args, cwd, soloCompleto }
const gates = [
  {
    nombre: 'Formato .NET',
    comando: 'dotnet',
    args: ['format', 'CaseritoApp.sln', '--verify-no-changes'],
    cwd: backend,
  },
  {
    nombre: 'Build Release (warnings as errors)',
    comando: 'dotnet',
    args: ['build', 'CaseritoApp.sln', '--configuration', 'Release'],
    cwd: backend,
  },
  {
    nombre: 'Unit tests',
    comando: 'dotnet',
    args: ['test', 'tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj',
           '--no-build', '--configuration', 'Release'],
    cwd: backend,
  },
  {
    nombre: 'Tests de arquitectura',
    comando: 'dotnet',
    args: ['test', 'tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj',
           '--no-build', '--configuration', 'Release'],
    cwd: backend,
  },
  { nombre: 'Lint web', comando: 'npm', args: ['run', 'lint'], cwd: frontend },
  { nombre: 'Typecheck web', comando: 'npm', args: ['run', 'typecheck'], cwd: frontend },
  { nombre: 'Tests web', comando: 'npm', args: ['run', 'test'], cwd: frontend },
  {
    nombre: 'Tests de integración (Docker)',
    comando: 'dotnet',
    args: ['test', 'tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj',
           '--no-build', '--configuration', 'Release'],
    cwd: backend,
    soloCompleto: true,
  },
];

let fallidos = 0;

for (const gate of gates) {
  if (gate.soloCompleto && !completo) continue;

  console.log(titulo(gate.nombre));
  const { codigo, duracionMs } = ejecutar(gate.comando, gate.args, { cwd: gate.cwd });

  if (codigo !== 0) {
    console.log(fallo(`${gate.nombre} falló`, `código ${codigo}`));
    fallidos += 1;
    break; // retroalimentación rápida: no seguir tras el primer fallo
  }
  console.log(exito(`${gate.nombre} (${duracionMs} ms)`));
}

if (fallidos > 0) {
  console.log(fallo('La puerta de calidad no pasó. Arregla el código, no el gate.'));
  process.exit(1);
}

console.log(exito(completo ? 'Puerta de calidad completa: verde.'
                           : 'Puerta de calidad rápida: verde.'));
```

`verify.sh`:

```sh
#!/bin/sh
# Puerta de calidad de CaseritoApp.
# Uso: ./verify.sh          -> nivel rápido, sin Docker
#      ./verify.sh --full   -> nivel completo, requiere Docker
node quality/verify.mjs "$@"
```

`verify.ps1`:

```powershell
# Puerta de calidad de CaseritoApp.
# Uso: ./verify.ps1         -> nivel rápido, sin Docker
#      ./verify.ps1 -Full   -> nivel completo, requiere Docker
param([switch]$Full)
if ($Full) { node quality/verify.mjs --full } else { node quality/verify.mjs }
exit $LASTEXITCODE
```

- [ ] **Paso 6: verificar el nivel rápido de extremo a extremo**

Ejecutar: `node quality/verify.mjs`
Esperado: todos los gates en verde. Anotar la duración total; el criterio de
aceptación 1 exige menos de 60 s.

Si algún gate falla aquí, **es un fallo preexistente del repositorio**: anotarlo
y reportarlo antes de continuar. No modificar el gate para taparlo.

- [ ] **Paso 7: commit**

```bash
git add quality/lib quality/verify.mjs quality/__tests__/salida.test.mjs verify.sh verify.ps1
git commit -m "feat(calidad): punto de entrada unico de la puerta de calidad"
```

---

## Tarea 2: Gate TDD

**Archivos:**
- Crear: `quality/check-tdd.mjs`, `quality/__tests__/check-tdd.test.mjs`
- Modificar: `quality/verify.mjs`

**Interfaces:**
- Consume: `fallo`, `exito`, `aviso` de `quality/lib/salida.mjs`.
- Produce: `evaluarTdd({ archivosCambiados, mensajeCommit }) -> { ok: boolean, motivo: string, exencion: boolean }`
  — función pura, para poder testearla sin git. El `.mjs` la envuelve leyendo el
  diff real.

- [ ] **Paso 1: escribir el test que falla**

`quality/__tests__/check-tdd.test.mjs`:

```javascript
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
```

- [ ] **Paso 2: ejecutar el test y ver el fallo correcto**

Ejecutar: `node --test quality/__tests__/check-tdd.test.mjs`
Esperado: FALLO — `Cannot find module '../check-tdd.mjs'`

- [ ] **Paso 3: implementar el gate**

`quality/check-tdd.mjs`:

```javascript
#!/usr/bin/env node
// Gate TDD: todo cambio de comportamiento en src/ debe venir acompañado de
// cambios en tests/. Ver docs/ai/PUERTA_CALIDAD.md para el procedimiento.

import { ejecutar } from './lib/ejecutar.mjs';
import { exito, fallo, aviso } from './lib/salida.mjs';

const PREFIJO_SRC = 'CaseritoApp/src/';
const PREFIJO_TESTS = 'CaseritoApp/tests/';
const MOTIVO_MINIMO = 20;

// Rutas de src/ que no exigen test por no contener comportamiento propio.
const EXCLUIDOS = [
  /\/Migrations\//,
  /\/Program\.cs$/,
  /\.Designer\.cs$/,
  /\.csproj$/,
];

function exigeTest(ruta) {
  if (!ruta.startsWith(PREFIJO_SRC)) return false;
  if (!ruta.endsWith('.cs')) return false;
  return !EXCLUIDOS.some((patron) => patron.test(ruta));
}

export function evaluarTdd({ archivosCambiados, mensajeCommit }) {
  const sinTest = archivosCambiados.filter(exigeTest);

  if (sinTest.length === 0) {
    return { ok: true, motivo: 'No hay cambios de comportamiento en src/.', exencion: false };
  }

  const hayTests = archivosCambiados.some((ruta) => ruta.startsWith(PREFIJO_TESTS));
  if (hayTests) {
    return { ok: true, motivo: 'Los cambios en src/ vienen con cambios en tests/.', exencion: false };
  }

  const etiqueta = mensajeCommit.indexOf('[sin-test]');
  if (etiqueta === -1) {
    return {
      ok: false,
      exencion: false,
      motivo:
        `Se modificaron ${sinTest.length} archivo(s) de src/ sin tocar tests/:\n       ` +
        sinTest.join('\n       ') +
        '\n       Escribe el test, o justifica con "[sin-test] <motivo>" en el commit.',
    };
  }

  const razon = mensajeCommit.slice(etiqueta + '[sin-test]'.length).trim();
  if (razon.length < MOTIVO_MINIMO) {
    return {
      ok: false,
      exencion: false,
      motivo: `La exención [sin-test] exige un motivo de al menos ${MOTIVO_MINIMO} caracteres. Recibido: ${razon.length}.`,
    };
  }

  return { ok: true, exencion: true, motivo: razon };
}

function archivosDelDiff() {
  const { salida } = ejecutar('git', ['diff', '--name-only', 'origin/master...HEAD'], { silencioso: true });
  return salida.split('\n').map((l) => l.trim()).filter(Boolean);
}

function ultimoMensaje() {
  const { salida } = ejecutar('git', ['log', '-1', '--pretty=%B'], { silencioso: true });
  return salida.trim();
}

// Punto de entrada CLI. Al importarse como módulo (tests) no se ejecuta.
if (process.argv[1] && process.argv[1].endsWith('check-tdd.mjs')) {
  const resultado = evaluarTdd({
    archivosCambiados: archivosDelDiff(),
    mensajeCommit: ultimoMensaje(),
  });

  if (!resultado.ok) {
    console.log(fallo('Gate TDD', resultado.motivo));
    process.exit(1);
  }
  if (resultado.exencion) {
    console.log(aviso(`Gate TDD eximido con [sin-test]: ${resultado.motivo}`));
  } else {
    console.log(exito('Gate TDD'));
  }
}
```

- [ ] **Paso 4: ejecutar el test y verlo pasar**

Ejecutar: `node --test quality/__tests__/check-tdd.test.mjs`
Esperado: PASA — 6 tests.

- [ ] **Paso 5: enchufarlo al orquestador**

En `quality/verify.mjs`, añadir como **primer** gate de la lista (es el más
barato, milisegundos):

```javascript
  {
    nombre: 'Gate TDD',
    comando: 'node',
    args: ['quality/check-tdd.mjs'],
    cwd: raiz,
  },
```

- [ ] **Paso 6: verificar el comportamiento real**

Ejecutar: `node quality/check-tdd.mjs`
Esperado: pasa (esta rama solo ha tocado `docs/` y `quality/`).

- [ ] **Paso 7: commit**

```bash
git add quality/check-tdd.mjs quality/__tests__/check-tdd.test.mjs quality/verify.mjs
git commit -m "feat(calidad): gate TDD con exencion auditable"
```

---

## Tarea 3: Fitness functions de capas

**Archivos:**
- Crear: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/Ensamblados.cs`
- Crear: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/CapasTests.cs`

**Interfaces:**
- Produce: `Ensamblados.Contextos` (`string[]`), `Ensamblados.Cargar(string nombre) -> Assembly`,
  `Ensamblados.De(string contexto, string capa) -> Assembly`. Las tareas 4 y 5
  reutilizan este helper.

- [ ] **Paso 1: escribir el helper y el test que falla**

`Ensamblados.cs`:

```csharp
using System.Reflection;

namespace CaseritoApp.ArchitectureTests;

/// <summary>
/// Resuelve los ensamblados de la solución por nombre. El proyecto de tests
/// referencia todos los csproj, así que los DLL están en el directorio de salida.
/// </summary>
internal static class Ensamblados
{
    /// <summary>Bounded contexts con las tres capas completas.</summary>
    internal static readonly string[] Contextos =
    [
        "Identity",
        "Catalog",
        "Chat",
        "Orders",
        "Reputation",
        "Notifications",
    ];

    internal static Assembly Cargar(string nombre) => Assembly.Load(nombre);

    internal static Assembly De(string contexto, string capa) =>
        Cargar($"CaseritoApp.{contexto}.{capa}");
}
```

`CapasTests.cs`:

```csharp
using NetArchTest.Rules;

namespace CaseritoApp.ArchitectureTests;

/// <summary>
/// Reglas de dependencia de Clean Architecture. Las capas internas no pueden
/// conocer a las externas ni a la infraestructura técnica.
/// </summary>
public sealed class CapasTests
{
    public static TheoryData<string> TodosLosContextos()
    {
        var datos = new TheoryData<string>();
        foreach (var contexto in Ensamblados.Contextos)
        {
            datos.Add(contexto);
        }
        return datos;
    }

    [Theory]
    [MemberData(nameof(TodosLosContextos))]
    public void Domain_no_depende_de_capas_externas(string contexto)
    {
        var resultado = Types.InAssembly(Ensamblados.De(contexto, "Domain"))
            .ShouldNot()
            .HaveDependencyOnAny(
                $"CaseritoApp.{contexto}.Application",
                $"CaseritoApp.{contexto}.Infrastructure",
                "CaseritoApp.Host")
            .GetResult();

        Assert.True(resultado.IsSuccessful, Describir(resultado, $"{contexto}.Domain"));
    }

    [Theory]
    [MemberData(nameof(TodosLosContextos))]
    public void Domain_no_depende_de_infraestructura_tecnica(string contexto)
    {
        var resultado = Types.InAssembly(Ensamblados.De(contexto, "Domain"))
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "MediatR",
                "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(resultado.IsSuccessful, Describir(resultado, $"{contexto}.Domain"));
    }

    [Theory]
    [MemberData(nameof(TodosLosContextos))]
    public void Application_no_depende_de_Infrastructure_ni_de_EF(string contexto)
    {
        var resultado = Types.InAssembly(Ensamblados.De(contexto, "Application"))
            .ShouldNot()
            .HaveDependencyOnAny(
                $"CaseritoApp.{contexto}.Infrastructure",
                "CaseritoApp.Host",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(resultado.IsSuccessful, Describir(resultado, $"{contexto}.Application"));
    }

    private static string Describir(TestResult resultado, string origen)
    {
        var tipos = resultado.FailingTypeNames ?? [];
        return $"{origen} viola la regla de capas. Tipos infractores: {string.Join(", ", tipos)}";
    }
}
```

- [ ] **Paso 2: ejecutar y observar el resultado real**

Ejecutar desde `CaseritoApp/`:
`dotnet test tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj --filter "FullyQualifiedName~CapasTests"`

**Este paso es de descubrimiento, no de TDD clásico:** las reglas describen el
estado que el código ya debería cumplir. Dos desenlaces posibles:

- **Todos pasan.** Perfecto, la arquitectura está sana. Continuar.
- **Alguno falla.** Es una violación real y preexistente. **Reportarla al humano
  antes de continuar** con el nombre del tipo infractor. No relajar la regla para
  que pase; decidir con el humano si se arregla el código o se documenta una
  excepción justificada en el propio test.

- [ ] **Paso 3: verificar que la regla realmente detecta violaciones**

Añadir temporalmente a un archivo de `CaseritoApp.Catalog.Domain` la línea
`using Microsoft.EntityFrameworkCore;` y una referencia de proyecto a EF.

Ejecutar el mismo comando.
Esperado: FALLA `Domain_no_depende_de_infraestructura_tecnica(contexto: "Catalog")`
con el nombre del tipo en el mensaje.

**Revertir el cambio** con `git checkout -- <archivo>` y volver a ejecutar para
confirmar que vuelve al verde. Una fitness function que nunca se ha visto fallar
no es una garantía, es decoración.

- [ ] **Paso 4: commit**

```bash
git add CaseritoApp/tests/CaseritoApp.ArchitectureTests/Ensamblados.cs \
        CaseritoApp/tests/CaseritoApp.ArchitectureTests/CapasTests.cs
git commit -m "test(arquitectura): reglas de dependencia entre capas"
```

---

## Tarea 4: Fitness functions de aislamiento entre contextos

**Archivos:**
- Crear: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/ContextosTests.cs`

**Interfaces:**
- Consume: `Ensamblados.Contextos`, `Ensamblados.De` de la tarea 3.

- [ ] **Paso 1: escribir el test**

```csharp
using NetArchTest.Rules;

namespace CaseritoApp.ArchitectureTests;

/// <summary>
/// Los bounded contexts se mantienen aislados: ninguno conoce el Domain ni el
/// Infrastructure de otro. La comunicación entre contextos ocurre a través de
/// BuildingBlocks.Contracts.
/// </summary>
public sealed class ContextosTests
{
    public static TheoryData<string, string> ParesDeContextos()
    {
        var datos = new TheoryData<string, string>();
        foreach (var origen in Ensamblados.Contextos)
        {
            foreach (var destino in Ensamblados.Contextos)
            {
                if (origen != destino)
                {
                    datos.Add(origen, destino);
                }
            }
        }
        return datos;
    }

    [Theory]
    [MemberData(nameof(ParesDeContextos))]
    public void Domain_no_conoce_otros_contextos(string origen, string destino)
    {
        var resultado = Types.InAssembly(Ensamblados.De(origen, "Domain"))
            .ShouldNot()
            .HaveDependencyOnAny(
                $"CaseritoApp.{destino}.Domain",
                $"CaseritoApp.{destino}.Application",
                $"CaseritoApp.{destino}.Infrastructure")
            .GetResult();

        Assert.True(
            resultado.IsSuccessful,
            $"{origen}.Domain depende de {destino}. Tipos: " +
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }

    [Theory]
    [MemberData(nameof(ParesDeContextos))]
    public void Application_no_conoce_el_interior_de_otros_contextos(string origen, string destino)
    {
        var resultado = Types.InAssembly(Ensamblados.De(origen, "Application"))
            .ShouldNot()
            .HaveDependencyOnAny(
                $"CaseritoApp.{destino}.Domain",
                $"CaseritoApp.{destino}.Infrastructure")
            .GetResult();

        Assert.True(
            resultado.IsSuccessful,
            $"{origen}.Application depende del interior de {destino}. " +
            $"Usa BuildingBlocks.Contracts. Tipos: " +
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }
}
```

- [ ] **Paso 2: ejecutar y observar el resultado real**

Ejecutar desde `CaseritoApp/`:
`dotnet test tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj --filter "FullyQualifiedName~ContextosTests"`

Mismo criterio que la tarea 3: si falla, es una violación preexistente.
**Reportarla al humano con los nombres de los tipos antes de tocar nada.**

Es plausible que aparezcan acoplamientos legítimos entre contextos (por ejemplo,
`Notifications` reaccionando a eventos de `Orders`). Si es así, la conversación
con el humano decide si se refactoriza hacia `Contracts` o si el test documenta
una excepción explícita y justificada. **No borrar la regla.**

- [ ] **Paso 3: commit**

```bash
git add CaseritoApp/tests/CaseritoApp.ArchitectureTests/ContextosTests.cs
git commit -m "test(arquitectura): aislamiento entre bounded contexts"
```

---

## Tarea 5: Fitness functions de convenciones CQRS

**Archivos:**
- Crear: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/ConvencionesTests.cs`

**Interfaces:**
- Consume: `Ensamblados.Contextos`, `Ensamblados.De` de la tarea 3.

- [ ] **Paso 1: escribir el test**

```csharp
using System.Reflection;
using NetArchTest.Rules;

namespace CaseritoApp.ArchitectureTests;

/// <summary>
/// Convenciones de CQRS-lite verificadas de forma automática. Codifican la
/// convención real del repositorio: los handlers son "public sealed class …Handler".
/// </summary>
public sealed class ConvencionesTests
{
    public static TheoryData<string> TodosLosContextos()
    {
        var datos = new TheoryData<string>();
        foreach (var contexto in Ensamblados.Contextos)
        {
            datos.Add(contexto);
        }
        return datos;
    }

    [Theory]
    [MemberData(nameof(TodosLosContextos))]
    public void Los_handlers_son_sealed(string contexto)
    {
        var resultado = Types.InAssembly(Ensamblados.De(contexto, "Application"))
            .That()
            .HaveNameEndingWith("Handler")
            .Should()
            .BeSealed()
            .GetResult();

        Assert.True(
            resultado.IsSuccessful,
            $"Handlers no sellados en {contexto}.Application: " +
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }

    [Theory]
    [MemberData(nameof(TodosLosContextos))]
    public void Los_handlers_viven_en_la_capa_Application(string contexto)
    {
        var enInfraestructura = Types.InAssembly(Ensamblados.De(contexto, "Infrastructure"))
            .That()
            .HaveNameEndingWith("CommandHandler")
            .Or()
            .HaveNameEndingWith("QueryHandler")
            .GetTypes();

        Assert.True(
            enInfraestructura.Count == 0,
            $"Handlers CQRS fuera de Application en {contexto}.Infrastructure: " +
            string.Join(", ", enInfraestructura.Select(t => t.Name)));
    }

    [Theory]
    [MemberData(nameof(TodosLosContextos))]
    public void Los_validadores_son_sealed(string contexto)
    {
        var resultado = Types.InAssembly(Ensamblados.De(contexto, "Application"))
            .That()
            .HaveNameEndingWith("Validator")
            .Should()
            .BeSealed()
            .GetResult();

        Assert.True(
            resultado.IsSuccessful,
            $"Validadores no sellados en {contexto}.Application: " +
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }
}
```

- [ ] **Paso 2: ejecutar y observar el resultado real**

Ejecutar desde `CaseritoApp/`:
`dotnet test tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj --filter "FullyQualifiedName~ConvencionesTests"`

Se espera verde en `Los_handlers_son_sealed`: se verificó que los 76 handlers son
`public sealed`. Si `Los_validadores_son_sealed` falla, reportar los nombres y
decidir con el humano entre sellar los validadores o retirar esa regla concreta.

- [ ] **Paso 3: commit**

```bash
git add CaseritoApp/tests/CaseritoApp.ArchitectureTests/ConvencionesTests.cs
git commit -m "test(arquitectura): convenciones CQRS verificadas"
```

---

## Tarea 6: Cobertura con baseline

**Archivos:**
- Modificar: `CaseritoApp/Directory.Packages.props`
- Modificar: `CaseritoApp/tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj`
- Modificar: `CaseritoApp/tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj`
- Modificar: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj`
- Crear: `quality/check-coverage.mjs`, `quality/coverage-baseline.json`
- Crear: `quality/__tests__/check-coverage.test.mjs`
- Modificar: `quality/verify.mjs`

**Interfaces:**
- Produce: `compararCobertura(actual, baseline, tolerancia) -> { ok, regresiones: [{ proyecto, baseline, actual, caida }] }`
  donde `actual` y `baseline` son `{ [proyecto: string]: { linea: number, rama: number } }`
  con porcentajes de 0 a 100, y `tolerancia` en puntos porcentuales.
- Produce: `parsearCobertura(xml) -> { [proyecto]: { linea, rama } }`.

- [ ] **Paso 1: escribir el test que falla**

`quality/__tests__/check-coverage.test.mjs`:

```javascript
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
```

- [ ] **Paso 2: ejecutar el test y ver el fallo correcto**

Ejecutar: `node --test quality/__tests__/check-coverage.test.mjs`
Esperado: FALLO — `Cannot find module '../check-coverage.mjs'`

- [ ] **Paso 3: implementar la comparación**

`quality/check-coverage.mjs`:

```javascript
#!/usr/bin/env node
// Gate de cobertura: no-regresión contra quality/coverage-baseline.json.
// No exige un mínimo absoluto; solo prohíbe empeorar.
// Ver docs/ai/PUERTA_CALIDAD.md para actualizar la baseline legítimamente.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { exito, fallo } from './lib/salida.mjs';

export const TOLERANCIA_PP = 0.5;

export function compararCobertura(actual, baseline, tolerancia) {
  const regresiones = [];

  for (const [proyecto, esperado] of Object.entries(baseline)) {
    const medido = actual[proyecto];

    if (!medido) {
      regresiones.push({
        proyecto,
        motivo: `El proyecto no aparece en el reporte de cobertura. ` +
                `¿Se eliminó, o dejó de ejecutarse su suite?`,
      });
      continue;
    }

    for (const metrica of ['linea', 'rama']) {
      const caida = esperado[metrica] - medido[metrica];
      if (caida > tolerancia) {
        regresiones.push({
          proyecto,
          metrica,
          baseline: esperado[metrica],
          actual: medido[metrica],
          caida: Number(caida.toFixed(2)),
          motivo: `Cobertura de ${metrica} cayó ${caida.toFixed(2)} pp ` +
                  `(${esperado[metrica]}% -> ${medido[metrica]}%).`,
        });
      }
    }
  }

  return { ok: regresiones.length === 0, regresiones };
}

// Parsea un cobertura.xml de coverlet y agrega por ensamblado.
export function parsearCobertura(xml) {
  const proyectos = {};
  const patron = /<package\s+name="([^"]+)"\s+line-rate="([^"]+)"\s+branch-rate="([^"]+)"/g;

  for (const [, nombre, linea, rama] of xml.matchAll(patron)) {
    proyectos[nombre] = {
      linea: Number((Number(linea) * 100).toFixed(2)),
      rama: Number((Number(rama) * 100).toFixed(2)),
    };
  }

  return proyectos;
}

function buscarReportes(directorio) {
  const encontrados = [];
  for (const entrada of readdirSync(directorio)) {
    const ruta = join(directorio, entrada);
    if (statSync(ruta).isDirectory()) {
      encontrados.push(...buscarReportes(ruta));
    } else if (entrada === 'coverage.cobertura.xml') {
      encontrados.push(ruta);
    }
  }
  return encontrados;
}

if (process.argv[1] && process.argv[1].endsWith('check-coverage.mjs')) {
  const directorio = process.argv[2];
  if (!directorio) {
    console.log(fallo('Uso: node quality/check-coverage.mjs <directorio-de-resultados>'));
    process.exit(1);
  }

  const actual = {};
  for (const reporte of buscarReportes(directorio)) {
    Object.assign(actual, parsearCobertura(readFileSync(reporte, 'utf8')));
  }

  const baseline = JSON.parse(
    readFileSync(new URL('./coverage-baseline.json', import.meta.url), 'utf8'),
  );

  const resultado = compararCobertura(actual, baseline.proyectos, TOLERANCIA_PP);

  if (!resultado.ok) {
    const detalle = resultado.regresiones
      .map((r) => `${r.proyecto}: ${r.motivo}`)
      .join('\n       ');
    console.log(fallo('Gate de cobertura', detalle));
    process.exit(1);
  }

  console.log(exito(`Gate de cobertura (${Object.keys(actual).length} proyectos)`));
}
```

- [ ] **Paso 4: ejecutar el test y verlo pasar**

Ejecutar: `node --test quality/__tests__/check-coverage.test.mjs`
Esperado: PASA — 7 tests.

- [ ] **Paso 5: habilitar la recolección de cobertura**

En `CaseritoApp/Directory.Packages.props`, en el `ItemGroup` de paquetes:

```xml
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />
```

En **cada uno** de los tres `.csproj` de `CaseritoApp/tests/`, dentro del
`ItemGroup` que ya contiene `Microsoft.NET.Test.Sdk`:

```xml
    <PackageReference Include="coverlet.collector" />
```

- [ ] **Paso 6: generar la baseline**

Ejecutar desde `CaseritoApp/`:

```bash
dotnet test CaseritoApp.sln --collect:"XPlat Code Coverage" --results-directory artifacts/coverage
```

Luego generar el archivo con los valores **reales medidos** (no inventados):

```bash
node -e "
import('./quality/check-coverage.mjs').then(async (m) => {
  const { readFileSync } = await import('node:fs');
  const { globSync } = await import('node:fs');
  const proyectos = {};
  for (const f of globSync('CaseritoApp/artifacts/coverage/**/coverage.cobertura.xml')) {
    Object.assign(proyectos, m.parsearCobertura(readFileSync(f, 'utf8')));
  }
  const salida = { generado: '2026-08-07', tolerancia_pp: 0.5, proyectos };
  console.log(JSON.stringify(salida, null, 2));
});
" > quality/coverage-baseline.json
```

Revisar el archivo generado. Debe contener una entrada por proyecto de `src/`
con `linea` y `rama`. **Anotar la cobertura global y reportarla al humano**: es
el dato que le dice si el punto de partida es aceptable.

- [ ] **Paso 7: verificar que el gate detecta una regresión**

Ejecutar: `node quality/check-coverage.mjs CaseritoApp/artifacts/coverage`
Esperado: PASA.

Editar `quality/coverage-baseline.json` a mano subiendo la cobertura de un
proyecto en 10 puntos, y volver a ejecutar.
Esperado: FALLA nombrando ese proyecto y la caída en pp.

**Revertir** con `git checkout -- quality/coverage-baseline.json`.

- [ ] **Paso 8: enchufarlo al nivel completo**

En `quality/verify.mjs`, añadir al final de la lista:

```javascript
  {
    nombre: 'Cobertura contra baseline',
    comando: 'node',
    args: ['quality/check-coverage.mjs', 'CaseritoApp/artifacts/coverage'],
    cwd: raiz,
    soloCompleto: true,
  },
```

Y **antes** de ese gate, sustituir en el nivel completo la ejecución de tests por
una que recolecte cobertura. Añadir este gate justo antes:

```javascript
  {
    nombre: 'Tests con cobertura',
    comando: 'dotnet',
    args: ['test', 'CaseritoApp.sln', '--configuration', 'Release',
           '--collect:XPlat Code Coverage',
           '--results-directory', 'artifacts/coverage'],
    cwd: backend,
    soloCompleto: true,
  },
```

Añadir `CaseritoApp/artifacts/coverage/` a `.gitignore`.

- [ ] **Paso 9: commit**

```bash
git add CaseritoApp/Directory.Packages.props CaseritoApp/tests/*/*.csproj \
        quality/check-coverage.mjs quality/coverage-baseline.json \
        quality/__tests__/check-coverage.test.mjs quality/verify.mjs .gitignore
git commit -m "feat(calidad): gate de cobertura con baseline de no-regresion"
```

---

## Tarea 7: Complejidad ciclomática con baseline

**Archivos:**
- Modificar: `CaseritoApp/Directory.Build.props`
- Modificar: `CaseritoApp/.editorconfig`
- Crear: `quality/check-complexity.mjs`, `quality/complexity-baseline.json`
- Crear: `quality/__tests__/check-complexity.test.mjs`
- Modificar: `quality/verify.mjs`

**Interfaces:**
- Produce: `contarViolaciones(salidaBuild) -> { [regla: string]: number }`.
- Produce: `compararComplejidad(actual, baseline) -> { ok, aumentos: [{ regla, baseline, actual }] }`.

- [ ] **Paso 1: escribir el test que falla**

`quality/__tests__/check-complexity.test.mjs`:

```javascript
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
```

- [ ] **Paso 2: ejecutar el test y ver el fallo correcto**

Ejecutar: `node --test quality/__tests__/check-complexity.test.mjs`
Esperado: FALLO — `Cannot find module '../check-complexity.mjs'`

- [ ] **Paso 3: implementar el contador**

`quality/check-complexity.mjs`:

```javascript
#!/usr/bin/env node
// Gate de complejidad: cuenta las violaciones de los analizadores de
// mantenibilidad y falla si el número sube respecto a la baseline.
// La deuda solo puede decrecer.

import { readFileSync } from 'node:fs';
import { exito, fallo } from './lib/salida.mjs';

const REGLAS = ['CA1502', 'CA1505', 'CA1506'];

export function contarViolaciones(salidaBuild) {
  const vistas = new Set();

  for (const linea of salidaBuild.split('\n')) {
    const coincidencia = linea.match(/^(.+?)\((\d+),(\d+)\):\s+warning\s+(CA\d+):/);
    if (!coincidencia) continue;

    const [, archivo, fila, columna, regla] = coincidencia;
    if (!REGLAS.includes(regla)) continue;

    // MSBuild repite el mismo warning una vez por target; deduplicar.
    vistas.add(`${regla}|${archivo}|${fila}|${columna}`);
  }

  const conteo = {};
  for (const clave of vistas) {
    const regla = clave.split('|')[0];
    conteo[regla] = (conteo[regla] ?? 0) + 1;
  }
  return conteo;
}

export function compararComplejidad(actual, baseline) {
  const aumentos = [];

  for (const regla of new Set([...Object.keys(actual), ...Object.keys(baseline)])) {
    const previo = baseline[regla] ?? 0;
    const ahora = actual[regla] ?? 0;
    if (ahora > previo) {
      aumentos.push({ regla, baseline: previo, actual: ahora });
    }
  }

  return { ok: aumentos.length === 0, aumentos };
}

if (process.argv[1] && process.argv[1].endsWith('check-complexity.mjs')) {
  const rutaLog = process.argv[2];
  if (!rutaLog) {
    console.log(fallo('Uso: node quality/check-complexity.mjs <ruta-log-build>'));
    process.exit(1);
  }

  const actual = contarViolaciones(readFileSync(rutaLog, 'utf8'));
  const baseline = JSON.parse(
    readFileSync(new URL('./complexity-baseline.json', import.meta.url), 'utf8'),
  ).reglas;

  const resultado = compararComplejidad(actual, baseline);

  if (!resultado.ok) {
    const detalle = resultado.aumentos
      .map((a) => `${a.regla}: ${a.baseline} -> ${a.actual} violaciones`)
      .join('\n       ');
    console.log(fallo('Gate de complejidad', detalle +
      '\n       Simplifica el código. No subas la baseline.'));
    process.exit(1);
  }

  const total = Object.values(actual).reduce((a, b) => a + b, 0);
  console.log(exito(`Gate de complejidad (${total} violaciones, sin aumentos)`));
}
```

- [ ] **Paso 4: ejecutar el test y verlo pasar**

Ejecutar: `node --test quality/__tests__/check-complexity.test.mjs`
Esperado: PASA — 6 tests.

- [ ] **Paso 5: activar los analizadores sin romper el build**

En `CaseritoApp/Directory.Build.props`, dentro del `PropertyGroup` existente,
**debajo** de `<AnalysisLevel>`:

```xml
    <!-- Analizadores de mantenibilidad: se miden, no rompen el build.
         El gate quality/check-complexity.mjs impide que el conteo suba. -->
    <WarningsNotAsErrors>$(WarningsNotAsErrors);CA1502;CA1505;CA1506</WarningsNotAsErrors>
```

En `CaseritoApp/.editorconfig`, dentro de la sección `[*.{cs,csx}]`:

```ini
# Analizadores de mantenibilidad. Severidad warning: el conteo se controla con
# quality/complexity-baseline.json, no con el compilador.
dotnet_diagnostic.CA1502.severity = warning
dotnet_diagnostic.CA1505.severity = warning
dotnet_diagnostic.CA1506.severity = warning
```

- [ ] **Paso 6: medir la baseline real**

Ejecutar desde `CaseritoApp/`:

```bash
dotnet build CaseritoApp.sln --configuration Release --no-incremental > ../artifacts-build.log 2>&1
```

Confirmar que el build **termina en éxito** pese a los warnings nuevos. Si falla,
`WarningsNotAsErrors` no está surtiendo efecto: revisar antes de seguir.

Generar la baseline:

```bash
node -e "
import('./quality/check-complexity.mjs').then(async (m) => {
  const { readFileSync } = await import('node:fs');
  const reglas = m.contarViolaciones(readFileSync('artifacts-build.log', 'utf8'));
  console.log(JSON.stringify({ generado: '2026-08-07', reglas }, null, 2));
});
" > quality/complexity-baseline.json
```

**Reportar al humano el número total de violaciones.** Este era el riesgo
anotado en el spec: si son cientos, decidir con él si se ataca la deuda ahora o
se congela.

Borrar `artifacts-build.log` y añadir `artifacts-build.log` a `.gitignore`.

- [ ] **Paso 7: verificar que el gate detecta un aumento**

Editar `quality/complexity-baseline.json` bajando `CA1502` en 1 y ejecutar:
`node quality/check-complexity.mjs artifacts-build.log`
Esperado: FALLA indicando el aumento.

**Revertir** con `git checkout -- quality/complexity-baseline.json`.

- [ ] **Paso 8: enchufarlo al orquestador**

El gate necesita el log del build. Modificar el gate «Build Release» en
`quality/verify.mjs` para que escriba su salida a `artifacts-build.log` usando el
campo `salida` que ya devuelve `ejecutar`, y añadir después:

```javascript
  {
    nombre: 'Complejidad contra baseline',
    comando: 'node',
    args: ['quality/check-complexity.mjs', 'artifacts-build.log'],
    cwd: raiz,
  },
```

Para que exista el log, tras el gate de build añadir en el bucle:

```javascript
  if (gate.nombre === 'Build Release (warnings as errors)') {
    writeFileSync(join(raiz, 'artifacts-build.log'), resultado.salida, 'utf8');
  }
```

con `import { writeFileSync } from 'node:fs';` al principio del archivo, y
guardando el resultado completo de `ejecutar` en una variable `resultado`.

- [ ] **Paso 9: commit**

```bash
git add CaseritoApp/Directory.Build.props CaseritoApp/.editorconfig \
        quality/check-complexity.mjs quality/complexity-baseline.json \
        quality/__tests__/check-complexity.test.mjs quality/verify.mjs .gitignore
git commit -m "feat(calidad): gate de complejidad ciclomatica con baseline"
```

---

## Tarea 8: Mutation testing nocturno

**Archivos:**
- Crear: `CaseritoApp/stryker-config.json`
- Crear: `.github/workflows/mutation.yml`
- Crear: `quality/mutation-baseline.json`

- [ ] **Paso 1: configurar Stryker**

`CaseritoApp/stryker-config.json`:

```json
{
  "$schema": "https://raw.githubusercontent.com/stryker-mutator/stryker-net/master/packages/Stryker.Core/schema.json",
  "stryker-config": {
    "project-info": {
      "name": "CaseritoApp"
    },
    "solution": "CaseritoApp.sln",
    "test-projects": [
      "tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj"
    ],
    "mutate": [
      "src/Identity/CaseritoApp.Identity.Domain/**/*.cs",
      "src/Identity/CaseritoApp.Identity.Application/**/*.cs",
      "src/Catalog/CaseritoApp.Catalog.Domain/**/*.cs",
      "src/Catalog/CaseritoApp.Catalog.Application/**/*.cs",
      "src/Chat/CaseritoApp.Chat.Domain/**/*.cs",
      "src/Chat/CaseritoApp.Chat.Application/**/*.cs",
      "src/Orders/CaseritoApp.Orders.Domain/**/*.cs",
      "src/Orders/CaseritoApp.Orders.Application/**/*.cs",
      "src/Reputation/CaseritoApp.Reputation.Domain/**/*.cs",
      "src/Reputation/CaseritoApp.Reputation.Application/**/*.cs",
      "src/Notifications/CaseritoApp.Notifications.Domain/**/*.cs",
      "src/Notifications/CaseritoApp.Notifications.Application/**/*.cs",
      "!**/Migrations/**"
    ],
    "reporters": ["json", "progress"],
    "thresholds": { "high": 80, "low": 60, "break": 0 }
  }
}
```

`break: 0` deliberado: el corte lo aplica la comparación contra baseline, no
Stryker, para que el criterio sea de no-regresión igual que los demás gates.

- [ ] **Paso 2: ejecutar Stryker una vez y medir**

```bash
cd CaseritoApp
dotnet tool install -g dotnet-stryker
dotnet stryker
```

Esto puede tardar. Anotar el **mutation score** del reporte JSON en
`StrykerOutput/**/reports/mutation-report.json`.

Si la ejecución excede lo razonable (más de ~40 min), reducir `mutate` a los
contextos `Catalog` y `Orders` y anotarlo en el archivo de baseline.

- [ ] **Paso 3: registrar la baseline con el valor real medido**

`quality/mutation-baseline.json` — sustituir `SCORE_MEDIDO` por el número real:

```json
{
  "generado": "2026-08-07",
  "alcance": "Domain y Application de los 6 bounded contexts",
  "score": SCORE_MEDIDO,
  "tolerancia_pp": 1.0,
  "nota": "El job nocturno falla si el score cae mas de la tolerancia."
}
```

- [ ] **Paso 4: crear el workflow nocturno**

`.github/workflows/mutation.yml`:

```yaml
name: Mutation testing

on:
  schedule:
    - cron: '0 5 * * *'   # 05:00 UTC, cada día
  workflow_dispatch:

jobs:
  mutation:
    runs-on: ubuntu-latest
    timeout-minutes: 90
    defaults:
      run:
        working-directory: CaseritoApp
    steps:
      - uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4

      - name: Setup .NET
        uses: actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9 # v4
        with:
          global-json-file: CaseritoApp/global.json

      - name: Instalar Stryker
        run: dotnet tool install -g dotnet-stryker

      - name: Ejecutar mutation testing
        run: dotnet stryker

      - name: Comparar contra baseline
        run: node ../quality/check-mutation.mjs
        working-directory: CaseritoApp

      - name: Publicar reporte
        if: always()
        uses: actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02 # v4
        with:
          name: mutation-report
          path: CaseritoApp/StrykerOutput/**/reports/
```

- [ ] **Paso 5: implementar el comparador**

`quality/check-mutation.mjs`:

```javascript
#!/usr/bin/env node
// Compara el mutation score de Stryker contra quality/mutation-baseline.json.
// Se ejecuta en el job nocturno, no en el gate de commit.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { exito, fallo } from './lib/salida.mjs';

function buscarReporte(directorio) {
  for (const entrada of readdirSync(directorio)) {
    const ruta = join(directorio, entrada);
    if (statSync(ruta).isDirectory()) {
      const hallado = buscarReporte(ruta);
      if (hallado) return hallado;
    } else if (entrada === 'mutation-report.json') {
      return ruta;
    }
  }
  return null;
}

const reporte = buscarReporte('StrykerOutput');
if (!reporte) {
  console.log(fallo('No se encontró mutation-report.json'));
  process.exit(1);
}

const datos = JSON.parse(readFileSync(reporte, 'utf8'));
let matados = 0;
let total = 0;

for (const archivo of Object.values(datos.files ?? {})) {
  for (const mutante of archivo.mutants ?? []) {
    if (mutante.status === 'Ignored' || mutante.status === 'CompileError') continue;
    total += 1;
    if (mutante.status === 'Killed' || mutante.status === 'Timeout') matados += 1;
  }
}

const score = total === 0 ? 0 : Number(((matados / total) * 100).toFixed(2));
const baseline = JSON.parse(
  readFileSync(new URL('./mutation-baseline.json', import.meta.url), 'utf8'),
);
const caida = baseline.score - score;

if (caida > baseline.tolerancia_pp) {
  console.log(fallo(
    'Mutation score en regresión',
    `${baseline.score}% -> ${score}% (caída de ${caida.toFixed(2)} pp). ` +
    `Hay tests que dejaron de aseverar comportamiento.`,
  ));
  process.exit(1);
}

console.log(exito(`Mutation score: ${score}% (baseline ${baseline.score}%)`));
```

- [ ] **Paso 6: verificar el workflow**

Lanzarlo manualmente desde la pestaña Actions (`workflow_dispatch`).
Esperado: verde, con el reporte publicado como artefacto.

- [ ] **Paso 7: commit**

```bash
git add CaseritoApp/stryker-config.json .github/workflows/mutation.yml \
        quality/mutation-baseline.json quality/check-mutation.mjs
git commit -m "feat(calidad): mutation testing nocturno con baseline"
```

---

## Tarea 9: Integración en CI

**Archivos:**
- Modificar: `.github/workflows/ci.yml`

- [ ] **Paso 1: añadir el job de gates**

En `.github/workflows/ci.yml`, añadir un job nuevo al final:

```yaml
  calidad:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4
        with:
          fetch-depth: 0   # el gate TDD necesita el historial para el diff

      - name: Setup Node
        uses: actions/setup-node@49933ea5288caeca8642d1e84afbd3f7d6820020 # v4
        with:
          node-version: 22

      - name: Tests de los scripts de calidad
        run: node --test quality/

      - name: Gate TDD
        run: node quality/check-tdd.mjs

      - name: Limite de archivos para la exencion [sin-test]
        run: node quality/check-exencion.mjs
```

- [ ] **Paso 2: escribir el test del límite de exención**

Añadir a `quality/__tests__/check-tdd.test.mjs`:

```javascript
import { validarLimiteExencion } from '../check-exencion.mjs';

test('la exencion permite hasta 5 archivos de src', () => {
  const archivos = Array.from({ length: 5 }, (_, i) => `CaseritoApp/src/A/F${i}.cs`);
  const r = validarLimiteExencion({ archivosCambiados: archivos, mensajeCommit: '[sin-test] refactor puro de nombres' });
  assert.equal(r.ok, true);
});

test('la exencion falla con mas de 5 archivos de src', () => {
  const archivos = Array.from({ length: 6 }, (_, i) => `CaseritoApp/src/A/F${i}.cs`);
  const r = validarLimiteExencion({ archivosCambiados: archivos, mensajeCommit: '[sin-test] refactor puro de nombres' });
  assert.equal(r.ok, false);
  assert.match(r.motivo, /6/);
});

test('sin exencion el limite no aplica', () => {
  const archivos = Array.from({ length: 20 }, (_, i) => `CaseritoApp/src/A/F${i}.cs`);
  const r = validarLimiteExencion({ archivosCambiados: archivos, mensajeCommit: 'feat: cambio grande' });
  assert.equal(r.ok, true);
});
```

- [ ] **Paso 3: ejecutar y ver el fallo**

Ejecutar: `node --test quality/__tests__/check-tdd.test.mjs`
Esperado: FALLO — `Cannot find module '../check-exencion.mjs'`

- [ ] **Paso 4: implementar**

`quality/check-exencion.mjs`:

```javascript
#!/usr/bin/env node
// Un refactor trivial no toca quince archivos. Este gate impide que la exención
// [sin-test] se use para colar cambios grandes sin pruebas.

import { ejecutar } from './lib/ejecutar.mjs';
import { exito, fallo } from './lib/salida.mjs';

export const LIMITE_ARCHIVOS = 5;

export function validarLimiteExencion({ archivosCambiados, mensajeCommit }) {
  if (!mensajeCommit.includes('[sin-test]')) {
    return { ok: true, motivo: 'Sin exención: el límite no aplica.' };
  }

  const enSrc = archivosCambiados.filter(
    (ruta) => ruta.startsWith('CaseritoApp/src/') && ruta.endsWith('.cs'),
  );

  if (enSrc.length > LIMITE_ARCHIVOS) {
    return {
      ok: false,
      motivo:
        `La exención [sin-test] modificó ${enSrc.length} archivos de src/, ` +
        `por encima del límite de ${LIMITE_ARCHIVOS}. ` +
        `Un cambio de este tamaño necesita pruebas.`,
    };
  }

  return { ok: true, motivo: `Exención dentro del límite (${enSrc.length}/${LIMITE_ARCHIVOS}).` };
}

if (process.argv[1] && process.argv[1].endsWith('check-exencion.mjs')) {
  const { salida: diff } = ejecutar('git', ['diff', '--name-only', 'origin/master...HEAD'], { silencioso: true });
  const { salida: mensaje } = ejecutar('git', ['log', '-1', '--pretty=%B'], { silencioso: true });

  const resultado = validarLimiteExencion({
    archivosCambiados: diff.split('\n').map((l) => l.trim()).filter(Boolean),
    mensajeCommit: mensaje.trim(),
  });

  if (!resultado.ok) {
    console.log(fallo('Límite de exención [sin-test]', resultado.motivo));
    process.exit(1);
  }
  console.log(exito(resultado.motivo));
}
```

- [ ] **Paso 5: ejecutar los tests y verlos pasar**

Ejecutar: `node --test quality/`
Esperado: PASAN todos los archivos de test.

- [ ] **Paso 6: commit**

```bash
git add .github/workflows/ci.yml quality/check-exencion.mjs quality/__tests__/check-tdd.test.mjs
git commit -m "ci(calidad): job de gates y limite de la exencion sin-test"
```

---

## Tarea 10: Reglas para todos los agentes

**Archivos:**
- Crear: `docs/ai/PUERTA_CALIDAD.md`
- Modificar: `AGENTS.md`, `docs/ai/WORKFLOW.md`, `docs/ai/README.md`

- [ ] **Paso 1: escribir la documentación de referencia**

`docs/ai/PUERTA_CALIDAD.md`:

```markdown
# Puerta de calidad

Verificaciones deterministas que permiten integrar cambios sin revisión humana
del código. Neutrales al proveedor: aplican a cualquier agente.

## Comandos

    ./verify.ps1          # Windows, nivel rápido, sin Docker
    ./verify.sh           # POSIX, nivel rápido
    ./verify.ps1 -Full    # nivel completo, requiere Docker
    ./verify.sh --full

Nivel rápido antes de **cada commit**. Nivel completo antes de **mergear**.

## Qué mide cada gate

| Gate | Qué comprueba | Cómo se arregla un fallo |
|---|---|---|
| Gate TDD | Todo cambio en `src/` trae cambios en `tests/` | Escribe el test. Si el cambio no admite test, usa la exención. |
| Formato .NET | `dotnet format` | `dotnet format CaseritoApp.sln` |
| Build Release | Warnings tratados como errores | Corrige el warning. Nunca lo silencies con `#pragma`. |
| Unit tests | 379+ tests | Arregla el código o el test, según cuál esté mal. |
| Tests de arquitectura | Capas, aislamiento de contextos, convenciones CQRS | Mueve el código a la capa correcta. Nunca relajes la regla. |
| Complejidad | El conteo de CA1502/CA1505/CA1506 no sube | Extrae métodos, reduce ramas. |
| Lint / typecheck / tests web | Calidad del frontend | Según el mensaje. |
| Integración (`--full`) | Testcontainers.MsSql | Requiere Docker en marcha. |
| Cobertura (`--full`) | No cae más de 0,5 pp por proyecto | Añade tests al código nuevo. |

## Reglas innegociables

1. **Nunca uses `--no-verify`.** Ni en commit ni en push.
2. **Nunca relajes una baseline, un umbral o una exclusión para que pase el
   gate.** Si el gate falla, el problema está en el código.
3. **Las baselines solo se mueven hacia mejor.** Se actualizan tras un cambio que
   mejore la métrica, en un commit propio que explique la mejora.
4. **Nunca afirmes verde sin haber ejecutado el comando y visto la salida.**

## La exención del gate TDD

Para cambios en `src/` que genuinamente no admiten test — refactor puro,
renombrados, textos de UI — incluye en el mensaje del commit:

    [sin-test] <motivo de al menos 20 caracteres>

Límites: máximo 5 archivos de `src/` por commit eximido. `verify` imprime un
aviso cuando la detecta, y el uso queda auditado:

    git log --grep="\[sin-test\]" --oneline

## Actualizar una baseline legítimamente

Solo cuando la métrica **ha mejorado**:

1. Haz el cambio que mejora el código y verifica que la métrica sube.
2. Regenera la baseline con el procedimiento del spec.
3. Commit separado: `chore(calidad): actualiza baseline de <métrica> tras <mejora>`.
4. El mensaje debe decir qué mejoró y por qué.

Nunca regeneres una baseline para hacer pasar un gate que falla.
```

- [ ] **Paso 2: añadir la sección a AGENTS.md**

Insertar en `AGENTS.md`, entre las secciones «Verificación» y «Economía de
contexto»:

```markdown
## Puerta de calidad

- Ejecutar `./verify.ps1` (o `./verify.sh`) antes de cada commit, y
  `./verify.ps1 -Full` antes de mergear. Es obligatorio y sustituye a la revisión
  humana del código.
- Prohibido `--no-verify` en commit o push.
- Prohibido relajar una baseline, un umbral o una exclusión para que pase el
  gate. Si el gate falla, se arregla el código, no el gate.
- Las baselines de `quality/` solo se actualizan hacia mejor, en commit propio
  que explique la mejora.
- Un cambio en `src/` sin cambio en `tests/` requiere `[sin-test] <motivo>` en el
  mensaje del commit; máximo 5 archivos.
- Detalle de cada gate: `docs/ai/PUERTA_CALIDAD.md`.
```

Mantener el archivo corto: la sección son ~10 líneas, el detalle vive aparte.

- [ ] **Paso 3: actualizar el workflow y el índice**

En `docs/ai/WORKFLOW.md`, en la fase de cierre, sustituir la verificación actual
por la exigencia de `verify --full` en verde, pegando la línea de resumen como
evidencia.

En `docs/ai/README.md`, añadir la entrada del mapa de documentación:

```markdown
- `PUERTA_CALIDAD.md` — gates deterministas y reglas de baseline.
```

- [ ] **Paso 4: verificación final completa**

Ejecutar: `./verify.sh --full`
Esperado: verde en todos los gates. Pegar el resumen como evidencia.

Recorrer los 9 criterios de aceptación del spec y confirmar cada uno con su
comando. Reportar al humano cualquiera que no se cumpla, con el motivo.

- [ ] **Paso 5: commit**

```bash
git add docs/ai/PUERTA_CALIDAD.md AGENTS.md docs/ai/WORKFLOW.md docs/ai/README.md
git commit -m "docs(calidad): reglas de la puerta de calidad para todo agente"
```

---

## Cierre

Con las 10 tareas completadas y `verify --full` en verde, integrar según el flujo
git autorizado: merge a `master`, push, y borrar la rama.

Reportar al humano en el cierre:

- Duración del nivel rápido y del completo.
- Cobertura global de la baseline.
- Número total de violaciones de complejidad.
- Mutation score inicial.
- Cualquier fitness function que haya revelado una violación preexistente.
