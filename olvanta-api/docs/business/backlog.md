# Alcance y backlog de Olvanta

Olvanta gestiona las operaciones de una empresa peruana con varias sucursales. La demostración final sigue un lote de aceitunas hasta su venta y comprobante, cobra una cuota de alquiler y una estancia de estacionamiento y reconcilia las tres operaciones al cerrar caja. El primer recorrido comercial cubre compra, recepción, venta, cobro y cierre.

El alcance inicial incluye compras básicas, inventario, ventas, caja, cuentas por cobrar, comprobantes peruanos, alquileres, estacionamientos y comercio de aceitunas. Quedan para posibles ampliaciones la contabilidad general, nóminas, conciliación bancaria automática, aislamiento entre empresas, hardware de estacionamientos, producción agrícola, gestión jurídica inmobiliaria y migración de sistemas reales.

Los identificadores remiten a las fases y secciones del [plan del 17 de noviembre de 2026](../plan.md). Cada fila define la evidencia que debe poder reproducir un revisor.

| ID | Plan | Entrega | Criterio de aceptación |
| --- | --- | --- | --- |
| DEF-01 | 0, §§2–3, 8–11 | Ubicación, alcance y vocabulario | Los dos repositorios residen en una carpeta independiente. Cada área cuenta con términos y reglas consultables. |
| DEF-02 | 0, §§6, 10–11 | Arquitectura, sesión y contrato | Los ADR explican las dependencias, el límite de confianza, el origen del contrato y la compatibilidad. |
| FND-01 | 1, §§5–7, 12 | Proyectos y ramas | En cada repositorio existen main y develop. Los locks coinciden con una restauración limpia. Las cinco capas backend compilan. |
| FND-02 | 1, §§10, 15–16 | Arranque integrado | Desde dos checkouts hermanos, un comando crea la base persistente y arranca API, worker y web. El navegador muestra la conexión real. |
| FND-03 | 1, §§14–15 | Controles de salud y CI | El proceso responde en live y la disponibilidad de SQL determina ready. La prueba de navegador repite la consulta. Los workflows validan compilación, contrato y pruebas. |
| IAM-01 | 2, §8.1 | Identidad, roles y sucursales | Un operador autorizado trabaja en su sucursal. La API deniega el mismo recurso a un operador de otra sucursal y registra cambios de permisos. |
| CAT-01 | 2, §§8.2–8.3 | Clientes, proveedores y catálogo | El operador busca por documento, pagina resultados y distingue un servicio de un producto inventariable. Se conservan operaciones de maestros inactivos. |
| INV-01 | 3, §8.4 | Compra, recepción, traslado y reserva | Toda variación deja movimiento. Con dos reservas de la última unidad solo una se confirma. El saldo del kardex concuerda con existencias. |
| POS-01 | 4, §8.5 | Cotización, venta, despacho y caja | Un cajero cobra una venta y registra un pago parcial de otra. El cierre concuerda con movimientos y deuda. La devolución conserva trazabilidad. |
| BIL-01 | 5, §8.6 | Facturación peruana | En simulación y luego en pruebas externas se conservan XML, firma, respuesta e intentos. Una respuesta perdida se consulta antes de reenviar y se conserva el correlativo. |
| RNT-01 | 6, §8.7 | Contratos, cuotas y cobros | Se impide el solapamiento de contratos incompatibles. Repetir la generación mensual mantiene una cuota por periodo. Garantías, rentas y pagos parciales se distinguen. |
| PRK-01 | 7, §8.8 | Entrada, salida y cobro | Dos entradas concurrentes respetan la capacidad. Repetir una salida cobra una sola vez y conserva la tarifa aplicada. |
| OLV-01 | 8, §8.9 | Lotes y reenvasado | Del cliente se llega al lote recibido. Peso neto, cantidades reenvasadas y mermas documentadas reconcilian la recepción. |
| MNT-01 | 9, §17 | Evolución e importaciones | Una importación inválida permite diagnosticar la fila. Una corrección tiene reproducción y prueba de regresión. Los flujos anteriores siguen funcionando. |
| REL-01 | 10, §§8.10, 14–18 | Reportes y entrega integrada | Se demuestra el recorrido completo, se exportan resultados, se registra la pareja de versiones y se restaura un respaldo en un entorno limpio. |

### Evidencia de la fase 2

IAM-01 y CAT-01 se entregan en la pareja API/web 0.2.0. La [guía de seguridad y maestros](security-and-masters.md) incluye el recorrido de operación, la matriz de roles y los comandos para repetir las pruebas con SQL Server. Los escenarios automatizados cubren la sucursal ajena, la revocación durante una sesión, la auditoría de permisos y la conservación de una ficha inactivada.

### Dependencias que se resolverán en su fase

Para INV-01 y OLV-01 se acordarán precisión, conversiones y valoración. POS-01 requiere políticas de descuentos y redondeo. BIL-01 requiere verificar la modalidad y reglas de SUNAT vigentes para el emisor. La emisión real requiere credenciales, certificado y aceptación operativa. RNT-01 y PRK-01 requieren definir mora, prorrateo, tolerancia y redondeo. REL-01 requiere definir el alojamiento, licencias y carga de referencia.
