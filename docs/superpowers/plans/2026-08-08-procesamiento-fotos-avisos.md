# Plan de procesamiento automático de fotos de avisos

## 1. Contrato frontend de procesamiento

- Crear `web/src/avisos/fotos/procesarFotoAviso.ts` y su test.
- Entrada: `File`; salida: JPEG normalizado o error genérico tipado.
- Inyectar primitivas de decodificación/canvas donde los tests necesiten aislar
  APIs del navegador.
- Prueba roja: formato, orientación, 1600 px, fondo blanco, calidad/peso,
  metadata implícitamente descartada y fallos.
- Verificación: `npm run test -- --run src/avisos/fotos/procesarFotoAviso.test.ts`.
- Commit previsto: procesamiento frontend.

## 2. Integración con cámara, galería y formulario

- Modificar `web/src/routes/FormAviso.tsx` y sus pruebas.
- Ambos inputs usan la misma preparación secuencial antes de preview/subida.
- Mostrar «Preparando fotos…», bloquear acciones y gestionar éxito parcial,
  límite absoluto, cinco fotos y revocación de URLs.
- Prueba roja: cámara, galería, múltiples, estado, error y límite.
- Verificación: test dirigido de `FormAviso`, typecheck y lint.
- Commit previsto: procesamiento frontend.

## 3. Contrato de normalización backend

- Añadir un puerto en Catalog.Application con resultado normalizado.
- Probar primero que el handler guarda exclusivamente el resultado del puerto,
  siempre como JPEG, y no lo invoca ante una entrada inválida.
- Ajustar endpoint y validación a entrada absoluta de 25 MiB.
- Verificación: filtro dirigido de tests unitarios Catalog.
- Commit previsto: normalización defensiva backend.

## 4. Implementación de imagen backend

- Incorporar una biblioteca de imagen compatible, con versión centralizada.
- Implementar en Catalog.Infrastructure orientación, escalado, fondo blanco,
  JPEG progresivamente ajustado a 1 MiB y salida sin metadata.
- Añadir pruebas con fixtures generados en memoria para JPEG/PNG, alpha,
  orientación, dimensiones, peso y archivo corrupto.
- Verificación: tests dirigidos y build backend.
- Commit previsto: normalización defensiva backend.

## 5. Almacenamiento durable

- Probar y adaptar `AlmacenFotoAvisoDisco` para escritura atómica, extensión
  `.jpg`, partición por prefijo y lectura/borrado retrocompatible `.bin/.meta`.
- Añadir volumen nombrado de fotos al servicio API en `docker-compose.dev.yml`.
- No modificar el bind mount de VPS existente.
- Verificación: tests dirigidos de almacenamiento y validación de compose.
- Commit previsto: persistencia durable de fotos.

## 6. Integración y cierre

- Añadir/ajustar integración de subida y entrega normalizada.
- Ejecutar frontend: test completo, typecheck, lint, format y build.
- Ejecutar backend: build, test y `dotnet format --verify-no-changes`.
- Ejecutar `git diff --check` y `./verify.ps1 -Changed`.
- Revisar diff contra el spec, anti-PII y ausencia de originales/metadata.
- Publicar los commits directamente a `origin/develop`; no operar sobre
  `master`.
