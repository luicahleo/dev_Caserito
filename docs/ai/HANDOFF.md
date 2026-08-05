# Handoff — Captura KYC desde navegador móvil

**Rama:** `feature/captura-kyc-pwa`

## Objetivo

Permitir captura guiada de CI y selfie en Android e iOS sin Play Store, App Store
ni instalación obligatoria.

## Estado

El gate detecta Android, iPhone e iPadOS con cámara web. Chrome/Safari móviles y
la PWA instalada pueden capturar; escritorio queda bloqueado. `getUserMedia`
solicita directamente el permiso y no se usa `@capacitor/camera`, selector de
archivos ni prompt de instalación propio.

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
