# Plan: despliegue con candidato seguro

## 1. Contratos rojos del workflow

- Ampliar `deploy/deploy-contract.test.mjs` para exigir `workflow_run` de CI exitoso, ausencia de `workflow_dispatch`, imagen smoke, candidato separado, `restart=no`, preflight KYC, captura de estado/logs, orden de health checks y promoción tardía.
- Ejecutar `node --test deploy/deploy-contract.test.mjs`; debe fallar contra el workflow anterior.

## 2. Compatibilidad de rollback de esquema

- Convertir `20260804161339_IdentidadPerfilContract` en migración no destructiva.
- Añadir migración idempotente que restaure `Nombre` y `Ciudad` si un intento anterior las eliminó.
- Añadir prueba de migraciones que verifique ausencia de `DropColumn` en la ruta pendiente.
- Verificar build, formato y prueba dirigida.

## 3. CI y smoke de imagen final

- Fijar actions de `.github/workflows/ci.yml` a SHA completo.
- Cambiar Deploy a `workflow_run` exitoso de `CI` en `master`.
- Construir y probar `caseritoapp:<SHA>-runner` antes de configurar SSH.
- Capturar estado/logs del smoke solo al fallar y limpiar el contenedor temporal.

## 4. Candidato remoto y promoción

- Validar configuración KYC sin imprimirla.
- Levantar candidato con nombre/puerto separados y sin política de reinicio.
- Validar health interno y loopback; guardar diagnóstico antes de retirarlo.
- Promover con Compose, validar HTTPS y actualizar `latest/current-release` al final.
- Restaurar la imagen anterior por SHA y comprobar rollback ante cualquier fallo posterior a promoción.

## 5. Respuesta al agenteVPS

- Crear respuesta junto al documento 23 con diagnóstico probado, límites de evidencia, diff funcional, comandos de verificación, estrategia y SHA candidato.
- Declarar expresamente que no contiene secretos.

## 6. Integración

- Ejecutar contrato de deploy, backend build/test/format, frontend typecheck/lint/test/format/build y smoke Docker final.
- Revisar `git diff --check`, anti-PII y archivos modificados.
