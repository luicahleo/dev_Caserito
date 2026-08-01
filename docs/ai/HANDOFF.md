# Handoff de sesión

## Objetivo

Cerrar la autenticación web con Google y Facebook para la PWA, conservando correo/contraseña y la sesión JWT/refresh propia de Caserito.

## Rama y estado de Git

- Rama: `feat/auth-google-facebook`.
- Los bloques 1–3, tareas 1–8, están implementados y versionados.
- El árbol estaba limpio antes de reemplazar este handoff.
- No se hizo push ni merge.

## Spec y plan activos

- `docs/superpowers/specs/2026-08-01-auth-google-facebook-design.md`
- `docs/superpowers/plans/2026-08-01-auth-google-facebook.md`

## Decisiones vigentes

- Se mantienen las decisiones del spec; no surgieron contradicciones ni ampliaciones.
- La UI conserva el ticket externo en cookie durante la verificación de una cuenta existente; después del login local ejecuta `link`, restaura sesión/perfil/claims y navega solo a un retorno local validado.
- El retorno del onboarding se transporta como ruta local codificada; no contiene tokens ni PII.
- Los endpoints de navegación `start` y `callback` están excluidos de OpenAPI. Capacidades, pendiente, completar y vincular forman el contrato JSON tipado.
- No se usaron credenciales reales ni se llamó a Google o Meta.

## Completado

### Tarea 7 — UI de login, retorno y onboarding

- Login consulta capacidades y muestra Facebook, Google, separador y formulario tradicional en el orden previsto; si falla, mantiene el formulario.
- Los enlaces externos hacen navegación completa e incluyen solo `returnUrl` local normalizado.
- Los códigos opacos del callback producen mensajes españoles genéricos.
- `/auth/external/completado` restaura refresh, perfil y claims antes de navegar.
- `/auth/external/onboarding` solicita solo los campos indicados por `pending`, impide doble envío y muestra errores genéricos.
- La vinculación ofrece login y recuperación; tras autenticar al dueño ejecuta `link` y restaura la sesión.
- El backend conserva el retorno local al redirigir hacia onboarding.

### Tarea 8 — OpenAPI, cliente y operación

- Los endpoints JSON externos tienen nombres de operación, `Accepts` y `Produces` explícitos.
- `start` y `callback` no se generan como llamadas del cliente.
- Se regeneraron `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json` y `web/src/api/schema.d.ts`.
- `web/src/api/auth.ts` consume los tipos y paths generados, sin `any` ni `fetch` paralelo.
- `.env.example` y `docker-compose.dev.yml` contienen solo plantillas vacías para la configuración OAuth.
- `docs/ai/AUTH_PROVEEDORES.md` documenta callbacks, alta en Google/Meta, secretos, privacidad, eliminación de datos, despliegue y prueba manual.

## Verificaciones ejecutadas

- TDD tarea 7: rojo observado por páginas y operaciones ausentes; después 11/11 pruebas dirigidas verdes.
- `npm run test -- --run src/routes/LoginPage.test.tsx src/routes/AuthExternaCallbackPage.test.tsx src/routes/CompletarRegistroExternoPage.test.tsx`: 11/11 verdes.
- `npm run typecheck`: correcto.
- `npm run lint`: correcto.
- `npm run build`: correcto; Vite solo informó el aviso no bloqueante de chunk mayor de 500 kB.
- Generación OpenAPI: `ASPNETCORE_ENVIRONMENT=Testing` + build opt-in del Host, correcto con 0 advertencias y 0 errores.
- `npm run generate:api`: ejecutado dos veces; generación reproducible.
- `git diff --check`: limpio antes del commit de tarea 8.
- Hooks de formato superados en ambos commits.
- No se ejecutó la suite completa frontend/backend ni la regresión completa de autenticación; corresponde a la tarea 9.

## Fallos o bloqueos

- Ninguno.
- La prueba manual con aplicaciones sandbox sigue pendiente porque requiere credenciales y callbacks HTTPS; no debe afirmarse verde hasta ejecutarla.

## Próximo paso

Ejecutar el Bloque 4, tarea 9: regresión completa y revisión final contra el spec. Leer solo esa tarea y los criterios de aceptación necesarios. Incluir las suites de autenticación tradicional/externa, verificaciones completas aplicables, revisión anti-PII y del diff acumulado. Docker es necesario para la integración backend.

## Commits de esta sesión

- `a546a12 feat(web): añade acceso con Facebook y Google`
- `4b55013 docs(auth): documenta configuración de proveedores`
