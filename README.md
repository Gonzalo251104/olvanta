# Olvanta Web

Interfaz en español de la plataforma de operaciones comerciales Olvanta.

La primera pantalla consulta la API, muestra la disponibilidad de SQL Server y permite repetir la comprobación. El entorno integrado usa la web 0.1.0 y el contrato de API 0.1.0.

## Arranque

Desde la carpeta que contiene ambos repositorios ejecute en PowerShell:

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

La prueba de navegador utiliza la aplicación integrada en el puerto 5180. Puede indicar otra URL con `OLVANTA_BASE_URL`. El workflow de CI clona el backend del mismo propietario de GitHub. La variable `OLVANTA_API_REF` fija la revisión compatible (por defecto, `develop`). Para repositorios privados se necesita el secreto `OLVANTA_READ_TOKEN` con lectura de contenidos.

En la revisión inicial, `npm audit` señala `picomatch` y sus dependientes entre las herramientas de desarrollo (GHSA-fc5q-39xc-vjpf). Los patrones utilizados por estas herramientas se definen en el repositorio. `npm audit --omit=dev` devuelve cero avisos. Revise la disponibilidad de una corrección al actualizar el lockfile.

Para actualizar el cliente copie la instantánea oficial de `olvanta-api/contracts/olvanta-v1.json` a `contracts/`, ejecute `npm run generate:api` y revise los cambios de tipos y comportamiento. La arquitectura y las reglas comunes están en `olvanta-api/docs`.
