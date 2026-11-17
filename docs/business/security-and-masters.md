# Seguridad y maestros · Fase 2

La entrega 0.2.0 cubre IAM-01 y CAT-01. La web y la API comparten origen. La base, el usuario SQL y la clave inicial de la fase 1 se conservan al actualizar. El proceso `migrate` aplica las migraciones de EF Core antes de arrancar la API, crea la empresa Olvanta y la sucursal Principal en una base vacía y aprovisiona la cuenta administradora.

## Primer acceso y administración

1. Abra la raíz de la web e ingrese con el usuario `admin` y el valor local `OLVANTA_ADMIN_PASSWORD` de `olvanta-api/deploy/.env`. **Mi cuenta** permite cambiar la contraseña. El valor inicial del archivo solo sirve para el aprovisionamiento.
2. En **Administración → Organización**, registre la razón social y el RUC si corresponde. En **Sucursales**, cree las sedes. Cada una tiene moneda e impuesto iniciales para nuevos productos y servicios. La tasa inicial es cero hasta que el administrador configure su operación.
3. En **Organización**, elija la sucursal y registre sus establecimientos. Cada serie pertenece a un establecimiento y es única por tipo de documento para la empresa. La emisión y los correlativos pertenecen a la fase 5.
4. En **Usuarios**, cree una cuenta y asigne un rol por sucursal. Puede dejar una sede sin acceso. El administrador puede restablecer la contraseña de una cuenta después de verificar la identidad del solicitante. El restablecimiento invalida las sesiones anteriores. El cambio propio requiere la contraseña actual.
5. En **Auditoría**, consulte las últimas cien operaciones, con fecha, actor y detalle. El cambio de permisos registra las asignaciones resultantes del usuario dentro de la misma transacción.

| Rol | Alcance |
| --- | --- |
| Administrador | Administración de empresa, sucursales, parámetros, usuarios y accesos. Lectura y escritura de todos los maestros. |
| Supervisor y vendedor | Lectura, alta, edición e importación de maestros en las sucursales asignadas. |
| Almacenero, cajero, alquileres, estacionamiento, facturación y auditor | Consulta de maestros e historial en sus sucursales asignadas. |

Los permisos se calculan a partir de las asignaciones actuales en SQL Server en cada operación. Una cuenta sin asignaciones puede iniciar sesión y cambiar su contraseña. Las cuentas se retiran de la operación quitando sus asignaciones. El acceso administrativo global se reserva a la cuenta aprovisionada.

## Operación de maestros

Seleccione la sucursal activa antes de consultar clientes, proveedores, productos o servicios. La búsqueda admite nombre y número de documento o código interno. Los resultados se ordenan por nombre e identificador, con filtro por estado y veinte registros por página en la web. **Ver detalle** muestra los datos comerciales y los últimos cien movimientos de la ficha.

Los códigos son únicos por sucursal y clase de maestro. El código se guarda en mayúsculas. Las fichas de personas incluyen clase, tipo y número de documento, dirección, contacto y condiciones comerciales. La importación admite JSON de hasta 200 filas y ofrece una plantilla en la interfaz. Se valida el lote completo y se confirma en una transacción.

Productos y servicios comparten categoría, unidad, precio, moneda e impuesto. El producto admite presentación, conversión a unidades y código de barras. El servicio carece de efectos de inventario. Las monedas iniciales son PEN y USD. La tasa y la moneda sugeridas se copian al crear una ficha y pueden ajustarse. La inactivación conserva el identificador y el historial. SQL Server usa rowversion para detectar ediciones sobre una versión anterior.

## Sesión y contrato

Las operaciones protegidas requieren una cookie HttpOnly. Para POST y PUT, obtenga un token con `GET /api/v1/auth/csrf` y envíelo en `X-CSRF-TOKEN`, incluyendo el inicio de sesión. Vuelva a obtener el token tras cambiar de identidad. El bloqueo de Identity se activa después de cinco intentos fallidos durante quince minutos. El entorno local utiliza HTTP en loopback. La configuración por defecto exige cookies seguras sobre HTTPS y SameSite Strict.

La API devuelve 401 cuando falta una sesión válida, 403 cuando falta el permiso correspondiente y 404 cuando el identificador solicitado no pertenece a la colección autorizada. Una versión desactualizada o una clave duplicada devuelve 409. Los ejemplos y esquemas de solicitud están en el [contrato v1](../../contracts/olvanta-v1.json).

## Reproducción

Las pruebas de extremo a extremo crean usuarios, sucursales y registros de muestra. Use una instancia de prueba independiente. Desde la carpeta contenedora, con las dependencias web y Chromium de Playwright instalados:

```powershell
$env:COMPOSE_PROJECT_NAME = 'olvanta-phase2-check'
$env:OLVANTA_PORT = '15180'
./olvanta-api/scripts/Start-Local.ps1
$env:OLVANTA_BASE_URL = 'http://127.0.0.1:15180'
$env:OLVANTA_TEST_ADMIN_PASSWORD = ((Select-String -LiteralPath './olvanta-api/deploy/.env' -Pattern '^OLVANTA_ADMIN_PASSWORD=').Line -split '=', 2)[1]
Push-Location olvanta-web
try { npm run test:e2e } finally { Pop-Location }
docker compose -p olvanta-phase2-check -f olvanta-api/deploy/compose.yaml down
Remove-Item Env:OLVANTA_TEST_ADMIN_PASSWORD, Env:OLVANTA_BASE_URL, Env:OLVANTA_PORT, Env:COMPOSE_PROJECT_NAME
```

Las pruebas comprueban permisos de lectura y edición, aislamiento de registros, CSRF, revocación de permisos, auditoría, recuperación de contraseña, inactivación, conflicto de versión, importación atómica, configuración de una sucursal y acceso real desde Chromium. El SQL de pruebas tiene su propio volumen.
