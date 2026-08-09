# Plan de implementación: Mis avisos responsive

Especificación:
`docs/superpowers/specs/2026-08-09-mis-avisos-responsive-design.md`.

## Tarea 1: describir ambas presentaciones

**Archivo:** `web/src/routes/MisAvisosPage.test.tsx`.

1. Añadir una prueba con foto, título largo y estado de moderación.
2. Exigir una región móvil identificable como lista y una tabla de escritorio.
3. Verificar en sus estilos MUI que la lista se muestra solo en `xs` y la tabla
   desde `sm`.
4. Mantener las pruebas observables de pausar, eliminar y vendido.

**Rojo esperado:** no existe la lista de tarjetas ni el contenedor responsive.

**Verificación:** desde `web/`, ejecutar
`npm run test -- src/routes/MisAvisosPage.test.tsx`.

## Tarea 2: implementar el layout responsive

**Archivo:** `web/src/routes/MisAvisosPage.tsx`.

1. Extraer unidades visuales locales pequeñas para estados y acciones, sin
   mover lógica remota fuera de la página.
2. Incorporar `Card`, `CardContent`, `CardMedia`, `Paper` y `TableContainer` de
   MUI.
3. Renderizar tarjetas en `xs` y tabla desde `sm`, compartiendo callbacks y
   estados pendientes.
4. Apilar el encabezado y expandir el CTA en móvil.
5. Limitar hermanos de paginación para 320 px.

**Verde esperado:** pruebas dirigidas verdes sin alterar el contrato de API.

**Verificación:** prueba dirigida, `npm run typecheck`, `npm run lint` y
Prettier dirigido sobre los dos archivos.

## Tarea 3: integrar y cerrar Mis avisos

1. Revisar el diff, accesibilidad, 320 px y ausencia de PII.
2. Ejecutar `git diff --check` y `./verify.ps1 -Changed`.
3. Crear el commit `feat(avisos): adapta mis avisos a móvil y escritorio` y
   publicarlo en `develop`.

## Tarea 4: auditar las demás páginas

1. Inventariar las rutas declaradas en `web/src/app/router.tsx`.
2. Revisar estáticamente patrones de ancho fijo, tablas, filas de acciones,
   diálogos y contenido susceptible de desbordamiento.
3. Cuando sea viable, ejecutar la aplicación y comprobar vistas públicas en
   320, 768 y 1280 px.
4. Entregar hallazgos priorizados por ruta, causa y propuesta. No corregir otras
   páginas sin diseño aprobado para ese siguiente bloque.
