# Plan de implementación: carrusel de fotos del aviso

Especificación: `docs/superpowers/specs/2026-08-09-carrusel-fotos-aviso-design.md`.

## Tarea 1: describir la navegación observable

**Entrada:** galería actual de `DetalleAvisoPage` y un aviso público con varias
fotos.

**Archivos:**

- Modificar `web/src/routes/DetalleAvisoPage.test.tsx`.

**Trabajo:**

1. Preparar un aviso con tres fotos identificables por URL.
2. Comprobar que `Imagen siguiente` avanza y envuelve de la última a la primera.
3. Comprobar que `Imagen anterior` retrocede y envuelve de la primera a la
   última.
4. Comprobar que las flechas no aparecen con una sola foto.
5. Conservar cobertura de selección mediante miniaturas.

**Rojo esperado:** Testing Library no encuentra los botones accesibles porque
la vista aún no los renderiza.

**Verificación:** desde `web/`, ejecutar
`npm run test -- --run src/routes/DetalleAvisoPage.test.tsx`; debe fallar por la
ausencia de los controles.

## Tarea 2: implementar los controles circulares

**Entrada:** pruebas rojas de la tarea 1.

**Archivos:**

- Modificar `web/src/routes/DetalleAvisoPage.tsx`.

**Trabajo:**

1. Importar `IconButton`, `ChevronLeft` y `ChevronRight` desde MUI.
2. Encapsular la imagen ampliada en un contenedor de posición relativa.
3. Añadir funciones locales que calculen el índice anterior y siguiente con
   módulo sobre la cantidad de fotos.
4. Renderizar ambos controles únicamente cuando haya más de una foto, con los
   nombres `Imagen anterior` e `Imagen siguiente`.
5. Mantener la selección de miniaturas y el resaltado existentes.

**Verde esperado:** todas las pruebas dirigidas pasan y no se introducen
desbordamientos ni APIs obsoletas de MUI.

**Verificación:** desde `web/`, ejecutar:

```powershell
npm run test -- --run src/routes/DetalleAvisoPage.test.tsx
npm run typecheck
npm run lint
npm run format:check
```

## Tarea 3: integrar y cerrar

**Entrada:** implementación y pruebas dirigidas verdes.

**Trabajo:**

1. Revisar el diff contra los cinco criterios de aceptación.
2. Ejecutar `git diff --check`.
3. Ejecutar `./verify.ps1 -Changed` desde la raíz.
4. Crear el commit de implementación y publicarlo en `develop` si la puerta está
   verde.

**Commit previsto:** `feat(avisos): añade navegación al carrusel de fotos`.
