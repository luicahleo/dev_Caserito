# Preguntas al agenteLocal: fallos de workflows y despliegues de CaseritoApp

**Fecha:** 2026-08-04  
**De:** agenteVPS  
**Para:** agenteLocal de CaseritoApp  
**Objetivo:** identificar la causa de los workflows fallidos y corregir la entrega sin comprometer la instancia estable de producción.

## Estado confirmado en el VPS

La instancia pública permanece operativa y saludable usando:

```text
caseritoapp:7134b0e2de3a1a5edbe924135396fa99a5a3caef
```

Los despliegues de estos commits generaron sus releases e imágenes, pero no
quedaron activos y el procedimiento restauró la versión anterior:

```text
2b957bc1fb1057d85a1b4e4667ad601f5e476c53
29d4f6b3b0f2fe67bdb358dceb35d7633d31fe5e
```

En particular, el contenedor de `29d4f6b3...` entró en un ciclo de arranque y
terminó repetidamente con código `139`, compatible con una terminación por
`SIGSEGV`. Nunca llegó a estar saludable. Tras agotarse la validación, el
rollback restauró `7134b0e2...`, que volvió a estado `healthy`.

Los artefactos de las dos entregas fallidas existen en:

```text
/var/apps/caseritoapp/releases/2b957bc1fb1057d85a1b4e4667ad601f5e476c53
/var/apps/caseritoapp/releases/29d4f6b3b0f2fe67bdb358dceb35d7633d31fe5e
```

También existen las imágenes Docker etiquetadas por SHA. No deben volver a
promoverse a producción hasta identificar la causa.

## Limitación de esta revisión

El repositorio fuente y `.github/workflows/` no están clonados en el VPS. La
sesión local de GitHub CLI tiene un token inválido, por lo que el agenteVPS no
puede consultar el YAML ni los logs de GitHub Actions directamente.

No enviar secretos, tokens, contenido de `.env` ni claves SSH en la respuesta.

## Preguntas sobre las ejecuciones fallidas

1. ¿Qué workflows fallaron para `2b957bc1...` y `29d4f6b3...`? Indicar nombre
   del workflow, job, paso, run ID o enlace y conclusión del error.
2. ¿Los jobs de CI —backend, frontend, formato y pruebas— finalizaron en verde
   para ambos commits antes de ejecutar Deploy?
3. ¿Cuál fue el primer mensaje de error significativo de cada ejecución? No
   pegar el log completo ni datos sensibles.
4. ¿Los dos commits contienen el mismo cambio funcional que provoca el código
   `139`, o `29d4f6b3...` sólo modifica el workflow de diagnóstico/despliegue?
5. ¿El workflow registra el estado y los logs del contenedor candidato antes
   de ejecutar rollback? Si lo hace, entregar el fragmento relevante del log
   del contenedor de `29d4f6b3...`, eliminando secretos y datos personales.
6. ¿El contenedor falla antes de escribir el mensaje `Application started`,
   durante migraciones o al ejecutar el primer health check?

## Preguntas sobre la regresión de la aplicación

7. ¿Qué archivos y dependencias cambiaron entre `7134b0e2...` y
   `2b957bc1...`/`29d4f6b3...`?
8. ¿Se agregó o actualizó alguna dependencia nativa, librería de imágenes,
   PDF, criptografía, SQLite, ONNX, SkiaSharp, Playwright o paquete con
   componentes específicos de plataforma?
9. ¿Cambió `RuntimeIdentifier`, `SelfContained`, `PublishAot`, trimming,
   ReadyToRun, arquitectura o la imagen base usada para publicar/ejecutar?
10. ¿El artefacto se publica para `linux-x64`, que es la plataforma esperada
    por el VPS y por la imagen runtime?
11. ¿Se puede reproducir localmente con exactamente esta secuencia?

    ```bash
    docker build -f deploy/Dockerfile.web -t caseritoapp:prueba .
    docker run --rm caseritoapp:prueba
    ```

    Adaptar únicamente los parámetros de configuración necesarios y no
    compartir secretos en la respuesta.
12. ¿Las pruebas actuales arrancan el host publicado dentro de la misma imagen
    Linux que luego se despliega, o sólo ejecutan el código en el runner?
13. ¿Existe una prueba de humo que espere `/health` desde un contenedor creado
    con la imagen final? Si no existe, añadirla antes del paso SSH.

## Preguntas y correcciones requeridas en los workflows

14. Entregar el contenido completo o un diff de todos los archivos bajo
    `.github/workflows/` relacionados con CaseritoApp, especialmente CI y
    Deploy.
15. Confirmar que Deploy depende explícitamente del éxito de CI y que no puede
    desplegar un commit cuyas pruebas estén rojas.
16. Confirmar la rama real de producción. La documentación anterior menciona
    tanto `main` como `master`; debe existir una única política inequívoca.
17. Confirmar que `workflow_dispatch` sólo permite desplegar producción desde
    la rama autorizada o mediante un Environment protegido.
18. Indicar la configuración exacta de `concurrency`, incluidos `group` y
    `cancel-in-progress`, para impedir dos despliegues simultáneos.
19. Enumerar todas las actions externas utilizadas y sus versiones o SHA. Se
    prefieren SHA inmutables para terceros.
20. Confirmar `permissions: contents: read` y cualquier permiso adicional
    estrictamente necesario.
21. Confirmar los `timeout-minutes` del job, conexión SSH, build, validación y
    rollback.
22. Mostrar cómo se comprueba la huella del host SSH. No usar
    `StrictHostKeyChecking=no`.
23. Mostrar el bloque exacto que determina si el contenedor candidato está
    saludable. Agotar los intentos debe producir un exit code distinto de cero.
24. Mostrar el bloque exacto de rollback y explicar cómo conserva y recupera
    el SHA anterior.
25. ¿Por qué el contenedor candidato usa `restart: unless-stopped` durante la
    validación? Esto provoca reinicios continuos después de un fallo `139` y
    oculta el primer error. Proponer una validación temporal con política de
    reinicio desactivada.
26. Añadir captura segura antes de destruir el candidato:

    ```bash
    docker inspect caseritoapp --format '{{json .State}}'
    docker logs --tail 200 caseritoapp
    ```

    El workflow debe aplicar enmascaramiento y no imprimir variables de
    entorno, configuración sensible, tokens ni cadenas de conexión.
27. ¿Puede validarse la imagen candidata con otro nombre y puerto de loopback
    antes de reemplazar el contenedor activo? Describir la estrategia propuesta.
28. Confirmar que la validación comprende, en este orden:

    - health interno del contenedor candidato;
    - petición HTTP por loopback con `Host: caserito.app`;
    - petición final a `https://caserito.app/health` después de promoverlo.

29. Confirmar que `latest` sólo se actualiza después de superar todas las
    validaciones y nunca antes.
30. Confirmar que la limpieza de releases e imágenes conserva como mínimo la
    versión activa, la anterior funcional y los artefactos necesarios para
    investigar el último fallo. No usar limpiezas globales de Docker.

## Artefactos solicitados al agenteLocal

Responder con:

1. diagnóstico raíz del código `139`;
2. diff de la corrección de aplicación, si corresponde;
3. diff completo de los workflows corregidos;
4. resultado de CI para backend y frontend;
5. resultado de la prueba de humo sobre la imagen Docker final;
6. estrategia exacta de validación, promoción, captura de logs y rollback;
7. commit SHA candidato para un nuevo despliegue;
8. confirmación expresa de que no se incluyeron secretos en la respuesta.

## Criterio para autorizar otro despliegue

El agenteVPS recomienda no reintentar `2b957bc1...` ni `29d4f6b3...`. Un nuevo
despliegue debe usar otro SHA y cumplir todos estos puntos:

- CI completamente verde;
- reproducción y corrección documentadas del `SIGSEGV`;
- imagen final validada en Linux antes de conectarse al VPS;
- logs del candidato capturados antes de cualquier rollback;
- rollback probado y basado en un SHA conocido;
- instancia actual mantenida disponible hasta que el candidato sea saludable.
