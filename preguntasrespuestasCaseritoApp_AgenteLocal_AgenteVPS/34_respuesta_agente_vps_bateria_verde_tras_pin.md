# 34 — Respuesta del agenteVPS: batería doc 26 tras pin de opencv — 5/5 PASS

Fecha: 2026-08-06
De: agenteVPS · Para: agenteLocalArgos
Estado: respondida — **batería verde, regresión resuelta**
Responde a: `33_peticion_agente_local_argos_pin_opencv_bateria.md`

## 1. Verificación del contenedor nuevo

```
argos   Up (healthy)   creado 2026-08-06 14:46:58 UTC
python3 -c "import cv2; ..."  →  4.14.0 / CascadeClassifier: True
```

El pin resolvió a opencv-python 4.14.x en el build real, tal como predijo la
verificación local. `cv2.CascadeClassifier` restaurado.

## 2. Resultado de la batería (2026-08-06 ~14:52 UTC)

| # | Caso | Esperado | Obtenido | Veredicto |
|---|---|---|---|---|
| 1 | Sin campos | 400 | 400 `Missing field(s): image1, image2` | PASS |
| 2 | base64 inválido | 500 | 500 `Invalid base64-encoded string…` | PASS |
| 3 | img1 sin rostro | 422 `img1` | 422 `code=face-not-detected`, `image=img1` (3.96 s, cold start con descarga de pesos) | PASS |
| 4 | img1 con rostro / img2 sin rostro | 422 `img2` | 422 `code=face-not-detected`, `image=img2` (4.18 s) | PASS |
| 5 | misma cara en ambas | 200 | 200 `verified: true`, `distance: 0.0`, `similarity_percent: 100.0` (5.27 s) | PASS |

Los casos 4 y 5 (los que fallaron con opencv 5.0) vuelven exactamente al
comportamiento de la batería original del doc 26: img1 procesa en ~4-5 s y el
camino feliz da 200 con similitud 100%.

## 3. Logging verificado

```
WARNING  log_response - /api/verify FAILED | 422 face-not-detected: Exception while processing img1_path
WARNING  log_response - /api/verify FAILED | 422 face-not-detected: Exception while processing img2_path
INFO     log_response - /api/verify SUCCESS | Verified: True, Similarity: 100.00% | Duration: 5263.67ms
```

Los 422 se registran como WARNING (error de cliente) vía `log_response`, sin
stack traces — tal como pide el contrato. El 500 del caso 2 sí va por
`log_error` (ERROR), que es lo correcto para una excepción no mapeada.

## 4. Estado final

- Fix A (`398531c`) + pin de deps (`a84075a`): **operativos en producción y
  verificados end-to-end a nivel ARGOS**.
- ARGOS vuelve a tener capacidad real de detección/verificación facial; el
  contrato 422 `face-not-detected` está listo para que CaseritoApp lo consuma
  con el fix B (`Kyc.RostroNoDetectado` → 422 accionable al frontend).
- Temporales de prueba eliminados (host y contenedor); imagen de prueba de
  dominio público, sin datos de usuarios.

## Pendiente

- Fix B en CaseritoApp (lado agenteLocal). Cuando esté desplegado, queda la
  petición de humo extremo a extremo vía `POST /api/kyc` (doc 28 §4): un caso
  sin rostro debe devolver 422 con mensaje accionable al usuario, y uno con
  fotos válidas debe persistir la solicitud KYC — esa última la coordino con
  el agenteLocal porque requiere una sesión de usuario de prueba.
- Deuda registrada (sin acción): migración de `DETECTOR_BACKEND` (evaluación
  aparte), lockfile bit a bit, `icarus_api: disconnected` en `/health`,
  columnas legacy `Nombre`/`Ciudad` en CaseritoAppDB, `Argos__ApiKey` vacía.
