# Plan de identidad visual de Caserito

## 1. Crear el arte maestro

- Entrada: dirección visual aprobada y paleta de `web/src/theme/theme.ts`.
- Salida: símbolo cuadrado generado con fondo plano removible, guardado en `web/public/brand/`.
- Verificación: inspección visual a tamaño original y miniatura de 16 px; ausencia de texto, marcas de agua y detalles frágiles.
- Commit previsto: `feat(brand): crea simbolo de intercambio de Caserito`.

## 2. Derivar recursos de plataforma

- Entrada: arte maestro seleccionado.
- Salida: `favicon.svg`, `favicon-32x32.png`, `favicon.ico`, `apple-touch-icon.png`, iconos PWA normales y `maskable`, e imagen Open Graph.
- Rutas: `web/public/` y `web/public/brand/`.
- Verificación: script dirigido comprueba formato, dimensiones, transparencia y margen seguro.
- Commit previsto: `feat(brand): genera iconos y recursos multiplataforma`.

## 3. Integrar la marca en la aplicación

- Entrada: recursos finales.
- Salida: componente reutilizable `MarcaCaserito`, cabecera/pie actualizados, enlaces HTML y manifiesto PWA coherentes.
- Rutas: `web/src/app/`, `web/index.html`, `web/vite.config.ts`.
- Prueba roja esperada: el layout no encuentra todavía la marca nueva por su nombre accesible y el manifiesto carece de variantes `maskable`.
- Verificación: prueba dirigida de layout, typecheck, lint y build.
- Commit previsto: `feat(brand): integra identidad visual en web y PWA`.

## 4. Revisión final

- Revisar el diff contra el spec y comprobar contraste y legibilidad a 16 px.
- Ejecutar `npm run typecheck`, `npm run lint`, `npm run test -- --run`, `npm run format:check` y `npm run build` desde `web/`.
- Ejecutar `git diff --check` y documentar cualquier limitación.
