# Versión integrada · 1.0.0

## Despliegue local

Desde la raíz del repositorio ejecute `./olvanta-api/scripts/Start-Local.ps1`. Docker Compose levanta SQL Server, ejecuta el aprovisionamiento y las migraciones y pone la API y la web en `http://127.0.0.1:5180`. La aplicación usa la [configuración de arranque de la API](../../README.md). Docker necesita memoria disponible para SQL Server. La comprobación de disponibilidad, `./olvanta-api/scripts/Test-Local.ps1`, requiere web, API y base en estado saludable.

| Componente | Versión o fuente |
| --- | --- |
| API, worker y web | 1.0.0 en el mismo commit |
| Contrato REST | OpenAPI v1, versión 1.0.0, idéntico en ambos componentes |
| SQL Server | SQL Server 2025 CU11 Developer para la demostración |
| Migración más reciente | `20261119050055_MaintenanceAndReconciliation` |
| Imágenes base fijadas por digest | [Docker Compose](../../deploy/compose.yaml), [Dockerfile de API](../../Dockerfile), [Dockerfile de web](../../../olvanta-web/Dockerfile) |

La verificación de la entrega usa el entorno local y los ejecutores de CI con datos sintéticos. La operación fiscal de la demostración usa simulación.

## Jornada de demostración

La [prueba de la jornada integrada](../../../olvanta-web/tests/e2e/release.spec.ts) opera una sola sucursal y una sola caja mediante la web y la API. Ejecútela en una base desechable. Crea un cliente, un proveedor, una zona de estacionamiento y los demás maestros de la jornada.

1. Registra una recepción de 22 kg brutos y 2 kg de tara. Vende y despacha 2 kg y recupera la constancia fiscal simulada de PEN 23,60.
2. Genera una cuota de alquiler de PEN 100, cobra PEN 40 y conserva una deuda de PEN 60.
3. Registra una estancia de estacionamiento, cobra PEN 7,50 por transferencia y libera la plaza.
4. Cierra la caja. Los PEN 100 de fondo y los PEN 63,60 en efectivo dan PEN 163,60 de saldo esperado. La diferencia del arqueo es cero.
5. Comprueba los 18 kg restantes y su valor de PEN 144. Relaciona el lote con el proveedor y confirma que solo aparece el comprobante rechazado en pendientes.

## Reportes y exportación

Abra **Resultados → Reportes** con la sucursal activa. Los permisos se aplican en el servidor tanto a la vista como a las descargas. Los reportes con fecha usan días de Lima y extremos inclusivos. Ventas usa la fecha de confirmación y el usuario que creó la cotización. Las rentas y el estacionamiento tienen sus propios informes. Los arqueos cerrados se consultan por fecha de cierre y los abiertos por fecha de apertura. Cuentas por cobrar, ocupación, stock, trazabilidad y comprobantes pendientes se calculan al consultar.

Cada reporte ofrece CSV. **Arqueos de caja** ofrece además PDF. Se pueden filtrar ventas por vendedor y los informes de inventario por código de lote. La prueba integrada verifica las once vistas y descarga el arqueo en ambos formatos. Para volúmenes superiores a 5.000 filas, la API devuelve un mensaje que permite ajustar los filtros aplicables.

## Respaldo y restauración

El respaldo contiene los datos comerciales, las cuentas y los archivos fiscales. Guárdelo en un lugar privado junto con la información de acceso de la instalación. Los archivos de claves de sesión y los certificados configurados fuera de SQL Server se administran aparte. La copia restaurada tiene sus propias claves de sesión y se accede iniciando sesión de nuevo con una cuenta de la base respaldada.

Los siguientes comandos se ejecutan desde la raíz del repositorio en PowerShell. Use un nombre nuevo de proyecto y un puerto libre.

```powershell
./olvanta-api/scripts/Backup-Local.ps1 -File ./olvanta-api/artifacts/olvanta.bak
./olvanta-api/scripts/Restore-Local.ps1 -File ./olvanta-api/artifacts/olvanta.bak -Project olvanta-recuperacion -Port 5191
```

El respaldo usa `COPY_ONLY`, compresión y checksum. La restauración verifica el archivo, crea la base en un volumen limpio, ejecuta `DBCC CHECKDB` en modo físico y vincula el usuario de base restaurado con el login de la aplicación. El script rechaza el proyecto habitual y destinos Docker que ya tengan contenedores o volúmenes. La restauración deja el entorno de recuperación accesible por el puerto elegido.

Cuando el respaldo corresponde a la prueba integrada, compare los resultados desde el origen.

```powershell
$evidence = Get-ChildItem ./olvanta-web/test-results -Recurse -Filter release-evidence.json | Select-Object -First 1
./olvanta-api/scripts/Verify-Restore.ps1 -Evidence $evidence.FullName -BaseUrl http://127.0.0.1:5191
```

La verificación compara el contrato de versión, las filas de los once reportes y el SHA-256 de una constancia fiscal recuperada. GitHub Actions ejecuta el ensayo en un segundo proyecto de Compose y conserva el origen hasta que termina el respaldo.
