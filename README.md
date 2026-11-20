# Olvanta

Plataforma de operaciones comerciales para una empresa peruana. La versión 0.7.0 incorpora estacionamientos a compras, inventario, ventas, caja, alquileres y facturación simulada por sucursal.

| Componente | Contenido |
| --- | --- |
| [API](olvanta-api/README.md) | ASP.NET Core, Identity, SQL Server y Docker Compose |
| [Web](olvanta-web/README.md) | Aplicación React y TypeScript |
| [Plan](olvanta-api/docs/plan.md) | Alcance y fases del proyecto |
| [Guía de seguridad y maestros](olvanta-api/docs/business/security-and-masters.md) | Uso y verificación de la fase 2 |
| [Guía de compras e inventario](olvanta-api/docs/business/purchasing-and-inventory.md) | Operación, valoración y pruebas de la fase 3 |
| [Guía de ventas y caja](olvanta-api/docs/business/sales-and-cash.md) | Cotización, cobros, devoluciones y cierre de la fase 4 |
| [Guía de facturación](olvanta-api/docs/business/electronic-billing.md) | Firma, envío de prueba, notas, bajas y conciliación de la fase 5 |
| [Guía de alquileres](olvanta-api/docs/business/commercial-rentals.md) | Locales, contratos, cuotas, garantías y cobros de la fase 6 |
| [Guía de estacionamientos](olvanta-api/docs/business/parking.md) | Ocupación, entradas, salidas, tarifas y abonos de la fase 7 |

## Arranque local

Con Docker Compose y PowerShell, ejecute desde la raíz:

    ./olvanta-api/scripts/Start-Local.ps1

Abra [Olvanta](http://127.0.0.1:5180). El usuario inicial es admin. Su contraseña de aprovisionamiento se genera localmente en el archivo olvanta-api/deploy/.env, excluido de Git.

## Integración continua

GitHub Actions usa el mismo checkout para ambos componentes y ejecuta la [CI integrada](.github/workflows/ci.yml) en pull requests y en cambios de `main` o `develop`. Compila y prueba cada componente, arranca Docker Compose con un SQL Server vacío, coteja el contrato OpenAPI, ejecuta las pruebas de navegador y comprueba la recuperación tras una interrupción de la base. Los README de API y web incluyen los comandos de verificación local.

El [plan original](olvanta-api/docs/plan.md) registra la estructura inicial. La API y la web se mantienen desde este repositorio único.
