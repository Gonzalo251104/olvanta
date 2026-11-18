# Ventas y caja · Fase 4

En **Comercial → Ventas y caja**, seleccione la sucursal. Las ventas, el kardex y la caja usan su moneda. La web ofrece un documento comercial y un arqueo imprimibles.

1. En **Cotizar**, elija un cliente, las partidas y la condición de pago. El crédito requiere fecha de vencimiento. En productos, seleccione el almacén y lote desde los que se despachará. Los servicios se cotizan por unidad.
2. Guarde la cotización, conviértala en pedido y confirme. La confirmación de todas las partidas se realiza en una transacción y reserva el stock. Si alguna partida excede el disponible, el pedido completo queda pendiente. Puede anular una venta confirmada sin cobros para liberar sus reservas.
3. En **Caja**, abra la sesión de la sucursal con el efectivo inicial. Desde el documento confirmado, **Registrar cobro** admite pagos completos o parciales en efectivo, tarjeta o transferencia. **Por cobrar** muestra el saldo y el vencimiento de los documentos pendientes.
4. **Despachar** consume la reserva y registra la salida al costo promedio del almacén. Una venta al contado requiere saldo cero para despacharse. Una venta a crédito puede despacharse con deuda.
5. En una venta despachada, **Registrar devolución** indica la partida, cantidad y motivo. Reduce el importe exigible y repone los productos al costo de su despacho en el lote original. Si resulta un saldo a favor, **Reembolsar** registra la devolución de dinero vinculada a ese documento y devolución.
6. En **Caja**, registre ingresos y retiros con su motivo. Para cerrar, elija **Cierre por arqueo** e ingrese el efectivo contado. El esperado incluye el fondo inicial, cobros en efectivo e ingresos, menos reembolsos en efectivo y retiros. El arqueo conserva el contado, el esperado, la diferencia y el historial del turno. Los importes con tarjeta y transferencia figuran en el historial con su medio de pago.

## Precios y permisos

La cotización conserva la identificación del cliente y las descripciones, unidades, precios, tasas y descuentos del momento. El precio de catálogo se interpreta como precio final con impuesto incluido. Se aplica el descuento porcentual por partida y se redondea el total al céntimo comercial, con los empates alejándose de cero. El impuesto incluido se obtiene restando al total su base redondeada. Una devolución parcial reparte el total original de forma acumulada para recuperar exactamente el importe al devolver la última unidad.

El vendedor cotiza, tramita pedidos y confirma. El almacenero despacha. El cajero registra cobros, reembolsos, ingresos, retiros y arqueos. El supervisor y el administrador también autorizan descuentos y devoluciones. El auditor consulta los documentos y sesiones de sus sucursales. Hay una caja abierta por sucursal. Cada operación lleva un identificador que permite repetir una petición con el mismo contenido y recuperar el resultado original.

## Verificación de POS-01

La prueba de navegador usa una base aislada. Desde `olvanta-web`, con `OLVANTA_BASE_URL` y `OLVANTA_TEST_ADMIN_PASSWORD` apuntando al entorno de prueba, ejecute `npm run test:e2e -- sales.spec.ts`.

El caso cobra una venta de PEN 23,60 en efectivo, devuelve y reembolsa PEN 11,80, y abona PEN 15 con tarjeta a otra venta de PEN 45. Con un fondo inicial de PEN 100, un ingreso de PEN 5 y un retiro de PEN 3, el esperado es PEN 113,80. Contar PEN 113,50 deja una diferencia de −PEN 0,30 y una deuda de PEN 30. El escenario verifica también la repetición del cobro, el aislamiento por sucursal, los permisos y la reversión de una reserva incompleta.
