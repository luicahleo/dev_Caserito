# Diseño — Captura KYC desde PWA móvil instalada

**Fecha:** 2026-08-05
**Estado:** sustituido parcialmente por decisión del usuario del 2026-08-05
**Sustituye:** la restricción Android/Capacitor del diseño
`2026-08-05-captura-kyc-movil-design.md`.

## Decisión posterior — instalación opcional

La instalación dejó de ser requisito funcional. La cámara web funciona mediante
`getUserMedia` en Chrome/Safari móviles bajo HTTPS; por tanto, se habilita en
Android, iPhone y iPad con cámara tanto desde una pestaña normal como desde la
PWA. Escritorio continúa bloqueado y nunca se ofrece selector de archivos.

Esta decisión sustituye las referencias posteriores a exigir `standalone`. La
PWA sigue siendo instalable, pero su instalación es opcional y no interviene en
el gate KYC.

## Objetivo

Permitir la captura guiada de CI y selfie en Android e iOS sin distribuir una
aplicación mediante Play Store o App Store. Caserito debe ejecutarse como PWA
instalada desde el sitio; una pestaña normal y un escritorio permanecen
bloqueados.

## Decisiones

- Habilitar captura solo si la aplicación está en modo `standalone` y el
  dispositivo es móvil.
- Detectar `standalone` con `display-mode: standalone` y, para Safari iOS, con
  `navigator.standalone`.
- Detectar Android, iPhone/iPad/iPod e iPadOS con user-agent/plataforma y soporte
  táctil. Es una regla de experiencia, no una frontera de seguridad.
- Solicitar cámara directamente mediante `getUserMedia` tras pulsar `Abrir
  cámara`; no usar permisos ni APIs nativas de Capacitor.
- Mantener el flujo, recorte, envío multipart y reglas anti-PII existentes.
- En navegador normal mostrar instrucciones de instalación diferenciadas para
  Android e iOS, sin input de archivos.

## Fuera de alcance

- tiendas, APK/IPA, firma nativa y publicación;
- impedir técnicamente que un navegador falsee su user-agent o display mode;
- OCR, liveness, reconocimiento facial y reverso del CI;
- instalación automática en iOS: Safari exige la acción manual del usuario.

## Estados de plataforma

| Entorno | Captura |
|---|---|
| PWA instalada en Android | habilitada |
| PWA añadida a inicio en iOS/iPadOS | habilitada |
| Chrome/Safari en pestaña normal | bloqueada |
| PWA instalada en escritorio | bloqueada |
| navegador sin `getUserMedia` | bloqueada con incompatibilidad |

## Seguridad y PII

Las imágenes permanecen en memoria, no se registran ni persisten localmente y
solo se envían al confirmar la solicitud KYC. No se solicitan micrófono, galería
ni almacenamiento. Los errores del navegador se normalizan sin conservar
mensajes del dispositivo.

## Pruebas

- detección Android/iOS/iPadOS standalone;
- rechazo de pestaña móvil y PWA de escritorio;
- apertura de cámara sin invocar plugins de Capacitor;
- instrucciones de instalación en navegador;
- regresión del flujo CI → selfie y del multipart existente;
- manifiesto y service worker generados por el build.

## Criterios de aceptación

1. Android e iOS pueden capturar desde la PWA instalada sin tiendas.
2. Navegador normal y escritorio no ofrecen captura ni subida.
3. El permiso se solicita al abrir la cámara mediante `getUserMedia`.
4. No existe dependencia funcional de Capacitor Camera.
5. Typecheck, lint, tests y build terminan correctamente.
