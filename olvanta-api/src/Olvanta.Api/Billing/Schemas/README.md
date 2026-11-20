# Esquemas UBL

`ubl-2.1.zip` contiene los esquemas de Invoice, CreditNote y DebitNote y sus dependencias, seleccionados de la [distribución OASIS UBL 2.1](https://docs.oasis-open.org/ubl/os-UBL-2.1/UBL-2.1.zip). Los encabezados originales conservan las atribuciones y condiciones de distribución.

En la copia de `UBL-xmldsig-core-schema-2.1.xsd` se eliminó el DTD interno y se expandió la entidad de espacio de nombres XMLDSig. El esquema declara explícitamente ese mismo espacio de nombres. Esto permite validar la firma con el procesamiento de DTD desactivado. El resolvedor de esquemas solo abre entradas del ZIP incluido en la aplicación.
