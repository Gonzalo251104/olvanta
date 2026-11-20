# Mantenimiento e integraciones · 0.9.0

## Importar con vista previa

En **Directorio → Clientes** o **Proveedores**, pulse **Importar** y descargue la plantilla CSV. Use UTF-8 y separe las columnas con comas o punto y coma. Encierre entre comillas los valores que contienen el separador. Mantenga los identificadores como texto para conservar ceros iniciales.

Las columnas requeridas son `code`, `name`, `personType` y `documentType`. Use `person` o `company` para `personType`. Puede añadir `active`, `address`, `contact` y `terms`. Una celda vacía en `active` equivale a activo. Una importación admite hasta 200 registros y el formulario procesa archivos de hasta 500 KB.

**Procesar archivo** muestra la vista previa CSV. La numeración cuenta el encabezado como fila 1 y los registros lógicos a partir de la fila 2. Cuando hay comillas inválidas, el mensaje incluye la línea física que detectó el lector. **Confirmar importación** vuelve a validar las filas y los duplicados bajo el bloqueo de la sucursal. Cualquier incidencia cancela el lote completo. El endpoint de importación JSON conserva su formato y la carga desde la web.

## Ajustes históricos

En **Administración → Organización**, el descuento máximo por partida se guarda por sucursal. El valor inicial es 100 %. Un cambio limita las cotizaciones que se creen después. Los importes y descuentos de cotizaciones ya guardadas conservan sus valores al confirmar y facturar. Los clientes de API pueden seguir actualizando moneda e impuesto con el cuerpo anterior.

La recepción de compras calcula el valor de cada parcial como la diferencia entre los importes acumulados redondeados a seis decimales. Para reproducir el caso, reciba dos partidas de 0,5 unidades con costo 0,333333. Las partidas registran 0,166667 y 0,166666. Con el redondeo individual anterior ambas registraban 0,166667. El cambio conserva los valores ya contabilizados.

## Conciliar un comprobante pendiente

En **Facturación**, un documento cuyo envío quedó incierto o cuyo ticket está en proceso permite **Programar consulta**. La API conserva la programación y el número de intentos en SQL Server. El servicio en segundo plano de la API usa el adaptador y el entorno del documento. Registra el resultado y los archivos en el historial de facturación.

Tras la primera consulta se programan hasta cuatro más si persiste el estado pendiente, con pausas de 15, 30, 60 y 120 segundos. El operador puede consultar manualmente y programar otra serie después de agotar los intentos. En BETA se requiere un ticket. La programación consulta el receptor y conserva el envío original. La prueba integrada simula una respuesta perdida y recupera su constancia mediante una consulta programada.

## Revisar el costo de SQL

El cálculo del efectivo esperado consulta `SUM(CashDelta)` por `CashSessionId`. La migración incorpora `CashDelta` al índice de sesión. El archivo [Inspect-CashIndex.sql](../../scripts/Inspect-CashIndex.sql) registra lecturas y el plan para una sesión con movimientos. Ejecútelo en SQL Server con permiso para consultar planes.

En una base aislada con 20.000 movimientos sintéticos y 1.000 en la sesión medida, ambos índices devolvieron 2.000,00. La comparación fijó el índice de cada consulta para medir la diferencia en el mismo conjunto de datos. El índice anterior necesitó 3.076 lecturas lógicas y un operador `Key Lookup`. El índice con cobertura necesitó 6 lecturas lógicas mediante `Index Seek`. La transacción de medición se revirtió al terminar.

El escenario `olvanta-web/tests/e2e/maintenance.spec.ts` reproduce una fila inválida, un conflicto entre vista previa y confirmación, y un cambio de descuentos con una cotización histórica. Las pruebas de API reproducen el redondeo de recepciones y la lectura de campos CSV entrecomillados.
