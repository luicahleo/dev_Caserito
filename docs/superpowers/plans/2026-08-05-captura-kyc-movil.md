# Plan — Captura guiada de CI y selfie en Android

**Spec:** `docs/superpowers/specs/2026-08-05-captura-kyc-movil-design.md`

**Objetivo:** sustituir los selectores de archivos de `/kyc` por una captura
guiada de CI y selfie disponible solo dentro de la aplicación Android de
Capacitor, conservando sin cambios el contrato multipart del backend.

**Arquitectura:** `KycPage` mantiene estado remoto, campos del CI y envío. Un
módulo `src/kyc/captura/` concentra plataforma, permisos/stream, geometría y UI.
Las capturas confirmadas salen del módulo como `File` JPEG en memoria. La
plataforma Android se incorpora como artefacto derivado de Capacitor y declara
solo el permiso de cámara necesario.

**Reglas:** TDD por tarea, textos/comentarios en español, TypeScript estricto,
sin inputs de archivo, sin PII en logs o almacenamiento, sin cambios backend.

## Tarea 1 — Incorporar y validar la plataforma Android

**Entradas:** `web/package.json`, `web/package-lock.json`,
`web/capacitor.config.ts`; Capacitor Core/CLI instalados en `8.4.2`.

**Salidas:**

- modificar `web/package.json` y `web/package-lock.json`;
- crear `web/android/` mediante Capacitor;
- modificar `web/android/app/src/main/AndroidManifest.xml`;
- modificar el archivo nativo mínimo que gestione el permiso WebView solo si el
  runtime generado no concede `RESOURCE_VIDEO_CAPTURE` después de obtener el
  permiso Android;
- documentar comandos Android en `web/README.md`.

**Dependencias:**

- `@capacitor/android@8.4.2`, alineado con Core/CLI según la recomendación
  oficial de mantener los runtimes en la misma versión;
- `@capacitor/camera@8.2.2`, solo para consultar/solicitar el permiso Android;
  no se invocarán `getPhoto` ni `pickImages`.

**Comportamiento:**

1. instalar las dependencias con versiones explícitas;
2. ejecutar `npm run build` y `npx cap add android`;
3. declarar `android.permission.CAMERA`; no declarar permisos de audio o lectura
   de fotos;
4. ejecutar `npx cap sync android`;
5. comprobar el manifiesto fusionado y el código generado: no debe aparecer una
   vía de galería agregada por Caserito;
6. registrar en README: build → sync → run, API 24+ y necesidad de dispositivo o
   emulador con cámara.

**Prueba roja esperada:** `npx cap sync android` falla antes de instalar/agregar
la plataforma porque `web/android/` no existe.

**Verificación:**

```powershell
npm run build
npx cap sync android
Set-Location android
.\gradlew.bat assembleDebug
```

**Resultado esperado:** build web, sync y APK debug correctos. Si el equipo no
tiene JDK/Android SDK, registrar el error causal y dejar `assembleDebug` como
verificación manual pendiente; no presentarlo como verde.

**Commit previsto:** `chore(android): incorpora plataforma para captura KYC`

## Tarea 2 — Encapsular plataforma y permiso de cámara

**Entradas:** `Capacitor.getPlatform()`, `Capacitor.isNativePlatform()` y
`Camera.checkPermissions/requestPermissions`.

**Salidas:**

- crear `web/src/kyc/captura/plataforma.ts`;
- crear `web/src/kyc/captura/plataforma.test.ts`;
- crear `web/src/kyc/captura/permisosCamara.ts`;
- crear `web/src/kyc/captura/permisosCamara.test.ts`.

**Interfaces:**

```ts
export interface PlataformaCaptura {
  esAndroidNativo(): boolean;
}

export type EstadoPermisoCamara =
  | 'concedido'
  | 'denegado'
  | 'solicitarEnAjustes';

export interface PermisosCamara {
  solicitar(): Promise<EstadoPermisoCamara>;
}
```

La implementación real devuelve `true` exclusivamente con plataforma nativa
`android`. Los adaptadores se inyectan en componentes/hooks para evitar alterar
globales de Capacitor en los tests.

El mapeo de permisos será cerrado y sin propagar mensajes nativos. `granted`
produce `concedido`; `denied` produce `denegado`; estados sin posibilidad de
prompt producen `solicitarEnAjustes`.

**Prueba roja esperada:** tests que importan los módulos inexistentes y describen
navegador, Android, iOS, permiso concedido, denegado y bloqueado.

**Verificación:**

```powershell
npm run test -- --run src/kyc/captura/plataforma.test.ts src/kyc/captura/permisosCamara.test.ts
```

**Resultado esperado:** todos los casos pasan sin inicializar cámara ni escribir
en consola.

**Commit previsto:** `feat(kyc): limita captura a Android nativo`

## Tarea 3 — Implementar geometría y generación segura del archivo

**Entradas:** dimensiones intrínsecas del vídeo, rectángulo renderizado del
vídeo, rectángulo de la guía y modo `cover`.

**Salidas:**

- crear `web/src/kyc/captura/geometriaCaptura.ts`;
- crear `web/src/kyc/captura/geometriaCaptura.test.ts`;
- crear `web/src/kyc/captura/crearCaptura.ts`;
- crear `web/src/kyc/captura/crearCaptura.test.ts`.

**Interfaces:**

```ts
export interface Rectangulo {
  x: number;
  y: number;
  ancho: number;
  alto: number;
}

export function calcularRecorteCover(
  video: { ancho: number; alto: number },
  visor: Rectangulo,
  guia: Rectangulo,
): Rectangulo;

export async function crearCaptura(
  video: HTMLVideoElement,
  recorte: Rectangulo,
  nombre: 'documento-ci.jpg' | 'selfie.jpg',
): Promise<File>;
```

**Comportamiento:**

- validar dimensiones finitas y positivas;
- trasladar y limitar el recorte a los bordes del frame;
- limitar el lado mayor de salida a 1920 px sin ampliar frames pequeños;
- exportar `image/jpeg` con calidad inicial `0.88`;
- si supera 5 MiB, reintentar una vez con calidad `0.75` y lado máximo 1600;
- si aún supera el límite, lanzar un error tipado genérico;
- no conservar EXIF, overlay ni identificadores personales;
- la selfie conserva un rectángulo; la guía ovalada solo orienta al usuario.

Los tests usarán canvas/video falsos y bytes sintéticos, nunca imágenes reales.

**Prueba roja esperada:** fallan imports y casos de `cover` horizontal/vertical,
límites, reducción, nombre/MIME y exceso de tamaño.

**Verificación:**

```powershell
npm run test -- --run src/kyc/captura/geometriaCaptura.test.ts src/kyc/captura/crearCaptura.test.ts
```

**Resultado esperado:** coordenadas exactas y archivos JPEG acotados en todos los
casos.

**Commit previsto:** `feat(kyc): recorta capturas dentro de la guía`

## Tarea 4 — Controlar el stream y su ciclo de vida

**Entradas:** `navigator.mediaDevices.getUserMedia`, permisos normalizados y
eventos `visibilitychange`/desmontaje.

**Salidas:**

- crear `web/src/kyc/captura/usarCamara.ts`;
- crear `web/src/kyc/captura/usarCamara.test.tsx`;
- crear `web/src/kyc/captura/erroresCamara.ts`;
- crear `web/src/kyc/captura/erroresCamara.test.ts`.

**Interfaz:**

```ts
export type LenteCamara = 'trasera' | 'frontal';
export type EstadoCamara =
  | 'inactiva'
  | 'solicitandoPermiso'
  | 'activa'
  | 'capturando'
  | 'error';

export interface ControlCamara {
  estado: EstadoCamara;
  stream: MediaStream | null;
  error: ErrorCamara | null;
  abrir(lente: LenteCamara): Promise<void>;
  cerrar(): void;
}
```

**Comportamiento:**

- pedir permiso desde `abrir`, invocado por el botón;
- solicitar vídeo con `audio: false`, lente ideal y resolución ideal
  1920×1080;
- ante `OverconstrainedError`, reintentar una vez sin `facingMode`;
- normalizar `NotAllowedError`, `NotFoundError`, `NotReadableError` y resto a
  categorías genéricas sin conservar el mensaje original;
- cerrar el stream anterior antes de abrir otro;
- ejecutar `stop()` en cada track de forma idempotente al cerrar, desmontar o
  pasar `document.visibilityState` a `hidden`;
- ignorar la resolución tardía de una apertura cancelada y detener ese stream.

**Prueba roja esperada:** el hook inexistente no satisface apertura tras acción,
fallback, errores, carreras y limpieza.

**Verificación:**

```powershell
npm run test -- --run src/kyc/captura/usarCamara.test.tsx src/kyc/captura/erroresCamara.test.ts
```

**Resultado esperado:** no queda ningún track activo en los caminos de salida.

**Commit previsto:** `feat(kyc): controla cámara y libera recursos`

## Tarea 5 — Construir el capturador guiado accesible

**Entradas:** control de cámara, geometría y creación de captura.

**Salidas:**

- crear `web/src/kyc/captura/CapturadorGuiado.tsx`;
- crear `web/src/kyc/captura/CapturadorGuiado.test.tsx`;
- crear `web/src/kyc/captura/FlujoCapturaKyc.tsx`;
- crear `web/src/kyc/captura/FlujoCapturaKyc.test.tsx`.

**Props principales:**

```ts
interface CapturadorGuiadoProps {
  tipo: 'documento' | 'selfie';
  onConfirmar(archivo: File): void;
  onCancelar(): void;
}

interface FlujoCapturaKycProps {
  documento: File | null;
  selfie: File | null;
  onDocumento(archivo: File | null): void;
  onSelfie(archivo: File | null): void;
}
```

**Comportamiento UI:**

- diálogo MUI titulado, visor estable, `<video autoPlay playsInline muted>` y
  marco superpuesto no interactivo;
- documento con proporción visual aproximada 1.586:1 y esquinas visibles;
- selfie con guía ovalada, preview espejada solo visualmente;
- instrucciones textuales, progreso `CI` → `Selfie` y botones con nombres
  accesibles;
- `Tomar foto`, `Repetir`, `Usar esta foto`, `Cancelar`;
- preview mediante object URL revocada al reemplazar/desmontar;
- cierre del diálogo detiene cámara;
- errores de permiso/cámara como `Alert` genérico con `Reintentar` cuando aplica;
- no mostrar nombres de archivo ni incorporar animación decorativa.

**Prueba roja esperada:** los componentes inexistentes no muestran la secuencia,
guías, confirmación, repetición, errores ni limpieza esperados.

**Verificación:**

```powershell
npm run test -- --run src/kyc/captura/CapturadorGuiado.test.tsx src/kyc/captura/FlujoCapturaKyc.test.tsx
```

**Resultado esperado:** flujo completo observable con cámara/canvas simulados,
sin input de archivo.

**Commit previsto:** `feat(kyc): añade captura guiada de CI y selfie`

## Tarea 6 — Integrar la captura en `/kyc`

**Entradas:** `web/src/routes/KycPage.tsx`, API `enviarKyc` sin cambios y módulo
de captura terminado.

**Salidas:**

- modificar `web/src/routes/KycPage.tsx`;
- modificar `web/src/routes/KycPage.test.tsx`;
- modificar `web/src/api/kyc.test.ts` solo si el nombre neutral de los nuevos
  `File` requiere ampliar aserciones del multipart.

**Comportamiento:**

- eliminar completamente los dos `<input type="file">`;
- conservar número, complemento, departamento y estados remotos actuales;
- navegador: mostrar el aviso de uso de la app y no renderizar controles de
  captura o envío;
- Android nativo: mostrar el flujo guiado;
- habilitar `Enviar` solo con número/departamento válidos y ambas capturas;
- en éxito, invalidar estado y liberar capturas/URLs;
- en 409, invalidar estado y limpiar recursos;
- en 503 o error genérico, conservar las capturas en memoria para reintento
  durante la misma vista;
- cambiar el texto que hoy afirma comparación automática de rostros por revisión
  humana fiel al sistema real;
- `Pendiente` y `Aprobada` nunca solicitan cámara; `Rechazada` inicia desde cero.

La firma de `enviarKyc` y el contrato OpenAPI no cambian.

**Prueba roja esperada:** los tests nuevos encuentran inputs de archivo y no
encuentran bloqueo web ni botones de captura.

**Verificación:**

```powershell
npm run test -- --run src/routes/KycPage.test.tsx src/api/kyc.test.ts
```

**Resultado esperado:** estados existentes y variantes web/Android pasan; el
multipart mantiene `documento` y `selfie`.

**Commit previsto:** `feat(kyc): exige captura móvil para verificar identidad`

## Tarea 7 — Integración y cierre

**Entradas:** todos los commits anteriores y spec aprobada.

**Revisión:**

- diff completo contra el spec;
- ausencia de `<input type="file">` en el flujo KYC de usuario;
- ausencia de `console.*`, almacenamiento web y nombres con PII en el módulo;
- ausencia de permiso de micrófono/galería incorporado por Caserito;
- liberación de streams/object URLs en éxito, error, repetición, cierre, ruta y
  segundo plano;
- UI desde 320 px, orientación y foco;
- no modificar administración KYC ni backend.

**Verificación frontend completa desde `web/`:**

```powershell
npm run typecheck
npm run lint
npm run test -- --run
npm run format:check
npm run build
npx cap sync android
```

Después, si el entorno Android está disponible:

```powershell
Set-Location android
.\gradlew.bat assembleDebug
```

Y en dispositivo físico:

- probar permiso concedido, denegado y revocado desde ajustes;
- probar cámara trasera/frontal, orientación y segundo plano;
- verificar recorte del CI y selfie no invertida en la revisión admin;
- confirmar bloqueo en Chrome de escritorio y Chrome móvil;
- confirmar que no se ofrece galería.

**Resultado esperado:** verificaciones automatizadas verdes; las manuales se
informan con dispositivo/API usados o se declaran pendientes con motivo.

**Commit previsto:** `test(kyc): integra captura móvil guiada`

## Orden de ejecución y límites

Las tareas se ejecutan en orden porque cada una consume interfaces de la
anterior. No se regenerará OpenAPI y no se ejecutarán suites backend porque el
contrato no cambia. Cualquier necesidad de OCR, liveness, reverso del CI, iOS o
cambio del endpoint detiene la ejecución y requiere una nueva decisión de
alcance/spec.
