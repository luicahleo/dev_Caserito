# Handoff — Captura KYC móvil

**Rama:** `feature/captura-kyc-movil`

## Objetivo

Capturar frontal del CI y selfie con cámara guiada únicamente en la aplicación
Android de Capacitor. Los navegadores quedan bloqueados y no existe selector de
archivos.

## Estado

Implementación terminada y commits locales creados. Spec y plan:

- `docs/superpowers/specs/2026-08-05-captura-kyc-movil-design.md`
- `docs/superpowers/plans/2026-08-05-captura-kyc-movil.md`

El frontend distingue Android nativo, solicita solo permiso de cámara, controla
y libera el stream, recorta el marco a JPEG en memoria, permite repetir/confirmar
CI y selfie e integra los mismos `File` en el multipart KYC existente. No se
modificó backend ni OpenAPI.

## Verificación realizada

Desde `web/`:

- `npm run typecheck` — verde.
- `npm run lint` — verde, sin warnings.
- `npm run test -- --run` — 53 archivos, 195 tests, todos verdes.
- Prettier dirigido a todos los archivos modificados — verde.
- `npm run build` — verde.
- `npx cap sync android` — verde.
- `git diff master...HEAD --check` — verde.
- Revisión dirigida — sin inputs de archivo, almacenamiento web, `console.*`,
  permiso de audio o permiso de galería en el flujo.

`npm run format:check` global no está verde por deuda previa: reporta numerosos
archivos existentes y artefactos generados de Android fuera del cambio lógico.
Los archivos modificados sí pasan el check dirigido.

## Pendiente externo

No se pudo ejecutar `web/android/gradlew.bat assembleDebug`: el equipo no tiene
`JAVA_HOME` ni `java` en `PATH`.

Con JDK/Android SDK disponibles:

```powershell
Set-Location web
npm run build
npx cap sync android
Set-Location android
.\gradlew.bat assembleDebug
```

Después probar en dispositivo físico: permiso concedido/denegado/revocado,
cámaras trasera y frontal, orientación, segundo plano, recorte del CI, selfie no
invertida, revisión admin y bloqueo en Chrome móvil/escritorio.

No hacer push ni merge sin autorización explícita.
