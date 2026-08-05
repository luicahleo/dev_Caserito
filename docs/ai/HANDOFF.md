# Handoff — Captura KYC desde PWA móvil

**Rama:** `feature/captura-kyc-pwa`

## Objetivo

Permitir captura guiada de CI y selfie en Android e iOS sin Play Store ni App
Store. La captura solo se habilita en la PWA instalada desde el sitio.

## Estado

Se sustituyó el gate Android/Capacitor por detección de PWA móvil `standalone`,
incluyendo Safari iOS/iPadOS. `getUserMedia` solicita directamente el permiso de
cámara y ya no se usa `@capacitor/camera`. Android puede mostrar un botón propio
`Instalar Caserito` mediante `beforeinstallprompt`; iOS mantiene el flujo manual
de Safari → Compartir → Añadir a pantalla de inicio.

Spec y plan:

- `docs/superpowers/specs/2026-08-05-captura-kyc-pwa-design.md`
- `docs/superpowers/plans/2026-08-05-captura-kyc-pwa.md`

## Verificación realizada

- Typecheck verde.
- Lint verde, sin warnings.
- Suite completa: 53 archivos y 195 tests verdes.
- Build PWA verde; manifiesto generado con `display: standalone`, nombre, colores
  e iconos 192/512 normales y maskable.
- Sincronización Capacitor verde y plugin nativo de cámara retirado.
- Formato dirigido verde.

## Pendiente externo

Probar en dispositivos reales:

- Android: prompt de instalación, standalone, cámara trasera/frontal y bloqueo
  en pestaña normal.
- iPhone/iPad: instalación desde Safari, standalone, permiso y cámaras.
- Escritorio: PWA instalada continúa bloqueada.

No hacer push ni merge sin autorización explícita.
