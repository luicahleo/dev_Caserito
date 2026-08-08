# Gate de calidad por alcance — plan

1. Crear pruebas de clasificación en `quality/__tests__/scope.test.mjs`; deben
   fallar porque el selector aún no existe.
2. Crear `quality/scope.mjs` con clasificación pura y lectura inyectable de Git;
   ejecutar su prueba dirigida hasta verde.
3. Incorporar `--changed` a `quality/verify.mjs`, `verify.ps1` y `verify.sh`, con
   filtros explícitos por gate y rechazo de `--full --changed`.
4. Actualizar `AGENTS.md`, `docs/ai/PUERTA_CALIDAD.md` y `docs/ai/WORKFLOW.md`.
5. Limitar los workers de Vitest mediante una prueba de configuración si la
   suite completa evidencia timeouts por contención sin fallos aislados.
6. Ejecutar tests de calidad, pruebas dirigidas del login pendiente, lint,
   typecheck, `verify -Changed` y finalmente `verify -Full` antes de integrar.
