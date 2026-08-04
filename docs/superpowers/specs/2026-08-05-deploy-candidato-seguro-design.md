# Despliegue con candidato seguro y diagnóstico previo

## Objetivo

Impedir que una imagen no arrancable sustituya la instancia estable de Caserito y garantizar que producción solo reciba commits con CI verde, imagen Linux validada y rollback trazable por SHA.

## Diagnóstico que motiva el cambio

- `2b957bc` introdujo `Kyc__ClaveHuellaCi` como configuración obligatoria en Production.
- La imagen final reproducida sin esa variable falla antes de `Application started` con un error genérico y código 1.
- El código 139 observado en el VPS no puede atribuirse sin el estado y los primeros logs del contenedor; el workflow anterior no los conservaba.
- `29d4f6b` no cambió backend, runtime ni Docker: reutiliza el mismo cambio funcional de KYC.
- Deploy se disparaba en paralelo a CI y reemplazaba el contenedor activo antes de validar la imagen.

## Flujo de CI y entrega

1. `CI` se ejecuta para cada push a `master` y debe completar backend, frontend y contrato.
2. `Deploy` se activa exclusivamente mediante `workflow_run` cuando `CI` concluye con éxito sobre `master`; no existe bypass manual.
3. Deploy publica el payload, construye la imagen final en Linux y ejecuta un smoke local sin secretos en entorno `Testing`.
4. Solo entonces sincroniza el release por SHA al VPS.

## Validación del candidato en el VPS

- Preflight verifica, sin imprimir valores, que `.env` contiene `Kyc__ClaveHuellaCi` con longitud suficiente.
- Se conserva la imagen activa y el SHA de `current-release` antes de cambiar estado.
- La imagen se construye como `caseritoapp:<SHA>`.
- Se inicia `caseritoapp-candidate-<SHA corto>` con:
  - `restart=no`;
  - puerto exclusivo en loopback;
  - misma red, `.env` y volúmenes persistentes;
  - migraciones habilitadas, una vez preservada la compatibilidad de rollback.
- La validación exige primero health interno y después HTTP loopback con `Host: caserito.app`.
- Ante fallo se guarda `docker inspect .State` y `docker logs --tail 200` antes de retirar el candidato. Nunca se inspecciona ni imprime `.Config.Env`.

## Promoción y rollback

- Solo un candidato saludable puede recrear el servicio activo mediante Compose.
- Tras promover se exige `https://caserito.app/health`.
- Si falla, se capturan diagnóstico operativo y logs, se recrea la imagen anterior exacta y se comprueba su salud.
- `latest` y `current-release` solo se actualizan después del health externo.
- No se ejecutan podas globales. Se conservan release e imagen activos, versión anterior y último fallo.

## Compatibilidad de base de datos

La migración contract que eliminaba `Nombre` y `Ciudad` se convierte en no destructiva antes de su primer despliegue exitoso. Una migración compensatoria agrega ambas columnas condicionalmente si algún intento previo alcanzó a eliminarlas. De este modo la imagen estable anterior puede volver a arrancar después de una migración del candidato.

## Seguridad

- `permissions: contents: read`.
- Actions externas fijadas a SHA completo.
- SSH usa `known_hosts` verificado; no se deshabilita la comprobación del host.
- Sin `set -x`, impresión de variables, `.env`, cadenas de conexión, tokens o configuración de contenedores.
- Los errores de preflight son genéricos y solo indican la clave ausente.

## Criterios de aceptación

- Las pruebas estáticas impiden Deploy sin CI verde, smoke Docker, candidato aislado, diagnóstico previo y `latest` tardío.
- La imagen final responde `/health` dentro de Docker Linux.
- Backend y frontend pasan sus suites.
- Ningún secreto real forma parte del diff o de la respuesta al agenteVPS.
