# Handoff — exención de KYC para `AdminPlataforma`

**Fecha:** 2026-07-31  
**De:** agente del VPS / propietario de la plataforma  
**Para:** agente local de CaseritoApp  
**Repositorio:** `luicahleo/dev_Caserito`  
**Rama:** `master`  
**Objetivo:** evitar que una cuenta con rol `AdminPlataforma` sea obligada a completar KYC.

## 1. Comportamiento observado

El administrador creado mediante el seed tiene correctamente:

- email confirmado;
- rol `AdminPlataforma`;
- todos los permisos administrativos definidos en `MapaRolesPermisos`.

Sin embargo, al intentar acceder a una función condicionada por KYC, la interfaz muestra:

```text
Necesitas verificar tu identidad antes de publicar un aviso.
```

La base de datos confirma que el usuario es administrador, pero no tiene una
`VerificacionKyc` aprobada.

## 2. Causa identificada

Actualmente rol/permisos y KYC son estados independientes.

En `web/src/routes/CrearAvisoPage.tsx` se comprueba únicamente:

```ts
if (!verificado) {
  // muestra la obligación de verificar identidad
}
```

El frontend no contempla una excepción para `AdminPlataforma`.

En backend, `AvisosEndpoints.CrearAsync()` pasa al comando exclusivamente el claim
`verificado=true`:

```csharp
new CrearAvisoCommand(
    userId,
    EstaVerificado(usuario),
    ...)
```

Por tanto, cambiar solamente la interfaz no es suficiente: el backend seguiría respondiendo 403.

## 3. Regla de negocio solicitada

Una cuenta con el rol exacto **`AdminPlataforma`** debe considerarse exenta del requisito KYC.

La condición efectiva debe ser equivalente a:

```text
identidad habilitada = KYC aprobado OR rol AdminPlataforma
```

La exención:

- aplica exclusivamente a `AdminPlataforma`;
- no aplica automáticamente a `AdminKyc`, `Moderador`, `Soporte`, `Sistema` o `Cliente`;
- no debe crear una verificación KYC falsa ni insertar blobs/solicitudes KYC;
- no debe marcar al administrador como si ARGOS hubiese validado su identidad;
- debe tratarse como una excepción de autorización basada en rol, auditable y explícita.

## 4. Cambios requeridos

### 4.1 Backend autoritativo

Modificar las comprobaciones que bloquean operaciones por falta de KYC para aceptar también
`AdminPlataforma`.

No confiar solo en el frontend. La decisión definitiva debe permanecer en el backend.

Usar el nombre canónico:

```csharp
RolesApp.AdminPlataforma
```

Evitar strings duplicados como `"AdminPlataforma"` si el proyecto ya expone la constante.

Revisar como mínimo:

- creación de avisos;
- creación/participación en órdenes;
- adaptadores que consultan `IConsultaVerificacionKyc`;
- cualquier handler o endpoint que produzca `NoVerificado`/403;
- claims emitidos y renovados en access/refresh tokens.

La implementación debe centralizar la regla cuando sea posible para evitar que frontend,
Catalog y Orders tengan criterios diferentes.

### 4.2 Frontend

Actualizar el estado de autenticación para poder distinguir que el usuario es
`AdminPlataforma`, usando roles o una capacidad explícita entregada por el backend.

En las pantallas condicionadas por KYC, usar una regla equivalente a:

```ts
const habilitadoPorIdentidad = verificado || esAdminPlataforma;
```

No usar un permiso administrativo no relacionado como sustituto ambiguo, salvo que se documente
como capacidad canónica para esta excepción.

Revisar como mínimo:

- `CrearAvisoPage`;
- navegación y avisos que enlacen a `/kyc`;
- acciones de órdenes o acuerdos condicionadas por verificación;
- perfil o indicadores que puedan seguir mostrando “identidad pendiente”.

La UI puede indicar “Exento por rol de administrador” si necesita mostrar el estado, pero no debe
afirmar “Identidad verificada por KYC”.

## 5. Pruebas obligatorias

Agregar pruebas que cubran al menos:

1. Usuario `Cliente` sin KYC: continúa bloqueado.
2. Usuario `Cliente` con KYC aprobado: permitido.
3. Usuario `AdminPlataforma` sin KYC: permitido.
4. Usuario `AdminKyc` sin KYC: continúa bloqueado para operaciones del marketplace.
5. Usuario `Moderador` sin KYC: continúa bloqueado.
6. La API no depende de que el frontend oculte el botón.
7. La excepción no crea registros falsos en `VerificacionesKyc`.
8. El token renovado conserva la información necesaria para reconocer al administrador.

Ejecutar:

```bash
cd CaseritoApp
dotnet format CaseritoApp.sln --verify-no-changes --no-restore
dotnet build CaseritoApp.sln --configuration Release
dotnet test CaseritoApp.sln --no-build --configuration Release

cd ../web
npm ci
npm run lint
npm run typecheck
npm run test
npm run build
```

## 6. Criterios de aceptación

- El administrador seeded puede acceder a las operaciones antes bloqueadas sin enviar KYC.
- Un usuario normal sin KYC continúa recibiendo la restricción.
- Backend y frontend aplican la misma regla.
- No se debilitan los endpoints administrativos de revisión KYC.
- No se generan verificaciones, solicitudes ni blobs ficticios para el administrador.
- CI completo en verde: `build`, `frontend` y `contract`.
- Deploy en verde y `GET /health` devuelve HTTP 200.

## 7. Entrega esperada

Responder indicando:

- archivos modificados;
- ubicación de la regla central de exención;
- pruebas añadidas;
- commit integrado en `master`;
- enlace al CI exitoso.

