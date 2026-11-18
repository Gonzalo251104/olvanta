# Seguridad

Las credenciales de desarrollo se generan en `deploy/.env`. Guarde ese archivo con el volumen que inicializó. El proxy local escucha en loopback. La API usa un usuario SQL con permisos de datos por esquema. El historial de auditoría admite lectura e inserción. Las migraciones se ejecutan por separado. El volumen `identity-keys` conserva las claves de protección de las cookies entre reinicios. Proteja ese volumen y la base al realizar respaldos.

Al informar una incidencia, incluya la versión, el componente y el efecto observado. Comparta los detalles con el mantenedor por un canal privado y retire credenciales, certificados y datos personales de los registros adjuntos.
