# Olvanta Web

Interfaz en español de la plataforma de operaciones comerciales Olvanta.

La web 0.4.0 usa el contrato de API 0.4.0. La sucursal activa determina los maestros y las operaciones disponibles. Abra ventas y caja en `/ventas`, compras e inventario en `/inventario` y el estado técnico en `/estado`.

## Arranque

Desde la raíz del repositorio de Olvanta ejecute en PowerShell:

```powershell
./olvanta-api/scripts/Start-Local.ps1
```

Abra http://127.0.0.1:5180. Para desarrollar con recarga en vivo, arranque la API en el puerto 5080 siguiendo el README de backend. Desde este repositorio use la versión de Node de `.nvmrc` y ejecute `npm ci` seguido de `npm run dev`.

## Verificaciones

```sh
npm ci
npm run check:api
npm run lint
npm test
npm run build
npx --no-install playwright install chromium
npm run test:e2e
```

Los escenarios de seguridad crean datos de prueba. Se habilitan con `OLVANTA_TEST_ADMIN_PASSWORD` en un entorno desechable. El procedimiento está en la [guía de seguridad y maestros](../olvanta-api/docs/business/security-and-masters.md). La prueba de navegador utiliza la aplicación integrada en el puerto 5180. Puede indicar otra URL con `OLVANTA_BASE_URL`. La [CI integrada](../.github/workflows/ci.yml) prueba la web con la API del mismo commit y un SQL Server nuevo.

En la revisión inicial, `npm audit` señala `picomatch` y sus dependientes entre las herramientas de desarrollo (GHSA-fc5q-39xc-vjpf). Los patrones utilizados por estas herramientas se definen en el repositorio. `npm audit --omit=dev` devuelve cero avisos. Revise la disponibilidad de una corrección al actualizar el lockfile.

Para actualizar el cliente copie la instantánea oficial de `olvanta-api/contracts/olvanta-v1.json` a `contracts/`, ejecute `npm run generate:api` y revise los cambios de tipos y comportamiento. La arquitectura y las reglas comunes están en `olvanta-api/docs`.
