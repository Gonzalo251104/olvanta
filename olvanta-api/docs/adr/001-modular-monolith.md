# ADR 001 · Monolito modular

**Decisión.** La API HTTP y el worker comparten Application, Domain e Infrastructure. Application referencia Domain. Infrastructure implementa persistencia e integraciones. Los procesos ejecutables componen las dependencias. Cada módulo será propietario de sus reglas y de su esquema en un contexto EF Core común.

**Motivo.** Ventas, almacén y caja necesitan transacciones locales y una entrega coordinada. Un único backend permite preservar esos límites y probar concurrencia con SQL Server. Los trabajos externos se incorporarán mediante outbox cuando exista la primera integración.

**Consecuencia.** La fase 1 comprueba acceso a la base. El primer esquema funcional y sus migraciones llegarán con los maestros. El worker arranca y admite apagado ordenado, a la espera de los trabajos de las fases posteriores.
