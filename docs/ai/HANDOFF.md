# Handoff — preparación para Fase 4

Fecha: 2026-07-24

## Autorización y objetivo siguiente

El usuario autorizó comenzar la Fase 4 de CaseritoApp en una sesión nueva.
Aplicar `docs/ai/WORKFLOW.md` desde brainstorming. No implementar antes de
presentar el diseño y recibir aprobación.

## Estado de Git

- Rama actual: `feat/pruebas-manuales-fases-1-3`.
- La rama contiene el bootstrap Development, documentación de pruebas y dos
  correcciones encontradas manualmente.
- No se hizo push ni merge.
- El usuario confirmó que las pruebas manuales realizadas bastan por el momento
  y que Fase 4 debe continuar sobre los bugs ya corregidos.
- Tras verificar Git, usar el `HEAD` actual de esta rama como base para la rama
  dedicada del primer bloque de Fase 4. No volver a `master`, descartar commits
  ni repetir las pruebas manuales pendientes.
- No mergear ni pushear sin autorización explícita.

Commits propios de esta rama:

- `a9fe8d1` diseño del bootstrap;
- `1a397cd` plan del bootstrap;
- `9e8d568` bootstrap y pruebas;
- `5206f01` guía manual;
- `e8850dd` detección de aviso ajeno;
- `ea905e1` proxy SignalR en Vite;
- `84864f9` handoff temporal anterior;
- `aea8a6a` retirada del handoff temporal anterior.

## Estado funcional confirmado

- Automatizado: backend 390/390 antes de los dos arreglos web; frontend 100/100
  después de ambos arreglos; builds, formato, typecheck y lint correctos.
- Entorno reconstruido: SQL Server saludable, API y web 200.
- Manualmente confirmado: registro, perfil, KYC, publicación/detalle, contacto
  y mensajes bidireccionales en tiempo real.
- No se completaron manualmente no leídos, recuperación, seguridad ni
  moderación; el usuario decidió detener esas pruebas. No declararlas superadas.

## Seguridad local

`.env` contiene configuración sintética local y está ignorado. No leer, mostrar
ni registrar sus valores. `.env.example` permanece sin credenciales.

## Inicio recomendado de Fase 4

1. Ejecutar el descubrimiento inicial de `AGENTS.md`.
2. Leer reglas, workflow, plan MVP y solo specs/planes directamente vinculados.
3. Verificar este handoff contra Git y código actual.
4. Investigar el estado real de `Orders` y contratos preparados en Fase 0.
5. Delimitar un único bloque pequeño de Fase 4.
6. Presentar alternativas y un diseño breve; esperar aprobación.
7. Tras aprobar: spec, plan, TDD, integración y documentación.

Mantener fuera de alcance reputación, Fase 5/6 y cualquier custodia de dinero.
Las pruebas manuales pendientes de Fases 1–3 quedan diferidas por decisión del
usuario y no bloquean el inicio de Fase 4.
