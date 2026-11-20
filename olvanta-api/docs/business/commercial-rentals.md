# Alquiler de locales · 0.6.0

## Desde el local hasta el cobro

1. Registre el arrendatario en **Directorio → Clientes** y un servicio de alquiler en **Catálogo → Servicios** con la moneda operativa de la sucursal. La moneda y el impuesto del servicio se copian al registrar el contrato. La renta pactada incluye el impuesto.
2. En **Inmuebles → Alquileres**, registre el código, nombre, dirección y características del local. La consulta de disponibilidad indica si un contrato ocupa el local en la fecha elegida.
3. Pulse **Crear contrato**. Elija arrendatario y servicio, indique el inicio, el fin, la renta mensual, la garantía, el día de vencimiento y las condiciones. Las fechas son inclusivas. Dos contratos del mismo local pueden ser consecutivos.
4. En el contrato, elija un **Periodo de renta** y pulse **Generar cuota**. Se crea una cuenta por cobrar en Ventas con el mismo arrendatario, importe y vencimiento. Repetir la generación recupera la cuota existente. Un supervisor o administrador también puede autorizar un cargo adicional con concepto, referencia única e importe.
5. Abra una sesión desde **Ventas y caja → Caja** en la moneda del contrato. En la fila de la cuota use **Cobrar** para registrar un abono parcial o total en efectivo, tarjeta o transferencia. Ventas y Alquileres consultan la misma cuenta. Solo el efectivo mueve el importe esperado en el arqueo.
6. En la misma fila, **Facturación** abre el módulo común con la cuenta seleccionada. Permite preparar el comprobante o consultar el ya generado. Rigen la configuración de series, los documentos de identidad y las reglas de la [fase de facturación](electronic-billing.md). La instalación local sigue en simulación.
7. Use **Operar garantía** para recibir o devolver un depósito. Cada movimiento identifica contrato, caja, medio, importe y operador. La custodia puede ser parcial y tiene como máximo la garantía pactada. La devolución tiene como máximo el saldo en custodia. La garantía se concilia en caja por separado y la deuda por rentas sigue su propio historial.

## Reglas del calendario

La renta se calcula por mes calendario. Para un periodo parcial se cuentan los días de vigencia, incluidos los extremos, y se divide entre los días reales del mes. Se redondea una vez a dos decimales, con los empates hacia arriba. Por ejemplo, una renta de PEN 310 del 16 al 31 de enero genera PEN 160. El mes completo conserva la renta pactada.

El vencimiento usa el día acordado. Si el mes tiene menos días o la vigencia comienza después o termina antes, se usa el día más cercano dentro del periodo cubierto. Una cuota con saldo pasa a **Vencida** al día siguiente del vencimiento según la fecha de Lima. La mora se muestra como deuda vencida. Cualquier importe adicional requiere autorización y un concepto explícito.

**Renovar** crea un nuevo contrato para el mismo local y arrendatario desde el día siguiente al fin efectivo del anterior. Los importes y condiciones de ambos quedan disponibles por separado. La garantía del contrato anterior se devuelve desde ese contrato. **Finalizar contrato** registra un último día y un motivo, preservando el fin pactado. Una fecha que recortaría un periodo ya cargado o rompería una renovación registrada produce un conflicto. Las cuotas pendientes siguen siendo cobrables después del fin de la vigencia.

## Permisos y verificación

| Rol de la sucursal | Acciones |
| --- | --- |
| Encargado de alquileres | Locales, contratos, cuotas, renovación y finalización |
| Cajero | Consulta, abonos y movimientos de garantía en una caja abierta |
| Supervisor y administrador | Operación completa y autorización de cargos adicionales |
| Auditor | Consulta de contratos, deuda y garantía |

El escenario `olvanta-web/tests/e2e/rentals.spec.ts` usa un SQL Server de prueba. Comprueba contratos solapados y cuotas simultáneas, prorrateos, permisos por sucursal, un abono en efectivo y otro por transferencia, recepción y devolución parcial de una garantía, arqueo, facturación simulada, finalización y renovación. La prueba de calendario de la API cubre febrero bisiesto y fines de vigencia parciales.
