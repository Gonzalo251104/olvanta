# Contribuir a Olvanta

Abra una rama `feature/<issue>-<description>` desde la rama de integración. Use `main` como base inicial y `develop` cuando esté habilitada. Vincule el requisito del backlog en el pull request y use Conventional Commits en inglés.

Incluya en el mismo pull request los cambios de API, contrato y web que deban desplegarse juntos. Cuando cambie el contrato OpenAPI, actualice ambas instantáneas y regenere el cliente TypeScript.

Las ramas `release/<version>` estabilizan una entrega. Cree `hotfix/<issue>-<description>` desde `main` para correcciones urgentes y propague los cambios a las ramas activas. Registre la versión compatible de cada componente en la entrega.

La [CI integrada](.github/workflows/ci.yml) verifica el mismo commit de principio a fin.
