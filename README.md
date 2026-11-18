# Olvanta

Plataforma de operaciones comerciales para una empresa peruana. La versión 0.2.0 incluye usuarios y permisos por sucursal, auditoría, configuración de la organización y maestros de clientes, proveedores, productos y servicios.

| Componente | Contenido |
| --- | --- |
| [API](olvanta-api/README.md) | ASP.NET Core, Identity, SQL Server y Docker Compose |
| [Web](olvanta-web/README.md) | Aplicación React y TypeScript |
| [Plan](olvanta-api/docs/plan.md) | Alcance y fases del proyecto |
| [Guía de seguridad y maestros](olvanta-api/docs/business/security-and-masters.md) | Uso y verificación de la fase 2 |

## Arranque local

Con Docker Compose y PowerShell, ejecute desde la raíz:

    ./olvanta-api/scripts/Start-Local.ps1

Abra [Olvanta](http://127.0.0.1:5180). El usuario inicial es admin. Su contraseña de aprovisionamiento se genera localmente en el archivo olvanta-api/deploy/.env, excluido de Git.

## Integración continua

GitHub Actions usa el mismo checkout para ambos componentes y ejecuta la [CI integrada](.github/workflows/ci.yml) en pull requests y en cambios de `main` o `develop`. Compila y prueba cada componente, arranca Docker Compose con un SQL Server vacío, coteja el contrato OpenAPI, ejecuta las pruebas de navegador y comprueba la recuperación tras una interrupción de la base. Los README de API y web incluyen los comandos de verificación local.

El [plan original](olvanta-api/docs/plan.md) registra la estructura inicial. La API y la web se mantienen desde este repositorio único.
