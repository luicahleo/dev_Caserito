# Diseño — Resolución automática de KYC

Fecha: 2026-09-19
Bloque: 1 de 4 de la serie «verificación al contactar».

## 1. Contexto y objetivo

Hoy la verificación de identidad solo se resuelve automáticamente cuando ARGOS
dice que el rostro **no** coincide. Si coincide, la solicitud queda
`pendiente-revision` esperando a que un administrador la apruebe a mano en
`/admin/kyc`; y si ARGOS no puede procesar las imágenes o está caído, el envío
falla con un error duro y no queda constancia de que el usuario lo intentó.

Eso hace que verificarse tarde horas. Esta serie de bloques mueve el momento en
que se exige la verificación al instante en que un comprador quiere contactar a
un vendedor, y ese diseño solo funciona si la aprobación es inmediata para el
caso normal. Este bloque construye esa inmediatez.

**Objetivo**: que una solicitud de KYC se resuelva sola —aprobada o rechazada—
cuando la evidencia es clara, y que todo lo demás quede en una cola de revisión
humana con el motivo registrado, sin que ningún intento se pierda.

### Serie completa (contexto, no alcance de este spec)

1. **Este bloque**: umbral de auto-aprobación, zona de revisión y resolución
   automática.
2. Gate de verificación al iniciar conversación y conversación retenida hasta
   que la verificación se resuelve.
3. Aviso al administrador de que hay solicitudes esperando (correo + contador en
   el panel).
4. Distintivo de verificado visible en listado, detalle de aviso y chat.

## 2. No objetivos

- No se toca el gate de publicar aviso: el vendedor sigue necesitando estar
  verificado (`CrearAvisoCommand`), y eso no cambia.
- No se toca el gate de `SolicitarOrdenCommand`.
- No se retienen conversaciones ni mensajes (bloque 2).
- No se avisa al administrador (bloque 3).
- No se muestra el motivo de revisión en `AdminKycPage` (bloque 3, que ya toca
  esa pantalla).
- No se cambia el modelo de comparación facial, ni el umbral interno de ARGOS,
  ni se hace la llamada asíncrona.
- No se implementa OCR, liveness ni verificación de teléfono.

## 3. Datos de partida verificados

De `ARGOS/views.py:167-169` (proyecto ARGOS local, mismo código que el VPS):

```python
distance = result["distance"]                    # distancia coseno, ArcFace
verified = distance <= VERIFICATION_THRESHOLD    # 0.68
similarity = max(0, (1 - distance) * 100)
```

Consecuencias que el diseño asume:

- `similarity_percent = (1 − distancia_coseno) × 100`, acotado inferiormente a 0.
- El corte propio de ARGOS (`distance ≤ 0.68`) equivale a `similarity ≥ 32`.
  La escala no es intuitiva: 32 ya es «coincide», y un match legítimo fuerte
  ronda 60–80. Un umbral fijado por intuición en 85–90 no aprobaría a nadie.
- Todo fallo de DeepFace sale como HTTP 500 con `success: false`, y el mensaje
  distingue qué imagen falló. El adaptador ya lo traduce a
  `ErroresKyc.RostroNoDetectado` frente a `ErroresKyc.ServicioVerificacionNoDisponible`.
- El detector `opencv` falla con frecuencia ante fotos de cédula (plastificada,
  con holograma y reflejos), según la caracterización del agente VPS en
  `26_respuesta_agente_vps_pruebas_argos.md`. El camino «ARGOS no pudo
  procesar» no es excepcional: es esperable.
- Una verificación tarda ~4.7 s en CPU.

## 4. Decisiones

1. **El corte inferior lo sigue decidiendo ARGOS.** La app no duplica el umbral
   de no-coincidencia: reutiliza `verified`, ya calibrado en 0.68. Solo añade un
   umbral superior propio para la auto-aprobación. Un parámetro nuevo, no dos.
2. **La regla vive en el dominio**, en un tipo puro y sin dependencias, no en el
   handler ni en el adaptador HTTP. Motivos: se prueba con tests unitarios sin
   fakes ni I/O, y la política de negocio no puede vivir en Infrastructure
   (`CaseritoApp/AGENTS.md`).
3. **El umbral es configuración, no código.** Entra por un puerto de
   Application implementado en Infrastructure sobre `OpcionesArgos`.
   `Identity.Application` no referencia `Microsoft.Extensions.Options` y no debe
   empezar a hacerlo.
4. **Ningún intento se pierde.** Cualquier fallo técnico de ARGOS deja la
   solicitud encolada para revisión humana, conservando las imágenes, y el
   usuario recibe una respuesta de éxito con estado «en revisión», no un error.
5. **La auto-aprobación reutiliza el camino existente**: publica los mismos
   eventos que la aprobación manual, para que el correo al usuario y el badge
   salgan por código ya probado.
6. **Valor inicial del umbral: 60** (equivale a distancia 0.40), notablemente
   más estricto que el 32 de ARGOS. Es una estimación informada, no una
   medición; queda pendiente calibrarla (§10).

## 5. Comportamiento

Único caso de uso afectado: `EnviarSolicitudKycCommand`.

| Resultado de ARGOS | Resolución | Estado | `ResueltaPor` | `ScoreSimilitud` | `MotivoRevision` |
|---|---|---|---|---|---|
| `verified`, score ≥ umbral | Aprobación automática | `Aprobada` | `SistemaActor.Id` | score | `null` |
| `verified`, score < umbral | A revisión | `Pendiente` | `null` | score | `ScoreInsuficiente` |
| `verified: false` | Rechazo automático | `Rechazada` | `SistemaActor.Id` | score | `null` |
| Rostro no detectado | A revisión | `Pendiente` | `null` | `null` | `RostroNoDetectado` |
| Servicio caído o error | A revisión | `Pendiente` | `null` | `null` | `ServicioNoDisponible` |

El límite es **inclusivo**: un score exactamente igual al umbral aprueba.

`MotivoRevision` es un registro histórico del porqué del encolado: **no se
limpia** cuando el administrador resuelve la solicitud. Queda junto a
`MotivoRechazo`, `ResueltaPor` y `ResueltaEn` como parte de la traza de esa
solicitud.

La reserva del CI (paso 8 de §5.1) usa `ReservarDocumentoAsync` tal como existe,
sin cambios: en ese punto ya se sabe que la huella no es de otro usuario, así que
su comprobación interna resulta redundante pero inofensiva, y mantenerla evita
una condición de carrera entre la comprobación del paso 3 y la escritura.

Efectos de la aprobación automática, idénticos a los de
`AprobarSolicitudKycCommand`:

- publica `UserVerified` por `IPublicadorEventosIntegracion`;
- publica `KycResuelto` por `IPublisher`, lo que dispara el correo al usuario a
  través de `NotificarKycResueltoHandler`, ya existente.

Las ramas de revisión y de rechazo automático no publican `UserVerified`. El
rechazo automático conserva el comportamiento actual, incluido su motivo.

### 5.1 Orden de operaciones del handler

El orden actual reserva el CI antes de llamar a ARGOS, lo que provoca el defecto
de §6. El orden nuevo es:

1. validar imágenes (ya lo hace el validator);
2. proteger el CI y calcular la huella;
3. comprobar que la huella no pertenece a **otro** usuario; si pertenece,
   `Result.Fallo` sin escribir nada. Esta comprobación **no** reserva: requiere
   un método nuevo de solo lectura en `IRepositorioVerificacionKyc`,
   `HuellaPerteneceAOtroUsuarioAsync(byte[] huella, Guid usuarioId, CancellationToken ct)`,
   porque el `ReservarDocumentoAsync` actual comprueba y hace `db.Add` en la
   misma llamada, y aquí esas dos cosas ocurren en momentos distintos;
4. guardar los blobs de documento y selfie;
5. llamar a ARGOS;
6. crear la solicitud en el agregado (`EnviarSolicitud`); si el dominio la
   rechaza, borrar los blobs y devolver fallo **sin** haber reservado el CI;
7. registrar score y motivo de revisión según la política;
8. reservar el CI;
9. resolver según la política (aprobar, dejar pendiente o rechazar);
10. devolver éxito.

La reserva ocurre solo cuando la solicitud se persiste de verdad.

## 6. Defecto corregido en este bloque

`UnitOfWorkBehavior` invoca `GuardarCambiosAsync` **siempre**, sin inspeccionar
si el `Result` fue éxito o fallo:

```csharp
var respuesta = await next();
foreach (var unidad in unidadesDeTrabajo)
    await unidad.GuardarCambiosAsync(cancellationToken);
```

Como `ReservarDocumentoAsync` hace `db.Add` y se llama antes de ARGOS
(`EnviarSolicitudKycCommand.cs:60`), cualquier fallo posterior devolvía
`Result.Fallo`, borraba los blobs… y persistía la reserva del CI igual.

Impacto: un CI queda reservado por un usuario que nunca completó la
verificación. El propio usuario puede reintentar, porque la reserva compara
`UsuarioId`; pero si el número no era suyo —un error de tipeo, o el CI de un
tercero—, el dueño legítimo recibe «documento en uso» de forma permanente y no
puede verificarse nunca.

Ningún test cubre esto hoy: `ReservarDocumento` y `DocumentoEnUso` no aparecen
en la suite, y el test de ARGOS caído
(`EnviarSolicitudKycCommandHandlerTests.cs:158`) usa un repositorio falso, por lo
que no atraviesa EF ni el behavior.

**No se modifica `UnitOfWorkBehavior`**: cambiar su semántica afectaría a todos
los bounded contexts y excede este bloque. Se corrige el orden en el handler,
que es donde está el error.

## 7. Componentes

### Nuevos

| Ruta | Contenido |
|---|---|
| `src/Identity/CaseritoApp.Identity.Domain/Kyc/ResolucionKyc.cs` | `enum { AprobarAutomatico, EnviarARevision, RechazarAutomatico }` |
| `src/Identity/CaseritoApp.Identity.Domain/Kyc/MotivoRevisionKyc.cs` | `enum { ScoreInsuficiente, RostroNoDetectado, ServicioNoDisponible }` |
| `src/Identity/CaseritoApp.Identity.Domain/Kyc/PoliticaResolucionKyc.cs` | `EntradaResolucionKyc(bool ServicioRespondio, bool RostroDetectado, bool Coinciden, double? Score)` y `Decidir(entrada, double umbral)` que devuelve resolución y motivo. Puro: sin I/O, sin configuración, sin logging |
| `src/Identity/CaseritoApp.Identity.Application/Kyc/IOpcionesResolucionKyc.cs` | Puerto: `double UmbralAutoAprobacionSimilitud { get; }` |
| `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/OpcionesResolucionKycDesdeConfig.cs` | Implementa el puerto sobre `OpcionesArgos` |
| Migración `KycMotivoRevision` | Solo la columna nueva |

### Modificados

- `Identity.Domain/Kyc/SolicitudKyc.cs`: propiedad `MotivoRevision`
  (`MotivoRevisionKyc?`) y `RegistrarMotivoRevision(MotivoRevisionKyc motivo)`,
  en el estilo del `RegistrarScoreSimilitud` existente.
- `Identity.Application/Kyc/EnviarSolicitudKycCommand.cs`: reordenar según
  §5.1; traducir el `Result<VerificacionFacialResultado>` de ARGOS a
  `EntradaResolucionKyc` distinguiendo `ErroresKyc.RostroNoDetectado` de
  `ErroresKyc.ServicioVerificacionNoDisponible`; aplicar la política; publicar
  eventos en la rama de aprobación.
- `Identity.Infrastructure/Kyc/OpcionesArgos.cs`: propiedad
  `UmbralAutoAprobacion` (`double`, default 60).
- `Identity.Infrastructure/Kyc/ConfiguracionKyc.cs`: mapear el enum con
  `HasConversion<string>().HasMaxLength(30)`, como `Estado` y `TipoDocumento`
  (líneas 43-44). Nullable, sin `IsRequired`.
- `Identity.Infrastructure/DependencyInjection.cs`: registrar el puerto.
- `Identity.Application/Kyc/DtosKyc.cs`: `MotivoRevision` (`string?`) en
  `SolicitudKycResumenDto`, junto al `ScoreSimilitud` que ya expone.
- `Identity.Infrastructure/Kyc/RepositorioVerificacionKycEfCore.cs`: proyectar
  el campo nuevo.
- `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json` y
  `web/src/api/schema.d.ts`: regenerados por el job `contract`.

### Configuración

```dotenv
Argos__UmbralAutoAprobacion=60
```

Se añade a las plantillas de entorno correspondientes. Sin la variable, el
default del código es 60. No es un secreto y puede figurar en compose.

## 8. Contrato HTTP y respuestas

`POST /api/kyc` sigue devolviendo lo mismo que hoy en el camino de éxito, e
incluye ahora el caso de encolado por fallo técnico, que antes era un error. El
cliente distingue el resultado consultando el estado, como ya hace.

Errores que permanecen:

- huella de CI perteneciente a otro usuario → fallo de documento en uso;
- solicitud inválida según el agregado (por ejemplo, usuario ya aprobado) →
  fallo de transición;
- validación de imágenes → fallo de validación.

Lo que deja de ser error: rostro no detectado y servicio no disponible.

## 9. Seguridad y PII

- Se mantiene `RegistrarEnvio(logger, usuarioId, resultado)`, ampliando los
  valores de `resultado` a: `aprobado-automatico`, `pendiente-score`,
  `pendiente-rostro-no-detectado`, `pendiente-servicio`,
  `rechazado-automatico`.
- **El score no se escribe en logs.** Es un dato biométrico derivado y
  vincularlo a un `usuarioId` en logs viola la política anti-PII del proyecto.
  Vive únicamente en la columna `ScoreSimilitud`, prevista para auditoría.
- El motivo de revisión sí puede loguearse: es una categoría técnica que no
  describe a la persona.
- Sin cambios en el resto: ni claves de blob, ni número de CI, ni nombre, ni
  bytes, ni content-type en logs o mensajes de error.
- La auto-aprobación queda auditada por `ResueltaPor = SistemaActor.Id`, que
  permite distinguirla de una aprobación humana en cualquier revisión posterior.

## 10. Diferidos y riesgos

- **Calibración del umbral.** El valor 60 es estimación. Se solicita al agente
  VPS la distribución de `ScoreSimilitud` de las solicitudes ya resueltas en
  producción, agrupada por resolución y sin identidades, para fijarlo con
  evidencia. Al ser configuración, ajustarlo no requiere recompilar ni
  redesplegar la imagen.
- **Propagación del claim `verificado`.** Viaja dentro del JWT y solo se
  actualiza al refrescar el token. En este bloque es inocuo; en el bloque 2, con
  el gate en el contacto, un usuario recién aprobado vería la app diciéndole que
  no está verificado. Debe resolverse allí.
- **Latencia.** La llamada a ARGOS sigue siendo sincrónica (~4.7 s en CPU). No
  empeora respecto a hoy, pero marca el techo de lo que se puede prometer al
  usuario en el bloque 2.
- **Carga de revisión.** Encolar los fallos técnicos aumenta el volumen de
  revisión manual respecto a hoy, donde simplemente se perdían. Es intencional:
  antes esos usuarios quedaban sin atención.
- `UnitOfWorkBehavior` guarda sin mirar el `Result`. Queda documentado como
  deuda transversal; no se aborda aquí.

## 11. Pruebas

### Unitarias de la política (`PoliticaResolucionKyc`)

- score por encima del umbral con `verified` → `AprobarAutomatico`;
- score **exactamente igual** al umbral → `AprobarAutomatico` (fija el límite
  inclusivo para que nadie lo invierta después);
- score por debajo del umbral con `verified` → `EnviarARevision` con
  `ScoreInsuficiente`;
- `verified: false` → `RechazarAutomatico`, con score alto y bajo;
- rostro no detectado → `EnviarARevision` con `RostroNoDetectado` y score nulo;
- servicio no disponible → `EnviarARevision` con `ServicioNoDisponible` y score
  nulo.

### Unitarias del handler (con fakes)

- aprobación automática publica `UserVerified` **y** `KycResuelto`;
- encolado no publica ninguno de los dos;
- rechazo automático no publica `UserVerified`;
- en la rama de encolado por fallo técnico los blobs **no** se eliminan;
- el resultado devuelto al llamador es éxito en las tres ramas de resolución.

### Integración (Testcontainers, pipeline real)

- **Regresión del defecto de §6**: cuando el agregado rechaza la solicitud, la
  tabla de documentos registrados no contiene la huella. Es el único test capaz
  de detectarlo, porque el problema está en el behavior y no se ve con fakes.
- **Camino feliz**: con un verificador falso que devuelve score alto, la
  solicitud queda `Aprobada` por `SistemaActor` y `GET /api/perfil` ya informa
  al usuario como verificado.
- **Encolado**: con un verificador falso que devuelve
  `ServicioVerificacionNoDisponible`, la solicitud queda `Pendiente` con
  `MotivoRevision = ServicioNoDisponible`, el endpoint responde éxito y los
  blobs siguen existiendo.

## 12. Criterios de aceptación

1. Un envío con `verified` y score ≥ umbral queda `Aprobada` con
   `ResueltaPor = SistemaActor.Id`, sin intervención humana, y dispara el correo
   al usuario.
2. Un envío con `verified` y score < umbral queda `Pendiente` con
   `MotivoRevision = ScoreInsuficiente`.
3. Un envío en el que ARGOS no detecta rostro o no responde queda `Pendiente`
   con el motivo correspondiente, conserva las imágenes y devuelve éxito al
   usuario.
4. `verified: false` sigue produciendo rechazo automático.
5. Ningún camino de fallo deja el CI reservado.
6. El umbral se modifica por variable de entorno, sin recompilar.
7. Ningún log contiene score, CI, nombre ni clave de blob.
8. El contrato OpenAPI y el cliente TypeScript quedan regenerados y sin deriva
   en el job `contract`.
9. `./verify.ps1 -Changed` en verde.
