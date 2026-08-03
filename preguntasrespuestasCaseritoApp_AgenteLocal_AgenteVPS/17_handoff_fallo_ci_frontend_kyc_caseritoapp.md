# Handoff — corrección del fallo CI frontend de CaseritoApp

**Fecha:** 2026-07-31  
**De:** agente del VPS  
**Para:** agente local de CaseritoApp  
**Repositorio:** `luicahleo/dev_Caserito`  
**Rama:** `master`  
**Objetivo:** corregir la única prueba frontend que deja el workflow CI en rojo.

## 1. Estado del despliegue

El workflow de despliegue ya fue corregido y terminó correctamente:

- Ejecución Deploy: `30616277171`
- Commit desplegado: `f696b34689c569eaf1497da7c8f0925891dab130`
- URL: `https://caserito.app`
- `GET /health`: HTTP 200, cuerpo `Healthy`
- Contenedor: saludable

Este handoff no solicita cambios en el despliegue ni en el VPS. El problema pendiente pertenece
exclusivamente al workflow **CI** y a una prueba frontend.

## 2. Ejecución afectada

```text
Workflow: CI
Run: 30616277273
Job: frontend
Job ID: 91110092408
Commit: f696b34689c569eaf1497da7c8f0925891dab130
```

Resultado de los jobs:

| Job | Resultado |
|---|---|
| Backend build + tests | Correcto |
| Contrato OpenAPI | Correcto |
| Frontend lint | Correcto |
| Frontend typecheck | Correcto |
| Frontend tests | Fallo: 1 de 134 |
| Frontend build | Omitido por el fallo anterior |

## 3. Error exacto

```text
FAIL src/api/kyc.test.ts
api/kyc > obtenerImagenKyc convierte el Blob en objectURL

TypeError: object.stream is not a function
  at src/api/kyc.test.ts:54:7
```

Resumen de Vitest:

```text
Test Files: 1 failed | 37 passed
Tests:      1 failed | 133 passed
```

La línea que falla construye esta respuesta simulada:

```ts
new Response(new Blob(['x'], { type: 'image/png' }), {
  status: 200,
  headers: { 'Content-Type': 'image/png' },
})
```

## 4. Diagnóstico

La prueba mezcla implementaciones de Web APIs de dos entornos:

- `Blob` proviene del entorno `jsdom`;
- `Response`/`fetch` en Node 22 se apoya en la implementación de Undici.

Undici intenta tratar el objeto recibido como su `Blob` compatible y llama a `object.stream()`.
El `Blob` de `jsdom` usado por la prueba no ofrece ese método con el contrato esperado. Por eso el
fallo ocurre al construir el mock `Response`, antes de probar realmente
`obtenerImagenKyc()`.

La evidencia apunta a una incompatibilidad del fixture de test, no a un fallo del endpoint KYC ni
de la implementación de producción.

## 5. Corrección solicitada

Corregir la prueba sin cambiar el comportamiento productivo de `obtenerImagenKyc`.

Opciones aceptables:

1. Simular la respuesta que devuelve `api.GET()`/`openapi-fetch` en el nivel apropiado,
   proporcionando directamente un `Blob` compatible como `data`.
2. Usar un cuerpo compatible con `Response` de Node/Undici, por ejemplo bytes mediante
   `Uint8Array`, y mantener la verificación de que `URL.createObjectURL()` recibe el blob.
3. Definir en el setup de Vitest implementaciones coherentes de `Blob`, `File`, `Request` y
   `Response`, siempre que no rompa las pruebas multipart existentes.

La solución debe conservar la intención de la prueba:

- una respuesta KYC HTTP 200 se interpreta como blob;
- se invoca `URL.createObjectURL()` con el blob obtenido;
- la función devuelve la URL generada;
- no se imprimen imágenes, contenido binario ni identificadores KYC sensibles.

No se debe:

- eliminar o saltar la prueba;
- reducir la cobertura;
- ocultar el error con `try/catch`;
- alterar el código de producción únicamente para acomodar una incompatibilidad artificial del
  entorno de test;
- usar imágenes o documentos reales.

## 6. Validaciones requeridas

Ejecutar localmente desde `web/`:

```bash
npm ci
npm run lint
npm run typecheck
npm run test
npm run build
```

Resultado esperado:

```text
Test Files: 38 passed
Tests:      134 passed
```

Después:

1. integrar la corrección en `master`;
2. confirmar el commit resultante;
3. comprobar que los tres jobs del workflow CI quedan verdes:
   - `build`;
   - `frontend`;
   - `contract`.

## 7. Advertencias secundarias observadas

No bloquean este arreglo, pero conviene tratarlas posteriormente:

- Algunos tests React muestran advertencias por actualizaciones no envueltas en `act(...)`.
- MUI informa valores fuera del rango disponible en algunos tests de formularios.
- GitHub advierte que varias acciones basadas en Node 20 están siendo forzadas a Node 24.

No mezclar estas limpiezas secundarias con la corrección mínima del test KYC salvo que resulte
necesario para dejar CI estable.

