# Diseño: Frontend auth (Fase 1 — Bloque B2)

- **Fecha**: 2026-07-14
- **Estado**: Aprobado (brainstorming)
- **Alcance**: Frontend de autenticación en `web/`: manejo de sesión (access token en memoria + refresh vía cookie httpOnly), pantallas Registro/Login/Perfil, rutas protegidas, y el cableado del cliente API (interceptor refresh-on-401 + bootstrap al cargar). Consume la API del Bloque B1. NO incluye RBAC/KYC ni features de producto.

## Contexto

B1 dejó el backend de auth: `POST /api/auth/{register,login,refresh,logout}` (login/refresh devuelven `{ accessToken }` y setean/rotan la cookie httpOnly `refreshToken`), y `GET/PUT /api/perfil` (`[Authorize]`). El frontend `web/` ya tiene React+TS+Vite, MUI+tema, React Router, TanStack Query, y `api/client.ts` (`getJson`) + `useHealth`. Decisión de sesión (B1): **access token en memoria + refresh en cookie httpOnly SameSite=Strict**; la SPA llama por su propio origen (proxy de Vite en dev, NGINX en prod) → la cookie fluye same-site.

## Decisiones tomadas

| Tema | Decisión |
|---|---|
| Estado de sesión | **React Context** (`AuthContext`) para la UI + **holder en módulo** (`session.ts`) para el access token (accesible por el cliente fetch no-React) |
| Formularios | **React Hook Form + Zod** (`@hookform/resolvers`) |
| Access token | En memoria (variable de módulo), **no persistido** |
| Refresh | Cookie httpOnly gestionada por el navegador; el cliente envía `credentials: 'include'` |
| Interceptor | En `api/client.ts`: adjunta `Authorization: Bearer`, y ante **401 intenta `refresh` una vez y reintenta**; si el refresh falla, limpia la sesión |
| Bootstrap | Al montar la app, un intento de `refresh` restaura la sesión sin re-login |

## Componentes

- **`src/auth/session.ts`**: holder del access token en memoria — `getAccessToken()`, `setAccessToken(t)`, `clearAccessToken()`. Sin persistencia (no localStorage).
- **`src/auth/AuthContext.tsx`**: `AuthProvider` + `useAuth()`. Estado: `usuario` (id/email/nombre/ciudad o null), `estaAutenticado`, `cargando` (durante el bootstrap). Métodos: `iniciarSesion(email, password)`, `registrar(...)`, `cerrarSesion()`. En `useEffect` de montaje: intenta `refrescar()`; si ok, carga el perfil y marca autenticado; al terminar, `cargando=false`.
- **`src/api/client.ts`** (extendido): `getJson`/`postJson` con `credentials: 'include'` y `Authorization: Bearer` desde `session.ts`. Lógica 401: si una respuesta es 401 y no es la propia llamada de refresh, intenta `POST /api/auth/refresh` una vez, actualiza el access token y reintenta la petición original; si el refresh falla, `clearAccessToken()` + propaga el error (la UI redirige a login). Evita recursión en el endpoint de refresh.
- **`src/api/auth.ts`**: `registrar({email,password,nombre,ciudad})`, `iniciarSesion({email,password}) → { accessToken }`, `refrescar() → { accessToken }`, `cerrarSesion()`.
- **`src/api/perfil.ts`**: `obtenerPerfil() → Perfil`, `actualizarPerfil({nombre,ciudad})`.
- **Pantallas (MUI + RHF + Zod)**: `routes/RegistroPage.tsx`, `routes/LoginPage.tsx`, `routes/PerfilPage.tsx` (ver + editar). Esquemas Zod para validación de cliente (email válido, password mínimo, nombre no vacío), con mensajes en español.
- **`src/auth/ProtectedRoute.tsx`**: envuelve rutas privadas; si `cargando` muestra spinner; si no autenticado redirige a `/login`.
- **Router** (`app/router.tsx`): `/registro`, `/login` (públicas), `/perfil` (protegida). `AuthProvider` envuelve el árbol (en `providers.tsx`).

## Flujo de datos

1. **Login**: `LoginPage` → `iniciarSesion` → `POST /api/auth/login` (cookie de refresh seteada por el server) → `setAccessToken` + cargar perfil → redirige a `/perfil`.
2. **Petición autenticada**: `postJson`/`getJson` adjunta el bearer; si 401 → refresh-once → retry.
3. **Recarga de página**: el access token en memoria se pierde; el bootstrap del `AuthProvider` llama `refrescar()` (cookie httpOnly) → restaura sesión.
4. **Logout**: `cerrarSesion` → `POST /api/auth/logout` (revoca + borra cookie) → `clearAccessToken` → redirige a `/login`.

## Calidad y seguridad

- TypeScript estricto, ESLint + Prettier limpios; textos de UI y comentarios en español.
- **Nunca** persistir el access token (solo memoria) ni loguear tokens/PII en el cliente.
- El refresh token nunca se maneja en JS (cookie httpOnly); el cliente solo usa `credentials: 'include'`.
- Deps nuevas: `react-hook-form`, `zod`, `@hookform/resolvers`.

## Tests (Vitest + RTL)

- Cliente: ante 401, hace refresh una vez y reintenta; si el refresh también falla, limpia sesión y no reintenta en bucle (mock de `fetch`).
- `ProtectedRoute`: redirige a `/login` sin sesión; renderiza hijo si autenticado.
- `LoginPage`: envía credenciales y, con login ok (mock), marca sesión; muestra error con credenciales inválidas.
- (Opcional) `PerfilPage`: carga y edita el perfil (mock).

## Fuera de alcance

RBAC/roles en UI, KYC (subida de documentos), features de producto (catálogo, etc.), branding definitivo, recuperación de contraseña/verificación de email (backend no los expone aún), y el empaquetado Capacitor (la PWA ya está lista; se envuelve cuando se prepare la publicación).

## Verificación

1. `npm run lint`, `npm run typecheck`, `npm run test` verdes en `web/`.
2. `npm run build` genera el bundle (PWA intacta).
3. Con el stack dev (`rebuild.ps1`) o el host + `npm run dev`: registrar un usuario, iniciar sesión, ver/editar el perfil, recargar la página (la sesión persiste vía refresh), y cerrar sesión (redirige a login; el perfil deja de ser accesible).
