# Handoff — pruebas manuales de fases 1–3

Fecha: 2026-07-24  
Rama: `feat/pruebas-manuales-fases-1-3`

## Objetivo vigente

Completar en PC1 las pruebas manuales reproducibles de las fases 1–3. No
implementar Fase 4. No declarar las pruebas manuales superadas hasta que el
usuario confirme cada resultado observado.

## Estado verificado

- El bootstrap exclusivo de Development está implementado, opt-in e idempotente.
- `.env` local contiene la configuración sintética y está ignorado por Git.
- `.env.example` no contiene credenciales.
- SQL Server está saludable; API y web respondieron 200.
- Las tres cuentas bootstrap pudieron iniciar sesión:
  administrador con permisos administrativos; vendedor y comprador sin permisos
  administrativos; vendedor inicialmente sin KYC.
- Manualmente confirmado:
  - registro de otro usuario;
  - persistencia de perfil;
  - solicitud y aprobación KYC;
  - publicación y apertura de detalle;
  - contacto buyer-vendedor;
  - mensajes bidireccionales en tiempo real.
- Aún no confirmado:
  - lectura y contador de no leídos;
  - desconexión, reconexión y recuperación;
  - cierre y reapertura;
  - bloqueo y desbloqueo;
  - reportes de aviso, conversación, mensaje y contraparte;
  - moderación de avisos y chat.

## Cambios y commits

- `a9fe8d1` — spec del bootstrap y pruebas manuales.
- `1a397cd` — plan de ejecución.
- `9e8d568` — bootstrap seguro, configuración Compose y pruebas.
- `5206f01` — procedimiento manual en README.
- `e8850dd` — corrige detección de aviso ajeno: backend devuelve 403, no 404.
- `ea905e1` — añade proxy Vite `/hubs` con `ws: true` y prueba de configuración.

No se hizo push ni merge.

## Verificaciones ejecutadas

Backend:

- build correcto;
- 197 unitarias, 140 integración y 53 arquitectura: 390/390;
- `dotnet format --verify-no-changes` correcto.

Frontend tras el último arreglo:

- typecheck correcto;
- lint correcto;
- 29 archivos y 100/100 pruebas;
- build correcto.

Entorno:

- `.\rebuild.ps1` correcto;
- SQL Server saludable;
- API `/health` 200;
- web 200;
- negociación SignalR a través de Vite alcanza el Hub: sin token devuelve 401,
  no 404;
- `git diff --check` correcto y worktree limpio al cerrar.

## Continuación recomendada

1. Ejecutar descubrimiento inicial y verificar este handoff contra Git.
2. Confirmar contenedores y salud sin imprimir configuración sensible.
3. Retomar por contador/no leídos:
   - vendedor fuera de la conversación;
   - buyer envía dos mensajes;
   - verificar contador global y de bandeja;
   - abrir conversación y verificar contador en cero incluso tras recarga.
4. Probar DevTools Offline, mensajes durante desconexión, vuelta Online,
   recuperación ordenada y sin duplicados.
5. Probar cierre/reapertura y bloqueo/desbloqueo.
6. Crear los cuatro tipos de reporte previstos.
7. Cambiar a administrador y resolver moderación de avisos y chat.
8. Ante fallos, diagnosticar y corregir con TDD sin ampliar a Fase 4.
9. Al terminar, ejecutar suites proporcionales, actualizar documentación si
   cambió el procedimiento y eliminar este handoff.

## Seguridad

No leer ni imprimir valores de `.env`. No mostrar ni registrar correos,
contraseñas, tokens, documentos, imágenes, mensajes, reportes o IDs. Usar solo
datos sintéticos. Si se comparte una captura, pedir que oculte esos datos.
