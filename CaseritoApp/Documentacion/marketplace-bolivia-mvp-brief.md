# Brief de Proyecto: Marketplace C2C para Bolivia (MVP v1.0)

> Documento de contexto para agente IA. Objetivo: usar esta información como base para generar un plan de desarrollo detallado, y devolver feedback/ajustes antes de ejecutar.
>
> **Actualización (v1.1)**: tras la revisión de alcance se incorporaron al MVP el **pago por QR sin custodia** y la **coordinación de envío sin custodia** (Opción A), y se ajustó la estrategia de diferenciación y los riesgos identificados. Los cambios están marcados a lo largo del documento. El plan por fases derivado vive en `plan-desarrollo-mvp-v1.md`.

---

## 1. Contexto y motivación

### 1.1 Qué estamos construyendo
Una aplicación multiplataforma (web + Android) tipo marketplace C2C (compra-venta entre particulares), similar en concepto a Wallapop o Facebook Marketplace, enfocada en el mercado boliviano.

### 1.2 Por qué (el problema de negocio)
En Bolivia, las plataformas actuales de clasificados (Facebook Marketplace, grupos de Facebook, portales locales) tienen carencias estructurales frente a modelos maduros como Wallapop:

- No hay integración de envíos nacionales automatizados (todo se coordina manualmente vía buses interprovinciales o couriers locales).
- No existe pasarela de pago propia que retenga el dinero del comprador hasta confirmar la entrega (el pago es directo, en efectivo o transferencia/QR, sin intermediario).
- No hay seguro de protección al comprador (sin mediación ni reembolsos si el producto llega dañado, falso o no coincide con lo publicado).
- No hay cálculo automático de tarifas de envío por peso/volumen.
- No hay verificación estricta de identidad de los usuarios (perfiles falsos son comunes).
- No hay sistema formal de disputas entre comprador y vendedor.

### 1.3 Para qué (la apuesta del producto)
La hipótesis de negocio es que **la confianza es el producto**: si resolvemos verificación de identidad + reputación + comunicación segura antes que la competencia, generamos una ventaja defendible.

**Ajuste v1.1 — diferenciación sentible frente a Facebook Marketplace**: verificación + reputación por sí solas son un gancho débil frente al efecto de red de Facebook (la experiencia del comprador seguiría siendo "coordinar y pagar por fuera"). Por eso el MVP incorpora, en **versión sin custodia** (la app nunca toca el dinero ni el paquete), dos de los dolores que Facebook no resuelve y que el equipo puede ofrecer sin roce regulatorio:

- **Pago por QR sin custodia**: la app muestra el QR del vendedor y registra el pago; el dinero va banco→banco directo.
- **Coordinación de envío sin custodia**: la app informa tarifas y genera un resumen de envío; el courier y las partes se hacen cargo.

Lo que sí sigue **fuera del MVP** por costo operativo y regulatorio (posible roce con regulación de dinero electrónico/ASFI en Bolivia) es la versión **con custodia/automatizada** de esas piezas: pago con retención (escrow), envío integrado (la app contrata al courier y responde por el paquete) y disputas automatizadas. Se planifican para versiones futuras.

---

## 2. Alcance del MVP (v1.0)

El MVP es, en esencia, **un clasificados con perfiles verificados, chat, reputación, y coordinación de pago (QR) y envío sin custodia**. La app **nunca retiene dinero ni asume responsabilidad sobre el paquete**: el pago va banco→banco directo entre las partes y el envío lo ejecuta un courier. Frente a Facebook Marketplace, el diferenciador es doble: más señales de confianza (verificación + reputación) *y* facilitar pago/envío que Facebook no cubre, sin volverse una entidad financiera.

### 2.1 Funcionalidades incluidas en v1.0

| Módulo | Alcance |
|---|---|
| **Identity / Auth** | Registro, login, perfil de usuario. Verificación de identidad (KYC) básica: carga de foto de documento (CI) + selfie, con revisión **manual** por un administrador (no automatizada aún). |
| **Catalog** | Publicación de productos: título, descripción, fotos, precio, categoría, estado (nuevo/usado), ciudad/ubicación. Edición y eliminación de publicaciones propias. |
| **Búsqueda y filtros** | Búsqueda por texto, filtros por categoría, ciudad, rango de precio, estado del producto. |
| **Chat** | Mensajería en tiempo real entre comprador y vendedor asociada a una publicación. |
| **Orders (versión mínima)** | Entidad de orden con estados mínimos para representar un acuerdo de compra-venta (ver sección 4 para detalle de diseño). Sin custodia de dinero ni envío integrado. |
| **Pago por QR (sin custodia)** | La app muestra el QR del vendedor dentro del flujo del acuerdo y registra que el pago ocurrió (referencia/comprobante opcional). El dinero va banco→banco directo; **la app nunca lo recibe**. Sujeto a confirmación legal (ver 2.3). |
| **Coordinación de envío (sin custodia)** | Tabla de tarifas de courier/flota por origen-destino (inicialmente manual, bien instrumentada) y generación de un "resumen de envío". **La app no cobra el envío ni responde por el paquete.** |
| **Reputación** | Calificación 1-5 + comentario post-transacción, **atada a un `Order` marcado como completado por ambas partes** (no a un simple mensaje), una reseña por parte, y **ciega** (oculta hasta que ambos califiquen) para reducir represalias. |
| **Notificaciones** | Notificaciones push/in-app básicas (nuevo mensaje, interés en publicación, cambio de estado de orden) + búsquedas guardadas/alertas como loop de retención. |
| **Moderación** | Reportar y ocultar/eliminar publicaciones y conversaciones (ítems prohibidos, fraude, abuso), ejercible por el rol Moderador. |

### 2.2 Explícitamente fuera de alcance en v1.0 (pero planificado a futuro)

Estas funcionalidades **no se implementan en el MVP**, pero el diseño de datos y eventos debe dejar el espacio para incorporarlas sin reescribir lo ya construido. Nótese que v1.1 ya incorpora las versiones *sin custodia* de pago y envío (ver 2.1); lo que sigue fuera es la versión **con custodia / automatizada**:

- **Pasarela de pago propia / escrow (con custodia)**: retención del dinero del comprador hasta confirmar recepción del producto. Requiere definir partner financiero (billetera electrónica autorizada, banco con cuenta de garantía, etc.) — pendiente de decisión de negocio, no solo técnica. *(El pago por QR de v1.0 es sin custodia y sí está incluido.)*
- **Envío integrado y cálculo automático de tarifas**: la app contrata al courier, cobra el envío y responde por el paquete; tarifas por peso/volumen vía API de couriers. *(La coordinación de envío de v1.0 es sin custodia, con tabla manual, y sí está incluida.)*
- **Seguro de protección al comprador**: reembolsos o compensación ante productos dañados/falsos.
- **Sistema automatizado de disputas**: mediación con soporte humano o reglas automáticas para devolución de dinero.
- **KYC automático** (OCR/liveness): en v1.0 la verificación es manual.

**Importante para el agente que planifique**: estas features quedan fuera de alcance de *desarrollo*, pero **no fuera de alcance de diseño**. El modelo de datos y los contratos de eventos del MVP deben anticipar su llegada (ver sección 4).

### 2.3 Restricciones y consideraciones

- **Regulatorio (sin custodia = fuera de ASFI)**: retener dinero de terceros cae bajo regulación de dinero electrónico en Bolivia (ASFI). El MVP **no retiene ni procesa dinero de ningún tipo**: el pago por QR va banco→banco directo entre comprador y vendedor, y la app solo muestra el QR y registra que el pago ocurrió. Esta línea es la que mantiene a CaseritoApp fuera del ámbito de ASFI y **no es negociable en v1.0**.
- **Confirmación legal del QR (bloqueante)**: la implementación concreta del pago por QR sin custodia debe validarse con un abogado boliviano especializado en regulación financiera antes de desarrollarse, porque los detalles de implementación pueden mover la línea regulatoria.
- **Protección de datos / PII (riesgo alto de v1.0)**: el KYC manual implica almacenar foto de CI + selfie biométrica. Este es el activo de mayor riesgo legal del MVP (más inmediato que los pagos). Requiere cifrado en reposo, acceso auditado, acceso mínimo y política de retención, considerando el marco boliviano de protección de datos.
- **KYC manual, no automático**: en v1.0 la verificación de identidad es revisada por un humano (admin), no por OCR/IA. Se debe guardar el documento y el resultado de la verificación de forma que sirva de base para automatizar esto después.
- **Plataformas objetivo**: Web + Android nativo (o multiplataforma). iOS no es prioridad en v1.0 (a confirmar con el equipo si aplica).
- **Stack tecnológico de referencia del equipo**: .NET (backend), experiencia previa en microservicios y Clean Architecture. Estas preferencias deben respetarse salvo justificación técnica fuerte para desviarse.
- **Presupuesto de complejidad**: priorizar velocidad de lanzamiento sobre automatización completa. Preferir soluciones manuales/semi-manuales bien instrumentadas (con eventos y datos capturados) sobre automatizaciones prematuras.

---

## 3. Objetivos de la v1.0 (criterios de éxito)

1. Un usuario puede registrarse, verificar su identidad y publicar un producto en menos de 5 minutos.
2. Un comprador puede encontrar un producto por búsqueda/filtro y contactar al vendedor por chat sin salir de la app.
3. Al cerrar una compra-venta, ambas partes pueden marcarla como completada y calificarse mutuamente (reseña atada a la orden, una por parte, ciega).
4. El sistema permite verificar identidad de usuarios y este dato es visible en el perfil (badge de "verificado"); los documentos KYC se almacenan cifrados y con acceso auditado.
5. El comprador puede pagar por QR del vendedor **sin que el dinero pase por la app** y coordinar un envío con costo estimado.
6. La arquitectura de datos/eventos soporta agregar escrow, envío integrado y disputas en versiones futuras sin migraciones destructivas.
7. Existen flujos de reporte y moderación para publicaciones y chat, ejercibles por el rol Moderador.

---

## 4. Lineamientos de diseño para no bloquear el futuro

Estos lineamientos son la razón de ser de este documento: aunque v1.0 no incluye pagos/envíos/disputas, el diseño debe anticiparlos.

### 4.1 Entidad `Order` desde v1.0 (aunque mínima)

Aunque no haya pago, el "acuerdo de compra-venta" debe modelarse como una entidad formal desde el inicio, no como un simple mensaje de chat:

```
Order
 - Id
 - ProductId
 - BuyerId
 - SellerId
 - Status: Requested -> Agreed -> MarkedAsSold -> Completed
 - PaymentReference (opcional, sin custodia): comprobante/nota de que el pago QR ocurrió fuera de la app
 - OriginCity / DestinationCity: para coordinación de envío
 - Courier (opcional): courier/flota elegido
 - EstimatedShippingCost (opcional): costo estimado según tabla de tarifas
 - CreatedAt
 - UpdatedAt
```

**Sin custodia**: `PaymentReference` es solo un registro de que el pago sucedió banco→banco; la app **no** modela saldos, retenciones ni movimientos de dinero. Los campos de envío son informativos; la app no contrata ni cobra el transporte.

En versiones futuras, este mismo estado se extiende (no se reemplaza) con: `Paid`, `Shipped`, `Delivered`, `Disputed`, `Refunded`, etc. Al pasar a escrow/envío integrado, estos campos ya capturados evitan re-modelar la entidad.

### 4.2 Eventos de dominio desde v1.0

Publicar eventos de cambio de estado aunque hoy no haya consumidores para ellos, de forma que en el futuro los nuevos servicios (Payments, Shipping, Disputes) puedan suscribirse sin tocar el servicio de Orders:

```
OrderStatusChanged { OrderId, OldStatus, NewStatus, Timestamp }
```

### 4.3 Reputación como contexto separado

El módulo de Reputación debe diseñarse como bounded context propio (no como un campo suelto en Orders o Users), porque en el futuro se alimentará también del resultado de disputas, no solo de reseñas manuales.

### 4.4 KYC con datos reutilizables

El resultado de la verificación de identidad (documento + selfie + estado de aprobación) debe guardarse con una estructura que permita, en el futuro, pasar de revisión manual a automática (OCR/liveness) sin tener que re-solicitar documentos a los usuarios ya verificados.

### 4.5 Servicios que NO se crean todavía

`Payments`, `Shipping` y `Disputes` no deben crearse como proyectos/servicios vacíos en v1.0. Solo se planifica el contrato de eventos que consumirán a futuro. Crear infraestructura vacía antes de tiempo agrega complejidad sin valor inmediato.

---

## 5. Propuesta de módulos/servicios para v1.0

| Servicio | Incluir en v1.0 | Notas |
|---|---|---|
| Identity | Sí (completo para el alcance de v1) | Auth + KYC manual |
| Catalog | Sí | Publicaciones, búsqueda, categorías |
| Orders | Sí (versión mínima, sin pago) | Ver sección 4.1 |
| Chat | Sí | Tiempo real (ej. WebSockets/SignalR si el stack es .NET) |
| Reputation | Sí | Reviews atadas a Order, ciegas, bounded context separado |
| Notifications | Sí | Básico (push/in-app) + búsquedas guardadas |
| Moderation | Sí | Reporte/ocultar publicaciones y chat |
| Payments | **Coordinación sin custodia sí**; escrow no | v1.0: mostrar QR + registrar pago (la app no toca dinero). Escrow con custodia queda a futuro (solo contrato de eventos) |
| Shipping | **Coordinación sin custodia sí**; integrado no | v1.0: tarifas manuales + resumen de envío. Envío integrado (contratar courier, cobrar, responder por paquete) queda a futuro (solo contrato de eventos) |
| Disputes | No (solo contrato de eventos a futuro) | — |

---

## 6. Lo que se le pide al agente que reciba este documento

1. Revisar el alcance y restricciones descritos y **dar feedback**: ¿algo falta, algo sobra, algo es poco realista para un MVP?
2. Proponer un **plan de desarrollo por fases** (no todo de una vez), con criterios claros de "listo para lanzar v1.0".
3. Proponer un **modelo de datos detallado** para los módulos incluidos en v1.0, respetando los lineamientos de la sección 4.
4. Señalar **riesgos técnicos u operativos** que no se hayan contemplado aquí (ej. moderación de contenido, spam, seguridad del chat, abuso del sistema de reputación).
5. Sugerir **stack técnico concreto** compatible con: backend .NET + Clean Architecture + microservicios, cliente web y Android (evaluar si conviene multiplataforma tipo MAUI/Flutter/React Native o nativo separado), y justificar la elección.
6. No proponer diseño de Payments/Shipping/Disputes en detalle todavía — solo la forma de los eventos que estos consumirán en el futuro, como se describe en la sección 4.2.
7. Priorizar **solidez de diseño sobre velocidad de despliegue**: justificar explícitamente los patrones elegidos (Clean Architecture, DDD, bounded contexts, manejo de eventos, RBAC para roles/permisos) y cómo cada decisión previene refactors futuros al incorporar pagos, envíos y disputas. Esto se detalla en la parte técnica del plan, no es responsabilidad de este documento de alcance.

---

## 7. Preguntas abiertas (para resolver antes o durante el plan)

- ~~¿La verificación manual de KYC la hará el mismo equipo fundador al inicio, o se necesita definir un rol de "moderador/admin" desde v1.0?~~ **Resuelto, ver sección 8.1**
- ~~¿Hay una ciudad o segmento de producto específico para lanzar primero?~~ **Resuelto, ver sección 8.2**
- ~~¿Cuál es el volumen esperado de usuarios en los primeros 3 meses?~~ **Resuelto, ver sección 8.2**
- ~~¿iOS entra en el roadmap cercano o se posterga indefinidamente?~~ **Resuelto, ver sección 8.3**

---

## 8. Respuestas a la sección 7 y definiciones adicionales

### 8.1 Verificación de identidad y roles

La verificación de KYC en v1.0 la hará el propio fundador manualmente. Sin embargo, el sistema de **roles y permisos debe diseñarse desde v1.0** pensando en el crecimiento del equipo, no solo en el usuario final. Roles propuestos para el modelo de permisos (a validar/ajustar por el agente que planifique):

| Rol | Descripción | ¿Existe ya en v1.0? |
|---|---|---|
| **Cliente/Usuario** | Usuario final que compra y/o vende. Puede publicar, buscar, chatear, calificar. | Sí |
| **Vendedor verificado** | No es un rol distinto de permisos, sino un *estado* del usuario Cliente (badge de KYC aprobado). Se modela como atributo, no como rol aparte. | Sí (como atributo) |
| **Moderador** | Revisa publicaciones reportadas, puede ocultar/eliminar contenido inapropiado o sospechoso de fraude, puede suspender publicaciones. No necesariamente aprueba KYC. | Sí (aunque en v1.0 lo ejerza el fundador) |
| **Admin (KYC)** | Aprueba/rechaza verificaciones de identidad. Puede ser el mismo rol que Moderador al inicio, pero conviene separarlo como **permiso**, no fusionarlo como si fuera la misma persona siempre. | Sí (ejercido por el fundador) |
| **Admin (plataforma)** | Rol superior: gestión de usuarios, roles, configuración general, métricas. Acceso total. | Sí (el fundador) |
| **Soporte/Atención al cliente** | A futuro, cuando existan Disputes: atenderá tickets, verá evidencia, podrá escalar a Admin. En v1.0 no hay disputas formales, pero conviene dejar el rol definido para no reestructurar permisos después. | No operativo aún, pero definir el rol en el modelo |
| **Sistema/Servicio (machine user)** | Para cuando haya automatización (ej. un servicio de OCR que apruebe KYC automáticamente, o un bot que modere contenido). Útil tenerlo contemplado como tipo de "actor" en el sistema de auditoría desde ya. | No operativo aún, pero definir el concepto |

**Recomendación de diseño**: separar **Roles** de **Permisos** desde el inicio (RBAC simple), en vez de hardcodear "if (user.Role == Admin)". Así, cuando el equipo crezca (ej. un moderador que no es admin de plataforma), no hay que reescribir lógica de autorización, solo reasignar permisos a roles.

Con solo 2 personas haciendo las pruebas iniciales (ver 8.2), probablemente ambas actúen como Admin/Moderador simultáneamente — pero el modelo debe soportar que en el futuro sean personas distintas con permisos distintos.

### 8.2 Mercado inicial y volumen esperado

- **Ciudades de lanzamiento**: Cochabamba y La Paz.
- **Equipo de pruebas**: 2 personas realizarán las pruebas iniciales (posiblemente actuando como usuarios de prueba, moderadores y validadores de KYC a la vez).
- **Volumen esperado a 3 meses**: ~20 usuarios/clientes.

**Implicación para el plan técnico**: el volumen bajo (20 usuarios/3 meses) es una señal de dimensionamiento de infraestructura física (servidores, réplicas, clusters), **no una señal para apurar el diseño ni saltarse buenas prácticas**. La prioridad del equipo es la contraria a "lanzar rápido y ya se verá": se prefiere invertir el tiempo necesario ahora en diseño correcto, patrones adecuados y bounded contexts bien definidos, precisamente para **no tener que refactorizar** cuando lleguen pagos, envíos y disputas en versiones futuras.

En otras palabras, separar dos ejes que no deben confundirse:
- **Infraestructura de despliegue** (servidores, escalabilidad horizontal, costos de hosting): puede ser modesta, acorde a 20-200 usuarios. Aquí sí es razonable no sobre-invertir.
- **Diseño de software** (arquitectura, patrones, modelo de datos, límites entre servicios, contratos de eventos): debe hacerse con el mismo rigor que si fuera a escalar a miles de usuarios desde el día uno. Aquí no hay atajos aceptables — es la parte que evita el refactor costoso más adelante.

El agente que planifique debe reflejar esto en el plan: **el cronograma no está optimizado para velocidad de lanzamiento, está optimizado para solidez del diseño**. Se espera que el plan dedique tiempo explícito a: elección justificada de patrones (Clean Architecture, DDD/bounded contexts, CQRS si aplica, Saga para flujos futuros), definición de contratos de eventos, y convenciones de código antes de empezar a construir features.

### 8.3 Estrategia multiplataforma (Web + Android + iOS)

Contexto del equipo: experiencia previa con **.NET MAUI**, pero percepción de que es lento y con experiencia de usuario menos pulida comparado a otras alternativas. No hay experiencia previa con otros frameworks móviles (Flutter, React Native, Kotlin/Swift nativos).

iOS **sí** entra en el roadmap si se busca escalar a más usuarios (no es un "nunca", es un "todavía no" para v1.0, dado que el volumen inicial de prueba es de 20 usuarios en 2 ciudades).

**Se le pide al agente que planifique que incluya una recomendación de framework**, considerando:
- La **curva de aprendizaje de un framework nuevo no es un factor limitante**: el equipo se apoya en herramientas de IA para el desarrollo, por lo que no experiencia previa en Flutter/React Native no debe pesar como desventaja relevante en la comparación. El criterio de decisión debe basarse en **mérito técnico** (rendimiento, calidad de UX/UI resultante, soporte real de Android + iOS desde una sola base de código, madurez del ecosistema, mantenibilidad a largo plazo), no en familiaridad previa del equipo.
- Alternativas a evaluar explícitamente: **Flutter** (buen rendimiento, UI consistente entre plataformas) y **React Native** (ecosistema grande, pero introduce stack JS/TS junto al backend .NET) — y también se puede reconsiderar **.NET MAUI** si el agente considera que sus limitaciones de rendimiento/UX han mejorado o no son tan determinantes como percibe el equipo, siempre con justificación basada en evidencia técnica, no en preferencia de stack.
- Dado que v1.0 se probará primero en Android (el brief original prioriza Android + Web), la decisión de framework debe evaluarse pensando en que **iOS se sumará después**, por lo que el framework elegido debe soportar bien ambas plataformas desde el mismo código sin reescritura mayor.
- El agente debe dar una recomendación concreta con justificación técnica (no dejarlo abierto), sin necesidad de ponderar la falta de experiencia previa del equipo como un costo relevante.

---

## 9. Estrategia de producto y diferenciación (añadido v1.1)

### 9.1 El diferenciador real frente a Facebook Marketplace

Verificación + reputación por sí solas son un gancho **débil** frente al efecto de red de Facebook: sin pago ni envío, la experiencia del comprador sería idéntica a la de un grupo de Facebook (coordinar y pagar por fuera). Por eso v1.1 añade pago QR y coordinación de envío **sin custodia** (ver 1.3 y 2.1): son justo los dolores que Facebook no cubre y que se pueden ofrecer sin volverse entidad financiera.

Aun así, conviene tener presente que el foso más fuerte (escrow + envío integrado + disputas) sigue fuera de v1.0. El MVP prueba una versión intermedia de la hipótesis; el equipo debe definir de antemano **qué señal validaría la tesis** para no interpretar mal un resultado.

### 9.2 Arranque en frío (cold-start)

Con ~20 usuarios en 2 ciudades, el riesgo #1 es un marketplace vacío. Recomendaciones:

- **Enfocar un nicho + una ciudad** para lograr densidad (muchas publicaciones relevantes en un mismo tema), en vez de dispersarse en todas las categorías × 2 ciudades. Facebook gana en amplitud; se le compite en profundidad de nicho.
- **Resolver primero el lado de la oferta**: reclutar a mano los primeros buenos vendedores; ellos definen la percepción de calidad.
- **Que "verificado" se sienta**, no solo se vea: vincular el badge a beneficios visibles (prioridad en búsqueda, acceso a QR/envío, sello de confianza con historial).

*(Decisiones de nicho y ciudad de lanzamiento pendientes de confirmar.)*

---

## 10. Riesgos identificados (añadido v1.1)

| Riesgo | Descripción | Mitigación en el plan |
|---|---|---|
| **PII sensible (alto)** | Almacenar CI + selfie biométrica es el mayor riesgo legal del MVP, más inmediato que los pagos. | Cifrado en reposo, acceso auditado y mínimo, política de retención (Fase 1). |
| **Regulatorio (QR)** | Un detalle mal implementado del QR podría cruzar la línea de custodia y caer bajo ASFI. | Sin custodia estricta + confirmación con abogado antes de desarrollar (bloqueante, Fase 4). |
| **Reputación manipulable** | Reseñas autodeclaradas sin ancla en transacción se manipulan (ventas ficticias, represalias), destruyendo la propuesta de valor. | Reseña atada a Order completado por ambos, una por parte, ciega, rate-limiting (Fase 5). |
| **Abuso en el chat** | Salida de plataforma, estafas, acoso, bienes ilegales. | Reportar/bloquear como núcleo, moderación (Fase 3). |
| **Moderación de contenido** | Ítems prohibidos (armas, drogas, falsificaciones), spam. | Flujo de reporte + rol Moderador operativo (Fase 2). |
| **Marketplace vacío** | Sin densidad, comprador entra, no encuentra nada y no vuelve. | Estrategia de nicho + oferta primero (sección 9.2). |
| **Diferenciación insuficiente** | El gancho podría no bastar para vencer el efecto de red de Facebook. | Pago QR + envío sin custodia + curaduría de verificados; definir métrica de validación. |
