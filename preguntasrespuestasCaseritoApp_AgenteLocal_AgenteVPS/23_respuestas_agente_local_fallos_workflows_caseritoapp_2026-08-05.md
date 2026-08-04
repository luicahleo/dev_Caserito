# Respuestas del agente local: fallos de workflows y despliegue seguro

**Fecha:** 2026-08-05  
**Commit técnico candidato:** `5c2bc4c`  
**Rama de producción:** `master`

## Diagnóstico

No se pudo demostrar que el código `139` fuera causado por una regresión nativa. La evidencia local sí demuestra una causa de arranque anterior al mensaje `Application started`: desde `2b957bc`, producción exige `Kyc__ClaveHuellaCi`; al ejecutar la imagen sin esa configuración, el host termina con `InvalidOperationException: La protección documental KYC no está configurada.` y código `1`. El `139` observado en el VPS requiere el `.State` y los primeros logs de aquel contenedor para atribuirlo a `SIGSEGV`; el workflow anterior destruía el candidato sin conservar esa evidencia.

No se deben volver a promover `2b957bc1...` ni `29d4f6b3...`. El nuevo candidato incorpora preflight de configuración, captura diagnóstica y validación aislada.

## Respuestas 1–13

1. No fue posible recuperar run ID, job o enlace: `gh` está instalado, pero no existe una sesión autenticada y el repositorio es privado. No se inventan identificadores.
2. El Deploy anterior se disparaba directamente por `push` a `master`; no dependía del resultado de CI. Por tanto, el YAML no garantiza que ambos commits estuvieran verdes antes de desplegar.
3. La reproducción local significativa fue la excepción genérica de configuración KYC indicada arriba. No se dispone del primer log del contenedor que terminó con `139`.
4. `2b957bc` introdujo el requisito KYC. `29d4f6b` solo integra correcciones de formato web respecto de ese commit; no cambia runtime, dependencias nativas ni Dockerfile.
5. El workflow anterior no capturaba `.State` ni los últimos logs antes del rollback. El corregido sí lo hace.
6. La reproducción por configuración falla antes de `Application started`. No prueba en qué punto exacto ocurrió el `139` del VPS.
7. Entre la versión estable y `2b957bc` cambiaron identidad visual, perfil/KYC y configuración asociada. Entre `2b957bc` y `29d4f6b` no existe un cambio backend que explique un fallo nativo.
8. No se agregó ni actualizó una dependencia nativa de imágenes, PDF, criptografía nativa, SQLite, ONNX, SkiaSharp o Playwright en el tramo investigado.
9. No cambiaron `RuntimeIdentifier`, `SelfContained`, AOT, trimming, ReadyToRun, arquitectura ni imagen base.
10. La publicación es framework-dependent dentro de la imagen Linux ASP.NET 10; no fija RID. Docker Desktop validó la misma imagen Linux que produce el workflow.
11. Se reprodujo construyendo el payload publicado y `Dockerfile.web`. En `Production` sin clave KYC falla de forma determinista; en `Testing` la imagen queda saludable.
12. Antes solo se probaba código en el runner. Ahora se construye y arranca la imagen final antes de cualquier paso SSH.
13. Se añadió smoke de `/health`, health interno y petición loopback con `Host: caserito.app`.

## Respuestas 14–30 y correcciones

14. Se corrigieron `.github/workflows/ci.yml`, `.github/workflows/deploy.yml`, `Dockerfile.web` y el contrato automatizado `deploy/deploy-contract.test.mjs`.
15. Deploy usa `workflow_run` de `CI`, únicamente cuando la conclusión es `success` y la rama es `master`.
16. La única rama de producción es `master`.
17. Se eliminó `workflow_dispatch`; ya no existe un bypass manual. El job conserva el Environment `production`.
18. `group: caseritoapp-production` y `cancel-in-progress: false` serializan los despliegues sin cancelar uno en curso.
19. Actions fijadas por SHA: `actions/checkout@11d5960a326750d5838078e36cf38b85af677262`, `actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9`, `actions/setup-node@49933ea5288caeca8642d1e84afbd3f7d6820020` y `webfactory/ssh-agent@dc588b651fe13675774614f8e6a936a468676387`.
20. El único permiso declarado es `contents: read`.
21. Job: 30 minutos; conexión SSH: 15 segundos; sincronización: 60 segundos; sesión remota completa: 15 minutos; candidato: 30 intentos de 5 segundos; HTTPS: 12 intentos de 5 segundos. El rollback se ejecuta dentro del límite remoto.
22. `VPS_SSH_KNOWN_HOSTS` se instala con modo `0600` y se verifica con `ssh-keygen -F`. No se desactiva `StrictHostKeyChecking`.
23. El candidato debe tener estado Docker `healthy` y responder por `127.0.0.1:18084/health` con `Host: caserito.app`; al agotarse los intentos, la función devuelve `1`.
24. Antes de promover se conserva `docker inspect --format '{{.Config.Image}}' caseritoapp`. El rollback solo acepta una etiqueta `caseritoapp:<SHA>`, recrea con esa etiqueta y vuelve a comprobar loopback y HTTPS.
25. El candidato usa `docker run --restart=no`; un crash no entra en ciclo y conserva el primer fallo.
26. Antes de eliminar un candidato fallido se guardan únicamente `docker inspect --format '{{json .State}}'` y `docker logs --tail 200`, con archivo `0600`. Nunca se inspecciona `.Config.Env`.
27. El candidato se llama `caseritoapp-candidate-<sha corto>`, usa el puerto exclusivo `127.0.0.1:18084` y no reemplaza al contenedor activo.
28. Orden aplicado: health interno, loopback con Host, promoción, health del activo y HTTPS público.
29. `caseritoapp:latest` y `current-release` se actualizan únicamente después de todas las validaciones.
30. No existe `docker system prune` ni limpieza global. La versión activa, la anterior y los diagnósticos quedan preservados.

## Compatibilidad de rollback

La migración `IdentidadPerfilContract` ya no elimina `Nombre` ni `Ciudad`: las conserva anulables para que el modelo actual pueda insertar usuarios y una imagen anterior pueda leerlas. Una migración compensatoria idempotente restaura ambas columnas si un intento previo alcanzó a eliminarlas.

## Evidencia de verificación

- Contrato de despliegue: 7/7 pruebas aprobadas.
- Backend: build Release, 0 advertencias y 0 errores.
- Unitarios: 375/375 aprobados.
- Arquitectura: 57/57 aprobados en la ejecución completa inicial.
- Integración: 219/219 aprobados después de la migración definitiva.
- Frontend: lint, typecheck y build aprobados; 45 archivos y 175 pruebas aprobadas.
- `dotnet format --verify-no-changes`: aprobado.
- Imagen Docker final: estado `running`, health `healthy`, `ExitCode: 0` y `/health` correcto por loopback.

El SHA técnico validado es `5c2bc4c`. Al integrarlo, GitHub desplegará el SHA resultante de `master`, que contiene el mismo payload más esta documentación. No se incluyeron secretos, tokens, valores de configuración, cadenas de conexión ni datos personales en esta respuesta.
