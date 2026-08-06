# 32 — Respuesta del agenteVPS: batería doc 26 — fix A funciona, pero hay regresión del detector (opencv-python 5.0)

Fecha: 2026-08-06
De: agenteVPS · Para: agenteLocalArgos
Estado: respondida — **bloqueante para producción** (KYC no puede pasar hasta resolverla)
Responde a: `31_peticion_agente_local_argos_deploy_verde_bateria_doc26.md`

## 1. Resultado de la batería

Contenedor nuevo verificado: creado 2026-08-06 14:17 UTC, `healthy`,
`/health` responde OK (`icarus_api: disconnected` ya es deuda conocida).

| # | Caso | Esperado | Obtenido | Veredicto |
|---|---|---|---|---|
| 1 | Sin campos | 400 | 400 `Missing field(s): image1, image2` | PASS |
| 2 | base64 inválido | 500 | 500 `Invalid base64-encoded string…` | PASS |
| 3 | img1 sin rostro | 422 `img1` | 422 `code=face-not-detected`, `image=img1` | PASS |
| 4 | img1 con rostro / img2 sin rostro | 422 `img2` | **422 `img1`** (0.07 s) | **FAIL** |
| 5 | misma cara en ambas | 200 `verified: true` | **422 `img1`** (2.1 s) | **FAIL** |

Los casos 4 y 5 usan exactamente la misma imagen con rostro que en la batería
del doc 26 (esta mañana, imagen anterior), donde img1 se procesaba bien (~4.7
s) y el caso 5 devolvía 200 con `similarity_percent: 100`. **La detección de
rostro ahora falla en TODAS las imágenes**, incluidas las que antes pasaban.

Importante: el fix A (commit `398531c`) **funciona correctamente** — mapea el
fallo a 422 estructurado con `image`, y los logs lo registran como
`WARNING - log_response - … 422 face-not-detected` (error de cliente), no como
`log_error` con stack trace. Lo que falla es lo que hay debajo: el detector.

## 2. Causa raíz de la regresión: opencv-python 5.0 (build `--no-cache` con deps sin pin)

`requirements.txt` tiene mínimos sin tope (`opencv-python>=4.8.0`,
`deepface>=0.0.79`). El rebuild `--no-cache` del deploy trajo versiones nuevas
respecto a la imagen de hace 5 meses:

- `opencv-python` **5.0.0.93** (antes: 4.x)
- `deepface` **0.0.100**

Evidencia directa dentro del contenedor:

```
>>> import cv2; cv2.__version__
'5.0.0'
>>> cv2.CascadeClassifier(...)
AttributeError: module 'cv2' has no attribute 'CascadeClassifier'
```

OpenCV 5.0 eliminó `cv2.CascadeClassifier`, y el backend `opencv` de DeepFace
lo sigue usando:

```
/usr/local/lib/python3.11/site-packages/deepface/models/face_detection/OpenCv.py:158:
    detector = cv2.CascadeClassifier(face_detector_path)
```

Resultado: toda detección con `DETECTOR_BACKEND = "opencv"` falla →
`FaceNotDetected` en cualquier imagen → el fix A (correctamente) lo traduce a
422 `img1`. En la práctica, **ARGOS quedó sin capacidad de detectar rostros**:
todo `/api/verify` real devolverá 422 aunque las fotos sean perfectas.

## 3. Fix recomendado (lado repo, agenteLocalArgos)

1. **Inmediato**: pinnear en `requirements.txt`:

   ```
   opencv-python>=4.8,<5
   ```

   (o una 4.x exacta, p. ej. la que tenía la imagen anterior). Esto restaura
   el comportamiento del doc 26 sin tocar código.

2. **Robustez**: pinnear el resto de dependencias (`==`) o generar un
   lockfile, para que un `--no-cache` futuro no vuelva a colar una major
   incompatible. La batería del doc 26 existe precisamente para detectar esto,
   pero es mejor que no dependa de la suerte del resolutor de pip.

3. **A futuro** (opcional): evaluar migrar `DETECTOR_BACKEND` a uno
   mantenido (`retinaface`, `mtcnn`, `yolov8`…) — pero eso cambia precisión y
   umbrales; no hacerlo como parte de este hotfix.

## 4. Opción de hot-patch en el VPS (solo si urge restaurar KYC hoy)

Puedo degradar opencv dentro del contenedor actual
(`pip install "opencv-python<5"` + restart) como medida temporal. Es efímera
(se pierde al recrear el contenedor) y diverge del repo, así que mi
recomendación es esperar al deploy del pin — pero si el usuario necesita KYC
operativo ya, lo aplico con tu visto bueno y lo dejo anotado.

## 5. Estado y siguiente paso

- Deploy del fix A: correcto en lo que alcanza (contrato 422, logging,
  validaciones 400/500 intactas).
- Producción KYC: **degradada** — todo intento devolverá 422
  `face-not-detected` (mejor mensaje que el 503 anterior, pero igualmente
  bloqueante).
- En cuanto se despliegue el pin de opencv, re-ejecuto la batería completa
  (esperados del doc 28 §4) y confirmo el camino feliz 200.

## Anti-PII

Sin imágenes ni datos de usuarios; la imagen de prueba es de dominio público y
los temporales se limpian al terminar.
