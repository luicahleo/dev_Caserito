# Frontend auth (Fase 1 — Bloque B2) — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** UI y sesión de auth en `web/`: registro/login/perfil/logout, con access token en memoria + refresh vía cookie httpOnly, interceptor refresh-on-401 y bootstrap al cargar.

**Architecture:** React+TS+Vite+MUI existente. Access token en un holder de módulo (leído por el cliente fetch); `AuthContext` para el estado de UI. El cliente `api/client.ts` adjunta el bearer y ante 401 intenta `refresh` una vez y reintenta. La SPA llama por su propio origen (proxy Vite/NGINX) con `credentials: 'include'` para la cookie httpOnly. Formularios con React Hook Form + Zod.

**Tech Stack:** React 19, TypeScript, MUI, React Router, TanStack Query, react-hook-form, zod, @hookform/resolvers, Vitest + RTL.

## Global Constraints

- Todo en `web/`. TypeScript estricto; ESLint + Prettier limpios (el CI y `format:check` los aplican).
- Textos de UI y comentarios en **español**.
- **Nunca persistir el access token** (solo memoria; no localStorage) ni loguear tokens/PII en el cliente.
- El refresh token nunca se toca en JS (cookie httpOnly); el cliente solo usa `credentials: 'include'`.
- Rutas de API **relativas** (`/api/...`) para pasar por el proxy same-origin.
- No commitear `node_modules`/`dist`. Cada paso: `npm run lint && npm run typecheck && npm run test` verdes.

## Fuera de alcance

RBAC en UI, KYC, features de producto, recuperación de contraseña/verificación de email, branding, Capacitor.

---

### Task 1: Dependencias + holder de sesión

**Files:**
- Modify: `web/package.json` (deps)
- Create: `web/src/auth/session.ts`
- Test: `web/src/auth/session.test.ts`

**Interfaces:**
- Produces: `getAccessToken(): string | null`, `setAccessToken(t: string | null): void`, `clearAccessToken(): void` (holder en memoria, sin persistencia).

- [ ] **Step 1: Instalar deps**

Run (desde `web/`): `npm install react-hook-form zod @hookform/resolvers`

- [ ] **Step 2: Test del holder**

`web/src/auth/session.test.ts`:
```ts
import { describe, it, expect, afterEach } from 'vitest';
import { getAccessToken, setAccessToken, clearAccessToken } from './session';

afterEach(() => clearAccessToken());

describe('session', () => {
  it('guarda y devuelve el token en memoria', () => {
    expect(getAccessToken()).toBeNull();
    setAccessToken('abc');
    expect(getAccessToken()).toBe('abc');
  });
  it('clear lo borra', () => {
    setAccessToken('abc');
    clearAccessToken();
    expect(getAccessToken()).toBeNull();
  });
});
```

- [ ] **Step 3: Implementar `session.ts`**

```ts
// Holder en memoria del access token. NO se persiste (ni localStorage): al
// recargar, la sesión se restaura vía refresh (cookie httpOnly).
let accessToken: string | null = null;

export function getAccessToken(): string | null {
  return accessToken;
}

export function setAccessToken(token: string | null): void {
  accessToken = token;
}

export function clearAccessToken(): void {
  accessToken = null;
}
```

- [ ] **Step 4: Verificar** — `npm run test && npm run typecheck && npm run lint` verdes.

- [ ] **Step 5: Commit**

```bash
cd ..
git add web/package.json web/package-lock.json web/src/auth
git commit -m "feat(web): deps de auth (RHF/zod) + holder de sesión en memoria"
```

---

### Task 2: Cliente API con bearer + refresh-on-401 (TDD)

**Files:**
- Modify: `web/src/api/client.ts`
- Test: `web/src/api/client.test.ts`

**Interfaces:**
- Consumes: `session.ts`.
- Produces: `getJson<T>(ruta)`, `postJson<T>(ruta, cuerpo?)`, `putJson<T>(ruta, cuerpo?)` — envían `credentials: 'include'` + `Authorization: Bearer` (si hay token); ante 401 (excepto en `/api/auth/*`) intentan `POST /api/auth/refresh` una vez y reintentan; si el refresh falla, `clearAccessToken()`.

- [ ] **Step 1: Test del refresh-on-401 (RED)**

`web/src/api/client.test.ts`:
```ts
import { describe, it, expect, vi, afterEach } from 'vitest';
import { getJson } from './client';
import { setAccessToken, getAccessToken, clearAccessToken } from '../auth/session';

afterEach(() => { vi.restoreAllMocks(); clearAccessToken(); });

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? '' : JSON.stringify(cuerpo), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('client refresh-on-401', () => {
  it('ante 401 refresca una vez y reintenta con el nuevo token', async () => {
    setAccessToken('viejo');
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(respuesta(401)) // GET original
      .mockResolvedValueOnce(respuesta(200, { accessToken: 'nuevo' })) // refresh
      .mockResolvedValueOnce(respuesta(200, { ok: true })); // reintento

    const data = await getJson<{ ok: boolean }>('/api/perfil');
    expect(data.ok).toBe(true);
    expect(getAccessToken()).toBe('nuevo');
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it('si el refresh falla, limpia la sesión y propaga el error', async () => {
    setAccessToken('viejo');
    vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(respuesta(401))
      .mockResolvedValueOnce(respuesta(401)); // refresh falla
    await expect(getJson('/api/perfil')).rejects.toThrow();
    expect(getAccessToken()).toBeNull();
  });
});
```

- [ ] **Step 2: Verificar RED** — `npm run test` → falla (client no exporta la lógica nueva / getJson no refresca).

- [ ] **Step 3: Reescribir `client.ts`**

```ts
import { clearAccessToken, getAccessToken, setAccessToken } from '../auth/session';

async function refrescarToken(): Promise<boolean> {
  const r = await fetch('/api/auth/refresh', { method: 'POST', credentials: 'include' });
  if (!r.ok) return false;
  const data = (await r.json()) as { accessToken: string };
  setAccessToken(data.accessToken);
  return true;
}

async function ejecutar(ruta: string, init: RequestInit, reintentar = true): Promise<Response> {
  const headers = new Headers(init.headers);
  const token = getAccessToken();
  if (token) headers.set('Authorization', `Bearer ${token}`);

  const respuesta = await fetch(ruta, { ...init, headers, credentials: 'include' });

  const esRutaAuth = ruta.startsWith('/api/auth/');
  if (respuesta.status === 401 && reintentar && !esRutaAuth) {
    if (await refrescarToken()) {
      return ejecutar(ruta, init, false);
    }
    clearAccessToken();
  }
  return respuesta;
}

async function leer<T>(respuesta: Response, ruta: string): Promise<T> {
  if (!respuesta.ok) {
    throw new Error(`Petición fallida (${respuesta.status}) a ${ruta}`);
  }
  const texto = await respuesta.text();
  return (texto ? (JSON.parse(texto) as T) : (undefined as T));
}

export async function getJson<T>(ruta: string): Promise<T> {
  const r = await ejecutar(ruta, { method: 'GET', headers: { Accept: 'application/json' } });
  return leer<T>(r, ruta);
}

export async function postJson<T>(ruta: string, cuerpo?: unknown): Promise<T> {
  const r = await ejecutar(ruta, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
  });
  return leer<T>(r, ruta);
}

export async function putJson<T>(ruta: string, cuerpo?: unknown): Promise<T> {
  const r = await ejecutar(ruta, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
  });
  return leer<T>(r, ruta);
}
```
> Nota: `api/health.ts` sigue usando `getJson('/health')` sin cambios (health nunca da 401, y `credentials/bearer` extra son inocuos). Verifica que `useHealth`/HomePage siguen compilando.

- [ ] **Step 4: Verificar GREEN** — `npm run test && npm run build && npm run lint && npm run typecheck` verdes.

- [ ] **Step 5: Commit**

```bash
cd ..
git add web/src/api/client.ts web/src/api/client.test.ts
git commit -m "feat(web): cliente API con bearer + refresh-on-401"
```

---

### Task 3: Capa tipada auth + perfil

**Files:**
- Create: `web/src/api/auth.ts`, `web/src/api/perfil.ts`

**Interfaces:**
- Produces:
  - `auth`: `registrar(datos)`, `iniciarSesion(cred)` (setea el access token), `refrescar(): Promise<boolean>`, `cerrarSesion()` (limpia el token).
  - `perfil`: `obtenerPerfil(): Promise<Perfil>`, `actualizarPerfil(datos): Promise<void>`; `Perfil = { id, email, nombre, ciudad }`.

- [ ] **Step 1: `api/auth.ts`**

```ts
import { clearAccessToken, setAccessToken } from '../auth/session';
import { postJson } from './client';

export interface RegistroDatos {
  email: string;
  password: string;
  nombre: string;
  ciudad: string;
}
export interface Credenciales {
  email: string;
  password: string;
}

export async function registrar(datos: RegistroDatos): Promise<void> {
  await postJson('/api/auth/register', datos);
}

export async function iniciarSesion(cred: Credenciales): Promise<void> {
  const { accessToken } = await postJson<{ accessToken: string }>('/api/auth/login', cred);
  setAccessToken(accessToken);
}

export async function refrescar(): Promise<boolean> {
  try {
    const { accessToken } = await postJson<{ accessToken: string }>('/api/auth/refresh');
    setAccessToken(accessToken);
    return true;
  } catch {
    return false;
  }
}

export async function cerrarSesion(): Promise<void> {
  try {
    await postJson('/api/auth/logout');
  } finally {
    clearAccessToken();
  }
}
```

- [ ] **Step 2: `api/perfil.ts`**

```ts
import { getJson, putJson } from './client';

export interface Perfil {
  id: string;
  email: string;
  nombre: string;
  ciudad: string;
}

export function obtenerPerfil(): Promise<Perfil> {
  return getJson<Perfil>('/api/perfil');
}

export function actualizarPerfil(datos: { nombre: string; ciudad: string }): Promise<void> {
  return putJson('/api/perfil', datos);
}
```

- [ ] **Step 3: Verificar** — `npm run build && npm run lint && npm run typecheck` verdes.

- [ ] **Step 4: Commit**

```bash
cd ..
git add web/src/api/auth.ts web/src/api/perfil.ts
git commit -m "feat(web): capa tipada de auth y perfil"
```

---

### Task 4: AuthContext + bootstrap

**Files:**
- Create: `web/src/auth/AuthContext.tsx`
- Modify: `web/src/app/providers.tsx`

**Interfaces:**
- Consumes: `api/auth.ts`, `api/perfil.ts`.
- Produces: `AuthProvider` (envuelve la app) y `useAuth(): { usuario: Perfil | null, estaAutenticado: boolean, cargando: boolean, iniciarSesion(cred), registrar(datos), cerrarSesion() }`. En el montaje intenta `refrescar()`; si ok carga el perfil; `cargando` cubre ese arranque.

- [ ] **Step 1: `AuthContext.tsx`**

```tsx
import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import * as auth from '../api/auth';
import { obtenerPerfil, type Perfil } from '../api/perfil';

interface EstadoAuth {
  usuario: Perfil | null;
  estaAutenticado: boolean;
  cargando: boolean;
  iniciarSesion: (cred: auth.Credenciales) => Promise<void>;
  registrar: (datos: auth.RegistroDatos) => Promise<void>;
  cerrarSesion: () => Promise<void>;
}

const AuthContext = createContext<EstadoAuth | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<Perfil | null>(null);
  const [cargando, setCargando] = useState(true);

  useEffect(() => {
    let activo = true;
    (async () => {
      if (await auth.refrescar()) {
        try {
          const p = await obtenerPerfil();
          if (activo) setUsuario(p);
        } catch {
          if (activo) setUsuario(null);
        }
      }
      if (activo) setCargando(false);
    })();
    return () => {
      activo = false;
    };
  }, []);

  const iniciarSesion = useCallback(async (cred: auth.Credenciales) => {
    await auth.iniciarSesion(cred);
    setUsuario(await obtenerPerfil());
  }, []);

  const registrar = useCallback(async (datos: auth.RegistroDatos) => {
    await auth.registrar(datos);
    await auth.iniciarSesion({ email: datos.email, password: datos.password });
    setUsuario(await obtenerPerfil());
  }, []);

  const cerrarSesion = useCallback(async () => {
    await auth.cerrarSesion();
    setUsuario(null);
  }, []);

  return (
    <AuthContext.Provider
      value={{ usuario, estaAutenticado: usuario !== null, cargando, iniciarSesion, registrar, cerrarSesion }}
    >
      {children}
    </AuthContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): EstadoAuth {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth debe usarse dentro de AuthProvider');
  return ctx;
}
```

- [ ] **Step 2: Envolver en `providers.tsx`**

Modifica `web/src/app/providers.tsx` para envolver los hijos con `<AuthProvider>` dentro del `QueryClientProvider`:
```tsx
import { type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from '../auth/AuthContext';

const queryClient = new QueryClient();

export function AppProviders({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>{children}</AuthProvider>
    </QueryClientProvider>
  );
}
```

- [ ] **Step 3: Verificar** — `npm run build && npm run lint && npm run typecheck` verdes.

- [ ] **Step 4: Commit**

```bash
cd ..
git add web/src/auth/AuthContext.tsx web/src/app/providers.tsx
git commit -m "feat(web): AuthContext con bootstrap por refresh"
```

---

### Task 5: ProtectedRoute + rutas (TDD)

**Files:**
- Create: `web/src/auth/ProtectedRoute.tsx`
- Test: `web/src/auth/ProtectedRoute.test.tsx`
- Modify: `web/src/app/router.tsx`

**Interfaces:**
- Consumes: `useAuth`.
- Produces: `ProtectedRoute` que muestra spinner si `cargando`, redirige a `/login` si no autenticado, o renderiza `children`.

- [ ] **Step 1: Test (RED)**

`web/src/auth/ProtectedRoute.test.tsx`:
```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { ProtectedRoute } from './ProtectedRoute';
import * as ctx from './AuthContext';

function montar(estado: Partial<ReturnType<typeof ctx.useAuth>>) {
  vi.spyOn(ctx, 'useAuth').mockReturnValue({
    usuario: null, estaAutenticado: false, cargando: false,
    iniciarSesion: vi.fn(), registrar: vi.fn(), cerrarSesion: vi.fn(),
    ...estado,
  } as ReturnType<typeof ctx.useAuth>);
  return render(
    <MemoryRouter initialEntries={['/perfil']}>
      <Routes>
        <Route path="/login" element={<div>pantalla login</div>} />
        <Route path="/perfil" element={<ProtectedRoute><div>perfil privado</div></ProtectedRoute>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('ProtectedRoute', () => {
  it('redirige a login si no autenticado', () => {
    montar({ estaAutenticado: false });
    expect(screen.getByText('pantalla login')).toBeInTheDocument();
  });
  it('renderiza el hijo si autenticado', () => {
    montar({ estaAutenticado: true });
    expect(screen.getByText('perfil privado')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Verificar RED** — `npm run test` → falla (ProtectedRoute no existe).

- [ ] **Step 3: Implementar**

`web/src/auth/ProtectedRoute.tsx`:
```tsx
import { type ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { Box, CircularProgress } from '@mui/material';
import { useAuth } from './AuthContext';

export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { estaAutenticado, cargando } = useAuth();
  if (cargando) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }
  if (!estaAutenticado) {
    return <Navigate to="/login" replace />;
  }
  return <>{children}</>;
}
```

- [ ] **Step 4: Rutas** — en `web/src/app/router.tsx`, añade `/registro`→`RegistroPage`, `/login`→`LoginPage`, y `/perfil` envuelto en `ProtectedRoute`→`PerfilPage`; cambia `/` para redirigir a `/perfil`. (Las páginas se crean en la Task 6; para que compile ahora, crea stubs mínimos o define las rutas en la Task 6.) Recomendado: define las rutas en la Task 6 junto con las páginas, y en esta task deja el router sin tocar salvo que crees stubs. Si dejas el router para la Task 6, el test de ProtectedRoute usa su propio `MemoryRouter` y no depende del router de la app.

- [ ] **Step 5: Verificar GREEN** — `npm run test && npm run typecheck && npm run lint` verdes.

- [ ] **Step 6: Commit**

```bash
cd ..
git add web/src/auth/ProtectedRoute.tsx web/src/auth/ProtectedRoute.test.tsx
git commit -m "feat(web): ProtectedRoute (spinner/redirect/render)"
```

---

### Task 6: Pantallas Login/Registro/Perfil + rutas

**Files:**
- Create: `web/src/routes/LoginPage.tsx`, `web/src/routes/RegistroPage.tsx`, `web/src/routes/PerfilPage.tsx`
- Modify: `web/src/app/router.tsx`
- Test: `web/src/routes/LoginPage.test.tsx`

**Interfaces:**
- Consumes: `useAuth`, MUI, react-hook-form, zod, `@hookform/resolvers/zod`, react-router.
- Produces: pantallas con formularios validados (Zod, mensajes en español); rutas cableadas.

- [ ] **Step 1: LoginPage (MUI + RHF + Zod)**

`web/src/routes/LoginPage.tsx`:
```tsx
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useNavigate, Link as RouterLink } from 'react-router-dom';
import { Button, Container, Stack, TextField, Typography, Alert, Link } from '@mui/material';
import { useAuth } from '../auth/AuthContext';

const esquema = z.object({
  email: z.string().email('Email inválido'),
  password: z.string().min(1, 'La contraseña es obligatoria'),
});
type Datos = z.infer<typeof esquema>;

export function LoginPage() {
  const { iniciarSesion } = useAuth();
  const navigate = useNavigate();
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null);
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<Datos>({
    resolver: zodResolver(esquema),
  });

  const onSubmit = async (datos: Datos) => {
    setErrorGeneral(null);
    try {
      await iniciarSesion(datos);
      navigate('/perfil');
    } catch {
      setErrorGeneral('Credenciales inválidas');
    }
  };

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>Iniciar sesión</Typography>
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <Stack spacing={2}>
          {errorGeneral && <Alert severity="error">{errorGeneral}</Alert>}
          <TextField label="Email" type="email" {...register('email')}
            error={!!errors.email} helperText={errors.email?.message} />
          <TextField label="Contraseña" type="password" {...register('password')}
            error={!!errors.password} helperText={errors.password?.message} />
          <Button type="submit" variant="contained" disabled={isSubmitting}>Entrar</Button>
          <Link component={RouterLink} to="/registro">¿No tienes cuenta? Regístrate</Link>
        </Stack>
      </form>
    </Container>
  );
}
```

- [ ] **Step 2: RegistroPage** — análoga a LoginPage, esquema Zod `{ email (email), password (min 8, 'Mínimo 8 caracteres'), nombre (min 1), ciudad (min 1) }`, campos MUI para email/password/nombre/ciudad, llama `registrar(datos)` y navega a `/perfil`; enlace a `/login`. Muestra `Alert` de error si el registro falla (p. ej. email duplicado → "No se pudo registrar").

- [ ] **Step 3: PerfilPage** — usa `useAuth()`; si `usuario` es null muestra spinner; muestra email (solo lectura) y un formulario RHF+Zod con `nombre`/`ciudad` prellenados desde `usuario`, botón "Guardar" que llama `actualizarPerfil` (de `api/perfil.ts`) y refresca el perfil mostrado (`obtenerPerfil` o actualiza el contexto — para simplicidad, re-fetch local y `setUsuario` no está expuesto; usa un estado local `perfil` inicializado con `usuario` y actualízalo tras guardar). Botón "Cerrar sesión" que llama `cerrarSesion()` y navega a `/login`. Muestra un `Alert`/snackbar "Perfil actualizado" al guardar.

- [ ] **Step 4: Rutas** — en `web/src/app/router.tsx`:
```tsx
import { createBrowserRouter, Navigate } from 'react-router-dom';
import { LoginPage } from '../routes/LoginPage';
import { RegistroPage } from '../routes/RegistroPage';
import { PerfilPage } from '../routes/PerfilPage';
import { NotFoundPage } from '../routes/NotFoundPage';
import { ProtectedRoute } from '../auth/ProtectedRoute';

export const router = createBrowserRouter([
  { path: '/', element: <Navigate to="/perfil" replace /> },
  { path: '/login', element: <LoginPage /> },
  { path: '/registro', element: <RegistroPage /> },
  { path: '/perfil', element: <ProtectedRoute><PerfilPage /></ProtectedRoute> },
  { path: '*', element: <NotFoundPage /> },
]);
```

- [ ] **Step 5: Test de LoginPage**

`web/src/routes/LoginPage.test.tsx`: renderiza `LoginPage` envuelto en `MemoryRouter`, con `useAuth` mockeado (`iniciarSesion` mock). Verifica: al enviar email+password válidos llama a `iniciarSesion` con esos datos; si `iniciarSesion` rechaza, muestra "Credenciales inválidas"; email inválido muestra el error de Zod y no llama a `iniciarSesion`. Usa `@testing-library/user-event`.

- [ ] **Step 6: Verificar** — `npm run test && npm run build && npm run lint && npm run typecheck` verdes. (El build genera la PWA intacta.)

- [ ] **Step 7: Commit**

```bash
cd ..
git add web/src/routes web/src/app/router.tsx
git commit -m "feat(web): pantallas login/registro/perfil + rutas protegidas"
```

---

## Verificación end-to-end (al terminar)

Desde `web/`:
1. `npm run lint`, `npm run typecheck`, `npm run test` verdes (holder, refresh-on-401, ProtectedRoute, LoginPage).
2. `npm run build` genera el bundle (PWA intacta: manifest + sw).
3. Con el stack dev (`rebuild.ps1`) o host + `npm run dev`: registrar un usuario → sesión iniciada y en `/perfil`; ver/editar perfil (persiste); **recargar** (sesión se mantiene vía refresh con la cookie httpOnly); cerrar sesión → redirige a `/login`, `/perfil` deja de ser accesible; credenciales inválidas muestran error.
4. Confirmar que el access token no aparece en localStorage y que no se loguean tokens/PII en consola.
