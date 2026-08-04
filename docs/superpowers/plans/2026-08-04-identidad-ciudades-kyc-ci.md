# Plan — Identidad completa, ciudades controladas y CI en KYC

> Spec: `docs/superpowers/specs/2026-08-04-identidad-ciudades-kyc-ci-design.md`
> Rama: `feat/identidad-ciudades-kyc-ci`
> Método: TDD, una tarea y un commit lógico a la vez.

## Reglas de ejecución

- Ejecutar backend desde `CaseritoApp/` y frontend desde `web/`.
- Escribir primero la prueba mínima, observar el fallo correcto y recién entonces implementar.
- No usar números de CI, nombres reales ni imágenes reales en fixtures, logs o mensajes de error.
- No crear FK ni referencia directa entre bounded contexts.
- No editar `web/src/api/schema.d.ts` a mano; regenerarlo desde OpenAPI.
- No limpiar producción desde una migración. La limpieza de usuarios de prueba será un paso
  operacional explícito y separado.
- Si una decisión descubierta contradice el spec, detener la ejecución y actualizar primero diseño
  y plan con aprobación.

## Tarea 1 — Completar y exponer el catálogo canónico de ciudades

**Entradas:** catálogo actual de ocho ciudades y endpoint `GET /api/catalogo/ciudades`.

**Archivos:**

- Modificar: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/SeedCatalogoExtensions.cs`
- Modificar: `CaseritoApp/tests/CaseritoApp.IntegrationTests/CatalogoReferenciaTests.cs`
- Crear: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Perfil/IConsultaCiudadesPerfil.cs`
- Crear: `CaseritoApp/src/Host/CaseritoApp.Host/Perfil/ConsultaCiudadesPerfilAdapter.cs`
- Modificar: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`
- Crear o modificar pruebas unitarias/arquitectura del adaptador en los proyectos existentes.

**Comportamiento:**

- Añadir Trinidad y Cobija con GUID fijos y orden 9/10.
- Mantener nombres y GUID de las ocho ciudades actuales.
- Definir en Identity.Application un puerto `ExisteActivaAsync(Guid ciudadId, CancellationToken)`.
- Implementarlo en Host mediante la consulta pública de Catalog, sin referencia de Identity a
  Catalog.Domain o Catalog.Infrastructure.

**Prueba roja:** el catálogo devuelve exactamente las diez ciudades ordenadas; una ciudad
inexistente/inactiva no pasa el puerto.

**Verificación:**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~CatalogoReferenciaTests
dotnet test tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj
```

**Salida:** fuente canónica reutilizable por perfiles.
**Commit:** `feat(catalogo): completa ciudades bolivianas`

## Tarea 2 — Cambiar el modelo persistente de perfil

**Entradas:** `ApplicationUser.Nombre`, `ApplicationUser.Ciudad`, ciudad canónica de Tarea 1.

**Archivos:**

- Modificar: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/ApplicationUser.cs`
- Modificar: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/IdentityDbContext.cs`
- Modificar: configuración EF relacionada dentro de Identity.Infrastructure.
- Crear: migración EF `IdentidadPerfilCompleto` y actualizar
  `IdentityDbContextModelSnapshot.cs`.
- Modificar: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/SeedAdminPlataforma.cs`
- Modificar: configuración/env de seed que todavía use nombre o ciudad libres.
- Añadir pruebas de modelo/migración en `CaseritoApp.IntegrationTests`.

**Comportamiento:**

- Añadir `Nombres`, `Apellidos`, `CiudadId` como fase de expansión del esquema.
- Mantener temporalmente `Nombre` y `Ciudad` para que los consumidores todavía no migrados sigan
  compilando; no escribir código nuevo contra esos campos.
- Longitudes y nulabilidad coherentes con validadores.
- El seed administrativo debe exigir datos nuevos y un GUID válido.

**Prueba roja:** el modelo EF exige los tres campos, elimina las columnas anteriores y crea un
usuario válido con ciudad canónica.

**Verificación:** test dirigido de Identity + generación reproducible de la migración +
`dotnet build src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj`.

**Salida:** esquema expandido, listo para migrar consumidores sin romper la rama.
**Commit:** `feat(identity): separa nombres y referencia ciudad`

## Tarea 3 — Actualizar perfil privado y política de edición

**Entradas:** nuevo `ApplicationUser`, puerto de ciudades y estado KYC existente.

**Archivos:**

- Modificar: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Perfil/IRepositorioPerfil.cs`
- Modificar: `ActualizarPerfilCommand.cs`, `ObtenerPerfilQuery.cs` y validadores relacionados.
- Modificar: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Perfil/RepositorioPerfilUserManager.cs`
- Modificar: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/PerfilEndpoints.cs`
- Modificar: `CaseritoApp/tests/CaseritoApp.IntegrationTests/PerfilTests.cs`
- Añadir pruebas unitarias dirigidas del comando.

**Contratos:**

- `PerfilDto(Id, Email, Nombres, Apellidos, CiudadId, NombreCiudad, Verificado)`.
- `ActualizarPerfilCommand(UserId, Nombres, Apellidos, CiudadId)`.
- `ActualizarPerfilRequest(Nombres, Apellidos, CiudadId)`.

**Comportamiento:**

- Validar nombres/apellidos no vacíos, longitudes y ciudad activa.
- Permitir ciudad siempre.
- Bloquear cambios de nombres/apellidos con KYC pendiente o aprobado; permitirlos sin solicitud o
  después de rechazo.
- Devolver errores estables y genéricos.

**Prueba roja:** actualización válida; ciudad inventada rechazada; identidad bloqueada en pendiente
y aprobada; identidad editable tras rechazo; cambio exclusivo de ciudad permitido.

**Verificación:** `dotnet test ... --filter FullyQualifiedName~PerfilTests`.

**Salida:** perfil privado y reglas KYC aplicadas.
**Commit:** `feat(perfil): valida ciudad y bloquea identidad verificada`

## Tarea 4 — Actualizar registro local, externo y eventos de usuario

**Entradas:** modelo y validación de perfil de Tareas 1–3.

**Archivos:**

- Modificar: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs`
- Modificar: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthExternaEndpoints.cs`
- Modificar: clases de `CaseritoApp.Identity.Infrastructure/Auth/` que modelan el alta externa.
- Modificar: `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Usuarios/UsuarioRegistrado.cs`
  solo si sus consumidores requieren el nuevo nombre visible; no incluir apellidos completos en
  eventos si no son necesarios.
- Modificar pruebas de `AuthFlowTests.cs`, `AuthExternaLoginTests.cs` y tests vecinos.

**Contratos:** registro local y finalización externa reciben `nombres`, `apellidos`, `ciudadId`.
La proyección externa informa qué campos faltan y puede prellenarlos desde claims separados del
proveedor cuando estén disponibles.

**Prueba roja:** altas local/Google/Facebook válidas; rechazo de ciudad desconocida, apellidos
vacíos y payload antiguo; proveedor no evita la confirmación del usuario.

**Verificación:** filtros `AuthFlowTests|AuthExternaLoginTests`.

**Salida:** todas las vías de alta usan la misma identidad.
**Commit:** `feat(auth): exige perfil completo en altas`

## Tarea 5 — Minimizar el perfil público

**Entradas:** nombres/apellidos completos persistidos.

**Archivos:**

- Modificar: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Perfil/ObtenerPerfilPublicoQuery.cs`
- Modificar: `RepositorioPerfilUserManager.cs`
- Modificar endpoints/adaptadores que consumen `PerfilPublicoDto`.
- Modificar: `CaseritoApp/tests/CaseritoApp.IntegrationTests/ReputationPerfilPublicoTests.cs`
- Modificar tests frontend de perfil público/reputación afectados.

**Contrato:** `PerfilPublicoDto(Id, NombreVisible, CiudadId, NombreCiudad, Verificado)`.

**Comportamiento:** derivar en backend primer nombre + inicial del primer apellido; manejar espacios
y nombres compuestos sin exponer campos completos.

**Prueba roja:** “María Elena Quispe Flores” produce “María Q.” y la respuesta serializada no
contiene `nombres` ni `apellidos`.

**Verificación:** test dirigido backend y consumidores frontend vecinos.

**Salida:** privacidad por contrato, no solo por UI.
**Commit:** `fix(privacidad): minimiza nombre de perfil publico`

## Tarea 6 — Modelar CI cifrado y reserva única

**Entradas:** agregado KYC e infraestructura de cifrado existentes.

**Archivos:**

- Crear: `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/DepartamentoBolivia.cs`
- Crear: `.../Kyc/DocumentoKycRegistrado.cs`
- Modificar: `.../Kyc/SolicitudKyc.cs` y `VerificacionKyc.cs`.
- Crear en Identity.Application un puerto de protección/huella documental.
- Crear en Identity.Infrastructure su implementación con cifrado existente + HMAC.
- Modificar: `IdentityDbContext.cs` y configuración EF KYC.
- Crear migración `KycIdentidadDocumental` y actualizar snapshot.
- Modificar opciones/configuración de KYC y validación Production en `DependencyInjection.cs`.
- Añadir tests unitarios KYC y tests de infraestructura dirigidos.

**Comportamiento:**

- Cifrar número y complemento antes de persistir.
- Canonicalizar sin perder el valor cifrado presentado.
- HMAC con secreto independiente obligatorio en Production.
- Índice único en `DocumentoKycRegistrado.HuellaCi`.
- Reserva reutilizable solo por el mismo `UsuarioId`, incluidas solicitudes rechazadas.
- Nunca incluir PII en `ToString`, excepciones o logs.

**Prueba roja:** cifrado no contiene texto original; HMAC estable para representación equivalente;
cambia con complemento/expedición; otra cuenta recibe conflicto; el propietario puede reenviar.

**Verificación:** tests unitarios KYC + test dirigido de persistencia con SQL Server.

**Salida:** almacenamiento documental seguro y unicidad robusta.
**Commit:** `feat(kyc): protege y reserva identidad documental`

## Tarea 7 — Extender el envío KYC y conservar revisión manual

**Entradas:** modelo documental y endpoint multipart existentes.

**Archivos:**

- Modificar: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/EnviarSolicitudKycCommand.cs`
- Modificar: puertos/repositorios KYC necesarios.
- Modificar: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/KycEndpoints.cs`
- Modificar: `KycArgosFlujoTests.cs`, `KycFlujoTests.cs`, `KycConcurrenciaTests.cs`.

**Contrato multipart:** `numeroCi`, `complementoCi?`, `departamentoExpedicion`, `documento`, `selfie`.

**Comportamiento:**

- Exigir perfil completo y email confirmado según gates existentes.
- Validar número, complemento, departamento, frontal y selfie.
- Reservar huella y persistir datos cifrados de forma consistente con los blobs.
- Compensar blobs si falla el guardado.
- ARGOS sin coincidencia rechaza con motivo genérico.
- ARGOS con coincidencia guarda score y deja `Pendiente`; elimina toda autoaprobación.
- Resolver carreras de huella como conflicto genérico.

**Prueba roja:** payload anterior falla; coincidencia ya no produce `Aprobada`; duplicado concurrente
solo permite una cuenta; ningún error contiene el CI.

**Verificación:** filtros `KycArgosFlujoTests|KycFlujoTests|KycConcurrenciaTests`.

**Salida:** flujo KYC híbrido seguro.
**Commit:** `feat(kyc): incorpora datos de CI al envio`

## Tarea 8 — Añadir detalle administrativo auditado y resolución manual

**Entradas:** listado sin PII, lectura auditada de blobs y comandos aprobar/rechazar existentes.

**Archivos:**

- Crear: query/DTO de detalle en `CaseritoApp.Identity.Application/Kyc/`.
- Extender el auditor para acceso textual sin registrar el contenido.
- Modificar repositorio KYC e implementación EF para recuperar y descifrar solo el detalle.
- Modificar: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/KycEndpoints.cs`.
- Modificar: `AprobarSolicitudKycCommand.cs`, `RechazarSolicitudKycCommand.cs` si aún asumen actor sistema.
- Modificar pruebas `KycFlujoTests.cs`, `KycArgosFlujoTests.cs` y auditoría.

**API:** añadir `GET /api/admin/kyc/{solicitudId}` protegido por `kyc.revisar`. Devuelve nombres,
apellidos, número descifrado, complemento y departamento solo en el detalle. Listar continúa sin PII.

**Comportamiento:**

- Registrar actor, solicitud, tipo de acceso y fecha antes de devolver datos.
- Requerir administrador para aprobar/rechazar solicitudes pendientes.
- Eliminar la resolución automática del sistema y actualizar textos/metadatos asociados.

**Prueba roja:** listado no contiene PII; detalle sin permiso da 403; detalle autorizado audita;
ARGOS coincidente requiere comando administrativo para emitir verificación.

**Verificación:** flujo KYC de integración completo y tests de permisos.

**Salida:** cotejo manual operable y auditable.
**Commit:** `feat(admin): habilita revision documental KYC`

## Tarea 9 — Estabilizar OpenAPI y regenerar el cliente

**Entradas:** contratos backend definitivos de Tareas 1–8.

**Archivos:**

- Modificar anotaciones `.Accepts/.Produces` de endpoints.
- Regenerar: `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json`
- Regenerar: `web/src/api/schema.d.ts`
- Modificar adaptadores `web/src/api/auth.ts`, `perfil.ts`, `kyc.ts` y tipos consumidores.
- Modificar tests de contrato/generación existentes.

**Prueba roja:** typecheck falla con contratos antiguos y los tests OpenAPI no encuentran campos
nuevos.

**Verificación:** generación OpenAPI según comando del repo, `npm run generate:api`,
`npm run typecheck`.

**Salida:** una única fuente contractual.
**Commit:** `feat(api): actualiza contratos de identidad y KYC`

## Tarea 10 — Crear selector reutilizable y actualizar las altas web

**Entradas:** endpoint de ciudades y cliente generado.

**Archivos:**

- Crear componente/hook de ciudad dentro del dominio apropiado de `web/src/`.
- Modificar: `web/src/routes/RegistroPage.tsx` y su test.
- Modificar: `CompletarRegistroExternoPage.tsx` y su test.
- Modificar tipos de `web/src/api/auth.ts`.

**UI:** nombres y apellidos separados; selector MUI de ciudad; skeleton/progreso, error genérico y
reintento; no permitir envío sin catálogo/selección.

**Prueba roja:** no existe textbox libre; carga diez ciudades; fallo del catálogo es visible y
recuperable; se envían GUID y campos nuevos.

**Verificación:** tests dirigidos, `npm run typecheck`, `npm run lint`.

**Salida:** altas locales y externas controladas.
**Commit:** `feat(web): completa identidad en registro`

## Tarea 11 — Actualizar perfil web y consumidores públicos

**Entradas:** contratos privado/público nuevos.

**Archivos:**

- Modificar: `web/src/routes/PerfilPage.tsx` y test.
- Modificar: `web/src/auth/AuthContext.tsx` y fixtures de usuario.
- Modificar páginas/tests que lean `usuario.nombre` o `usuario.ciudad` mediante búsqueda dirigida.
- Modificar: `PerfilPublicoPage.tsx` y consumidores de reputación.

**Comportamiento:** editar nombres/apellidos/ciudad; deshabilitar identidad según estado KYC con
explicación; mantener ciudad editable; mostrar solo `nombreVisible` en superficies públicas.

**Prueba roja:** bloqueo pendiente/aprobado, edición tras rechazo, selector controlado y ausencia
de apellido completo en perfil público.

**Verificación:** tests dirigidos de perfil/auth/reputación + typecheck.

**Salida:** UI coherente con privacidad y estados.
**Commit:** `feat(web): adapta perfiles a identidad completa`

## Tarea 12 — Actualizar KYC de usuario y panel administrativo web

**Entradas:** contratos KYC definitivos.

**Archivos:**

- Modificar: `web/src/routes/KycPage.tsx` y `KycPage.test.tsx`.
- Modificar: `web/src/routes/AdminKycPage.tsx` y `AdminKycPage.test.tsx`.
- Modificar: `web/src/api/kyc.ts` y tests.

**UI usuario:** número, complemento opcional, selector de nueve departamentos, frontal y selfie;
explicar comparación facial + revisión humana; no repetir PII en errores.

**UI admin:** listado mínimo; detalle bajo acción explícita; carga de datos documentales e imágenes;
botones aprobar/rechazar con estados de envío, confirmación y motivo; refrescar queries tras resolver.

**Prueba roja:** formulario envía todos los campos; no solicita reverso; lista no muestra PII; abrir
detalle carga PII; aprobación/rechazo solo aparecen en pendiente y manejan error genérico.

**Verificación:** tests dirigidos + `npm run typecheck`, `npm run lint`, `npm run build`.

**Salida:** flujo completo operable desde la PWA.
**Commit:** `feat(web): completa verificacion documental`

## Tarea 13 — Integración, anti-PII y preparación de despliegue

**Entradas:** bloque funcional completo.

**Archivos:**

- Completar tests end-to-end en `CaseritoApp.IntegrationTests`.
- Retirar `ApplicationUser.Nombre`/`Ciudad`, sus opciones antiguas y cualquier consumidor residual;
  generar la migración contract que elimina las columnas libres después de comprobar con `rg` que
  no queda código funcional dependiente.
- Actualizar configuración de ejemplo/documentación operacional sin secretos.
- Crear documento breve de despliegue o handoff solo si la limpieza de usuarios requiere agente VPS.
- No incluir comandos destructivos automáticos en workflow, migración o arranque.

**Escenarios finales:**

1. registro → login → perfil completo;
2. rechazo de ciudad libre/inexistente;
3. envío KYC → ARGOS coincide → pendiente;
4. detalle auditado → aprobación admin → perfil verificado;
5. mismo CI en otra cuenta → conflicto;
6. rechazo → corrección de nombre → reenvío del propietario;
7. logs capturados sin número, complemento, nombres completos, blobs ni huella.

La verificación final del modelo confirma que solo existen `Nombres`, `Apellidos` y `CiudadId`; las
columnas temporales de la fase expand ya no forman parte del snapshot final.

**Verificación backend:**

```powershell
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes
```

Las pruebas de integración requieren Docker/Testcontainers.

**Verificación frontend:**

```powershell
npm run typecheck
npm run lint
npm run test -- --run
npm run format:check
npm run build
```

**Revisión final:** `git diff --check`, diff contra spec, migraciones, OpenAPI, cliente generado,
permisos, auditoría, concurrencia y anti-PII.

**Despliegue:** respaldar primero; confirmar explícitamente el entorno y alcance; limpiar únicamente
usuarios/datos de prueba acordados; preservar secretos, key ring y datos ajenos; aplicar migraciones;
desplegar; ejecutar smoke tests sin PII. La limpieza requiere autorización operacional en el momento
de ejecutarla aunque el diseño permita descartarlos.

**Salida:** feature integrada y lista para revisión/despliegue.
**Commit:** `test: integra identidad y KYC documental`

## Secuencia de commits prevista

1. `feat(catalogo): completa ciudades bolivianas`
2. `feat(identity): separa nombres y referencia ciudad`
3. `feat(perfil): valida ciudad y bloquea identidad verificada`
4. `feat(auth): exige perfil completo en altas`
5. `fix(privacidad): minimiza nombre de perfil publico`
6. `feat(kyc): protege y reserva identidad documental`
7. `feat(kyc): incorpora datos de CI al envio`
8. `feat(admin): habilita revision documental KYC`
9. `feat(api): actualiza contratos de identidad y KYC`
10. `feat(web): completa identidad en registro`
11. `feat(web): adapta perfiles a identidad completa`
12. `feat(web): completa verificacion documental`
13. `test: integra identidad y KYC documental`
