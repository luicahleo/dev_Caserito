# Fase 2 — Bloque 2D: Moderación de avisos

**Fecha:** 2026-07-19  
**Contexto:** Catalog + frontend `web/`  
**Base:** bloques 2A, 2B, 2C y 2E integrados en `master`  
**Rama:** `feat/catalogo-2d-moderacion`

## 1. Objetivo

Permitir que una persona autenticada reporte un aviso visible y que un usuario
con el permiso `publicaciones.moderar` revise la cola, descarte reportes y oculte,
restaure o elimine avisos. Las decisiones deben ser trazables sin registrar PII
y respetar el aislamiento del bounded context Catalog.

## 2. Alcance

Incluye:

- Reportes autenticados con motivo tipificado y detalle opcional.
- Un reporte pendiente por usuario y aviso.
- Cola de moderación agrupada por aviso, con filtros por estado del reporte.
- Ocultar, restaurar y eliminar por moderación.
- Descartar reportes individuales.
- Auditoría append-only de acciones de moderación.
- Integración con descubrimiento público, fotos, vista del dueño y UI React.
- Autorización mediante la policy existente del permiso
  `publicaciones.moderar`.

Fuera de alcance:

- Apelaciones, baneos o suspensión de usuarios.
- Asignación de casos a moderadores o estado «En revisión».
- Notificaciones a reportantes o vendedores.
- Moderación automática de texto o imágenes.
- Notas libres del moderador.
- Borrado físico de avisos o blobs.

## 3. Decisiones de brainstorming

1. Solo usuarios autenticados pueden reportar.
2. Un usuario puede tener como máximo un reporte `Pendiente` por aviso; puede
   volver a reportar cuando el anterior haya sido descartado.
3. Motivos: `EstafaOEngano`, `ProductoProhibido`, `ContenidoInapropiado`,
   `DuplicadoOSpam` y `Otro`.
4. El motivo es obligatorio y el detalle es opcional, con máximo 500
   caracteres. El detalle se considera contenido potencialmente sensible y
   nunca se registra en logs o auditoría.
5. No se permite reportar un aviso propio.
6. Solo se reportan avisos públicamente visibles: `Activo + Visible`.
7. El estado de moderación es independiente del ciclo del dueño.
8. Ocultar es reversible por un moderador; eliminar por moderación es terminal.
9. La cola se agrupa por aviso y permite filtrar reportes `Pendiente`,
   `Atendido` o `Descartado`.
10. Ocultar o eliminar atiende todos los reportes pendientes del aviso;
    descartar afecta un reporte.
11. Si el dueño elimina el aviso, sus reportes pendientes pasan automáticamente
    a `Atendido`.
12. El dueño ve el estado de moderación. Puede seguir gestionando un aviso
    oculto, pero no uno eliminado por moderación.
13. Las fotos de un aviso que deje de ser `Activo + Visible` responden `404`.
14. La auditoría se persiste en una tabla append-only sin texto libre ni PII.

## 4. Dominio

### 4.1 Estado de moderación del aviso

`Aviso` incorpora `EstadoModeracionAviso`:

- `Visible`: estado inicial.
- `Oculto`: fuera de todas las superficies públicas; reversible.
- `EliminadoPorModeracion`: terminal y fuera de superficies públicas.

Transiciones:

- `OcultarPorModeracion`: `Visible → Oculto`.
- `RestaurarPorModeracion`: `Oculto → Visible`.
- `EliminarPorModeracion`: `Visible|Oculto → EliminadoPorModeracion`.

Las transiciones inválidas devuelven `avisos.transicion_moderacion_invalida`.
`EstadoModeracion` se configura como token de concurrencia. El dueño puede
editar, pausar, reactivar, eliminar y gestionar fotos mientras el estado sea
`Visible` u `Oculto`. Todas esas operaciones fallan con
`avisos.eliminado_por_moderacion` cuando sea terminal.

La visibilidad pública se define de forma única como:

```text
Estado == Activo && EstadoModeracion == Visible
```

### 4.2 ReporteAviso

Entidad del schema `catalog`:

- `Id: Guid`.
- `AvisoId: Guid`, FK interna a `catalog.Avisos`.
- `ReportanteId: Guid`, id opaco del claim `sub`, sin FK a Identity.
- `Motivo: MotivoReporteAviso`.
- `Detalle: string?`, máximo 500.
- `Estado: EstadoReporteAviso` (`Pendiente`, `Atendido`, `Descartado`).
- `FechaCreacion: DateTime` UTC.
- `FechaResolucion: DateTime?` UTC.
- `ResueltoPorId: Guid?`, id opaco del moderador; nulo en resolución automática
  por eliminación del dueño.

Comportamiento:

- Nace `Pendiente`.
- `Atender(moderadorId?, ahoraUtc)` y
  `Descartar(moderadorId, ahoraUtc)` solo parten de `Pendiente`.
- `Estado` es token de concurrencia.
- Índice único filtrado sobre `(AvisoId, ReportanteId)` para filas
  `Estado = 'Pendiente'`.

### 4.3 RegistroModeracion

Entidad append-only:

- `Id: Guid`.
- `AvisoId: Guid`.
- `ModeradorId: Guid` opaco.
- `Accion: AccionModeracionAviso`
  (`Ocultar`, `Restaurar`, `Eliminar`, `DescartarReporte`).
- `ReporteId: Guid?`, solo para descarte individual.
- `Fecha: DateTime` UTC.

No admite actualización ni texto libre. Las acciones sobre el aviso, la
resolución de reportes y el registro se guardan mediante un único `SaveChanges`
del `CatalogDbContext`.

## 5. Application e infraestructura

Se mantiene CQRS-lite con MediatR, `Result`, FluentValidation y `TimeProvider`.

Puertos nuevos:

- `IRepositorioReportesAviso`: crear, detectar pendiente, cargar por id, cargar
  pendientes del aviso y consultar cola agrupada/detalle.
- `IRepositorioRegistrosModeracion`: agregar registros append-only.
- Consulta pública de fotos que resuelve la clave contra el aviso padre antes de
  acceder a `IAlmacenFotosAviso`.

Casos de uso:

- `ReportarAvisoCommand`.
- `ListarAvisosReportadosQuery`.
- `ObtenerAvisoReportadoQuery`.
- `OcultarAvisoPorModeracionCommand`.
- `RestaurarAvisoPorModeracionCommand`.
- `EliminarAvisoPorModeracionCommand`.
- `DescartarReporteAvisoCommand`.
- `ObtenerFotoPublicaQuery`.

Los comandos administrativos reciben `ModeradorId` desde el endpoint, nunca un
rol o dependencia hacia Identity. La policy vive en Host y usa las constantes ya
existentes de Identity.

La migración `AgregarModeracionAvisos` añade las tablas `ReportesAviso` y
`RegistrosModeracion`, la columna `EstadoModeracion` en `Avisos`, restricciones,
índices y tokens de concurrencia. Todos los objetos viven en schema `catalog`.

## 6. API HTTP

### Reporte comunitario

`POST /api/avisos/{id}/reportes`, autenticado:

```json
{ "motivo": "EstafaOEngano", "detalle": "Texto opcional" }
```

Respuestas:

- `201 { id }`.
- `400` validación o autorreporte.
- `401` sin sesión.
- `404` si no existe o ya no está públicamente visible.
- `409` si el usuario ya tiene un reporte pendiente o hay una carrera.

### Moderación

Grupo `/api/admin/moderacion`, protegido con
`PoliticasAutorizacion.Permiso(Permisos.PublicacionesModerar)`:

- `GET /avisos?estado=Pendiente&pagina=1&tamano=20`.
- `GET /avisos/{id}?estado=Pendiente`.
- `POST /avisos/{id}/ocultar`.
- `POST /avisos/{id}/restaurar`.
- `POST /avisos/{id}/eliminar`.
- `POST /reportes/{id}/descartar`.

La lista devuelve una fila por aviso con contenido resumido, estado del aviso,
estado de moderación, cantidad de reportes, motivos distintos y fecha del reporte
más antiguo. El detalle incluye contenido y fotos del aviso y los reportes del
estado solicitado, pero nunca `ReportanteId` ni `ResueltoPorId`.

Los comandos devuelven `204`; inexistencia `404`, transición o concurrencia
`409`. Los errores al público no distinguen pausa, eliminación u ocultamiento.

## 7. Frontend

- `DetalleAvisoPage`: acción «Reportar aviso», diálogo de motivo/detalle,
  confirmación de éxito y errores genéricos. Un visitante anónimo se dirige a
  login mediante estado de navegación y vuelve al detalle tras autenticarse.
- `AdminModeracionPage`: patrón de `AdminKycPage`, filtro por estado, tabla
  agrupada, panel de detalle y confirmaciones de acciones.
- Ruta `/admin/moderacion`: `ProtectedRoute` +
  `RequierePermiso('publicaciones.moderar')`.
- `AppLayout`: enlace «Moderación» solo con el permiso.
- `MisAvisosPage`: muestra estado de moderación y deshabilita todas las acciones
  en `EliminadoPorModeracion`; un oculto sigue gestionable.
- La UI no muestra ids de reportantes ni moderadores.
- Todos los tipos HTTP proceden de `schema.d.ts` regenerado.

## 8. Anti-PII y seguridad

- Nunca loguear `Detalle`, título, descripción, fotos, email, tokens o ids de
  reportantes.
- La auditoría contiene solo ids opacos, acción y fecha.
- El texto de detalle solo se expone a la policy de moderación.
- Los endpoints administrativos verifican permiso, no nombre de rol.
- El acceso a fotos valida el estado del aviso antes de leer bytes.
- Los errores públicos son genéricos y no revelan estados internos.

## 9. Pruebas y aceptación

Backend:

- Dominio: transiciones de moderación y terminalidad; transiciones de reportes.
- Unit: validators, autorreporte, aviso no público, duplicado, acciones y
  resolución masiva/automática.
- Integración: creación, índice único, RBAC, filtros de cola, acciones,
  concurrencia, desaparición de búsqueda/detalle, fotos `404`, restauración y
  bloqueo de acciones del dueño.
- Arquitectura: Catalog continúa aislado de Identity y respeta capas/schema.

Frontend:

- Diálogo de reporte y retorno desde login.
- Capa API tipada.
- Cola agrupada, filtros y acciones.
- Guard de ruta y enlace condicionado por permiso.
- Estado del dueño y bloqueo terminal.

Verificación final:

```powershell
# CaseritoApp/
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes

# web/
npm run typecheck
npm run lint
npm run test
npm run build
```

No se hace merge ni push sin autorización explícita.
