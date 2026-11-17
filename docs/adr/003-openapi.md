# ADR 003 · Contrato versionado

**Decisión.** Los controladores publican OpenAPI en `/openapi/v1.json`. El backend guarda una instantánea en `contracts/olvanta-v1.json`. La web conserva una copia identificada por versión y genera sus tipos con openapi-typescript, utilizados por openapi-fetch.

**Motivo.** Cada repositorio puede compilar por separado. Los cambios incompatibles requieren coordinar ambas entregas y conservar una versión cliente reproducible.

**Consecuencia.** La CI compara el documento servido con la instantánea y comprueba la generación de tipos. El primer contrato 0.1.0 devuelve 200 cuando SQL responde o 503 cuando la API responde y SQL está indisponible. El cliente distingue esos casos de un fallo de red.
