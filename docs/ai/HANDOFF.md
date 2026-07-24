# Handoff — Fase 4A acuerdo básico de compra

Fecha: 2026-07-24

## Estado y alcance

El usuario aprobó el diseño del primer bloque mínimo de Fase 4:
`Requested → Agreed`, sin QR, envío, cierre, reputación ni custodia.

Rama: `feat/orders-4a-acuerdo-basico`, creada desde el HEAD corregido `62b2fc4`
de `feat/pruebas-manuales-fases-1-3`. No se hizo push ni merge.

Documentos:

- Spec aprobado:
  `docs/superpowers/specs/2026-07-24-orders-4a-acuerdo-basico-design.md`
- Plan:
  `docs/superpowers/plans/2026-07-24-orders-4a-acuerdo-basico.md`

## Commits de esta rama

- `aa58cde` diseño del bloque 4A.
- `608bdff` plan de implementación.
- `f8d9942` agregado `Orden`, estados, invariantes y eventos de dominio.
- `665eea3` caso de uso para solicitar un acuerdo y puertos iniciales.

## Implementado y verificado

- `Orden.Crear` produce `Requested`, congela monto/moneda y emite
  `OrdenSolicitada`.
- `Orden.Aceptar` permite solo al vendedor, produce `Agreed`, es idempotente y
  emite `EstadoOrdenCambiado`.
- `SolicitarOrdenCommandHandler` obtiene la instantánea desde el puerto de
  Catalog, comprueba ambos participantes mediante el puerto de verificación y
  rechaza duplicados.
- TDD observado:
  - dominio: rojo por tipos inexistentes, luego 7/7 verdes;
  - solicitud Application: rojo por contratos inexistentes, luego Orders
    completo 10/10 verde.
- `dotnet format` normalizó los archivos nuevos y los hooks de ambos commits de
  código pasaron.

## Siguiente tarea exacta

Continuar la tarea 2 del plan:

1. Escribir pruebas rojas de `AceptarOrdenCommandHandler`.
2. Implementar aceptación y consultas/listados.
3. Resolver publicación de `OrderStatusChanged` **después de persistir**. No
   copiar sin revisar el patrón directo de KYC, porque actualmente publica antes
   de que `UnitOfWorkBehavior` guarde. El spec exige publicación posterior a
   persistencia; una opción coherente es que `UnitOfWorkOrders` guarde, traduzca
   `EstadoOrdenCambiado`, publique y limpie eventos, manteniendo el fallo de
   publicación fuera de la transacción documentado. Outbox sigue fuera.
4. Completar las tareas 3–8 del plan mediante TDD.

## Restricciones

- No introducir QR: sigue bloqueado por validación legal.
- No incluir envío, `MarkedAsSold`, `Completed`, reputación, Fases 5/6, outbox,
  Payments/Shipping/Disputes ni pruebas manuales diferidas.
- Preservar los arreglos previos de detalle/contacto y proxy SignalR.
- No leer `.env`; no registrar PII, datos de pagos, claims o participantes.
- No hacer merge ni push sin autorización explícita.
