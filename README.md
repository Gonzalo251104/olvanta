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

Los flujos de GitHub Actions incluidos en cada componente corresponden a su estructura anterior de repositorios independientes. Los comandos de verificación local están en el README de cada componente.
