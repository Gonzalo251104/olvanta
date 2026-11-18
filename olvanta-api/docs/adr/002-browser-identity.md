# ADR 002 · Identidad y origen compartido

**Decisión.** La web llama a rutas relativas. El proxy sirve React y reenvía la API bajo el mismo origen. En fase 2 se usará ASP.NET Core Identity con cookie HttpOnly, Secure en HTTPS, SameSite explícito y protección antifalsificación para operaciones que cambian estado.

**Motivo.** Esta aplicación de navegador puede mantener las credenciales fuera de JavaScript. Los roles y la sucursal activa se verificarán en el servidor con el recurso solicitado.

**Consecuencia.** En fase 1 solo se expone información técnica mínima del estado. Las credenciales SQL se generan localmente y el usuario de ejecución tiene acceso de conexión. El despliegue de producción necesitará TLS, claves de protección persistentes y configuración de secretos propia.
