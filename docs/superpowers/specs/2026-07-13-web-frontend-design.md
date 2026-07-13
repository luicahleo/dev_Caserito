# Diseño: Frontend web de CaseritoApp (PWA con React)

- **Fecha**: 2026-07-13
- **Estado**: Aprobado (brainstorming)
- **Alcance**: Decidir y justificar el stack del **cliente web** y dejarlo listo para implementar. NO implementa features de producto (eso va por fase). La app móvil nativa separada queda descartada: el cliente web doblará como app móvil vía PWA + Capacitor.

## Contexto

CaseritoApp (marketplace C2C, backend .NET 10 / monolito modular ya con host Web API y `/health`). Decisión de alcance: **se deja de lado la app móvil nativa**; el foco es la **web**, y esa misma web debe servir como app móvil instalable y distribuible en tiendas, para no mantener una segunda base de código.

Restricciones/objetivos expresados por el equipo:
- Una sola app web que **doble como app móvil** (sin escribir otra app).
- **Poder distribuir en Play Store / App Store** a futuro.
- **SEO no es requisito**.
- No hay problema en usar un stack JS/TS junto al backend .NET.
- Preocupa la **adopción de mercado** del framework (por talento y material de referencia/IA).

## Decisiones tomadas

| Tema | Decisión | Motivo |
|---|---|---|
| Tipo de app | **SPA como PWA instalable** | Una sola base sirve web + móvil instalable; SEO no importa, así que SPA puro (sin SSR/Next.js) |
| Framework | **React + TypeScript** | Máxima adopción de mercado, ecosistema y material de IA; disipa la duda de talento |
| Bundler/tooling | **Vite** | Estándar moderno, dev rápido, buen soporte PWA |
| PWA | **vite-plugin-pwa** (service worker + manifest) | App instalable, offline básico, push a futuro |
| Distribución a tiendas | **Capacitor** (envuelve la misma PWA → APK/IPA) | Play/App Store sin reescribir ni mantener otra app |
| UI | **MUI (Material UI)** | Catálogo maduro con look tipo app móvil, mucho material/IA, rápido para construir |
| Navegación | **React Router** | Estándar de routing SPA |
| Estado de servidor / API | **TanStack Query** + cliente TS **tipado generado del OpenAPI** de la Web API | Cache/estado de servidor robusto; contrato tipado end-to-end con el backend |
| Ubicación | **Monorepo**: carpeta `web/` en la raíz del repo, separada de la solución .NET | Un repo, contexto compartido, simple para equipo pequeño |
| Auth | JWT emitido por el backend (Identity, Fase 1); el frontend lo almacena y adjunta | Alinea con el RBAC/Identity planificado |

## Estructura en el repo

```
dev_Caserito/                    ← raíz git
├─ CaseritoApp/                  ← backend .NET (sin cambios)
├─ web/                          ← frontend React (NUEVO)
│  ├─ src/
│  │  ├─ main.tsx, App.tsx
│  │  ├─ routes/                 (páginas por ruta)
│  │  ├─ api/                    (cliente tipado generado del OpenAPI + hooks TanStack Query)
│  │  ├─ components/             (componentes compartidos)
│  │  └─ theme/                  (tema MUI)
│  ├─ public/                    (iconos PWA, manifest assets)
│  ├─ index.html
│  ├─ vite.config.ts             (incluye vite-plugin-pwa)
│  ├─ tsconfig.json
│  ├─ package.json
│  └─ capacitor.config.ts        (config de Capacitor; plataformas android/ios se agregan luego)
├─ .github/workflows/            (se extiende para build/lint/test del frontend)
└─ .gitignore                    (se extiende: node_modules, dist, .capacitor, etc.)
```

## Integración con el backend

- El frontend consume la Web API del host `CaseritoApp.Host`. En dev, Vite proxya al host .NET (las rutas de negocio vivirán bajo `/api`; el health actual del host está en `/health`).
- El **contrato es el OpenAPI/Swagger** que expone el host; de ahí se genera un cliente TypeScript tipado (p. ej. con `openapi-typescript`/`orval`), de modo que un cambio incompatible en la API rompe el build del frontend.
- CORS configurado en el host para el origen del frontend en dev.
- Auth: el backend (Identity, Fase 1) emite JWT; el frontend lo guarda y lo adjunta en las peticiones; las rutas protegidas se guardan en el router.

## Calidad y homogeneidad (paralelo al andamiaje .NET)

- **TypeScript estricto**, **ESLint + Prettier** con reglas alineadas, aplicados en CI (mirror del rigor "día 1" del backend).
- **Vitest + React Testing Library** para tests de componentes/hooks.
- El **CI** de GitHub Actions se extiende con un job de frontend: install → lint → typecheck → test → build.
- Se añadirán a `CLAUDE.md` las convenciones del frontend (estructura, naming, dónde va el cliente de API, política de no exponer PII en logs del cliente).

## Fuera de alcance de este diseño (a fases/ciclos posteriores)

- Implementación de features de producto (listados, chat, etc.) — por fase del plan MVP.
- Wiring real de auth/JWT (depende de Identity, Fase 1).
- Alta/publicación efectiva en Play/App Store (solo se deja Capacitor configurado y la PWA lista para envolver).
- Diseño visual/branding definitivo (se parte del tema MUI por defecto).
- Notificaciones push (requiere backend de Notifications, Fase 6).

## Verificación (del andamiaje del frontend, cuando se implemente)

1. `npm install` en `web/` sin errores.
2. `npm run lint` y `npm run typecheck` limpios.
3. `npm run test` (Vitest) verde.
4. `npm run build` genera el bundle de producción y el service worker (PWA).
5. `npm run dev` levanta la app; una página smoke consume el `GET /health` del backend (vía el proxy de Vite) y muestra el estado.
6. La app es instalable como PWA (manifest + service worker válidos, auditable con Lighthouse).
7. El job de frontend del CI pasa en verde.
