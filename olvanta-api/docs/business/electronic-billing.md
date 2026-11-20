# Facturación electrónica peruana · 0.5.0

## Emitir el primer comprobante en simulación

1. En **Administración → Organización**, configure el nombre y el RUC del emisor. Para practicar en una instalación de pruebas puede usar el RUC ficticio `20000000001`. Registre un establecimiento de la sucursal con dirección y código de cuatro dígitos, por ejemplo `0001`.
2. Cree una serie por tipo. Por ejemplo, `F001` para factura y `B001` para boleta. Las notas de crédito y débito necesitan sus propias series configuradas para el tipo respectivo, con la misma inicial del comprobante afectado.
3. En **Maestros**, registre un cliente identificado. Para facturas se comprueba el dígito de control del RUC. En **Ventas y caja**, confirme una venta con ese cliente y con partidas en PEN o USD.
4. En **Facturación → Nuevo comprobante**, seleccione venta, tipo y serie. Si hay partidas al 0 %, indique si son exoneradas o inafectas. **Preparar y firmar** reserva el correlativo y guarda los datos de emisión, el XML firmado, el ZIP y el PDF con QR. El perfil admite ventas internas con importes positivos, IGV del 18 % y un tratamiento común para las partidas a tasa cero de la venta.
5. En una factura, pulse **Simular envío**. En una boleta, elija **Preparar documento vinculado → Resumen diario**, envíe el resumen y consulte su ticket. La respuesta queda asociada al resumen. Desde la boleta puede abrir el resumen y descargar su CDR.
6. Abra un documento aceptado para emitir una nota de crédito por una devolución previamente registrada en Ventas o una nota de débito por aumento de valor con una referencia única. Los ajustes fiscales conservan el vínculo con la venta. Los cobros y los movimientos de inventario se registran en sus módulos.

El facturador y el supervisor pueden operar en sus sucursales. El auditor puede consultar documentos y descargar los archivos. El administrador tiene acceso global.

## Cómo interpretar un resultado

| Situación | Acción |
| --- | --- |
| Preparado | Revisar el PDF y enviar el ZIP original. |
| Pérdida de respuesta | Consultar el resultado. El simulador conserva la recepción y devuelve la constancia original. |
| Ticket en proceso | Consultar el ticket del resumen o la baja hasta obtener respuesta. |
| Rechazado | Revisar el motivo en el historial. Una nueva preparación asigna otro correlativo y conserva el rechazo anterior. |
| Aceptado | Descargar la constancia. Para una corrección, preparar el documento vinculado que corresponda. |
| Comprobante aceptado sin otorgar al cliente | Confirmar la condición e indicar el motivo. Las series F usan una comunicación de baja (RA) y las B un resumen con condición 3 (RC). El perfil local admite la operación hasta siete días desde la emisión y requiere resolver primero las notas vinculadas. |

Cada resumen del perfil local contiene un comprobante. El correlativo diario lo comparten las sucursales de la empresa. Las solicitudes repetidas con la misma clave y contenido recuperan el documento inicial. En caso de incertidumbre el servidor bloquea el reenvío. Los intentos, el usuario, el ticket, el resultado y los archivos se consultan en la ficha del documento.

## Entornos y firma

**Simulación** es el entorno predeterminado. El backend genera en memoria un certificado RSA para firmar los XML. Los documentos originales incluyen su certificado público y se pueden verificar después de reiniciar el servicio. La aceptación local y el PDF se identifican como pruebas.

**SUNAT BETA** es un adaptador opcional para pruebas de estructura. La API usa el endpoint fijo de pruebas y la autenticación de pruebas documentada por SUNAT. Para un ensayo futuro, configure un RUC emisor autorizado para el ensayo en Organización y en la variable de entorno `OLVANTA_BETA_RUC`. Con datos exclusivamente de prueba, desde `olvanta-api/deploy` se puede activar con `docker compose -f compose.yaml -f compose.beta.yaml up -d api web`. En este entorno las facturas, boletas y notas se preparan para envío individual. Los formatos RC y RA y el ciclo de tickets se verificaron localmente. SUNAT publica el alcance del servicio BETA para los cuatro tipos individuales. [Pautas del servicio BETA](https://orientacion.sunat.gob.pe/12-pautas-servicio-beta)

El archivo de certificado contiene la identidad pública y su clave privada de firma. El backend puede generar una firma de prueba o cargar un archivo propio montado en el contenedor mediante `Billing__CertificatePath` y `Billing__CertificatePassword`. SUNAT admite certificados sin registrar en BETA. Para volver a simulación ejecute `docker compose -f compose.yaml up -d api web`. Los documentos se conservan con su entorno de origen. Si en BETA se pierde una respuesta individual y falta un ticket, el documento queda bloqueado para conciliación externa.

La versión 0.5.0 se verificó en simulación con SQL Server y Chromium. La comprobación externa se limitó a la lectura del WSDL público. La aceptación de un envío BETA y la emisión tributaria en producción requieren verificaciones separadas. La configuración de producción, los regímenes especiales, detracciones, percepciones, retenciones, operaciones gratuitas y exportaciones quedan fuera de esta versión.

## Verificación técnica

Los XML individuales se validan con los esquemas UBL 2.1 de OASIS y las firmas XMLDSig con RSA-SHA256. RC y RA usan los formatos agregados de SUNAT sobre UBL 2.0. Las pruebas de estos agregados cubren generación, firma, ticket y efecto sobre el comprobante. El PDF incluye el importe, IGV, identificación de las partes y el QR con los datos del comprobante y el resumen de firma. [Guías y manuales de SUNAT](https://cpe.sunat.gob.pe/guias-y-manuales) · [Manual del programador](https://cpe.sunat.gob.pe/sites/default/files/inline-files/manual_programador%20%281%29.pdf) · [Especificaciones del QR](https://cpe.sunat.gob.pe/sites/default/files/inline-files/anexoA-117-2017%20%281%29.pdf)

Las pruebas de navegador usan la administración, crean una venta y verifican factura, PDF, consultas tras una pérdida, rechazo, notas, resumen, baja y aislamiento por sucursal. Ejecútelas contra una instalación de prueba con base desechable, ya que configuran allí la empresa emisora. El flujo de GitHub Actions crea una base vacía para cada ejecución.
