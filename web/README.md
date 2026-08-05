# CaseritoApp Web

SPA en React + TypeScript (PWA) que consume la API .NET de CaseritoApp.

## Scripts

- `npm run dev` — servidor de desarrollo (Vite)
- `npm run build` — typecheck + build de producción
- `npm run lint` — ESLint
- `npm run typecheck` — verificación de tipos (tsc)
- `npm run test` — tests (Vitest)

## Android

La aplicación Android requiere API 24 o posterior. Para actualizar el proyecto
nativo después de cambiar el frontend:

```powershell
npm run build
npx cap sync android
npx cap run android
```

El último comando requiere Android Studio con el SDK configurado y un dispositivo
físico o emulador disponible. La captura KYC solicita únicamente permiso de cámara;
no usa permiso de micrófono ni ofrece acceso a la galería.
