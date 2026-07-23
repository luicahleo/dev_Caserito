# Plan TDD — Chat 3D UI de participantes y notificación básica

Spec: `docs/superpowers/specs/2026-07-23-chat-3d-ui-participantes-notificacion-design.md`

## Reglas

- Ejecutar desde `web/`, salvo verificaciones de Git y entorno.
- Una prueba roja causal antes de cada bloque y verde dirigido después.
- Usar exclusivamente contratos OpenAPI y convertir `number | string` con
  `Number()`.
- No registrar ni mostrar contenido sensible o identificadores técnicos.
- No ampliar backend, Notifications ni alcance de Fase 6 sin evidencia y aprobación.

## 1. Completar adaptadores HTTP

**Prueba roja:** ampliar `src/api/chat.test.ts` para inicio, historial reciente e
histórico, envío idempotente, lectura y conversiones.

**Implementación:** ampliar `src/api/chat.ts` con tipos `Conversacion`,
`PaginaMensajes`, `iniciarConversacion`, `obtenerMensajes`,
`enviarMensaje` y `marcarLectura`.

**Verificación:**

```powershell
npm run test -- --run src/api/chat.test.ts
```

**Commit:** `feat(web): completa cliente de chat para participantes`

## 2. Contacto desde detalle

**Prueba roja:** ampliar `src/routes/DetalleAvisoPage.test.tsx` para autenticación,
aviso propio/ajeno, navegación en creación/reutilización y error.

**Implementación:** usar `useAuth`, detectar propiedad mediante `obtenerMiAviso` con
404 esperado y añadir la mutación de inicio.

**Verificación:**

```powershell
npm run test -- --run src/routes/DetalleAvisoPage.test.tsx
```

**Commit:** `feat(web): inicia chat desde avisos ajenos`

## 3. Bandeja y contador básico

**Prueba roja:** crear `src/routes/ConversacionesPage.test.tsx` y ampliar
`src/app/AppLayout.test.tsx`.

**Implementación:** crear bandeja paginada, resolver títulos de avisos con fallback,
añadir ruta protegida y un badge que sume hasta 50 conversaciones con refresco
periódico.

**Verificación:**

```powershell
npm run test -- --run src/routes/ConversacionesPage.test.tsx src/app/AppLayout.test.tsx
```

**Commit:** `feat(web): agrega bandeja y contador de mensajes`

## 4. Historial y lectura

**Prueba roja:** crear `src/routes/ConversacionPage.test.tsx` para recientes,
anteriores, vacío, error, roles relativos y lectura monotónica.

**Implementación:** crear pantalla responsive, combinar páginas sin duplicados y
marcar la secuencia máxima.

**Verificación:**

```powershell
npm run test -- --run src/routes/ConversacionPage.test.tsx
```

**Commit:** `feat(web): muestra historial paginado y lectura`

## 5. Envío idempotente y SignalR

**Prueba roja:** ampliar el test de conversación y las pruebas de
`sincronizacionMensajes`/`tiempoReal` para reintento, evento vivo, hueco y reconexión.

**Implementación:** conservar clave por intento, integrar el cliente SignalR,
mostrar desconexión, deduplicar, ordenar y recuperar.

**Verificación:**

```powershell
npm run test -- --run src/routes/ConversacionPage.test.tsx src/chat/tiempoReal.test.ts src/chat/sincronizacionMensajes.test.ts
```

**Commit:** `feat(web): integra envio y tiempo real en conversaciones`

## 6. Seguridad y reportes

**Prueba roja:** ampliar `ConversacionPage.test.tsx` para `puedeEnviar`, cierre,
reapertura, bloqueo, desbloqueo y objetivos de reporte.

**Implementación:** menú y diálogo accesibles; invalidar consultas y
suscribir/desuscribir según la acción confirmada.

**Verificación:**

```powershell
npm run test -- --run src/routes/ConversacionPage.test.tsx
```

**Commit:** `feat(web): expone seguridad y reportes del chat`

## 7. Integración

**Acciones:** revisar diff contra spec, asegurar textos españoles, anti-PII, rutas y
responsive. No regenerar OpenAPI si no cambió backend.

**Verificación frontend:**

```powershell
npm run typecheck
npm run lint
npm run test -- --run
npm run build
```

**Entorno:**

```powershell
.\rebuild.ps1
docker compose -f docker-compose.dev.yml ps
```

Ejecutar un flujo comprador-vendedor que cubra contacto, tiempo real, reconexión,
reporte, bloqueo y cierre.

**Repositorio:**

```powershell
git diff --check
git status --short --branch
```

**Commit:** `chore(chat): integra experiencia de participantes 3d`
