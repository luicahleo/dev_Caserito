# Estrategia de backup — Piloto CaseritoApp

## Objetivo

Garantizar la recuperación del sistema ante fallos de infraestructura, corrupción de datos o errores humanos durante el piloto en Cochabamba.

## Alcance

- Base de datos SQL Server (todos los bounded contexts).
- Logs de aplicación estructurados.
- Blobs de imágenes de avisos.

## Políticas de backup

### Base de datos

| Tipo | Frecuencia | Retención | Destino |
|------|------------|-----------|---------|
| Full | Cada 24 horas | Mínimo 7 días | Almacenamiento redundante del proveedor cloud |
| Diferencial | Cada 6 horas | Mínimo 2 días | Mismo destino que full |
| Transaction log | Cada hora | Mínimo 24 horas | Mismo destino que full |

### Logs de aplicación

| Tipo | Frecuencia | Retención | Destino |
|------|------------|-----------|---------|
| Exportación de logs | Cada hora | Mínimo 7 días | Servicio de logs centralizado |

### Blobs de imágenes

- Las imágenes de avisos se almacenan en el proveedor de blobs configurado.
- Se aprovechan las políticas de versionamiento y soft-delete del proveedor.
- Retención mínima de versiones: 7 días.

## Procedimiento de restauración

1. Identificar el punto de recuperación objetivo (RPO) y el momento del fallo.
2. Restaurar el último backup full disponible anterior al fallo.
3. Aplicar el backup diferencial más reciente.
4. Aplicar los transaction logs hasta el punto deseado.
5. Verificar la integridad de los datos con `DBCC CHECKDB`.
6. Reconectar la aplicación y verificar `/health`.
7. Restaurar blobs de imágenes desde versiones si fueron afectados.

## Responsabilidades

- El equipo de infraestructura configura y monitorea los jobs de backup.
- El equipo de desarrollo verifica que `/health` reporte el estado de la base de datos.
- Se documenta cualquier incidente de restauración en el runbook del piloto.

## Consideraciones del piloto

- Durante el piloto la base de datos es pequeña; un backup full diario es suficiente.
- Los logs no deben contener PII; la exportación de logs se limita a metadata y trazas técnicas.
- Las credenciales de acceso al almacén de backups deben rotarse antes del piloto.
