# Comercialización de aceitunas · 0.8.0

## Recepción por peso

1. En **Catálogo → Productos**, registre la aceituna a granel en kg con factor 1. Cree un producto para cada presentación comercial, por ejemplo una bolsa con factor 0,5 o un balde con factor 5. Para los envases, el factor indica los kilogramos netos por unidad. Configure los precios por kilo o envase en sus respectivas fichas.
2. Genere la compra en **Compras e inventario** con el proveedor, los kilos netos esperados y el costo por kilo.
3. En **Aceitunas → Recepción pesada**, elija la partida y el almacén. Registre el código interno, el lote del proveedor, la procedencia, la variedad, la clasificación, el peso bruto, la tara y el vencimiento si aplica. Olvanta resta la tara del bruto y reduce lo pendiente por el peso neto. Cada pesada usa un código interno nuevo para el producto. Una entrega parcial puede usar el mismo lote del proveedor con otro código interno.

## Reenvasado y merma

Seleccione el lote de origen y su almacén. Indique la cantidad a consumir, las presentaciones resultantes, sus códigos nuevos, la merma pesada y el motivo. Se pueden registrar varias salidas de un solo lote en el mismo almacén. Para una merma total, retire las presentaciones resultantes e indique el peso consumido como merma.

El peso consumido debe igualar el peso de las presentaciones más la merma. Se admiten seis decimales en kilos y cantidades enteras para los envases. El kardex registra el consumo de las existencias disponibles y las entradas de las nuevas presentaciones con el mismo vencimiento. El factor queda congelado en cada lote. Editar un factor en el catálogo afecta a los lotes que se creen después.

Por ejemplo, consumir 12 kg a PEN 8/kg puede producir veinte bolsas de 0,5 kg y un balde de 1 kg, con una merma de 1 kg. Las bolsas ingresan con PEN 80, el balde ingresa con PEN 8 y la merma retira PEN 8 del inventario. Se aplica el promedio ponderado del almacén y se conserva el valor total en la distribución a seis decimales.

## Despacho y trazabilidad

Use los flujos existentes de traslados, ventas y devoluciones para operar las presentaciones. Seleccione el producto, su almacén y su lote. El precio de venta corresponde a la unidad de la ficha. Desde **Aceitunas → Trazabilidad**, seleccione un lote o elija un cliente y abra uno de sus despachos o devoluciones. La ruta muestra el proveedor, la recepción medida, el lote original y los pesos, valores y motivos de los reenvasados.

Los inventarios anteriores siguen siendo consultables en Compras e inventario. Los lotes con trazabilidad de aceitunas se originan mediante una recepción pesada. Las correcciones de peso de un lote trazable se registran como merma medida para preservar su historia.

La prueba `olvanta-web/tests/e2e/olives.spec.ts` cubre la recepción pesada, la protección contra un balance de masa inválido, el reenvasado concurrente, el traslado, el despacho y la devolución por unidades, la conciliación de valor y el acceso al origen desde un cliente.
