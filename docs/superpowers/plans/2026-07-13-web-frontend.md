# Frontend web (React PWA) — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scaffold del frontend web en `web/`: React+TS+Vite, tooling estricto (ESLint/Prettier/Vitest), MUI, React Router + TanStack Query, una página smoke que consume `/health` del backend, PWA instalable, Capacitor configurado, y el CI extendido — todo compilando/verde.

**Architecture:** SPA React + TypeScript (Vite) como PWA instalable, envuelta con Capacitor para tiendas. Monorepo: vive en `web/` en la raíz del repo, separado de la solución .NET en `CaseritoApp/`. Consume la Web API .NET del host. Sin features de producto ni auth real (Fase 1).

**Tech Stack:** React 18/19, TypeScript, Vite, MUI (@mui/material + Emotion), React Router, TanStack Query, vite-plugin-pwa, Vitest + React Testing Library, ESLint + Prettier, Capacitor.

## Global Constraints

- **Ubicación**: todo el frontend en `web/` (raíz del repo `dev_Caserito`). No mezclar con `CaseritoApp/`.
- **TypeScript estricto** (`strict: true`); **ESLint + Prettier** deben pasar limpios; el CI los aplica (mirror del rigor "día 1" del backend).
- **Versiones npm**: usa `npm install <pkg>` que resuelve la última estable; no fijes versiones exactas a mano. Confirma con `npm run build`/`test` que todo es compatible.
- **Idioma**: textos de UI y comentarios en **español**; identificadores de framework en su forma original.
- **NO** agregar plataformas nativas de Capacitor (android/ios) en este ciclo (requieren Android Studio/Xcode): solo `capacitor.config.ts` + dependencias core/cli.
- **NO** implementar features de producto ni wiring real de auth (dependen de fases posteriores).
- El hook pre-commit de Husky solo formatea `.cs` staged; los archivos de `web/` no lo disparan (se saltea). El formateo del frontend lo cubre Prettier/CI.
- Node disponible en el entorno: v24.x. Trabaja en `web/` para los comandos npm.

## Fuera de alcance (documentado en el spec)

Features de producto, generación del cliente OpenAPI (se hará cuando existan endpoints de negocio en Fase 1; aquí un cliente de `/health` escrito a mano), auth/JWT real, publicación en tiendas (solo Capacitor configurado), branding definitivo, push.

---

### Task 1: Scaffold Vite + React + TypeScript

**Files:**
- Create: `web/` (proyecto Vite: `package.json`, `vite.config.ts`, `tsconfig*.json`, `index.html`, `src/main.tsx`, `src/App.tsx`, etc.)
- Modify: `.gitignore` (raíz) — ignorar artefactos de Node

**Interfaces:**
- Produces: proyecto `web/` que instala, compila y arranca; base para las tareas siguientes.

- [ ] **Step 1: Generar el proyecto**

Run (desde la raíz `dev_Caserito`):
```bash
npm create vite@latest web -- --template react-ts
cd web
npm install
```

- [ ] **Step 2: Ignorar artefactos de Node en `.gitignore` (raíz)**

Añade al final de `dev_Caserito/.gitignore`:
```
# Frontend (Node / Vite)
node_modules/
web/dist/
web/dev-dist/
*.local
.eslintcache
# Capacitor
web/android/
web/ios/
.capacitor/
```

- [ ] **Step 3: Verificar build y typecheck**

Run (desde `web/`):
```bash
npm run build
```
Expected: build exitoso, genera `dist/`. (El template trae `tsc -b && vite build`.)

- [ ] **Step 4: Commit**

```bash
cd ..
git add web .gitignore
git commit -m "chore(web): scaffold Vite + React + TypeScript"
```
> Nota: `web/node_modules` y `web/dist` quedan git-ignored; el commit solo debe traer código fuente y config.

---

### Task 2: Tooling estricto (ESLint, Prettier, Vitest + RTL, scripts)

**Files:**
- Create/Modify: `web/eslint.config.js`, `web/.prettierrc.json`, `web/vitest.config.ts` (o config en `vite.config.ts`), `web/src/test/setup.ts`
- Modify: `web/package.json` (scripts + devDependencies), `web/tsconfig*.json` (asegurar `strict`)

**Interfaces:**
- Produces: scripts `lint`, `format`, `typecheck`, `test` funcionando; base de testing para las tareas con TDD.

- [ ] **Step 1: Instalar dependencias de tooling**

Run (desde `web/`):
```bash
npm install -D prettier eslint-config-prettier vitest @vitest/coverage-v8 jsdom @testing-library/react @testing-library/jest-dom @testing-library/user-event
```
(El template `react-ts` ya trae ESLint; si no, `npm install -D eslint @eslint/js typescript-eslint eslint-plugin-react-hooks eslint-plugin-react-refresh`.)

- [ ] **Step 2: Prettier**

`web/.prettierrc.json`:
```json
{
  "semi": true,
  "singleQuote": true,
  "trailingComma": "all",
  "printWidth": 100
}
```
Añade `eslint-config-prettier` al final de la config de ESLint (`web/eslint.config.js`) para desactivar reglas de formato que choquen con Prettier: importa `eslintConfigPrettier` y agrégalo como último elemento del array exportado.

- [ ] **Step 3: Config de Vitest**

Añade a `web/vite.config.ts` la sección de test (o crea `vitest.config.ts`):
```ts
/// <reference types="vitest/config" />
// dentro de defineConfig({...}):
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
  },
```
`web/src/test/setup.ts`:
```ts
import '@testing-library/jest-dom';
```

- [ ] **Step 4: Scripts en `package.json`**

Asegura estos scripts en `web/package.json`:
```json
{
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "preview": "vite preview",
    "lint": "eslint .",
    "format": "prettier --write .",
    "format:check": "prettier --check .",
    "typecheck": "tsc -b --noEmit",
    "test": "vitest run"
  }
}
```
Confirma `"strict": true` en `web/tsconfig.app.json` (Vite lo trae por defecto; verifícalo).

- [ ] **Step 5: Test trivial para validar el runner**

`web/src/test/smoke.test.ts`:
```ts
import { describe, it, expect } from 'vitest';

describe('runner', () => {
  it('funciona', () => {
    expect(1 + 1).toBe(2);
  });
});
```

- [ ] **Step 6: Verificar tooling**

Run (desde `web/`):
```bash
npm run lint
npm run typecheck
npm run test
npm run format:check
```
Expected: los cuatro terminan sin errores (corre `npm run format` una vez si `format:check` marca diferencias, y re-verifica).

- [ ] **Step 7: Commit**

```bash
cd ..
git add web
git commit -m "chore(web): tooling estricto (eslint, prettier, vitest, scripts)"
```

---

### Task 3: MUI + tema

**Files:**
- Create: `web/src/theme/theme.ts`
- Modify: `web/src/main.tsx`

**Interfaces:**
- Produces: `ThemeProvider` + `CssBaseline` de MUI envolviendo la app; tema base reutilizable.

- [ ] **Step 1: Instalar MUI**

Run (desde `web/`):
```bash
npm install @mui/material @emotion/react @emotion/styled @mui/icons-material @fontsource/roboto
```

- [ ] **Step 2: Tema**

`web/src/theme/theme.ts`:
```ts
import { createTheme } from '@mui/material/styles';

// Tema base de CaseritoApp; el branding definitivo se ajusta después.
export const theme = createTheme({
  palette: {
    primary: { main: '#2e7d32' },
    secondary: { main: '#ff8f00' },
  },
});
```

- [ ] **Step 3: Envolver la app**

Edita `web/src/main.tsx` para envolver `<App />` con MUI (importa Roboto, `ThemeProvider`, `CssBaseline`):
```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { ThemeProvider, CssBaseline } from '@mui/material';
import '@fontsource/roboto/400.css';
import '@fontsource/roboto/500.css';
import '@fontsource/roboto/700.css';
import { theme } from './theme/theme';
import App from './App.tsx';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <App />
    </ThemeProvider>
  </StrictMode>,
);
```

- [ ] **Step 4: Verificar**

Run (desde `web/`): `npm run build && npm run lint`
Expected: build y lint limpios.

- [ ] **Step 5: Commit**

```bash
cd ..
git add web
git commit -m "feat(web): MUI + tema base"
```

---

### Task 4: React Router + TanStack Query (con test)

**Files:**
- Create: `web/src/routes/HomePage.tsx`, `web/src/routes/NotFoundPage.tsx`, `web/src/app/providers.tsx`, `web/src/app/router.tsx`
- Modify: `web/src/App.tsx`
- Test: `web/src/routes/HomePage.test.tsx`

**Interfaces:**
- Consumes: MUI (Task 3).
- Produces: `AppProviders` (QueryClientProvider) y el router con rutas `/` (HomePage) y `*` (NotFoundPage). `HomePage` exporta un componente que las tareas siguientes enriquecen.

- [ ] **Step 1: Instalar**

Run (desde `web/`):
```bash
npm install react-router-dom @tanstack/react-query
```

- [ ] **Step 2: Escribir el test (RED)**

`web/src/routes/HomePage.test.tsx`:
```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { HomePage } from './HomePage';

describe('HomePage', () => {
  it('muestra el título de CaseritoApp', () => {
    render(<HomePage />);
    expect(screen.getByText(/CaseritoApp/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Verificar RED**

Run (desde `web/`): `npm run test`
Expected: FAIL (HomePage no existe).

- [ ] **Step 4: Implementar**

`web/src/routes/HomePage.tsx`:
```tsx
import { Container, Typography } from '@mui/material';

export function HomePage() {
  return (
    <Container sx={{ py: 4 }}>
      <Typography variant="h4" component="h1">
        CaseritoApp
      </Typography>
    </Container>
  );
}
```

`web/src/routes/NotFoundPage.tsx`:
```tsx
import { Container, Typography } from '@mui/material';

export function NotFoundPage() {
  return (
    <Container sx={{ py: 4 }}>
      <Typography variant="h5">Página no encontrada</Typography>
    </Container>
  );
}
```

`web/src/app/router.tsx`:
```tsx
import { createBrowserRouter } from 'react-router-dom';
import { HomePage } from '../routes/HomePage';
import { NotFoundPage } from '../routes/NotFoundPage';

export const router = createBrowserRouter([
  { path: '/', element: <HomePage /> },
  { path: '*', element: <NotFoundPage /> },
]);
```

`web/src/app/providers.tsx`:
```tsx
import { type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const queryClient = new QueryClient();

export function AppProviders({ children }: { children: ReactNode }) {
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}
```

`web/src/App.tsx` (reemplaza el contenido del template):
```tsx
import { RouterProvider } from 'react-router-dom';
import { AppProviders } from './app/providers';
import { router } from './app/router';

export default function App() {
  return (
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>
  );
}
```

- [ ] **Step 5: Verificar GREEN**

Run (desde `web/`): `npm run test && npm run build && npm run lint`
Expected: test PASS, build y lint limpios.

- [ ] **Step 6: Commit**

```bash
cd ..
git add web
git commit -m "feat(web): router + TanStack Query + páginas base"
```

---

### Task 5: Capa de API + página smoke contra /health (TDD)

**Files:**
- Create: `web/src/api/client.ts`, `web/src/api/health.ts`, `web/src/api/useHealth.ts`
- Modify: `web/src/routes/HomePage.tsx`, `web/vite.config.ts` (proxy dev)
- Test: `web/src/api/health.test.ts`, `web/src/routes/HomePage.test.tsx` (ampliar)

**Interfaces:**
- Consumes: TanStack Query (Task 4).
- Produces: `getHealth(): Promise<HealthStatus>` con `HealthStatus = { estado: string }`; hook `useHealth()`; `HomePage` muestra el estado del backend.

- [ ] **Step 1: Proxy de dev hacia el host**

En `web/vite.config.ts`, dentro de `defineConfig({...})`, añade `server.proxy` apuntando al host .NET. Lee el puerto HTTP real del host en `CaseritoApp/src/Host/CaseritoApp.Host/Properties/launchSettings.json` (perfil http) y úsalo como `target` (ejemplo con 5080; ajústalo):
```ts
  server: {
    proxy: {
      '/health': 'http://localhost:5080',
      '/api': 'http://localhost:5080',
    },
  },
```

- [ ] **Step 2: Escribir el test de la capa de API (RED)**

`web/src/api/health.test.ts`:
```ts
import { describe, it, expect, vi, afterEach } from 'vitest';
import { getHealth } from './health';

afterEach(() => vi.restoreAllMocks());

describe('getHealth', () => {
  it('devuelve el estado del backend', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ estado: 'ok' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    const resultado = await getHealth();
    expect(resultado.estado).toBe('ok');
  });

  it('lanza si la respuesta no es exitosa', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('', { status: 500 }));
    await expect(getHealth()).rejects.toThrow();
  });
});
```

- [ ] **Step 3: Verificar RED**

Run (desde `web/`): `npm run test`
Expected: FAIL (`getHealth` no existe).

- [ ] **Step 4: Implementar la capa de API**

`web/src/api/client.ts`:
```ts
// Cliente HTTP mínimo hacia la Web API. En Fase 1, cuando existan endpoints de
// negocio, se reemplaza/complementa con un cliente tipado generado del OpenAPI.
export async function getJson<T>(ruta: string): Promise<T> {
  const respuesta = await fetch(ruta, { headers: { Accept: 'application/json' } });
  if (!respuesta.ok) {
    throw new Error(`Petición fallida (${respuesta.status}) a ${ruta}`);
  }
  return (await respuesta.json()) as T;
}
```

`web/src/api/health.ts`:
```ts
import { getJson } from './client';

export interface HealthStatus {
  estado: string;
}

export function getHealth(): Promise<HealthStatus> {
  return getJson<HealthStatus>('/health');
}
```

`web/src/api/useHealth.ts`:
```ts
import { useQuery } from '@tanstack/react-query';
import { getHealth } from './health';

export function useHealth() {
  return useQuery({ queryKey: ['health'], queryFn: getHealth });
}
```

- [ ] **Step 5: Mostrar el estado en HomePage y ampliar su test**

Actualiza `web/src/routes/HomePage.tsx` para usar `useHealth()` y mostrar el estado (Chip de MUI):
```tsx
import { Container, Typography, Chip, Stack } from '@mui/material';
import { useHealth } from '../api/useHealth';

export function HomePage() {
  const { data, isLoading, isError } = useHealth();
  const estado = isLoading ? 'consultando…' : isError ? 'sin conexión' : (data?.estado ?? '—');

  return (
    <Container sx={{ py: 4 }}>
      <Stack spacing={2}>
        <Typography variant="h4" component="h1">
          CaseritoApp
        </Typography>
        <Chip label={`Backend: ${estado}`} color={isError ? 'error' : 'success'} />
      </Stack>
    </Container>
  );
}
```
Amplía `web/src/routes/HomePage.test.tsx` para envolver el render en `QueryClientProvider` (crea un `QueryClient` de test) y mockear `fetch`, verificando que el título sigue presente y que aparece "Backend:". Ejemplo del render envuelto:
```tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
// ...
const qc = new QueryClient();
render(
  <QueryClientProvider client={qc}>
    <HomePage />
  </QueryClientProvider>,
);
expect(screen.getByText(/CaseritoApp/i)).toBeInTheDocument();
expect(screen.getByText(/Backend:/i)).toBeInTheDocument();
```

- [ ] **Step 6: Verificar GREEN**

Run (desde `web/`): `npm run test && npm run build && npm run lint && npm run typecheck`
Expected: todo verde.

- [ ] **Step 7: Commit**

```bash
cd ..
git add web
git commit -m "feat(web): capa de API + smoke de /health en HomePage"
```

---

### Task 6: PWA (instalable)

**Files:**
- Modify: `web/vite.config.ts`
- Create: iconos PWA en `web/public/` (`pwa-192x192.png`, `pwa-512x512.png`) o placeholders

**Interfaces:**
- Produces: manifest + service worker generados en build; app instalable.

- [ ] **Step 1: Instalar el plugin**

Run (desde `web/`): `npm install -D vite-plugin-pwa`

- [ ] **Step 2: Configurar el plugin**

En `web/vite.config.ts`, importa y añade `VitePWA` a `plugins`:
```ts
import { VitePWA } from 'vite-plugin-pwa';
// dentro de plugins: [ react(), VitePWA({ ... }) ]
    VitePWA({
      registerType: 'autoUpdate',
      manifest: {
        name: 'CaseritoApp',
        short_name: 'Caserito',
        description: 'Marketplace C2C',
        theme_color: '#2e7d32',
        background_color: '#ffffff',
        display: 'standalone',
        start_url: '/',
        icons: [
          { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
          { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png' },
        ],
      },
    }),
```

- [ ] **Step 3: Iconos**

Coloca `web/public/pwa-192x192.png` y `web/public/pwa-512x512.png` (pueden ser placeholders de color sólido por ahora; el branding va después). Si no dispones de imágenes, genera dos PNG cuadrados simples del color del tema.

- [ ] **Step 4: Verificar generación de PWA**

Run (desde `web/`): `npm run build`
Expected: el build genera `dist/manifest.webmanifest` y `dist/sw.js` (o `dist/registerSW.js`). Confirma que existen en `dist/`.

- [ ] **Step 5: Commit**

```bash
cd ..
git add web
git commit -m "feat(web): PWA instalable (manifest + service worker)"
```

---

### Task 7: Capacitor (config, sin plataformas nativas)

**Files:**
- Create: `web/capacitor.config.ts`
- Modify: `web/package.json` (deps)

**Interfaces:**
- Produces: configuración de Capacitor lista para, en el futuro, `npx cap add android/ios` y envolver la PWA. NO agrega plataformas nativas ahora.

- [ ] **Step 1: Instalar Capacitor**

Run (desde `web/`):
```bash
npm install @capacitor/core
npm install -D @capacitor/cli
```

- [ ] **Step 2: Config**

`web/capacitor.config.ts`:
```ts
import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.caserito.app',
  appName: 'CaseritoApp',
  webDir: 'dist',
};

export default config;
```
> Las plataformas nativas (`npx cap add android` / `ios`) se agregarán cuando se prepare la publicación en tiendas; requieren Android Studio/Xcode y quedan fuera de este ciclo.

- [ ] **Step 3: Verificar**

Run (desde `web/`):
```bash
npm run build
npx cap sync || echo "cap sync requiere plataformas; OK omitir en este ciclo"
```
Expected: build exitoso; `capacitor.config.ts` compila (typecheck no rompe). `cap sync` puede advertir que no hay plataformas — es esperado.

- [ ] **Step 4: Commit**

```bash
cd ..
git add web
git commit -m "chore(web): configurar Capacitor (sin plataformas nativas aún)"
```

---

### Task 8: CI — job de frontend

**Files:**
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Produces: un job `frontend` que instala, lint, typecheck, test y build del `web/`, en paralelo al job de backend existente.

- [ ] **Step 1: Añadir el job**

Añade a `.github/workflows/ci.yml` un segundo job (mantén el job de backend existente):
```yaml
  frontend:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: web
    steps:
      - uses: actions/checkout@v4

      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: 22
          cache: npm
          cache-dependency-path: web/package-lock.json

      - name: Install
        run: npm ci

      - name: Lint
        run: npm run lint

      - name: Typecheck
        run: npm run typecheck

      - name: Test
        run: npm run test

      - name: Build
        run: npm run build
```

- [ ] **Step 2: Verificar localmente los mismos pasos**

Run (desde `web/`):
```bash
npm ci
npm run lint && npm run typecheck && npm run test && npm run build
```
Expected: todos exit 0. (El verde en GitHub Actions se confirma al hacer push cuando haya remoto.)

- [ ] **Step 3: Commit**

```bash
cd ..
git add .github/workflows/ci.yml
git commit -m "ci(web): job de frontend (install, lint, typecheck, test, build)"
```

---

### Task 9: Convenciones del frontend en CLAUDE.md

**Files:**
- Modify: `CLAUDE.md` (raíz)

**Interfaces:**
- Produces: sección que documenta el stack y convenciones del frontend para desarrollo asistido por IA.

- [ ] **Step 1: Añadir sección**

Añade a `CLAUDE.md` una sección `## Frontend web (web/)`:
```markdown
## Frontend web (web/)

SPA React + TypeScript (Vite) como PWA instalable; se envuelve con Capacitor para
tiendas. Consume la Web API de `CaseritoApp/` (host .NET). Vive en `web/`.

- UI: **MUI** (Material UI) + Emotion; tema en `src/theme/`.
- Navegación: **React Router** (`src/app/router.tsx`).
- Estado de servidor/API: **TanStack Query**; la capa de API vive en `src/api/`
  (hoy cliente a mano de `/health`; en Fase 1 se genera un cliente tipado del OpenAPI).
- Calidad: TypeScript estricto, ESLint + Prettier, Vitest + React Testing Library.
- Textos de UI y comentarios en **español**.
- Nunca exponer PII en logs del cliente (mismo criterio que el backend).

Comandos (desde `web/`): `npm run dev` | `build` | `lint` | `typecheck` | `test`.
Dev: Vite proxya `/health` y `/api` al host .NET (ver `vite.config.ts`).
```

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: convenciones del frontend web en CLAUDE.md"
```

---

## Verificación end-to-end (al terminar)

Desde `web/`:
1. `npm ci` sin errores.
2. `npm run lint` y `npm run typecheck` limpios.
3. `npm run test` verde (smoke runner + HomePage + getHealth).
4. `npm run build` genera `dist/` con `manifest.webmanifest` y service worker.
5. `npm run dev` levanta la app; con el host .NET corriendo (`dotnet run --project CaseritoApp/src/Host/CaseritoApp.Host`), HomePage muestra "Backend: ok" (vía proxy a `/health`).
6. `.github/workflows/ci.yml` tiene job `frontend` con install/lint/typecheck/test/build.
7. `CLAUDE.md` documenta el stack del frontend.
8. `web/node_modules` y `web/dist` están git-ignored (no aparecen en `git status`).
