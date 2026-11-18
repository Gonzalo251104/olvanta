# Compras e inventario · Fase 3

En la sucursal activa, abra **Abastecimiento → Compras e inventario**. Cada sucursal trabaja con sus propios productos, proveedores y almacenes.

1. En **Almacenes**, cree las ubicaciones y defina el mínimo disponible por producto y almacén. En **Existencias**, el filtro **Solo bajo mínimo** muestra los productos que requieren reposición, incluso antes de su primera entrada.
2. En **Compras**, elija un proveedor activo, indique su referencia y agregue las partidas. Ingrese las cantidades en la unidad base visible junto al producto y el costo por unidad en la moneda de la sucursal. Por ejemplo, una caja de 12 kg a PEN 60 se ingresa como 12 kg a PEN 5. La referencia es única por proveedor y sucursal.
3. Use **Recibir mercadería** en la partida. Indique almacén, cantidad recibida, guía o motivo y, cuando corresponda, lote y fecha de vencimiento. Cada recepción afecta al almacén elegido. Puede recibir el saldo en otra entrega. El pedido queda completo al recibir todas sus partidas.
4. En **Movimientos**, registre entradas directas, salidas, traslados o reservas. Una reserva bloquea una cantidad en un almacén y lote. Para liberarla, elija la reserva abierta. Un traslado conserva el lote, su vencimiento y el valor transportado entre ambos almacenes de la misma sucursal.
5. Consulte **Kardex** para rastrear el operador, documento, fecha, cantidades físicas y reservadas, y valor de cada operación. Los ajustes de conteo llevan un motivo. Para compensar un movimiento identificado, indique su número en un ajuste de signo opuesto. El original y la corrección permanecen visibles.

## Reglas de saldo y costo

El saldo físico es la suma de las cantidades del kardex. El disponible es el físico menos el reservado. Cada ingreso aporta su costo al valor del producto en ese almacén. Una salida o traslado consume el costo promedio ponderado vigente, calculado sobre todos sus lotes. Por ejemplo, 2 kg a PEN 5 y 1 kg a PEN 20 suman 3 kg y PEN 30. Trasladar 1 kg lleva PEN 10 al destino y deja PEN 20 en el origen. Reservar ese kilo en destino deja 1 kg físico y 0 kg disponibles.

Las cantidades, los costos por unidad y los valores del kardex admiten seis decimales. El último retiro consume el remanente de valor tras el redondeo. Las compras y movimientos fijan la moneda operativa de la sucursal. Un producto con compras, movimientos o mínimos conserva su unidad base. El vencimiento registrado para el lote de un producto se conserva en las operaciones posteriores.

El almacenero registra compras, recepciones, mínimos, movimientos y reservas. El vendedor consulta, reserva y libera. El auditor consulta. El administrador y el supervisor también pueden compensar y ajustar. La API valida la sucursal de cada recurso y registra la auditoría en la misma transacción.

## Cómo verificar INV-01

La CI integrada ejecuta `tests/e2e/inventory.spec.ts` con Chromium y SQL Server. Para repetirlo en un entorno desechable, configure `OLVANTA_BASE_URL` con la URL de esa instancia y `OLVANTA_TEST_ADMIN_PASSWORD` con su credencial de pruebas. Desde `olvanta-web`, ejecute `npm run test:e2e -- inventory.spec.ts`.

El escenario crea una sucursal, dos almacenes, un proveedor y un producto. Comprueba la recepción parcial y la final, la valoración de un traslado, el aislamiento de sucursales, la autorización de ajustes y la reconciliación de cantidades y valores. Al competir por la última unidad, una reserva se confirma y la otra recibe HTTP 409. Reenviar una recepción con el mismo identificador conserva un solo movimiento.
