# Contribuir

La rama de integración es `develop`. Abra una rama `feature/<issue>-<description>` desde ella y vincule el requisito del backlog en el pull request. Use Conventional Commits en inglés, por ejemplo `feat(system): expose database readiness`.

Una rama `release/<version>` estabiliza una entrega. Integre la entrega en `main`, etiquete la versión semántica y propague los cambios a `develop`. Cree `hotfix/<issue>-<description>` desde `main` para una corrección urgente y propáguela a las ramas activas. Cada repositorio conserva su propia versión y registra la versión compatible del otro.

Antes de integrar, ejecute los controles del README y revise los cambios de contrato. Al conectar los repositorios a GitHub, configure las verificaciones obligatorias de CI para `main` y `develop`.
