# CaseritoApp Web

SPA en React + TypeScript (PWA) que consume la API .NET de CaseritoApp.

## Scripts

- `npm run dev` — servidor de desarrollo (Vite)
- `npm run build` — typecheck + build de producción
- `npm run lint` — ESLint
- `npm run typecheck` — verificación de tipos (tsc)
- `npm run test` — tests (Vitest)

## Uso móvil sin tiendas

Caserito se distribuye como PWA desde el sitio HTTPS:

- Android/Chrome: usar el botón `Instalar Caserito` cuando aparezca o la opción
  `Instalar aplicación` del menú del navegador.
- iPhone/iPad: abrir con Safari, pulsar Compartir y `Añadir a pantalla de inicio`.

La instalación es opcional. La captura KYC funciona directamente en Chrome o
Safari desde Android, iPhone y iPad con cámara, y también desde la PWA instalada.
En escritorio permanece bloqueada. No se solicita micrófono, galería ni
almacenamiento.

## Contenedor Android opcional

La aplicación Android requiere API 24 o posterior. Para actualizar el proyecto
nativo después de cambiar el frontend:

```powershell
npm run build
npx cap sync android
npx cap run android
```

El último comando requiere Android Studio con el SDK configurado y un dispositivo
físico o emulador disponible. Este contenedor no es necesario para distribuir o
usar la PWA.
