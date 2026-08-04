# Despliegue de identidad y KYC documental

Antes de desplegar, configurar `Kyc__ClaveHuellaCi` con un secreto aleatorio,
independiente del JWT y de Data Protection. Debe conservarse entre despliegues:
cambiarlo alteraría la huella usada para detectar un CI ya registrado.

El arranque aplica dos migraciones de Identity: crea la reserva cifrada del CI y
elimina las columnas antiguas `Nombre` y `Ciudad`. Las cuentas actuales son datos
de prueba y pueden eliminarse mediante el procedimiento operativo habitual antes
del despliegue; la aplicación no ejecuta borrados automáticos.

Comprobación posterior:

1. registrar una cuenta con nombres, apellidos y una ciudad del catálogo;
2. confirmar el correo y enviar frontal del CI más selfie;
3. comprobar que una coincidencia facial queda `Pendiente`;
4. abrir el detalle en `/admin/kyc`, aprobarlo y confirmar el estado verificado;
5. verificar que otra cuenta no puede registrar el mismo CI.

No copiar números de CI, complementos, huellas ni contenido de imágenes a logs o
incidencias. Ante un fallo, registrar únicamente el código genérico y los ids
técnicos de actor/solicitud ya previstos por la auditoría.
