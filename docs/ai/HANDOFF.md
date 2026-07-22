# Handoff de sesión

## Objetivo

Completar Fase 3, sub-bloque 3C: seguridad, reportes, bloqueo, cierre y
moderación de conversaciones, siguiendo TDD y sin incorporar 3D ni Fase 4.

## Rama y estado verificado

- Rama: `feat/chat-3c-seguridad-moderacion`.
- `origin/feat/chat-3c-seguridad-moderacion` permanece en `786bc10`.
- La rama local está dos commits por delante del remoto; no se hizo push,
  merge ni rebase.
- Al cerrar las tareas 2 y 3 no había cambios de producto pendientes. Este
  handoff es el único cambio posterior a esos commits.

## Spec y plan activos

- `docs/superpowers/specs/2026-07-20-chat-3c-seguridad-moderacion-design.md`
- `docs/superpowers/plans/2026-07-20-chat-3c-seguridad-moderacion.md`

## Completado

- Tarea 1: estados y transiciones de conversación (`ae51597`).
- Tarea 2: bloqueo dirigido global (`0872897`):
  - `BloqueoUsuario` dirigido y válido solo entre usuarios distintos;
  - comandos idempotentes que derivan la contraparte desde la conversación;
  - solo el bloqueador retira su bloqueo;
  - iniciar y enviar consultan bloqueos en cualquier dirección;
  - falta de pertenencia conserva el `404` genérico y el bloqueo usa
    `chat_conversacion_no_disponible_para_envio` sin revelar dirección.
- Tarea 3: reportes y auditoría de moderación en dominio (`034d258`):
  - tipos de objetivo, siete categorías y cuatro estados;
  - objetivo discriminado, detalle normalizado de hasta 1000 caracteres;
  - toma exclusiva, liberación y resolución solo por moderador asignado;
  - estados finales terminales;
  - siete acciones y registro append-only sin contenido ni participantes.

## Verificaciones ejecutadas

- Tarea 2: filtro previsto, 17/17 pruebas verdes.
- Tarea 3: filtro previsto, 7/7 pruebas verdes.
- `dotnet format CaseritoApp.sln --no-restore` aplicado; ambos commits pasaron
  el hook `dotnet-format-staged`.
- `git diff --check` verde antes de cada commit.
- Revisión con `rg`: no se añadieron logs, excepciones con argumentos, payloads,
  tokens, texto ni campos de participantes en auditoría.
- No se ejecutaron suites de integración ni se comprobó Docker porque las
  tareas cerradas fueron de dominio/Application.

## Tarea exacta siguiente

Tarea 4 del plan: `Persistencia SQL Server y migración`.

### Próximo test rojo exacto

En `CaseritoApp/tests/CaseritoApp.IntegrationTests/ChatPersistenciaTests.cs`,
añadir primero un único test `Modelo_configura_bloqueos_en_schema_chat` que:

1. resuelva `ChatDbContext` desde la fábrica;
2. busque `BloqueoUsuario` en `db.Model`;
3. afirme tabla `BloqueosUsuario`, schema `chat`;
4. afirme índice único ordenado `(BloqueadorId, BloqueadoId)` e índices de
   consulta en cada dirección.

Ejecutar:

```powershell
dotnet test tests/CaseritoApp.IntegrationTests --filter FullyQualifiedName~ChatPersistenciaTests.Modelo_configura_bloqueos_en_schema_chat
```

Rojo causal esperado: `BloqueoUsuario` no está incluido en el modelo de EF.
Implementar solo el `DbSet` y `ConfiguracionSeguridadChat` mínimos hasta verde;
después continuar, un rojo por vez, con estado/default/concurrencia de
`Conversacion`, `ReporteChat`, unicidad filtrada de abiertos y
`RegistroModeracionChat`.

## Restricciones que siguen vigentes

- Anti-PII: nunca registrar texto, payloads, tokens, IDs sensibles,
  participantes, grupos, claves de idempotencia ni argumentos.
- Mantener errores genéricos e indistinguibilidad para participantes.
- No añadir 3D, Fase 4 ni capacidades excluidas por el spec.
- No hacer push, merge ni rebase sin autorización explícita.
