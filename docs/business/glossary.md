# Glosario y reglas de negocio

| Término | Nombre técnico | Regla |
| --- | --- | --- |
| Sucursal | Branch | Acota permisos, operaciones e informes de una empresa. Cada acceso se valida sobre el recurso. |
| Almacén | Warehouse | Conserva existencias por ubicación. Un traslado registra origen y destino dentro de la misma operación. |
| Kardex | StockMovement | Los movimientos confirmados forman el historial. Los ajustes se hacen mediante compensaciones. |
| Reserva | Reservation | Reduce el disponible sin alterar el stock físico. La confirmación protege frente a operaciones concurrentes. |
| Lote | Lot | Identifica una recepción y su procedencia hasta el despacho. Peso neto es peso bruto menos tara. |
| Venta | Sale | Confirmación comercial, despacho, pago y aceptación fiscal tienen estados independientes. |
| Cobro | Payment | Se asigna a una o varias deudas. Los pagos parciales conservan el saldo pendiente. |
| Caja | CashSession | El cierre compara movimientos esperados y efectivo contado y conserva la diferencia. |
| Comprobante | FiscalDocument | Conserva datos y cálculos emitidos y un correlativo único por empresa, tipo y serie. |
| Cuota | RentCharge | Corresponde a un contrato y periodo. La garantía se contabiliza por separado. |
| Estancia | ParkingStay | Una placa tiene como máximo una estancia activa por estacionamiento. La tarifa se conserva al cobrar. |
| Merma | Loss | Justifica la diferencia entre las cantidades de entrada y salida de un reenvasado. |

Los importes tienen moneda y precisión decimal explícitas. Los instantes se guardan en UTC y cada fecha de negocio se interpreta en su zona. Una operación confirmada conserva su historia. Los reintentos de cobros, envíos y generación de cuotas deben preservar el resultado original.
