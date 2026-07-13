# Andamiaje de homogeneidad de código — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Montar el andamiaje transversal que garantiza código homogéneo en CaseritoApp (enforcement mecánico + artefactos de Claude Code), verificado end-to-end con un proyecto smoke.

**Architecture:** Dos raíces. La raíz git y del proyecto Claude es `dev_Caserito/` (aloja `.claude/`, `.github/`, `CLAUDE.md`). La solución .NET vive bajo `CaseritoApp/` (config transversal, `global.json`, `.sln`, proyectos). El andamiaje se valida con un proyecto smoke desechable antes de que Fase 0 cree los bounded contexts reales.

**Tech Stack:** .NET 10 (SDK), C# `latest`, xUnit, NetArchTest.Rules, Roslynator.Analyzers, SonarAnalyzer.CSharp, Serilog (convención), Central Package Management, Husky.Net, GitHub Actions.

## Global Constraints

- **.NET 10** fijado en `global.json` con `rollForward: latestFeature` (confirmable en Fase 0).
- **Rigor estricto día 1**: `Nullable=enable`, `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, `EnableNETAnalyzers=true`, `AnalysisLevel=latest-recommended`. Nada compila si viola las reglas.
- **Sin StyleCop**; sin documentación XML obligatoria (`GenerateDocumentationFile=false`).
- **Central Package Management**: todas las versiones de paquetes se declaran en `CaseritoApp/Directory.Packages.props`. Ningún `.csproj` lleva `Version=` en sus `PackageReference`.
- **Rutas .NET** relativas a `CaseritoApp/`. **Artefactos de Claude y git** relativos a `dev_Caserito/`.
- **Idioma**: nombres de tests y comentarios en español (coherente con el resto del repo); identificadores de framework en su forma original.
- **Política anti-PII en logs, no negociable**: jamás loguear número de CI, imágenes de documento/selfie, tokens de sesión ni cadenas de QR/pago.
- Las versiones de paquetes NuGet indicadas son un piso razonable; si `dotnet restore` falla por versión inexistente, subir a la última estable disponible.

---

### Task 1: Bootstrap de la solución .NET + proyecto smoke

**Files:**
- Create: `CaseritoApp/global.json`
- Create: `CaseritoApp/CaseritoApp.sln`
- Create: `CaseritoApp/src/CaseritoApp.SmokeLib/CaseritoApp.SmokeLib.csproj`
- Create: `CaseritoApp/src/CaseritoApp.SmokeLib/Domain/SampleEntity.cs`
- Create: `CaseritoApp/src/CaseritoApp.SmokeLib/Application/SampleService.cs`

**Interfaces:**
- Produces: assembly `CaseritoApp.SmokeLib` con los namespaces `CaseritoApp.SmokeLib.Domain` (tipo `SampleEntity`) y `CaseritoApp.SmokeLib.Application` (tipo `SampleService`). La Task 3 depende de estos namespaces exactos.

- [ ] **Step 1: Crear `global.json`**

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

- [ ] **Step 2: Crear la solución y el proyecto smoke**

Run (desde `CaseritoApp/`):
```bash
cd CaseritoApp
dotnet new sln -n CaseritoApp
dotnet new classlib -n CaseritoApp.SmokeLib -o src/CaseritoApp.SmokeLib
dotnet sln add src/CaseritoApp.SmokeLib/CaseritoApp.SmokeLib.csproj
```
Borrar el `Class1.cs` autogenerado:
```bash
rm src/CaseritoApp.SmokeLib/Class1.cs
```

- [ ] **Step 3: Crear los tipos smoke en dos capas**

`CaseritoApp/src/CaseritoApp.SmokeLib/Domain/SampleEntity.cs`:
```csharp
namespace CaseritoApp.SmokeLib.Domain;

/// <summary>Entidad smoke para validar el andamiaje. Se elimina en Fase 0.</summary>
public sealed class SampleEntity
{
    public SampleEntity(string nombre) => Nombre = nombre;

    public string Nombre { get; }
}
```

`CaseritoApp/src/CaseritoApp.SmokeLib/Application/SampleService.cs`:
```csharp
using CaseritoApp.SmokeLib.Domain;

namespace CaseritoApp.SmokeLib.Application;

/// <summary>Servicio smoke: Application depende de Domain (dirección permitida).</summary>
public sealed class SampleService
{
    public string Describir(SampleEntity entidad) => $"Entidad: {entidad.Nombre}";
}
```

- [ ] **Step 4: Verificar que compila**

Run:
```bash
dotnet build CaseritoApp.sln
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 5: Commit**

```bash
git add CaseritoApp/global.json CaseritoApp/CaseritoApp.sln CaseritoApp/src
git commit -m "chore: bootstrap solución .NET + proyecto smoke"
```

---

### Task 2: Configuración transversal (estilo + analizadores + CPM)

**Files:**
- Create: `CaseritoApp/.editorconfig`
- Create: `CaseritoApp/Directory.Build.props`
- Create: `CaseritoApp/Directory.Packages.props`

**Interfaces:**
- Produces: enforcement estricto (warnings-as-errors + analizadores) y CPM activo para todos los proyectos bajo `CaseritoApp/`. Las Tasks 3+ crean proyectos sin `Version=` en sus `PackageReference`.

- [ ] **Step 1: Crear `Directory.Packages.props`**

`CaseritoApp/Directory.Packages.props`:
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <!-- Analizadores globales: se aplican a TODOS los proyectos automáticamente -->
  <ItemGroup>
    <GlobalPackageReference Include="Roslynator.Analyzers" Version="4.13.1" />
    <GlobalPackageReference Include="SonarAnalyzer.CSharp" Version="10.6.0.109712" />
  </ItemGroup>

  <!-- Dependencias de test y librerías -->
  <ItemGroup>
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="NetArchTest.Rules" Version="1.3.2" />
    <PackageVersion Include="Serilog" Version="4.1.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Crear `Directory.Build.props`**

`CaseritoApp/Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- Rigor estricto -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-recommended</AnalysisLevel>

    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Crear `.editorconfig`**

`CaseritoApp/.editorconfig`:
```ini
root = true

[*]
charset = utf-8
end_of_line = crlf
insert_final_newline = true
indent_style = space
trim_trailing_whitespace = true

[*.{cs,csx}]
indent_size = 4

# --- Organización de usings ---
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false
csharp_using_directive_placement = outside_namespace:error

# --- Namespaces con file scope ---
csharp_style_namespace_declarations = file_scoped:error

# --- 'this.' innecesario ---
dotnet_style_qualification_for_field = false:warning
dotnet_style_qualification_for_property = false:warning
dotnet_style_qualification_for_method = false:warning
dotnet_style_qualification_for_event = false:warning

# --- var cuando el tipo es evidente ---
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_elsewhere = false:suggestion

# --- Modificadores ---
dotnet_style_require_accessibility_modifiers = always:error
csharp_prefer_static_local_function = true:warning
dotnet_style_readonly_field = true:warning
csharp_preferred_modifier_order = public,private,protected,internal,static,extern,new,virtual,abstract,sealed,override,readonly,unsafe,volatile,async:warning

# --- Expresiones modernas ---
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_prefer_braces = true:warning
dotnet_style_prefer_is_null_check_over_reference_equality_method = true:warning

# --- Convenciones de nombres ---
dotnet_naming_rule.tipos_pascal_case.severity = error
dotnet_naming_rule.tipos_pascal_case.symbols = tipos
dotnet_naming_rule.tipos_pascal_case.style = pascal_case
dotnet_naming_symbols.tipos.applicable_kinds = class,struct,enum,property,method,event,delegate
dotnet_naming_symbols.tipos.applicable_accessibilities = *

dotnet_naming_rule.interfaces_con_i.severity = error
dotnet_naming_rule.interfaces_con_i.symbols = interfaces
dotnet_naming_rule.interfaces_con_i.style = prefijo_i
dotnet_naming_symbols.interfaces.applicable_kinds = interface
dotnet_naming_style.prefijo_i.required_prefix = I
dotnet_naming_style.prefijo_i.capitalization = pascal_case

dotnet_naming_rule.campos_privados_guion_bajo.severity = warning
dotnet_naming_rule.campos_privados_guion_bajo.symbols = campos_privados
dotnet_naming_rule.campos_privados_guion_bajo.style = guion_bajo_camel
dotnet_naming_symbols.campos_privados.applicable_kinds = field
dotnet_naming_symbols.campos_privados.applicable_accessibilities = private
dotnet_naming_style.guion_bajo_camel.required_prefix = _
dotnet_naming_style.guion_bajo_camel.capitalization = camel_case

dotnet_naming_style.pascal_case.capitalization = pascal_case

[*.{json,yml,yaml,csproj,props,targets,xml}]
indent_size = 2
```

- [ ] **Step 4: Verificar build estricto + formato**

Run:
```bash
cd CaseritoApp
dotnet build CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes
```
Expected: build `0 Warning(s) 0 Error(s)`; `dotnet format` termina sin reportar cambios (exit 0).

Si un analizador (Roslynator/Sonar) marca error en el código smoke: corregir el código smoke para cumplir la regla. Solo si la regla es genuinamente inaplicable al proyecto, bajar su severidad en `.editorconfig` con un comentario que justifique (coherente con "estricto día 1": la excepción es rara y documentada).

- [ ] **Step 5: Commit**

```bash
git add CaseritoApp/.editorconfig CaseritoApp/Directory.Build.props CaseritoApp/Directory.Packages.props
git commit -m "chore: configuración transversal estricta (editorconfig, analizadores, CPM)"
```

---

### Task 3: Tests de arquitectura (NetArchTest)

**Files:**
- Create: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj`
- Create: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/LayeringTests.cs`

**Interfaces:**
- Consumes: namespaces `CaseritoApp.SmokeLib.Domain` y `CaseritoApp.SmokeLib.Application` (Task 1).
- Produces: proyecto de tests que corre en CI (Task 5) y falla ante violaciones de capas.

- [ ] **Step 1: Crear el proyecto de tests y referenciarlo**

Run (desde `CaseritoApp/`):
```bash
cd CaseritoApp
dotnet new xunit -n CaseritoApp.ArchitectureTests -o tests/CaseritoApp.ArchitectureTests
dotnet sln add tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj
dotnet add tests/CaseritoApp.ArchitectureTests reference src/CaseritoApp.SmokeLib
rm tests/CaseritoApp.ArchitectureTests/UnitTest1.cs
```

Editar `tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj` para que el `<ItemGroup>` de paquetes NO tenga versiones (CPM las provee) y añadir NetArchTest. Debe quedar así:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="NetArchTest.Rules" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\CaseritoApp.SmokeLib\CaseritoApp.SmokeLib.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Escribir el test de capas (debe fallar primero)**

`CaseritoApp/tests/CaseritoApp.ArchitectureTests/LayeringTests.cs`:
```csharp
using CaseritoApp.SmokeLib.Domain;
using NetArchTest.Rules;
using Xunit;

namespace CaseritoApp.ArchitectureTests;

public sealed class LayeringTests
{
    private const string DomainNamespace = "CaseritoApp.SmokeLib.Domain";
    private const string ApplicationNamespace = "CaseritoApp.SmokeLib.Application";

    [Fact]
    public void Domain_no_debe_depender_de_Application()
    {
        var resultado = Types.InAssembly(typeof(SampleEntity).Assembly)
            .That().ResideInNamespace(DomainNamespace)
            .ShouldNot().HaveDependencyOn(ApplicationNamespace)
            .GetResult();

        Assert.True(
            resultado.IsSuccessful,
            $"Tipos que violan la regla: {string.Join(", ", resultado.FailingTypeNames ?? [])}");
    }
}
```

- [ ] **Step 3: Provocar el fallo para verificar que el test es real**

Temporalmente, en `src/CaseritoApp.SmokeLib/Domain/SampleEntity.cs`, agregar `using CaseritoApp.SmokeLib.Application;` y un campo `private readonly SampleService? _svc;`. Luego:
```bash
dotnet test CaseritoApp.sln
```
Expected: FAIL — el test reporta `SampleEntity` como tipo que viola la regla.

- [ ] **Step 4: Revertir la violación y confirmar que pasa**

Quitar el `using` y el campo agregados en el Step 3 (dejar `SampleEntity.cs` como en la Task 1). Luego:
```bash
dotnet test CaseritoApp.sln
```
Expected: PASS — `Passed! - Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add CaseritoApp/tests CaseritoApp/CaseritoApp.sln
git commit -m "test: reglas de arquitectura de capas con NetArchTest"
```

---

### Task 4: Helper de enmascarado de PII (convención de logging)

**Files:**
- Create: `CaseritoApp/src/CaseritoApp.SmokeLib/Logging/PiiRedaction.cs`
- Create: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/PiiRedactionTests.cs`

**Interfaces:**
- Produces: `CaseritoApp.SmokeLib.Logging.PiiRedaction.Redactar(string campo, string valor) : string` y `PiiRedaction.CamposProhibidos : IReadOnlySet<string>`. Referencia que Fase 1 reubicará en un proyecto building-blocks y conectará a una destructuring policy de Serilog.

- [ ] **Step 1: Escribir el test (debe fallar primero)**

`CaseritoApp/tests/CaseritoApp.ArchitectureTests/PiiRedactionTests.cs`:
```csharp
using CaseritoApp.SmokeLib.Logging;
using Xunit;

namespace CaseritoApp.ArchitectureTests;

public sealed class PiiRedactionTests
{
    [Theory]
    [InlineData("ci")]
    [InlineData("selfie")]
    [InlineData("token")]
    [InlineData("qr")]
    public void Redactar_enmascara_campos_prohibidos(string campo)
    {
        Assert.Equal("***", PiiRedaction.Redactar(campo, "valor-sensible"));
    }

    [Fact]
    public void Redactar_deja_pasar_campos_no_sensibles()
    {
        Assert.Equal("Bicicleta", PiiRedaction.Redactar("titulo", "Bicicleta"));
    }
}
```

- [ ] **Step 2: Verificar que falla por compilación**

Run:
```bash
dotnet test CaseritoApp.sln
```
Expected: FAIL — no compila: `PiiRedaction` no existe.

- [ ] **Step 3: Implementar el helper**

`CaseritoApp/src/CaseritoApp.SmokeLib/Logging/PiiRedaction.cs`:
```csharp
namespace CaseritoApp.SmokeLib.Logging;

/// <summary>
/// Enmascara campos considerados PII sensible para que nunca aparezcan en logs.
/// Referencia del andamiaje; Fase 1 la reubica y la conecta a una
/// destructuring policy de Serilog. Ver política anti-PII en CLAUDE.md.
/// </summary>
public static class PiiRedaction
{
    public static IReadOnlySet<string> CamposProhibidos { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ci",
            "documentoNumero",
            "documentoImagen",
            "selfie",
            "token",
            "qr",
            "pagoReferencia",
        };

    public static string Redactar(string campo, string valor) =>
        CamposProhibidos.Contains(campo) ? "***" : valor;
}
```

- [ ] **Step 4: Verificar que pasa**

Run:
```bash
dotnet test CaseritoApp.sln
```
Expected: PASS — `Passed! - Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add CaseritoApp/src/CaseritoApp.SmokeLib/Logging CaseritoApp/tests/CaseritoApp.ArchitectureTests/PiiRedactionTests.cs
git commit -m "feat: helper de enmascarado de PII para logging (convención)"
```

---

### Task 5: CI gate (GitHub Actions)

**Files:**
- Create: `.github/workflows/ci.yml` (en la raíz `dev_Caserito/`)

**Interfaces:**
- Consumes: la solución `CaseritoApp/CaseritoApp.sln` y `CaseritoApp/global.json`.

- [ ] **Step 1: Crear el workflow**

`.github/workflows/ci.yml`:
```yaml
name: CI

on:
  push:
    branches: [ main, dev ]
  pull_request:

jobs:
  build:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: CaseritoApp
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          global-json-file: CaseritoApp/global.json

      - name: Restore
        run: dotnet restore CaseritoApp.sln

      - name: Verificar formato
        run: dotnet format CaseritoApp.sln --verify-no-changes --no-restore

      - name: Build (warnings as errors)
        run: dotnet build CaseritoApp.sln --no-restore --configuration Release

      - name: Test
        run: dotnet test CaseritoApp.sln --no-build --configuration Release --verbosity normal
```

- [ ] **Step 2: Verificar localmente los mismos comandos del CI**

Run (desde `CaseritoApp/`):
```bash
cd CaseritoApp
dotnet restore CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes --no-restore
dotnet build CaseritoApp.sln --no-restore --configuration Release
dotnet test CaseritoApp.sln --no-build --configuration Release
```
Expected: los cuatro comandos terminan con exit 0. (El verde real en GitHub Actions requiere un remoto configurado; se confirma al hacer el primer push.)

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: workflow de GitHub Actions (format, build estricto, test)"
```

---

### Task 6: Git hook local con Husky.Net

**Files:**
- Create: `.config/dotnet-tools.json` (raíz `dev_Caserito/`)
- Create: `.husky/task-runner.json` (raíz `dev_Caserito/`)
- Create: `.husky/pre-commit` (generado por Husky)

**Interfaces:**
- Consumes: `dotnet format` sobre `CaseritoApp/CaseritoApp.sln`.

- [ ] **Step 1: Crear el manifiesto de herramientas e instalar Husky**

Run (desde la raíz `dev_Caserito/`):
```bash
dotnet new tool-manifest
dotnet tool install Husky
dotnet husky install
```
Expected: se crean `.config/dotnet-tools.json` y la carpeta `.husky/`; el hook queda instalado en `.git/hooks/pre-commit`.

- [ ] **Step 2: Configurar la tarea de formato pre-commit**

Sobrescribir `.husky/task-runner.json` con:
```json
{
  "tasks": [
    {
      "name": "dotnet-format-staged",
      "group": "pre-commit",
      "command": "dotnet",
      "args": [ "format", "CaseritoApp/CaseritoApp.sln", "--include", "${staged}" ],
      "include": [ "**/*.cs" ]
    }
  ]
}
```

Asegurar que `.husky/pre-commit` ejecute el task-runner. Su contenido debe ser:
```sh
#!/bin/sh
. "$(dirname "$0")/_/husky.sh"

dotnet husky run --group pre-commit
```

- [ ] **Step 3: Verificar el hook**

Run (desde `dev_Caserito/`):
```bash
git add .config/dotnet-tools.json .husky
git commit -m "chore: husky.net con formato pre-commit"
```
Expected: el commit dispara `dotnet husky run --group pre-commit`, que formatea los `.cs` staged y completa el commit. (Si no hay `.cs` staged, la tarea corre sin cambios y el commit procede.)

---

### Task 7: CLAUDE.md (instrucciones de proyecto)

**Files:**
- Create: `CLAUDE.md` (raíz `dev_Caserito/`)

- [ ] **Step 1: Crear `CLAUDE.md`**

`CLAUDE.md`:
```markdown
# CaseritoApp

Marketplace C2C para Bolivia (web + Android). Ver alcance en
`CaseritoApp/Documentacion/marketplace-bolivia-mvp-brief.md` y el plan por fases
en `CaseritoApp/Documentacion/plan-desarrollo-mvp-v1.md`.

## Estructura

- `CaseritoApp/` — solución .NET. Config transversal, `global.json`, `.sln`.
  - `src/` — proyectos de producción (bounded contexts; se definen en Fase 0).
  - `tests/` — proyectos de test, incluidos los de arquitectura.
- `.github/` — CI. `.claude/` — skills, agentes, hooks. `docs/superpowers/` — specs y planes.

## Arquitectura

Clean Architecture. Bounded contexts (a poblar en Fase 0): Identity, Catalog,
Chat, Orders, Reputation, Notifications. Reglas de capa (verificadas por
`CaseritoApp.ArchitectureTests`):

- `Domain` no depende de nada hacia afuera.
- `Application` depende solo de `Domain`.
- Las dependencias apuntan siempre hacia adentro.

El patrón interno de cada contexto (CQRS/MediatR/Result) se decide en Fase 0.

## Convenciones de código

Fuente de verdad: `CaseritoApp/.editorconfig` y `CaseritoApp/Directory.Build.props`.
Rigor estricto: nullable enable, warnings-as-errors, analizadores .NET +
Roslynator + Sonar. Nada compila si viola las reglas.

- Namespaces file-scoped; `using` fuera del namespace, System primero.
- `PascalCase` tipos/miembros; `camelCase` locales; `_camelCase` campos privados; `I` en interfaces.
- Versiones de paquetes solo en `CaseritoApp/Directory.Packages.props` (CPM). Nunca `Version=` en un `.csproj`.

## Política anti-PII en logs (NO negociable)

Jamás loguear: número de CI, imágenes de documento o selfie, tokens de sesión,
cadenas de QR/pago. Usar el helper de enmascarado (`PiiRedaction`) para cualquier
campo sensible. Es el activo de mayor riesgo legal del MVP.

## Comandos

Desde `CaseritoApp/`:

- Build: `dotnet build CaseritoApp.sln`
- Test: `dotnet test CaseritoApp.sln`
- Formatear: `dotnet format CaseritoApp.sln`
- Verificar formato (como el CI): `dotnet format CaseritoApp.sln --verify-no-changes`

## Ramas

`main` estable; trabajo en ramas `feat/*`, `fix/*`, `chore/*` con PR hacia `dev`.

## Especificaciones

Diseños y planes en `docs/superpowers/specs/` y `docs/superpowers/plans/`.
```

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: CLAUDE.md con convenciones y política anti-PII"
```

---

### Task 8: Hook de auto-formato en Claude Code

**Files:**
- Create: `.claude/hooks/format-cs.ps1` (raíz `dev_Caserito/`)
- Create/Modify: `.claude/settings.json` (raíz `dev_Caserito/`)

**Interfaces:**
- Consumes: el `file_path` que Claude Code entrega por stdin al hook `PostToolUse`.

- [ ] **Step 1: Crear el script de formato**

`.claude/hooks/format-cs.ps1`:
```powershell
# Lee el evento del hook por stdin y formatea el .cs editado.
$ErrorActionPreference = 'SilentlyContinue'
$raw = [Console]::In.ReadToEnd()
if (-not $raw) { exit 0 }

try { $evt = $raw | ConvertFrom-Json } catch { exit 0 }

$file = $evt.tool_input.file_path
if (-not $file) { exit 0 }
if ($file -notmatch '\.cs$') { exit 0 }
if (-not (Test-Path $file)) { exit 0 }

$sln = Join-Path $PSScriptRoot '..\..\CaseritoApp\CaseritoApp.sln'
if (-not (Test-Path $sln)) { exit 0 }

& dotnet format $sln --include $file --verbosity quiet | Out-Null
exit 0
```

- [ ] **Step 2: Registrar el hook en `.claude/settings.json`**

Si `.claude/settings.json` no existe, crearlo con este contenido. Si existe, fusionar la clave `hooks` sin borrar lo demás:
```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "powershell -NoProfile -ExecutionPolicy Bypass -File \"$CLAUDE_PROJECT_DIR/.claude/hooks/format-cs.ps1\""
          }
        ]
      }
    ]
  }
}
```

- [ ] **Step 3: Verificar el JSON y el hook**

Run (desde `dev_Caserito/`):
```bash
node -e "JSON.parse(require('fs').readFileSync('.claude/settings.json','utf8')); console.log('json-ok')"
```
Expected: `json-ok`.

Verificación funcional (manual): editar cualquier `.cs` con formato incorrecto (p. ej. indentación extra) mediante Claude Code y confirmar que, tras el `Edit`, el archivo queda formateado. Si el shell de hooks en la máquina no resuelve `powershell`, ajustar el comando a la ruta completa de PowerShell.

- [ ] **Step 4: Commit**

```bash
git add .claude/hooks/format-cs.ps1 .claude/settings.json
git commit -m "chore: hook de auto-formato de .cs en Claude Code"
```

---

### Task 9: Subagente code-reviewer del proyecto

**Files:**
- Create: `.claude/agents/code-reviewer-caserito.md` (raíz `dev_Caserito/`)

- [ ] **Step 1: Crear el subagente**

`.claude/agents/code-reviewer-caserito.md`:
```markdown
---
name: code-reviewer-caserito
description: Revisor de código afinado a las convenciones de CaseritoApp. Úsalo antes de mergear cambios en la solución .NET para verificar capas, naming, política anti-PII y patrones acordados.
tools: Glob, Grep, Read, Bash
---

Eres el revisor de código de CaseritoApp. Revisa el diff actual contra estas
reglas del proyecto y reporta hallazgos ordenados por severidad.

## Reglas a verificar

1. **Capas (Clean Architecture)**: `Domain` no referencia `Application`/`Infrastructure`/presentación;
   `Application` solo referencia `Domain`; dependencias hacia adentro. Ante la duda,
   revisa `CaseritoApp/tests/CaseritoApp.ArchitectureTests`.
2. **Anti-PII en logs (crítico)**: ningún log ni traza incluye número de CI, imágenes de
   documento/selfie, tokens ni cadenas de QR/pago. Debe usarse el helper `PiiRedaction`.
3. **Convenciones**: namespaces file-scoped; `PascalCase`/`camelCase`/`_camelCase`;
   `I` en interfaces; sin `Version=` en `.csproj` (CPM en `Directory.Packages.props`).
4. **Rigor**: nada que dependa de suprimir warnings sin justificación en `.editorconfig`.

## Cómo reportar

Para cada hallazgo: archivo:línea, regla violada, y corrección concreta.
Si no hay hallazgos, dilo explícitamente. No apruebes cambios con violaciones de
capas o de la política anti-PII.
```

- [ ] **Step 2: Commit**

```bash
git add .claude/agents/code-reviewer-caserito.md
git commit -m "chore: subagente code-reviewer con convenciones de CaseritoApp"
```

---

### Task 10: Skills de scaffolding

**Files:**
- Create: `.claude/skills/nuevo-bounded-context/SKILL.md` (raíz `dev_Caserito/`)
- Create: `.claude/skills/nuevo-caso-de-uso/SKILL.md` (placeholder)

- [ ] **Step 1: Crear la skill `nuevo-bounded-context`**

`.claude/skills/nuevo-bounded-context/SKILL.md`:
```markdown
---
name: nuevo-bounded-context
description: Genera el esqueleto de proyectos por capas (Domain, Application, Infrastructure) de un nuevo bounded context de CaseritoApp, respetando Clean Architecture y CPM. Úsalo al crear un contexto nuevo (Identity, Catalog, etc.).
---

# Nuevo bounded context

Crea la estructura estándar de un bounded context bajo `CaseritoApp/src/`.
Recibe el nombre del contexto (p. ej. `Catalog`) como argumento.

## Pasos

Sea `<Ctx>` el nombre en PascalCase (ej. `Catalog`). Desde `CaseritoApp/`:

1. Crear los tres proyectos por capa (sin `Version=`, CPM los resuelve):
   ```bash
   dotnet new classlib -n CaseritoApp.<Ctx>.Domain      -o src/<Ctx>/Domain
   dotnet new classlib -n CaseritoApp.<Ctx>.Application  -o src/<Ctx>/Application
   dotnet new classlib -n CaseritoApp.<Ctx>.Infrastructure -o src/<Ctx>/Infrastructure
   rm src/<Ctx>/Domain/Class1.cs src/<Ctx>/Application/Class1.cs src/<Ctx>/Infrastructure/Class1.cs
   ```

2. Referencias hacia adentro:
   ```bash
   dotnet add src/<Ctx>/Application reference src/<Ctx>/Domain
   dotnet add src/<Ctx>/Infrastructure reference src/<Ctx>/Application
   ```

3. Agregar a la solución:
   ```bash
   dotnet sln add src/<Ctx>/Domain src/<Ctx>/Application src/<Ctx>/Infrastructure
   ```

4. Añadir la regla de capas del nuevo contexto en
   `tests/CaseritoApp.ArchitectureTests` (copiar el patrón de `LayeringTests.cs`,
   sustituyendo los namespaces por `CaseritoApp.<Ctx>.Domain` y `.Application`).

5. Verificar:
   ```bash
   dotnet build CaseritoApp.sln && dotnet test CaseritoApp.sln
   ```
   Ambos deben terminar en verde.

## Nota

El patrón interno (CQRS/MediatR/Result, validación, mapeo) se define en Fase 0.
Esta skill solo crea el esqueleto de capas; no impone patrón de casos de uso.
```

- [ ] **Step 2: Crear el placeholder `nuevo-caso-de-uso`**

`.claude/skills/nuevo-caso-de-uso/SKILL.md`:
```markdown
---
name: nuevo-caso-de-uso
description: (PENDIENTE — no usar aún) Generará un caso de uso (command/query + handler + validator + test) una vez decidido el patrón interno en Fase 0.
---

# Nuevo caso de uso — PENDIENTE

Esta skill está intencionalmente incompleta. Su contenido depende del patrón
interno de los bounded contexts (¿MediatR/CQRS?, Result pattern, FluentValidation),
que se decide en la Fase 0 del plan de desarrollo.

**No la uses todavía.** Cuando se cierre esa decisión, completar aquí los pasos
para generar: el command/query, su handler, el validator y el test asociado,
siguiendo el patrón acordado.
```

- [ ] **Step 3: Verificar el registro de las skills**

Run (desde `dev_Caserito/`):
```bash
ls .claude/skills/nuevo-bounded-context/SKILL.md .claude/skills/nuevo-caso-de-uso/SKILL.md
```
Expected: ambas rutas existen. (Las skills de proyecto se descubren automáticamente por Claude Code al reiniciar la sesión.)

- [ ] **Step 4: Commit**

```bash
git add .claude/skills
git commit -m "chore: skill nuevo-bounded-context + placeholder nuevo-caso-de-uso"
```

---

## Verificación end-to-end (al terminar todas las tasks)

Desde `CaseritoApp/`:
1. `dotnet build CaseritoApp.sln` → `0 Warning(s) 0 Error(s)`.
2. `dotnet format CaseritoApp.sln --verify-no-changes` → sin cambios (exit 0).
3. `dotnet test CaseritoApp.sln` → `Passed! - Failed: 0` (incluye tests de arquitectura y de PII).

Desde `dev_Caserito/`:
4. Un `git commit` con un `.cs` staged dispara el formato de Husky sin errores.
5. `.claude/settings.json` es JSON válido y el hook formatea un `.cs` tras editarlo con Claude Code.
6. Las skills `nuevo-bounded-context` y `nuevo-caso-de-uso` aparecen en la lista de skills.
7. (Cuando exista remoto) el push a GitHub dispara el workflow CI y completa en verde.
```
