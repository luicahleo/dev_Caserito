# Diseño — Aviso al administrador de solicitudes KYC en espera

Fecha: 2026-09-24
Serie: «verificación al contactar», bloque 3
Depende de: `docs/superpowers/specs/2026-09-19-kyc-resolucion-automatica-design.md`

## 1. Contexto y objetivo

El bloque 1 introdujo la resolución automática: ARGOS decide y solo las
solicitudes sin evidencia concluyente quedan esperando revisión humana. Ese
cambio dejó un punto ciego: cuando una solicitud cae en revisión manual, **nadie
se entera**. `EnviarSolicitudKycCommandHandler` publica eventos únicamente en la
aprobación automática; la rama de revisión manual es un `_ => Result.Exito()`
silencioso, y `/admin/kyc` ni siquiera aparece en el menú de la aplicación web:
se llega por un enlace en el perfil.

El objetivo es que una solicitud en espera avise por dos vías: un correo
inmediato a un buzón de administración y un contador visible para quien puede
revisarla.

### Serie completa (contexto, no alcance)

1. Resolución automática de KYC — cerrado.
2. Conversación retenida, backend y UI web — cerrado.
3. Aviso al administrador de solicitudes en espera — **este spec**.
4. Distintivo de verificado en listado, detalle y chat — pendiente.

## 2. No objetivos

- Recordatorios, reintentos o escalado por antigüedad si el aviso se ignora.
- Avisos de solicitudes resueltas automáticamente: no requieren a nadie.
- Notificación en tiempo real hacia el administrador.
- Cualquier cambio en la política de resolución o en el umbral de ARGOS.
- El bloque 4.

## 3. Decisiones

1. **Un correo por solicitud que queda pendiente.** El volumen es bajo
   (producción tenía ≤ 2 solicitudes cuando se consultó). Se descartó el resumen
   periódico porque no existe ningún `BackgroundService` en la solución y habría
   que introducir el primero; y el antirrebote, porque exige persistir la marca
   del último envío para un problema que todavía no existe.
2. **Evento de dominio nuevo, consumido por un handler propio.**
   `SolicitudKycEnEspera` se publica por `IPublisher` en la rama de revisión
   manual y `NotificarSolicitudKycEnEsperaHandler` envía el correo. Es simétrico
   a `KycResuelto` / `NotificarKycResueltoHandler`: el comando no sabe de correo
   y un fallo de SMTP no afecta al envío de la solicitud. Se descartó un evento
   de integración porque el consumidor vive en el mismo bounded context.
3. **Buzón único por configuración, desactivable.** Una opción nueva,
   `Kyc__EmailAvisos`, expuesta a `Application` mediante el puerto
   `IOpcionesAvisosKyc` —el mismo patrón que `IOpcionesResolucionKyc`, que existe
   porque Application no referencia paquetes de configuración—. Con el valor
   vacío el handler no envía: desarrollo y test no necesitan buzón. Se descartó
   notificar a todos los usuarios con `kyc.revisar` (mete una consulta de correos
   en el camino del envío y multiplica mensajes) y reutilizar
   `SeedSettings:AdminEmail` (existe para sembrar el admin inicial; mezclar
   propósitos ata los avisos a una cuenta concreta).
4. **El correo es genérico.** Anuncia que hay una solicitud esperando revisión y
   enlaza al panel. Sin nombre, CI, score, motivo ni identificadores. El score ya
   se trata en el código como dato biométrico derivado y se excluye incluso de la
   auditoría (`EnviarSolicitudKycCommand.cs`, `EtiquetaAuditoria`).
5. **El contador reutiliza el listado existente.** `ResultadoPaginado<T>` ya
   expone `Total` y `GET /api/admin/kyc?estado=Pendiente` ya está protegido por
   `Permisos.KycRevisar`. No hace falta endpoint nuevo ni cambio de contrato.

## 4. Comportamiento

### 4.1 Correo

Cuando `PoliticaResolucionKyc.Decidir` devuelve `EnviarARevision`,
`EnviarSolicitudKycCommandHandler` publica `SolicitudKycEnEspera` por
`IPublisher`, junto a la rama que ya publica `UserVerified` y `KycResuelto` en la
aprobación automática.

`NotificarSolicitudKycEnEsperaHandler` lo consume y:

| Situación | Efecto |
|---|---|
| Buzón configurado | Envía el correo al buzón y lo registra en `Information` |
| Buzón vacío o en blanco | No envía nada; lo registra en `Debug` |
| `IServicioCorreo` lanza | Registra en `Error` y no propaga |

El cuerpo enlaza a `{OpcionesApp.UrlPublica}/admin/kyc`, la misma fuente que ya
usa `EnviarConfirmacionEmailHandler` para el enlace de confirmación.

### 4.2 Contador

El menú de cuenta gana una entrada «Verificaciones» hacia `/admin/kyc`, visible
solo con el permiso `kyc.revisar`, con un `Badge` como el de mensajes no leídos.
El número es el `total` de `listarSolicitudesKyc('Pendiente', 1, 1)`.

Refresco: `refetchInterval` de 300 000 ms (5 min) y `refetchOnWindowFocus`. Sin
tiempo real: no existe canal SignalR hacia el administrador y el correo ya es el
aviso inmediato. El badge es una referencia de estado, no una alarma.

## 5. Componentes

| Archivo | Cambio |
|---|---|
| `Identity.Domain/Kyc/SolicitudKycEnEspera.cs` | Nuevo: `record SolicitudKycEnEspera(Guid EventoId, DateTimeOffset OcurridoEn, Guid UsuarioId, Guid SolicitudId) : IDomainEvent` |
| `Identity.Application/Kyc/IOpcionesAvisosKyc.cs` | Nuevo puerto: `string? EmailAvisos { get; }` |
| `Identity.Application/Kyc/NotificarSolicitudKycEnEsperaHandler.cs` | Nuevo `IDomainEventConsumer<SolicitudKycEnEspera>` |
| `Identity.Application/Correo/IPlantillaCorreo.cs` | Añade `AsuntoSolicitudKycEnEspera()` y `CuerpoSolicitudKycEnEspera(string urlPanel)` |
| `Identity.Infrastructure/Correo/PlantillaCorreoTextoPlano.cs` | Implementa ambos |
| `Identity.Infrastructure/Kyc/OpcionesAvisosKycDesdeConfig.cs` | Nuevo adaptador de configuración |
| `Identity.Infrastructure/DependencyInjection.cs` | Registra el puerto |
| `Identity.Application/Kyc/EnviarSolicitudKycCommand.cs` | Publica el evento en la rama `EnviarARevision` |
| `web/src/api/kyc.ts` | `contarSolicitudesKycPendientes()` sobre el listado existente |
| `web/src/kyc/useContadorKyc.ts` | Hook con `useQuery` |
| `web/src/app/AppLayout.tsx` | Entrada «Verificaciones» con `Badge` |

## 6. Contrato

Sin endpoints nuevos ni cambios de firma. El contrato OpenAPI y el cliente
TypeScript no se regeneran: el contador consume `GET /api/admin/kyc` tal como
existe.

## 7. Seguridad y PII

- El correo no contiene nombre, CI, score, motivo de revisión ni identificadores.
  Es un disparador de atención; los datos se ven en el panel, que ya audita el
  acceso vía `IAuditorAccesoPii`.
- El buzón configurado nunca se registra en los logs.
- Los logs del handler siguen el patrón de Identity (`usuario={UsuarioId}`). La
  prohibición del literal `Id` en plantillas de log es del gate de Chat
  (`ArchitectureTests/Chat/ChatPiiTests.cs`) y no aplica a Identity.
- El contador solo se consulta con permiso `kyc.revisar`; el endpoint ya lo exige
  del lado del servidor. El badge es presentación, no control de acceso.

## 8. Pruebas

### Tests existentes afectados

Verificado en el código, no supuesto: **ninguno se rompe**.

- `EnviarSolicitudKycCommandHandlerTests.cs:177`, `:196` y `:216` cubren las tres
  ramas de revisión manual. Su `PublicadorFake` (línea 63) acumula en listas y no
  es estricto: implementa `IPublisher` guardando en `Notificaciones`. Las
  aserciones de «no publica» miran `publicador.Eventos` filtrando por
  `e is UserVerified` (línea 190), no el canal de notificaciones. Publicar
  `SolicitudKycEnEspera` por `IPublisher` no dispara esas aserciones.
- `KycArgosFlujoTests.cs:113` pasa por la rama nueva, pero en test el buzón no
  está configurado: el handler se abstiene y el flujo no cambia.

### Unitarios nuevos — `NotificarSolicitudKycEnEsperaHandlerTests`

1. Con buzón configurado, envía un correo dirigido a ese buzón.
2. Con buzón vacío, no envía nada.
3. Si `IServicioCorreo` lanza, el handler no propaga la excepción.
4. Ni el asunto ni el cuerpo contienen el id de usuario, el de solicitud ni el
   score.

### Unitarios en el comando

- Score bajo publica `SolicitudKycEnEspera` con el id de solicitud correcto.
- Score alto no lo publica, y sigue publicando `UserVerified` y `KycResuelto`.

### Integración — `KycNotificacionTests`

Reutiliza `ServicioCorreoCapturador` (línea 25) y el patrón `FactoryConCorreo`
(línea 36): un envío que cae en revisión manual con buzón configurado deja en la
cola un correo dirigido al buzón, no al usuario.

### Frontend

- `useContadorKyc` devuelve el `total` del listado filtrado por `Pendiente`.
- `AppLayout` muestra la entrada con badge con permiso `kyc.revisar` y no la
  muestra sin él.

## 9. Criterios de aceptación

1. Una solicitud que queda en revisión manual envía un correo al buzón
   configurado.
2. Una solicitud aprobada o rechazada automáticamente no lo envía.
3. Sin buzón configurado no se envía nada y el flujo de la solicitud termina con
   éxito.
4. Un fallo de SMTP no altera el resultado del envío de la solicitud.
5. El correo no contiene nombre, CI, score, motivo ni identificadores.
6. Un usuario con `kyc.revisar` ve «Verificaciones» en el menú con el número de
   solicitudes pendientes.
7. Un usuario sin ese permiso no ve la entrada.

## 10. Riesgos y diferidos

- **Sin recordatorio.** Si nadie abre el correo no hay segundo aviso; el badge es
  la red de seguridad pasiva. El escalado por antigüedad queda fuera.
- **Sin transacción con el envío.** El evento se publica in-process por MediatR,
  sin outbox: si el handler falla tras persistirse la solicitud, el aviso se
  pierde y solo queda el contador. Es el mismo compromiso ya aceptado en
  `KycResuelto`.
- **El buzón vacío desactiva el aviso en silencio.** Intencional para desarrollo
  y test, pero un despliegue mal configurado se quedaría sin avisos sin que nada
  falle. El log en `Debug` es la única señal.
- **Volumen.** Un correo por solicitud es correcto hoy. Si crece, la opción
  natural es el antirrebote descartado en la decisión 1.
