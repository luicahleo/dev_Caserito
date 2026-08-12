# Seeder de usuarios de Development

Fecha: 2026-08-08
Estado: aprobado por el pedido de implementación

## Objetivo

Preparar, de forma opt-in, idempotente y exclusiva de `Development`, doce
identidades sintéticas que cubran todos los roles humanos y los estados de
confirmación de email y KYC actualmente implementados.

## Diseño

Los alias `admin-plataforma`, `revisor-kyc`, `moderador`, `soporte`,
`vendedor-1`, `vendedor-2`, `comprador-1`, `comprador-2`,
`cliente-email-pendiente`, `cliente-kyc-no-iniciado`,
`cliente-kyc-pendiente` y `cliente-kyc-rechazado` identifican GUID fijos.
Compradores y vendedores conservan exclusivamente el rol `Cliente`.

Los cuatro actores operativos tienen email confirmado y KYC no iniciado. Los
cuatro actores comerciales tienen email confirmado y KYC aprobado. Los cuatro
clientes restantes cubren email pendiente y KYC no iniciado, pendiente y
rechazado.

La sección `SeedUsuariosDesarrollo` contiene el flag, una contraseña local común
y un diccionario de correos por alias. No hay valores reales en Git. El seeder
solo reconoce cuentas por GUID; una colisión de correo con otro GUID falla sin
exponer datos.

En reejecuciones se reparan únicamente el rol exacto, `EmailConfirmed` y el KYC
esperado. KYC se crea y transiciona con `VerificacionKyc`; un estado irreversible
incompatible falla de forma genérica. Logs contienen solo cantidades.

## Fuera de alcance

Avisos, reportes, conversaciones, órdenes y reseñas requieren seeders propios
de Catalog, Chat, Orders y Reputation. Bloqueo no se añade porque no existe un
flujo de negocio específico que necesite una persona adicional.
