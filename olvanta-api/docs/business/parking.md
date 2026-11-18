# Estacionamientos · 0.7.0

## Preparar la operación

1. Registre un servicio de estacionamiento en **Catálogo → Servicios**, en la moneda de la sucursal y con el impuesto correspondiente.
2. En **Movilidad → Estacionamientos → Configuración**, un supervisor o administrador registra la playa, sus zonas y la capacidad de cada una. Publique una tarifa con precio por bloque incluido el impuesto, duración del bloque y tolerancia. Una tarifa nueva se usa para ingresos posteriores.
3. Abra una sesión en **Ventas y caja → Caja** para cobrar salidas y abonos. El operador de estacionamientos puede registrar ingresos y cobrar en esa sesión. El cajero consulta y cobra. El auditor consulta.

## Ingreso, salida y abonos

- **En curso** muestra la ocupación de cada zona y las estancias activas. Ingrese la placa y seleccione una zona con plazas disponibles. Se eliminan espacios y guiones y se guardan las letras en mayúsculas. Cada placa puede aparecer una vez en curso por estacionamiento.
- El **ticket de ingreso** muestra placa, zona, hora y tarifa congelada. Para dar salida, abra el ticket, revise el importe y elija el medio de pago. Si pasó a otro bloque desde la consulta, use **Recalcular salida**. La plaza vuelve a estar disponible al confirmar la salida.
- **Abonos** registra el precio y la vigencia pagada para una placa en un estacionamiento. Las fechas son inclusivas en hora de Lima. Un abono vigente al ingreso se aplica a la estancia y cubre hasta el fin de su último día. La compra posterior al ingreso se aplica a futuras estancias. Después del vencimiento se calculan bloques solo por el tiempo descubierto.
- La tolerancia cubre un intervalo facturable de duración igual o inferior al límite. Si se supera, se redondea hacia arriba por bloques iniciados y se cuenta el intervalo facturable completo. Con bloques de 30 minutos y 5 minutos de tolerancia, exactamente 5 minutos son gratis, 5 minutos y un segundo cuestan un bloque y 31 minutos cuestan dos.
- **Salidas** muestra las últimas 100 operaciones de la vista operativa y permite buscar una placa en todo el historial. Imprima el ticket o la constancia de salida desde su ficha. Al cobrar con un cliente identificado queda disponible su venta en Facturación. La instancia local usa [facturación en simulación](electronic-billing.md). Un cobro sin ficha de cliente genera constancia interna y movimiento de caja.

El cobro y la salida se guardan en la misma transacción. Los reintentos idénticos recuperan el registro previo. Una salida cubierta por abono o tolerancia libera la plaza con importe cero. Los pagos en efectivo incrementan el efectivo esperado y los demás medios quedan identificados en el historial de caja.

El escenario `olvanta-web/tests/e2e/parking.spec.ts` comprueba entradas concurrentes, placa normalizada, preservación de tarifa, rechazo de cotización desactualizada, reintento de salida, abono, ocupación, impresión, permisos y arqueo. Las pruebas de API verifican los límites de tolerancia, bloques y fin de cobertura.
