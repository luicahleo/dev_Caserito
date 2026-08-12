# Plan: seeder de usuarios de Development

1. Renombrar las pruebas del bootstrap y expresar en rojo la matriz, guardrails,
   reparación e inexistencia de PII en logs.
2. Reemplazar `BootstrapUsuariosPrueba` por `SeederUsuariosDesarrollo`, con IDs y
   perfiles deterministas, reparación de roles/email y KYC mediante el dominio.
3. Renombrar el cableado a `SeedUsuariosDesarrollo` y actualizar la plantilla y
   documentación sin credenciales.
4. Ejecutar pruebas dirigidas, suite relevante y `verify.ps1 -Changed`; revisar,
   hacer commit y publicar `develop`.
