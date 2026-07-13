# Plan de Desarrollo por Fases: CaseritoApp MVP v1.0

> Documento derivado de `marketplace-bolivia-mvp-brief.md`. Define **qué se construye y en qué orden**, con criterios de "listo" por fase. Los detalles técnicos profundos (stack concreto, topología de arquitectura, framework móvil, esquema de tablas) se definen en la **Fase 0**, como paso siguiente a este plan.

---

## Premisas de este plan (decisiones ya tomadas)

1. **Modelo de pago y envío: Opción A — sin custodia.**
   - **Pago por QR sin custodia**: la app muestra el QR del vendedor y registra que el pago ocurrió, pero **el dinero nunca pasa por CaseritoApp** (va banco→banco directo). Esto mantiene a la app *fuera* del ámbito de ASFI (dinero electrónico), a confirmar con abogado.
   - **Coordinación de envío sin custodia**: la app informa tarifas y genera un resumen de envío, pero **no cobra el envío ni responde por el paquete**. El courier y las partes se hacen cargo.
2. **La velocidad de lanzamiento NO es prioridad.** El cronograma está optimizado para **solidez de diseño** (bounded contexts, contratos de eventos, RBAC, modelo de datos que no requiera refactor al llegar pago/envío/disputa completos). No hay atajos aceptables en diseño de software (ref. brief §8.2).
3. **Volumen esperado bajo** (~20 usuarios / 3 meses, 2 ciudades). Esto solo dimensiona la infraestructura física, no el rigor del diseño.
4. **Los detalles técnicos se definen después de aprobar este plan**, en la Fase 0.

### Decisiones de producto pendientes de confirmar (no bloquean el plan, pero conviene cerrarlas)

- **Estrategia de arranque (cold-start)**: recomendación → lanzar en **un nicho + una ciudad** (p. ej. una sola categoría en Cochabamba) para lograr densidad antes de expandir, en vez de "todas las categorías × 2 ciudades" desde el día uno. Marcado como recomendación; se puede sobrescribir.
- **Nicho / categoría inicial**: por definir.
- **Acceso a QR bancario**: confirmar si el equipo ya tiene banco/billetera con QR utilizable, o si arranca como registro manual del comprobante.

---

## Principios rectores (aplican a todas las fases)

- **Diseñar para el futuro, construir lo mínimo hoy.** Cada entidad (sobre todo `Order`) y cada evento de dominio se modela anticipando pago/envío/disputa completos, aunque hoy se entregue la versión ligera.
- **La confianza es el producto.** Verificación, reputación no manipulable y comunicación segura son núcleo, no accesorios.
- **PII sensible con máximo cuidado.** Documentos de identidad y selfies son el activo de mayor riesgo legal del MVP: cifrado en reposo, acceso auditado, acceso mínimo, política de retención.
- **Sin custodia, siempre.** Ninguna fase puede introducir retención de dinero de terceros. Esa línea es regulatoria, no negociable en v1.0.
- **Todo cambio de estado relevante emite un evento de dominio**, aunque hoy no tenga consumidores.

---

## Visión general de las fases

| Fase | Nombre | Entrega valor visible | Depende de |
|---|---|---|---|
| **0** | Fundaciones técnicas y de diseño | No (habilitador) | — |
| **1** | Identidad, verificación y confianza | Sí | 0 |
| **2** | Catálogo y descubrimiento | Sí | 1 |
| **3** | Comunicación (chat) | Sí | 1, 2 |
| **4** | Transacción ligera (Order + QR + envío, sin custodia) | Sí | 2, 3 |
| **5** | Reputación (no manipulable) | Sí | 4 |
| **6** | Notificaciones, endurecimiento y piloto | Sí | todas |

Las fases son **secuenciales en dependencia**, pero el diseño de datos y eventos de todas se define de forma consistente en la Fase 0.

---

## Fase 0 — Fundaciones técnicas y de diseño

**Objetivo**: dejar decididas y documentadas las bases sobre las que se construye todo, *antes* de escribir features. Esta es la fase donde se definen "los detalles técnicos" mencionados como paso siguiente.

**Entregables**
- Decisión y justificación de **topología de arquitectura** (p. ej. monolito modular con bounded contexts vs. microservicios) — a resolver en la definición técnica.
- Decisión y justificación de **stack backend** (.NET + Clean Architecture) y **framework de cliente** (Web + Android; iOS a futuro).
- Mapa de **bounded contexts** y sus límites: Identity, Catalog, Chat, Orders, Reputation, Notifications (+ los contratos que consumirán Payments/Shipping/Disputes en el futuro).
- **Contratos de eventos de dominio** iniciales (p. ej. `OrderStatusChanged`, `UserVerified`, `ProductPublished`).
- Modelo de **RBAC** (Roles ≠ Permisos) según brief §8.1.
- Convenciones de código, estrategia de ramas, entornos (dev/prod), CI básico.
- Estrategia de **manejo de PII** (dónde y cómo se cifran documentos/selfies, quién accede, auditoría).

**Listo cuando**: existe un documento técnico aprobado con estas decisiones justificadas y un esqueleto de proyecto que compila con los bounded contexts vacíos ya delimitados.

**Deja preparado el futuro**: los contratos de eventos ya contemplan pago/envío/disputa; los servicios futuros no requieren tocar Orders para suscribirse (brief §4.2, §4.5).

---

## Fase 1 — Identidad, verificación y confianza

**Objetivo**: que un usuario pueda existir, verificarse y ser confiable. Es el núcleo del diferenciador.

**Entregables**
- Registro, login, perfil de usuario.
- **KYC manual**: carga de foto de documento (CI) + selfie; estado de verificación (`Pendiente / Aprobado / Rechazado`); badge "verificado" visible en el perfil.
- **Panel de administración mínimo** para que el fundador apruebe/rechace KYC.
- **RBAC operativo**: roles (Cliente, Moderador, Admin KYC, Admin plataforma, +Soporte y Sistema definidos aunque no operativos) y permisos separados.
- **Manejo seguro de PII**: cifrado en reposo de documentos/selfies, acceso auditado.

**Listo cuando**: un usuario se registra, sube sus documentos, un admin lo aprueba, y el badge "verificado" aparece en su perfil. Los accesos a documentos quedan auditados.

**Deja preparado el futuro**: el resultado de KYC (documento + selfie + estado) se guarda con estructura reutilizable para migrar a OCR/liveness automático sin re-solicitar documentos (brief §4.4).

---

## Fase 2 — Catálogo y descubrimiento

**Objetivo**: que existan productos y se puedan encontrar.

**Entregables**
- Publicación de producto: título, descripción, fotos, precio, categoría, estado (nuevo/usado), ciudad/ubicación.
- Edición y eliminación de publicaciones propias.
- **Búsqueda por texto** + filtros (categoría, ciudad, rango de precio, estado). Con el volumen previsto, búsqueda por base de datos es suficiente; no se introduce motor de búsqueda dedicado.
- Almacenamiento de imágenes.
- **Moderación de publicaciones**: reportar publicación, ocultar/eliminar por Moderador (ítems prohibidos, sospecha de fraude).

**Listo cuando**: un vendedor verificado publica un producto en < 5 min y un comprador lo encuentra por búsqueda y filtros.

**Deja preparado el futuro**: la publicación captura ciudad/ubicación y atributos que luego alimentan cálculo de envío por peso/volumen.

---

## Fase 3 — Comunicación (chat)

**Objetivo**: que comprador y vendedor se comuniquen sin salir de la app.

**Entregables**
- Mensajería en tiempo real asociada a una publicación.
- **Reportar y bloquear** usuario / conversación.
- Notificación de nuevo mensaje (versión básica; se completa en Fase 6).

**Listo cuando**: un comprador contacta a un vendedor por chat asociado a una publicación, y puede reportar/bloquear.

**Consideración de riesgo**: el chat es la principal superficie de abuso (salir de la plataforma, estafas, acoso, bienes ilegales). El flujo de reporte/bloqueo es parte del núcleo, no opcional.

---

## Fase 4 — Transacción ligera: Order + QR + envío (Opción A, sin custodia)

**Objetivo**: formalizar el acuerdo de compra-venta y facilitar pago y envío **sin que la app toque dinero ni mercancía**.

**Entregables**
- Entidad **`Order`** con máquina de estados mínima: `Requested → Agreed → MarkedAsSold → Completed` (extensible a `Paid`, `Shipped`, `Delivered`, `Disputed`, `Refunded` en el futuro, sin reemplazo).
- **Pago por QR sin custodia**: mostrar el QR del vendedor dentro del flujo del acuerdo; registrar "pago realizado" (referencia/comprobante, opcional). La app **no recibe fondos**.
- **Coordinación de envío sin custodia**: tabla de tarifas de courier/flota (inicialmente manual, bien instrumentada) según origen/destino; generación de un "resumen de envío" (dirección, courier, costo estimado). La app **no cobra ni responde por el paquete**.
- Emisión del evento `OrderStatusChanged { OrderId, OldStatus, NewStatus, Timestamp }` en cada transición.

**Listo cuando**: dos usuarios verificados crean una orden, ven/usan el QR del vendedor para pagar por fuera, coordinan un envío con costo estimado, y marcan el acuerdo como completado — sin que ningún dinero pase por la app.

**Deja preparado el futuro**: el `Order` ya guarda referencia de pago, ciudad origen/destino y courier elegido. El salto a escrow (Opción B) y a envío integrado solo agrega estados y el paso de cobro, sin reescribir el modelo (brief §4.1).

⚠️ **Bloqueante legal**: la implementación del QR sin custodia debe confirmarse con un abogado boliviano especializado en regulación financiera antes de desarrollarse.

---

## Fase 5 — Reputación (no manipulable)

**Objetivo**: reputación creíble, porque una reputación manipulable destruye la propuesta de valor.

**Entregables**
- Calificación (1–5) + comentario **atados a un `Order` marcado como completado por ambas partes** (no a un simple mensaje).
- **Una reseña por parte y por orden**; rate-limiting.
- **Reseñas ciegas**: ocultas hasta que ambas partes califican (reduce represalias).
- Reputación como **bounded context propio** (no un campo suelto en Users/Orders).

**Listo cuando**: tras completar una orden, ambas partes pueden calificarse una vez, y la reputación agregada es visible en el perfil.

**Deja preparado el futuro**: el bounded context de Reputación se alimentará también del resultado de disputas, no solo de reseñas manuales (brief §4.3).

---

## Fase 6 — Notificaciones, endurecimiento y piloto

**Objetivo**: cerrar el loop de retención, endurecer y lanzar el piloto controlado.

**Entregables**
- **Notificaciones push/in-app** completas (nuevo mensaje, interés en publicación, cambio de estado de orden).
- **Loop de retención**: búsquedas guardadas / alertas (p. ej. "nuevo producto que coincide con tu búsqueda") — diferenciador frente al ruido de Facebook.
- **Puntos de encuentro seguros** en la ciudad de lanzamiento (contenido/config, no infraestructura).
- **Endurecimiento**: revisión de seguridad, pruebas de flujos críticos, observabilidad/logs mínima, respaldo de datos.
- **Lanzamiento piloto** al nicho + ciudad elegidos.

**Listo cuando**: se cumplen todos los criterios de "listo para lanzar v1.0" (abajo).

---

## Criterios "listo para lanzar v1.0"

Adaptados y ampliados desde el brief §3:

1. Un usuario puede registrarse, verificar identidad y publicar un producto en < 5 min.
2. Un comprador encuentra un producto por búsqueda/filtro y contacta al vendedor por chat sin salir de la app.
3. Al cerrar una compra-venta, ambas partes la marcan como completada y se califican mutuamente (reseñas atadas a la orden, una por parte, ciegas).
4. La identidad verificada es visible como badge en el perfil, y los documentos KYC se almacenan cifrados y con acceso auditado.
5. El comprador puede pagar por QR del vendedor **sin que el dinero pase por la app**, y coordinar un envío con costo estimado.
6. La arquitectura de datos/eventos soporta agregar escrow, envío integrado y disputas sin migraciones destructivas.
7. Existen flujos de **reporte y moderación** para publicaciones y chat, ejercibles por el rol Moderador.

---

## Explícitamente fuera de v1.0 (recordatorio)

Sigue **fuera** de v1.0 (pero anticipado en el diseño):

- **Escrow / custodia de dinero** (Opción B). El pago QR de v1.0 es sin custodia.
- **Envío integrado** (la app contrata al courier y responde por el paquete). La coordinación de v1.0 es sin custodia.
- **Cálculo automático de tarifas** integrado con APIs de couriers (v1.0 usa tabla manual).
- **KYC automático** (OCR/liveness). v1.0 es manual.
- **Seguro de protección al comprador**.
- **Sistema automatizado de disputas**.

---

## Siguiente paso

Aprobar este plan y pasar a la **Fase 0 (definición técnica)**: topología de arquitectura, stack backend, framework móvil, modelo de datos detallado y contratos de eventos.
