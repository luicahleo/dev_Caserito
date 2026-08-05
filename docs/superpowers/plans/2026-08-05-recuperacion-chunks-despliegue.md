# Plan: recuperación ante chunks obsoletos

## 1. Clasificar y limitar la recuperación

- Crear `web/src/app/recuperacionCarga.ts` y su prueba dirigida.
- Detectar solo errores conocidos de módulos dinámicos/chunks.
- Guardar por ruta una marca en `sessionStorage` y recargar una sola vez.
- Resultado esperado: pruebas rojas antes de implementar y verdes después.
- Commit previsto: `fix(web): recupera cargas de chunks obsoletos`.

## 2. Sustituir el error predeterminado

- Crear `web/src/app/ErrorAplicacion.tsx` y pruebas de conducta observable.
- Integrarlo como `errorElement` de la ruta raíz en `web/src/app/router.tsx`.
- Mostrar mensaje genérico y acciones de recarga e inicio, sin detalles internos.
- Verificación: prueba dirigida y typecheck.
- Commit previsto junto con la tarea 1 por formar una única conducta.

## 3. Compatibilidad durante despliegues

- Modificar `.github/workflows/deploy.yml` antes del `docker build` remoto.
- Crear un contexto temporal, leer de forma validada `current-release` y copiar
  allí los assets anteriores con `cp -an`, sin sobrescribir los actuales.
- Si el release o sus assets no existen, continuar sin error.
- Verificar sintaxis y revisar el diff dirigido.
- Commit previsto: `fix(deploy): conserva assets de la versión anterior`.

## 4. Integración y cierre

- Ejecutar pruebas dirigidas y suite completa, typecheck, lint, format check y
  build desde `web/`.
- Ejecutar `git diff --check`, revisar anti-PII y el diff completo.
- Eliminar el handoff obsoleto, integrar en `master` y publicar, conforme a la
  autorización permanente del usuario.
