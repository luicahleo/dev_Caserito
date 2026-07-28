# Checklist de seguridad — Piloto CaseritoApp

> Revisión realizada como parte del endurecimiento de la Fase 6.
> Fecha: 2026-07-27.

## 1. Autenticación y autorización en endpoints

- [x] Todos los grupos de endpoints definen explícitamente `RequireAuthorization()` o `AllowAnonymous`.
  - Verificado contando directivas en `src/Host/CaseritoApp.Host/Endpoints/`.
  - 41 ocurrencias de `RequireRateLimiting`, `RequireAuthorization` o `AllowAnonymous` distribuidas en 14 archivos.
- [ ] Revisión manual endpoint a endpoint para confirmar que no hay fugas de autorización (pendiente de auditoría final).

## 2. Rate limiting

- [x] Endpoints sensibles (auth, chat, orders, reputation, búsquedas guardadas) tienen políticas de rate limiting.
  - Políticas registradas en `Program.cs`: `chat-iniciar`, `chat-enviar`, `chat-consultas`, `chat-seguridad-acciones`, `orders-crear`, `orders-acciones`, `orders-consultas`, `reputation-crear`, `reputation-consultas`, `reputation-publico`, `busquedas-crear`.
- [ ] Revisión final de límites según tráfico esperado del piloto.

## 3. CORS y headers de seguridad

- [ ] No se detecta configuración explícita de CORS ni headers de seguridad (HSTS, CSP, X-Frame-Options, etc.) en `Program.cs`.
  - **Acción recomendada:** configurar CORS restrictivo y headers de seguridad en el reverse proxy (nginx/ingress) o en el host antes de producción.

## 4. Protección de PII

- [x] Reglas Anti-PII vigentes en todo el codebase: no se loggean emails, cuerpos de mensajes, IDs sensibles, contenido de reseñas, documentos ni tokens.
  - Revisión sintáctica no detecta patrones obvios de secretos hardcodeados en `src/Host/CaseritoApp.Host`.
- [ ] Auditoría manual final de mensajes de log y respuestas de error.

## 5. Dependencias vulnerables

- [x] `dotnet list package --vulnerable --include-transitive` ejecutado.
  - Resultado: ningún proyecto tiene paquetes vulnerables en los orígenes actuales.

## 6. Secretos y credenciales

- [x] Búsqueda automatizada de patrones `Password`, `Secret`, `Key`, `Token` con valores literales en `src/Host/CaseritoApp.Host` no arrojó coincidencias.
- [ ] Revisión manual de `appsettings*.json`, user-secrets y variables de entorno en el entorno de despliegue.

## 7. Health checks

- [x] Endpoint `/health` configurado con `MapHealthChecks` y checks de conexión a todos los `DbContext` del piloto.

## 8. Base de datos

- [x] Sin FK cruzadas entre bounded contexts.
- [x] Migraciones por contexto y schema.
- [ ] Rotación de credenciales de SQL Server antes del piloto real.

## Acciones pendientes antes del piloto

1. Configurar CORS y headers de seguridad.
2. Revisar manualmente todos los endpoints para autorización correcta.
3. Ajustar límites de rate limiting según métricas reales.
4. Rotar secretos y credenciales; confirmar que no quedan valores por defecto.
5. Ejecutar `dotnet list package --vulnerable` periódicamente en CI.
