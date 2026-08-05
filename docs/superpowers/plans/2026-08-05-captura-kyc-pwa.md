# Plan — Migrar captura KYC a PWA móvil instalada

> **Actualización 2026-08-05:** la instalación pasó a ser opcional. El cierre
> reemplaza `esPwaMovilInstalada` por `esMovilConCamara`, retira el prompt propio
> y permite Chrome/Safari móviles manteniendo escritorio bloqueado.

**Spec:** `docs/superpowers/specs/2026-08-05-captura-kyc-pwa-design.md`

## Tarea 1 — Detectar PWA móvil instalada

- Modificar `web/src/kyc/captura/plataforma.ts` y su test.
- Producir `esPwaMovilInstalada()` a partir de dependencias inyectables:
  `matchMedia`, user-agent, plataforma, puntos táctiles y flag iOS standalone.
- Cubrir Android/iPhone/iPadOS instalados, pestaña normal y escritorio.
- Prueba roja: los nuevos casos no existen y el gate solo reconoce Android
  nativo.
- Verificación: test dirigido y typecheck.
- Commit: `feat(kyc): habilita captura en PWA móvil instalada`.

## Tarea 2 — Usar permisos web y adaptar la UI

- Modificar `web/src/kyc/captura/usarCamara.ts` para que `getUserMedia` sea quien
  solicite permiso; retirar el proveedor Capacitor del camino productivo.
- Eliminar `permisosCamara.ts` y sus tests si quedan sin consumidores.
- Modificar `web/src/routes/KycPage.tsx` y test para el nuevo gate y el mensaje
  de instalación desde Android/iOS.
- Mantener `audio: false`, errores normalizados y limpieza de tracks.
- Prueba roja: PWA móvil sigue bloqueada o intenta usar el plugin nativo.
- Verificación: tests de cámara, plataforma y ruta KYC.
- Commit: `feat(kyc): captura CI desde PWA Android e iOS`.

## Tarea 3 — Retirar infraestructura nativa innecesaria

- Eliminar `@capacitor/camera` de package/lock.
- Mantener Capacitor base y `web/android/` únicamente como infraestructura
  histórica compatible, sin usarla para habilitar KYC; no es requisito de
  distribución.
- Actualizar `web/README.md` con instalación PWA en Android/iOS.
- Actualizar `docs/ai/HANDOFF.md` para reemplazar la validación APK por pruebas
  reales de PWA.
- Verificación completa desde `web/`: typecheck, lint, tests, formato dirigido y
  build. Revisar el manifiesto generado.
- Commit: `docs(kyc): documenta instalación PWA sin tiendas`.

No cambia backend, OpenAPI ni administración KYC.
