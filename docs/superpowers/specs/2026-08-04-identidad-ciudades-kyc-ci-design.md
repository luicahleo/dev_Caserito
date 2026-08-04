# Diseño — Identidad completa, ciudades controladas y CI en KYC

> Fecha: 2026-08-04
> Estado: aprobado en brainstorming
> Rama: `feat/identidad-ciudades-kyc-ci`

## 1. Objetivo

Fortalecer la identidad de CaseritoApp para un marketplace C2C boliviano:

- separar nombres y apellidos en el registro y perfil;
- reemplazar la ciudad libre por un catálogo cerrado de ciudades bolivianas;
- solicitar el número de CI únicamente al iniciar la verificación KYC;
- impedir que un mismo documento verifique varias cuentas;
- mantener la comparación facial de ARGOS como señal previa y exigir aprobación
  administrativa para verificar la identidad documental.

El alta seguirá siendo ligera: registrarse no requiere CI ni imágenes. La PII
documental solo entra al sistema cuando el usuario decide verificarse.

## 2. No objetivos

- OCR o extracción automática de datos del documento.
- Consulta a SEGIP u otra fuente gubernamental.
- Prueba de vida avanzada.
- Captura del reverso del CI.
- Soporte para documentos extranjeros.
- Catálogo de todos los municipios bolivianos.
- Compatibilidad de datos de usuarios actuales, que son exclusivamente de prueba.
- Cambio del modelo de categorías o de ubicación de los avisos más allá de reutilizar
  el catálogo de ciudades.

## 3. Decisiones de producto

### 3.1 Registro y perfil

El registro local exige:

- email;
- contraseña;
- nombres;
- apellidos;
- ciudad seleccionada del catálogo.

Google y Facebook pueden prellenar nombres y apellidos, pero el usuario debe revisarlos,
completar cualquier dato faltante y seleccionar una ciudad antes de crear la cuenta. Los
datos aportados por el proveedor externo no se consideran identidad verificada.

El perfil privado devuelve nombres y apellidos completos. El perfil público muestra un
nombre derivado con el primer nombre y la inicial del primer apellido, por ejemplo,
`María Q.`. No expone los campos completos.

La ciudad sigue siendo editable. Nombres y apellidos:

- son editables antes de enviar KYC;
- quedan bloqueados mientras exista una solicitud pendiente;
- vuelven a ser editables después de un rechazo y antes de reenviar;
- quedan bloqueados tras la aprobación; corregirlos requerirá intervención administrativa
  y una futura reapertura de verificación, fuera del alcance de este bloque.

### 3.2 Catálogo inicial de ciudades

El selector cerrado contiene diez ciudades, con identificadores estables:

1. Cochabamba
2. Santa Cruz de la Sierra
3. La Paz
4. El Alto
5. Sucre
6. Oruro
7. Tarija
8. Potosí
9. Trinidad
10. Cobija

Se amplía el catálogo existente con Trinidad y Cobija. Registro, registro externo y perfil
consumen el mismo endpoint de referencia que los avisos. El backend valida que el identificador
exista y esté activo; nunca acepta un nombre de ciudad libre.

Identity persiste `CiudadId` como referencia lógica sin FK cruzada. La validación se realiza
mediante un puerto de aplicación y un adaptador de composición que consume un contrato estable
de Catalog. Identity no referencia Infrastructure ni Domain de Catalog.

## 4. Modelo de identidad

`ApplicationUser` reemplaza `Nombre` y `Ciudad` por:

- `Nombres: string`;
- `Apellidos: string`;
- `CiudadId: Guid`.

Los contratos privados de perfil incluyen `nombres`, `apellidos`, `ciudadId`, `nombreCiudad`
y `verificado`. El contrato público incluye únicamente `nombreVisible`, `ciudadId`,
`nombreCiudad` y `verificado` junto con los identificadores ya necesarios.

La derivación de `nombreVisible` ocurre en backend para que ningún consumidor público reciba
apellidos completos por accidente. Se recortan espacios exteriores y se normalizan espacios
repetidos; no se alteran acentos ni caracteres legítimos.

Las validaciones de nombres y apellidos exigen contenido no vacío y límites razonables. No se
intenta validar nombres humanos mediante una expresión regular restrictiva.

## 5. Modelo KYC documental

Cada nueva `SolicitudKyc` captura, además de las referencias de imágenes existentes:

- `NumeroCiCifrado: string`;
- `ComplementoCiCifrado: string?`;
- `HuellaCi: string`;
- `DepartamentoExpedicion: DepartamentoBolivia`;
- resultado facial de ARGOS ya existente;
- estado y metadatos de resolución existentes.

Una entidad separada `DocumentoKycRegistrado` mantiene la reserva de identidad:

- `HuellaCi`, con índice único;
- `UsuarioId`, propietario de esa huella;
- fecha de primer registro.

Esta entidad no contiene el número ni el complemento, y no sustituye el historial de solicitudes.

`DepartamentoBolivia` es un conjunto cerrado y estable:

- Chuquisaca
- La Paz
- Cochabamba
- Oruro
- Potosí
- Tarija
- Santa Cruz
- Beni
- Pando

La identidad canónica del documento para detectar duplicados se construye con número de CI,
complemento normalizado y departamento de expedición. El número es obligatorio; el complemento
es opcional. El sistema no acepta el departamento como texto libre.

La unicidad se aplica sobre `DocumentoKycRegistrado.HuellaCi`. Una solicitud rechazada del mismo
usuario puede reutilizar su reserva al reenviar; ningún otro usuario puede emplearla. Las huellas
históricas no se liberan automáticamente después de un rechazo, para impedir que la misma identidad
migre entre cuentas. La lógica distingue el reintento del propietario de una colisión entre cuentas
y evita carreras mediante la restricción de base de datos.

## 6. Cifrado y detección de duplicados

El número y complemento de CI son PII y se cifran antes de persistirse usando el puerto de cifrado
existente. No se almacenan también en claro en ninguna tabla, blob, evento o artefacto derivado.

La huella se genera con HMAC sobre la representación canónica del documento y una clave secreta
independiente. No se usa SHA-256 simple porque el espacio de números de CI es enumerable. La clave
HMAC se obtiene de configuración segura, falla al arrancar en Production si falta y nunca se
registra. La huella no se devuelve por API.

Las imágenes frontal y selfie mantienen el almacenamiento cifrado existente. No se captura el
reverso por minimización de datos.

## 7. Flujo de verificación

1. El usuario autenticado, con email confirmado y perfil completo, abre KYC.
2. Introduce número de CI, complemento opcional y departamento de expedición.
3. Adjunta cara frontal del CI y selfie.
4. El backend valida campos, tamaños, tipos MIME y magic bytes.
5. El backend comprueba que el documento no pertenezca a otra cuenta.
6. Se guardan los datos textuales e imágenes cifrados.
7. ARGOS compara el rostro del documento con la selfie.
8. Si ARGOS no encuentra coincidencia, la solicitud queda rechazada con un motivo genérico.
9. Si ARGOS encuentra coincidencia, la solicitud queda pendiente de revisión administrativa.
10. Un administrador autorizado abre el detalle auditado, compara los datos declarados con el CI
    y aprueba o rechaza.
11. Solo la aprobación administrativa produce el estado verificado y el evento correspondiente.

ARGOS nunca aprueba por sí solo. Su indisponibilidad conserva el comportamiento seguro existente:
la operación falla de forma genérica sin filtrar datos ni dejar una aprobación parcial.

## 8. API y errores

### Registro y perfil

- `POST /api/auth/register`: recibe `nombres`, `apellidos`, `ciudadId`.
- Flujo externo pendiente/completado: expone y recibe nombres, apellidos y `ciudadId`.
- `GET /api/perfil`: devuelve el contrato privado completo.
- `PUT /api/perfil`: actualiza nombres, apellidos y ciudad respetando el bloqueo KYC.
- `GET /api/perfiles-publicos/{id}`: devuelve solo `nombreVisible` y ubicación pública.
- `GET /api/catalogo/ciudades`: continúa como fuente única del selector.

### KYC

`POST /api/kyc` conserva `multipart/form-data` y añade:

- `numeroCi`;
- `complementoCi` opcional;
- `departamentoExpedicion`;
- `documento`;
- `selfie`.

El listado administrativo no contiene número, complemento, huella, imágenes ni nombres completos.
El detalle administrativo autorizado devuelve los datos necesarios para cotejar y mantiene los
endpoints auditados de imágenes. El acceso a los datos textuales sensibles se audita igual que el
acceso a los blobs.

Los errores públicos son genéricos y estables, por ejemplo:

- perfil incompleto;
- ciudad inválida;
- identidad bloqueada por estado KYC;
- documento ya utilizado;
- datos de documento inválidos;
- verificación facial fallida;
- servicio de verificación no disponible.

Los errores nunca incluyen número, complemento, nombres completos, referencias de blob, imágenes,
huellas ni respuestas crudas de proveedores.

## 9. UI

### Registro local y externo

- Campos separados “Nombres” y “Apellidos”.
- Selector de ciudad cargado desde el catálogo.
- Estados de carga, error y reintento del catálogo.
- El envío permanece deshabilitado hasta contar con una ciudad válida.

### Perfil

- Muestra y permite editar nombres, apellidos y ciudad según el estado KYC.
- Explica por qué los datos de identidad están bloqueados cuando corresponda.
- No permite convertir el selector de ciudad en texto libre.

### KYC de usuario

- Campos “Número de CI”, “Complemento (opcional)” y “Departamento de expedición”.
- Selector cerrado para departamento.
- Captura/subida de frontal del CI y selfie.
- Texto claro indicando que ARGOS compara rostros y que un administrador realizará la revisión final.
- Errores breves y genéricos, sin repetir PII introducida.

### Administración KYC

- El listado muestra solo identificador de solicitud, fecha, estado y metadatos no sensibles.
- El detalle protegido muestra los datos completos estrictamente necesarios para el cotejo.
- Cada apertura de PII textual o imagen queda auditada.

## 10. Migración y despliegue

No se implementa migración de compatibilidad para `Nombre` y `Ciudad`. Los usuarios actuales son
datos de prueba y pueden eliminarse. Las migraciones transforman el esquema al modelo nuevo; el
procedimiento de despliegue debe decidir explícitamente entre limpiar las tablas afectadas o
reinicializar la base de datos de prueba antes de aplicar migraciones.

La eliminación de datos no ocurre automáticamente desde la aplicación ni desde una migración sin
una decisión operacional visible. Antes de ejecutar la limpieza se verifica el entorno y se crea
el respaldo aplicable. La autorización dada cubre los usuarios de prueba, no secretos, blobs,
claves de Data Protection ni otros datos persistentes ajenos.

OpenAPI y `web/src/api/schema.d.ts` se regeneran después de estabilizar los contratos.

## 11. Seguridad, privacidad y auditoría

- Anti-PII no negociable en logs, trazas, métricas, errores, eventos y tests.
- Número y complemento cifrados; huella HMAC no reversible y nunca expuesta.
- Acceso de lectura restringido a `kyc.revisar`.
- Auditoría append-only para lectura de campos documentales e imágenes.
- Los listados no recuperan ni descifran PII.
- Las respuestas públicas derivan un nombre visible y no transportan apellidos completos.
- No se guardan imágenes o valores reales de CI como fixtures del repositorio.
- Se preservan rate limiting, validación de archivos y límites de tamaño existentes.

## 12. Pruebas

- Dominio: estados KYC, bloqueo y desbloqueo de edición de identidad.
- Aplicación: validación de perfil, ciudad y datos documentales.
- Infraestructura: cifrado reversible autorizado, HMAC estable, separación por complemento y
  expedición, y unicidad concurrente.
- Integración: registro local, registro externo, perfil privado, perfil público y rechazo de ciudad
  inventada.
- Integración KYC: envío completo, duplicado entre cuentas, reintento del propietario, fallo facial,
  coincidencia facial que permanece pendiente y aprobación administrativa.
- Auditoría: toda lectura de PII genera entrada; los listados no la generan ni la exponen.
- Frontend: selectores, carga/error, validación, bloqueo por estado y contratos accesibles.
- Regresión anti-PII: logs y errores no contienen valores sensibles de las pruebas.

## 13. Criterios de aceptación

1. Ningún registro o actualización de perfil acepta ciudad libre o inexistente.
2. Trinidad y Cobija están disponibles junto con las ocho ciudades actuales.
3. Todo usuario nuevo tiene nombres, apellidos y `CiudadId` válidos.
4. El perfil público solo muestra primer nombre e inicial del primer apellido.
5. El CI no se solicita durante el registro y sí al enviar KYC.
6. Número y complemento nunca se persisten en claro.
7. Una identidad documental no puede verificar dos cuentas, incluso con solicitudes concurrentes.
8. ARGOS coincidente deja la solicitud pendiente; nunca la aprueba automáticamente.
9. Solo un administrador con `kyc.revisar` puede consultar PII y resolver la solicitud.
10. Nombres y apellidos respetan la política de edición según estado KYC.
11. OpenAPI, cliente generado, backend y frontend usan el mismo contrato.
12. Build, tests, formato, lint y typecheck aplicables quedan verdes antes del cierre.

## 14. Trabajo diferido

- Flujo administrativo para corregir identidad de un usuario ya verificado y exigir reverificación.
- OCR del CI y cotejo automatizado de datos textuales.
- Integración con una fuente oficial de identidad.
- Prueba de vida avanzada.
- Ampliación a todos los municipios o documentos extranjeros.
