# Olvanta API

Backend de operaciones comerciales en ASP.NET Core y SQL Server.

## Arranque local

Clone el repositorio de Olvanta. Utilice Docker con contenedores Linux en x86-64, Docker Compose y PowerShell. SQL Server necesita al menos 2 GB de memoria disponibles para su contenedor. Desde la raíz del repositorio ejecute:

```powershell
./olvanta-api/scripts/Start-Local.ps1
```

Abra **http://127.0.0.1:5180**. Ingrese con el usuario `admin` y la contraseña inicial `OLVANTA_ADMIN_PASSWORD` de `deploy/.env`. Puede consultar la disponibilidad en `/estado`. La primera ejecución descarga imágenes, compila y crea el volumen de SQL Server. El script genera las credenciales SQL y la contraseña inicial de administración en `deploy/.env`, excluido de Git. Conserve ese archivo para reutilizar el volumen. Para cambiar el puerto establezca `$env:OLVANTA_PORT` antes de arrancar.

| Ruta | Verificación |
| --- | --- |
| `/health/live` | El proceso HTTP responde. |
| `/health/ready` | La conexión a SQL Server funciona. Devuelve 503 si SQL está caído. |
| `/api/v1/system/status` | Estado que consume el navegador, con HTTP 200 o 503. |
| `/openapi/v1.json` | Contrato REST generado por la API. |

Para comprobar el entorno en ejecución use `./olvanta-api/scripts/Test-Local.ps1`. Para detenerlo conservando los datos, desde `olvanta-api/deploy` ejecute `docker compose down`. Desde esa misma carpeta, `docker compose ps -a` muestra los procesos y `docker compose logs api sql` permite investigar un fallo. El servicio `sql-init` finaliza correctamente al crear la base y el usuario de ejecución.

## Desarrollo y verificaciones

Desde este repositorio, con el SDK indicado en `global.json`:

```powershell
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
```

Para depurar fuera de Docker defina `ConnectionStrings__Olvanta` con una conexión a su base local y ejecute `dotnet run --project src/Olvanta.Api --urls http://127.0.0.1:5080`. El proxy de Vite usa ese puerto. Para HTTP local configure `Security__LocalHttp=true`. Aplique primero las migraciones con una conexión de aprovisionamiento y `dotnet run --project src/Olvanta.Api -- --initialize`, proporcionando `Bootstrap__AdminPassword` para una base nueva.

La [CI integrada](../.github/workflows/ci.yml) valida el contrato servido y la comunicación con la web del mismo commit.

### Verificación de la fase 1

El 17 de noviembre de 2026 se construyeron las imágenes con restauración bloqueada y se inicializó un volumen SQL vacío. La página consultó la base desde Chromium y repitió la consulta con el botón de actualización. La instantánea OpenAPI coincidió con el contrato servido por el proxy. Al detener SQL, `live` conservó HTTP 200 y tanto `ready` como el estado de la aplicación devolvieron HTTP 503. Al reiniciar SQL, la verificación integrada volvió a pasar con el mismo volumen.

## Definición del proyecto

- [Guía de seguridad y maestros](docs/business/security-and-masters.md)
- [Guía de compras e inventario](docs/business/purchasing-and-inventory.md)
- [Plan original del 17 de noviembre de 2026](docs/plan.md)
- [Alcance, backlog y criterios de aceptación](docs/business/backlog.md)
- [Glosario y reglas del negocio](docs/business/glossary.md)
- [Decisiones de arquitectura, identidad y contrato](docs/adr)
- [Guía de ventas y caja](docs/business/sales-and-cash.md)
- [Contrato OpenAPI 0.6.0](contracts/olvanta-v1.json)
- [Alquiler de locales](docs/business/commercial-rentals.md)
- [Facturación electrónica en simulación](docs/business/electronic-billing.md)
- [Flujo de contribución](CONTRIBUTING.md)
