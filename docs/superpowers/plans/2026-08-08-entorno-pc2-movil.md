# Plan de implementación — entorno PC2 accesible desde móvil

**Spec:** `docs/superpowers/specs/2026-08-08-entorno-pc2-movil-design.md`

## Tarea 1: indicador global de Development

**Entradas:** `web/src/app/AppLayout.tsx`, configuración Vite existente.

**Salidas:**

- `web/src/app/IndicadorEntorno.tsx`: franja accesible que consulta una utilidad
  tipada y solo renderiza en Development.
- `web/src/lib/entorno.ts`: normaliza `VITE_CASERITO_ENVIRONMENT` sin inferir
  Production por hostname.
- `web/src/app/AppLayout.tsx`: monta el indicador antes de la barra principal.
- `web/src/app/AppLayout.test.tsx` o test dirigido del componente: cubre visible
  y ausente.
- `web/src/vite-env.d.ts`: declara la variable Vite.

**TDD:** añadir primero un test que espere “Entorno de desarrollo — usa
únicamente datos de prueba” y falle porque no existe; añadir el caso Production.

**Verificación:**

```powershell
cd web
npm run test -- --run src/app/AppLayout.test.tsx
npm run typecheck
```

**Commit previsto:** `feat(web): identifica visualmente el entorno de desarrollo`

## Tarea 2: configuración segura del simulador social

**Entradas:** `OpcionesAutenticacionExterna`, registro de autenticación y
`AuthExternaEndpoints`.

**Salidas:**

- `OpcionesAutenticacionExterna` incorpora `Simulador.Habilitado` y un correo
  sintético configurable para el escenario de vinculación.
- La validación de opciones rechaza el simulador fuera de Development.
- El listado de proveedores devuelve Google/Facebook cuando el simulador está
  activo, sin registrar handlers OAuth falsos.
- El inicio redirige a la ruta React del simulador conservando proveedor y
  `returnUrl`; deshabilitado mantiene el comportamiento actual.
- Tests en `CaseritoApp.IntegrationTests/AuthExternaConfiguracionTests.cs`.

**TDD:** tests rojos para proveedores simulados en Development, rechazo de host
en Production y 404 del inicio cuando está deshabilitado.

**Verificación:**

```powershell
cd CaseritoApp
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter AuthExternaConfiguracionTests
```

**Commit previsto:** `feat(auth): protege la configuración del simulador social`

## Tarea 3: autorización social sintética y continuación real

**Entradas:** esquema externo de ASP.NET Identity, callback y gestor pendiente
existentes.

**Salidas:**

- Tipo de escenarios sintéticos cerrado, con identificadores estables y sin
  entrada libre de PII.
- Endpoint POST excluido de OpenAPI que valida proveedor, escenario, acción y
  retorno; cancelar vuelve al login, aprobar firma el principal temporal en
  `IdentityConstants.ExternalScheme` y redirige al callback real.
- Endpoint ausente/404 si no está habilitado o el entorno no es Development.
- Perfiles: Google nuevo verificado, Facebook nuevo no verificado, identidad
  estable reutilizable y correo sintético existente para vinculación.
- Tests de integración en un nuevo
  `CaseritoApp.IntegrationTests/AuthExternaSimuladorTests.cs` que cubren rechazo,
  cancelación, onboarding, claims y reutilización por el flujo real.

**TDD:** cada escenario comienza con una petición que falle por endpoint ausente;
la implementación mínima reutiliza `CallbackAsync` y `ServicioRegistroExterno`,
sin duplicar emisión de sesión ni reglas de vinculación.

**Verificación:**

```powershell
cd CaseritoApp
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter AuthExternaSimuladorTests
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter AuthExterna
```

**Commit previsto:** `feat(auth): simula proveedores sociales en desarrollo`

## Tarea 4: pantalla local del simulador

**Entradas:** redirección producida en tarea 2 y endpoint POST de tarea 3.

**Salidas:**

- `web/src/routes/AuthExternaSimuladorPage.tsx`: pantalla mobile-first y
  accesible, rotulada “Simulador local”, con escenarios sintéticos, aprobar y
  cancelar.
- Ruta `/auth/external/simulador` registrada de forma diferida.
- El formulario usa navegación HTTP hacia el endpoint para conservar cookies y
  redirects; no registra valores.
- Tests de ruta para proveedor inválido, retorno normalizado y acciones.

**TDD:** test rojo que abra la ruta de Google, vea la advertencia local y verifique
el action y campos cerrados del formulario.

**Verificación:**

```powershell
cd web
npm run test -- --run src/routes/AuthExternaSimuladorPage.test.tsx
npm run typecheck
npm run lint
npm run build
```

**Commit previsto:** `feat(web): agrega pantalla del simulador social local`

## Tarea 5: gateway HTTPS y generación de certificados

**Entradas:** `docker-compose.dev.yml`, proxy Vite y scripts actuales.

**Salidas:**

- `infra/pc2/Caddyfile`: termina TLS y reenvía HTTP/WebSocket al servicio web.
- `docker-compose.pc2.yml`: extensión con gateway, montaje de certificados,
  variables del simulador y leyenda Development.
- `.gitignore`: ignora claves/certificados generados bajo `.local/pc2/`.
- `scripts/pc2/NuevaAutoridadLocal.ps1`: usa una imagen OpenSSL fijada para crear
  CA y certificado SAN de la IP/localhost; no imprime claves y renueva ante IP
  distinta.
- Tests estáticos PowerShell o validación encapsulada para IP, rutas y metadatos
  del certificado donde sea viable.

**TDD:** validación roja de compose por archivos/variables inexistentes y tests
de funciones de IP/certificado antes de implementar los scripts.

**Verificación:**

```powershell
docker compose -f docker-compose.dev.yml -f docker-compose.pc2.yml config --quiet
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/pc2/Test-Pc2.ps1
```

**Commit previsto:** `feat(dev): añade gateway HTTPS para pruebas móviles`

## Tarea 6: comando único y operación segura en PC2

**Entradas:** compose PC2, generación TLS, `docker-compose.argos.yml`.

**Salidas:**

- `iniciar-pc2.ps1`: valida Docker/`.env`, selecciona IPv4 de ruta por defecto o
  `-Ip`, genera TLS, levanta base + PC2 y opcionalmente ARGOS con `-Argos`, espera
  salud y muestra URL/certificado público.
- `detener-pc2.ps1`: detiene sin borrar volúmenes; `-BorrarDatos` exige una
  confirmación explícita o flag inequívoco y enumera el alcance local.
- `estado-pc2.ps1`: estado y logs acotados sin mostrar secretos.
- `.env.example` expone únicamente switches y valores sintéticos documentados.
- README contiene preparación PC2, instalación de CA en Android/iOS, firewall,
  cambio de IP, arranque, parada y limitación temporal del subdominio.

**TDD:** tests de funciones puras del script para selección de IP, composición
con/sin ARGOS y fallo por `.env`; ejecutar primero con fixtures temporales.

**Verificación:**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/pc2/Test-Pc2.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File iniciar-pc2.ps1 -Ip <IP_LAN>
docker compose -f docker-compose.dev.yml -f docker-compose.pc2.yml ps
curl.exe -k -I https://<IP_LAN>/
```

**Commit previsto:** `feat(dev): automatiza el entorno móvil de PC2`

## Tarea 7: integración y cierre

**Entradas:** todas las tareas anteriores.

**Salidas:** artefactos OpenAPI/cliente solo si cambió contrato público, diff
autorrevisado, compose validado, entorno real levantado, documentación final y
worktree limpio.

**Verificación:**

```powershell
./verify.ps1
./verify.ps1 -Full
git diff --check
```

Prueba manual desde un móvil de la misma Wi-Fi: confiar en la CA, abrir la URL,
confirmar leyenda, registro/login, ambos proveedores y escenarios, cámara/carga,
PWA, API, SignalR y KYC con ARGOS cuando esté disponible. Si PC1 no tiene Wi-Fi,
la comprobación física móvil se informará como pendiente de ejecutar en PC2; no
se declarará verde sin evidencia.

**Commit previsto:** correcciones de integración si fueran necesarias; después
merge a `master`, push y borrado de rama según el flujo autorizado.
