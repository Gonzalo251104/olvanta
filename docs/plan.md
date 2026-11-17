# Olvanta — Plan de proyecto

> Estado: planificación. No implica implementación ni creación de repositorios.
> Fecha: 17 de noviembre de 2026.
> Idioma de comunicación e interfaz inicial: español.
> Código, identificadores técnicos y Conventional Commits: inglés.

## 1. Identidad y propósito

**Olvanta** será una plataforma integral de operaciones comerciales que conecte ventas, inventarios, facturación electrónica, alquiler de locales, estacionamientos y comercialización de aceitunas.

El escenario será una empresa ficticia que administra locales y estacionamientos y comercializa productos. Los módulos compartirán clientes, sucursales, catálogo, cobros, caja, facturación, reportes y auditoría.

El objetivo es desarrollar un proyecto de portafolio funcional, con reglas de negocio, persistencia en SQL Server, seguridad, pruebas, documentación y despliegue. Permitirá demostrar competencias de desarrollo y mantenimiento en C#, ASP.NET Core, .NET, SQL Server, XML, APIs y servicios web.

El nombre es una propuesta creativa. La disponibilidad comercial, de marca y de dominio deberá revisarse si se publica como producto.

## 2. Repositorios y ubicación

Se utilizarán dos repositorios Git independientes:

- `olvanta-api`: backend, persistencia, integraciones, worker e infraestructura compartida.
- `olvanta-web`: frontend, experiencia de usuario y pruebas de navegador.

```text
olvanta/                 # Carpeta contenedora sin repositorio Git
├── olvanta-api/         # Repositorio independiente
└── olvanta-web/         # Repositorio independiente
```

La carpeta actual contiene GarantiaTrack y cambios sin confirmar. Este documento se entrega allí por solicitud del usuario, pero no convierte GarantiaTrack en Olvanta. Antes de inicializar los repositorios se establecerá un destino independiente, preservando el proyecto y sus cambios existentes. Se evitará anidar los nuevos repositorios dentro del repositorio actual.

## 3. Alcance

La primera versión completa atenderá una empresa con varias sucursales, almacenes, locales y estacionamientos. No incluirá aislamiento multiempresa tipo SaaS.

| Incluido | Fuera del alcance inicial |
| --- | --- |
| Gestión comercial y operativa | Contabilidad general completa |
| Compras básicas e inventario | Nóminas y recursos humanos |
| Caja, cobros y cuentas por cobrar | Conciliación bancaria automática |
| Facturación electrónica peruana | Facturación multinacional |
| Contratos y cobros de alquiler | Gestión inmobiliaria legal completa |
| Operación de estacionamientos | Barreras, cámaras y lectura automática de placas |
| Comercialización por lotes y peso | Producción agrícola e industrial completa |
| Importación y evolución de módulos | Migración de un sistema real no proporcionado |

Las ampliaciones futuras tendrán alcance y criterios de aceptación propios.

## 4. Relación con los requisitos laborales

| Requisito | Evidencia en el proyecto |
| --- | --- |
| C#, ASP.NET y .NET | API, reglas, procesamiento en segundo plano e integraciones |
| SQL Server | Modelo relacional, transacciones, restricciones, índices y migraciones |
| Modificar y finalizar módulos | Entregas incrementales, incidencias y refactorizaciones |
| Facturación electrónica | XML, firma, envío, respuestas y seguimiento |
| HTML y JavaScript | HTML semántico, React y TypeScript |
| XML, APIs y servicios web | REST interno y adaptadores XML/SOAP externos |
| Ventas e inventarios | Compra, recepción, venta, despacho y devolución |
| Alquileres | Locales, contratos, cuotas y cobros |
| Estacionamientos | Entrada, tarifa, salida y cobro |
| Aceitunas | Lotes, presentaciones, pesos y trazabilidad |
| Corrección de errores | Reproducción, prueba de regresión y solución documentada |

El proyecto demuestra competencias técnicas; no acredita requisitos académicos.

## 5. Stack tecnológico

| Área | Elección propuesta |
| --- | --- |
| Backend | C# y ASP.NET Core sobre .NET 10 LTS |
| API | REST con controladores y contrato OpenAPI |
| Persistencia | Entity Framework Core |
| Base de datos | SQL Server |
| Base de datos de desarrollo | SQL Server 2025 Developer |
| Frontend | React, TypeScript y Vite |
| Navegación | React Router |
| Estado remoto | TanStack Query |
| Formularios | React Hook Form y Zod |
| Interfaz | Material UI con tema propio |
| Identidad | ASP.NET Core Identity |
| Pruebas backend | xUnit e integración contra SQL Server |
| Pruebas frontend | Vitest y Testing Library |
| Pruebas de navegador | Playwright |
| Entorno local | Docker Compose y scripts PowerShell |
| Integración continua | GitHub Actions |
| Observabilidad | Logs estructurados y OpenTelemetry |

.NET 10 LTS tiene soporte previsto hasta noviembre de 2028. Las versiones exactas de SDK, paquetes, herramientas e imágenes se verificarán y fijarán al implementar. Se versionarán los archivos de bloqueo correspondientes.

SQL Server Developer se utilizará para desarrollo y pruebas. Producción requerirá una edición o servicio con licencia apropiada.

## 6. Arquitectura backend

Se utilizará un **monolito modular**, organizado por funcionalidades dentro de capas con dependencias explícitas.

| Capa | Responsabilidad |
| --- | --- |
| Domain | Entidades, reglas, estados y objetos de valor |
| Application | Casos de uso, validación, permisos y coordinación |
| Infrastructure | SQL Server, archivos, correo y servicios externos |
| Api | HTTP, autenticación, endpoints y respuestas |
| Worker | Procesamiento durable de trabajos e integraciones |

```text
Api ───────────► Application ───────────► Domain
                       ▲
                       │
                Infrastructure

Worker utiliza Application e Infrastructure.
```

### Decisiones

- Casos de uso explícitos: registrar venta, cerrar caja, emitir comprobante.
- Entidades independientes de solicitudes y respuestas HTTP.
- DTOs específicos; sin exponer entidades de persistencia.
- Un DbContext inicial, con configuración organizada por módulo.
- Esquemas SQL por área de negocio.
- Cada módulo será responsable de sus datos y reglas.
- Coordinación entre módulos mediante operaciones de aplicación y eventos internos.
- Transacciones locales para cambios que deban confirmarse juntos.
- Patrón outbox para registrar trabajos externos junto con la operación de negocio.
- Worker con registro de intentos, toma exclusiva temporal de trabajos y recuperación tras fallos.
- Reintentos únicamente cuando la operación y el estado externo permitan realizarlos.
- Pruebas de arquitectura para controlar las dependencias.

## 7. Estructura de repositorios

### Backend

```text
olvanta-api/
├── src/
│   ├── Olvanta.Api/
│   ├── Olvanta.Application/
│   │   └── Modules/
│   ├── Olvanta.Domain/
│   │   └── Modules/
│   ├── Olvanta.Infrastructure/
│   │   ├── Persistence/
│   │   ├── Integrations/
│   │   └── Files/
│   └── Olvanta.Worker/
├── tests/
│   ├── Olvanta.UnitTests/
│   ├── Olvanta.IntegrationTests/
│   └── Olvanta.ArchitectureTests/
├── docs/
│   ├── adr/
│   ├── business/
│   ├── database/
│   ├── integrations/
│   └── runbooks/
├── contracts/
├── deploy/
├── scripts/
├── .github/
├── README.md
├── CONTRIBUTING.md
├── CHANGELOG.md
└── SECURITY.md
```

### Frontend

```text
olvanta-web/
├── src/
│   ├── app/
│   │   ├── providers/
│   │   ├── router/
│   │   └── layouts/
│   ├── features/
│   │   ├── auth/
│   │   ├── dashboard/
│   │   ├── customers/
│   │   ├── purchasing/
│   │   ├── inventory/
│   │   ├── sales/
│   │   ├── billing/
│   │   ├── rentals/
│   │   ├── parking/
│   │   └── olive-trading/
│   ├── shared/
│   │   ├── api/
│   │   ├── components/
│   │   ├── hooks/
│   │   ├── lib/
│   │   └── styles/
│   └── generated/
├── tests/
│   └── e2e/
├── public/
├── docs/
├── deploy/
├── .github/
├── README.md
├── CONTRIBUTING.md
└── CHANGELOG.md
```

El backend será la fuente oficial del contrato OpenAPI. El frontend generará su cliente TypeScript desde una versión identificada del contrato. No se requerirá un tercer repositorio.

## 8. Módulos funcionales

### 8.1. Administración y seguridad

- Inicio y cierre de sesión; recuperación de contraseña.
- Usuarios, roles y permisos.
- Empresa, sucursales y establecimientos.
- Restricciones de acceso por sucursal.
- Monedas, impuestos, series y parámetros.
- Auditoría de operaciones relevantes.

Roles iniciales: administrador, supervisor, vendedor, almacenero, cajero, encargado de alquileres, operador de estacionamiento, responsable de facturación y auditor.

Los permisos se comprobarán en el backend y sobre el recurso solicitado, además de controlar la interfaz.

### 8.2. Clientes y proveedores

- Personas y empresas, documentos, direcciones y contactos.
- Condiciones comerciales e historial.
- Búsqueda, filtros y paginación.
- Importación validada.

Las consultas externas de documentos serán opcionales y dependerán del proveedor disponible.

### 8.3. Productos y servicios

- Productos inventariables y servicios.
- Categorías, unidades, presentaciones y conversiones.
- Precios, impuestos y códigos internos.
- Códigos de barras opcionales.
- Activación e inactivación.

Los servicios y productos compartirán capacidades comerciales, conservando sus reglas operativas particulares.

### 8.4. Compras e inventario

- Compras básicas y recepción de mercadería.
- Almacenes, entradas, salidas y transferencias.
- Reservas, ajustes autorizados y kardex.
- Existencias mínimas.
- Lotes y vencimientos cuando correspondan.
- Costo promedio ponderado para valorización.

Reglas:

1. Todo cambio de stock tendrá un movimiento registrado.
2. Los movimientos confirmados se corregirán mediante compensaciones.
3. No se venderá ni reservará más stock del disponible.
4. Las operaciones concurrentes se protegerán con transacciones y controles apropiados.
5. Los saldos deberán reconciliarse con los movimientos.

### 8.5. Ventas, cobros y caja

- Cotizaciones, pedidos y ventas.
- Descuentos sujetos a permisos.
- Ventas al contado y crédito.
- Pagos parciales y cuentas por cobrar.
- Despachos y devoluciones.
- Apertura, movimientos y cierre de caja.
- Diferencias entre saldo esperado y efectivo contado.
- Representaciones imprimibles.

```text
Cotización → Pedido → Confirmación de venta → Despacho
                           ├── Cobros
                           └── Comprobante electrónico
```

Los estados de venta, pago y aceptación tributaria serán independientes. Las devoluciones, restituciones de stock y devoluciones de dinero tendrán operaciones relacionadas y trazables.

### 8.6. Facturación electrónica peruana

- Facturas, boletas, notas de crédito y débito.
- Series y correlativos.
- XML UBL 2.1, validación y firma digital.
- Empaquetado, envío, consultas y respuestas.
- Conservación de XML, ZIP y CDR.
- PDF y QR conforme a requisitos aplicables.
- Resúmenes y comunicaciones de baja cuando correspondan.
- Historial de intentos y diagnóstico de incidencias.

Niveles de integración:

| Nivel | Resultado |
| --- | --- |
| Simulación local | Respuestas ficticias identificadas y flujo reproducible |
| Pruebas externas | Comunicación con el servicio de pruebas aplicable |
| Producción | Emisión con configuración, credenciales y certificado reales |

Reglas técnicas:

- Correlativos únicos por empresa, tipo y serie.
- Idempotencia para impedir emisiones duplicadas.
- Datos y cálculos congelados en el documento emitido.
- Archivos originales y respuestas conservados.
- Estados diferenciados de preparación, envío, aceptación y rechazo.
- Consulta previa al reenvío cuando el resultado sea incierto.
- Correcciones según tipo de documento y estado; no edición libre de documentos aceptados.
- Certificados y credenciales exclusivamente en backend.
- Adaptador desacoplado para SUNAT o proveedor.

Antes de implementar se revisarán modalidad de emisión, anexos técnicos, catálogos y reglas vigentes. El procedimiento se definirá por tipo de documento; no se asumirá un envío idéntico para todos.

Las pruebas no equivalen a autorización ni acreditan cumplimiento completo en producción. La emisión real dependerá de los recursos y condiciones del emisor.

### 8.7. Alquileres

- Locales, características y disponibilidad.
- Arrendatarios y contratos con vigencia, moneda y condiciones.
- Garantías, cuotas mensuales y vencimientos.
- Pagos parciales, deuda, renovación y finalización.
- Cargos adicionales autorizados.
- Facturación mediante el módulo común.

Reglas:

- Impedir contratos incompatibles para el mismo local y periodo.
- Una cuota por contrato y periodo.
- Separar garantías y rentas.
- Conservar condiciones históricas.
- Definir prorrateo y mora antes de programarlos.

### 8.8. Estacionamientos

- Estacionamientos, zonas y capacidad.
- Entrada, ticket y placa normalizada.
- Tarifas por tiempo y abonos.
- Cálculo de salida, cobro y comprobante.
- Ocupación e historial.

Reglas:

- Una estancia activa por placa y estacionamiento.
- Control de capacidad frente a entradas concurrentes.
- Tarifa aplicada conservada en la estancia.
- Tolerancia y redondeo definidos expresamente.
- Protección contra cierres y cobros duplicados.

### 8.9. Comercialización de aceitunas

Extenderá compras, inventario y ventas con:

- Variedad y clasificación comercial.
- Proveedor, procedencia y lote.
- Peso bruto, tara y peso neto.
- Presentaciones: granel, baldes, bolsas u otras.
- Venta por peso o presentación.
- Conversiones configuradas.
- Reenvasado y mermas justificadas.
- Trazabilidad entre recepción, lote y cliente.

Las cantidades usarán precisión decimal. Cada reenvasado registrará entradas, salidas y mermas, permitiendo reconciliar el balance de cantidades.

### 8.10. Reportes y soporte

- Ventas por periodo, sucursal y vendedor.
- Cobros, deuda y cierres de caja.
- Stock, valorización y kardex.
- Trazabilidad por lote.
- Ocupación y deuda de locales.
- Ingresos y ocupación de estacionamientos.
- Comprobantes pendientes y rechazados.
- Exportaciones CSV y reportes PDF seleccionados.

Las incidencias del desarrollo se gestionarán en los repositorios. No se construirá inicialmente un sistema adicional de tickets dentro del producto.

## 9. Modelo de datos y reglas compartidas

Esquemas propuestos:

```text
identity, organization, catalog, purchasing, inventory, sales,
finance, billing, rentals, parking, olive, audit, integration
```

| Área | Entidades representativas |
| --- | --- |
| Organización | Company, Branch, Establishment |
| Seguridad | User, Role, Permission |
| Comercial | Customer, Supplier, Product, PriceList |
| Compras | Purchase, PurchaseLine, GoodsReceipt |
| Inventario | Warehouse, StockMovement, Reservation, Lot |
| Ventas | Quote, SalesOrder, Sale, SaleLine |
| Finanzas | Receivable, Payment, PaymentAllocation, CashSession |
| Facturación | FiscalDocument, FiscalDocumentLine, Submission |
| Alquileres | CommercialUnit, LeaseContract, RentCharge |
| Estacionamiento | ParkingSite, RatePlan, ParkingStay |
| Aceitunas | OliveLotDetails, PackagingConversion |
| Integración | OutboxMessage, IntegrationAttempt |

Convenciones:

- Importes decimal, moneda explícita y redondeo definido.
- Instantes UTC; fechas de negocio y zona horaria tratadas expresamente.
- Restricciones únicas, claves foráneas e índices justificados.
- Control de concurrencia con rowversion donde corresponda.
- Datos históricos preservados en operaciones confirmadas.
- Bajas lógicas de maestros cuando sean necesarias.
- Migraciones versionadas; datos de demostración separados.
- Consultas parametrizadas.
- Vistas y procedimientos almacenados para necesidades concretas de consulta o rendimiento.
- Ninguna corrección de saldos mediante borrado de operaciones confirmadas.

## 10. Contrato API

Ruta base inicial: `/api/v1/`.

Ejemplos orientativos:

```text
POST /api/v1/auth/login
GET  /api/v1/customers
POST /api/v1/sales
POST /api/v1/sales/{id}/confirm
POST /api/v1/payments
POST /api/v1/fiscal-documents/{id}/submit
POST /api/v1/leases/{id}/generate-charges
POST /api/v1/parking-stays/{id}/checkout
```

El contrato documentará paginación, filtros, ordenamiento, formatos, validación, errores ProblemDetails, correlación, permisos, idempotencia y conflictos de concurrencia.

Cada publicación web identificará su compatibilidad con la API. Los cambios incompatibles requerirán una migración coordinada. El cliente generado se comprobará en CI contra el contrato fijado para la entrega.

## 11. Interfaz y seguridad

### Experiencia de usuario

- Interfaz inicial en español y glosario de términos técnicos.
- Navegación por módulos y permisos.
- Sucursal activa visible.
- Tablas con filtros y paginación.
- Errores junto a los campos.
- Estados de carga, vacío, éxito y fallo.
- Navegación por teclado y etiquetas accesibles.
- Diseño adaptable, priorizando escritorio para operación.
- Operación de estacionamientos y consultas utilizables desde móvil.
- Confirmaciones para operaciones sensibles.
- Pantallas para diagnosticar y resolver incidencias de facturación.

### Seguridad

- Identity y cookies seguras HttpOnly para el navegador.
- Protección CSRF y configuración explícita de SameSite y orígenes.
- Frontend y API bajo un mismo origen mediante proxy en despliegue.
- HTTPS, límites de intentos y bloqueo según configuración.
- Autorización por operación, sucursal y recurso.
- Secretos fuera del repositorio.
- Persistencia segura de claves de protección de datos.
- Validación de archivos y límites de tamaño.
- Lectura de XML con entidades externas deshabilitadas.
- Límites de descompresión y tamaño de ZIP.
- Logs sin contraseñas, certificados ni datos sensibles innecesarios.
- Auditoría de permisos, stock, caja y comprobantes.

## 12. Gitflow, commits y versiones

| Rama | Uso |
| --- | --- |
| main | Versiones publicadas |
| develop | Integración |
| feature/<issue>-<description> | Funcionalidades desde develop |
| release/<version> | Estabilización |
| hotfix/<issue>-<description> | Correcciones urgentes desde main |

Reglas:

1. Integración mediante pull requests y verificaciones obligatorias.
2. Protección de main y develop según las capacidades del alojamiento.
3. Releases integradas en main, con propagación a develop.
4. Hotfixes propagados a develop y a releases activas cuando corresponda.
5. Etiquetas, changelog y notas por publicación.
6. Versionado semántico independiente por repositorio.
7. Registro de versiones frontend/backend compatibles en cada entrega integrada.

Conventional Commits en inglés:

```text
feat(inventory): add warehouse stock transfers
fix(billing): prevent duplicate invoice submissions
refactor(rentals): extract monthly charge calculation
test(parking): cover overnight parking charges
docs(architecture): document module boundaries
ci(api): run integration tests against sql server
```

Tipos: feat, fix, docs, refactor, test, perf, build, ci, chore y revert. Se validarán los mensajes de cambios y se mantendrán mensajes compatibles para las integraciones. Los cambios incompatibles se identificarán explícitamente.

## 13. Documentación

| Documento | Contenido |
| --- | --- |
| README.md | Propósito, requisitos y arranque |
| CONTRIBUTING.md | Gitflow, commits y contribución |
| CHANGELOG.md | Cambios por versión |
| SECURITY.md | Secretos y reporte de vulnerabilidades |
| Arquitectura | Contexto, componentes y dependencias |
| ADR | Decisiones y motivos |
| Glosario | Conceptos del negocio y traducciones |
| Reglas de negocio | Estados, cálculos y restricciones |
| Modelo de datos | Diagrama y diccionario |
| OpenAPI | Contrato y ejemplos |
| Manual de usuario | Operación por rol |
| Runbooks | Despliegue, respaldos, restauración e incidencias |
| Guía de demostración | Datos y recorrido reproducible |

El backend mantendrá las referencias compartidas de negocio, arquitectura y contrato. El frontend documentará su estructura e interfaz y enlazará esas referencias. La documentación se actualizará con cada funcionalidad.

## 14. Pruebas y calidad

| Tipo | Objetivo |
| --- | --- |
| Unitarias | Cálculos, estados y reglas |
| Integración | SQL Server, restricciones y transacciones |
| Autorización | Roles, sucursales y recursos |
| Contrato | Compatibilidad entre API y cliente |
| Componentes | Formularios e interacciones relevantes |
| End-to-end | Flujos completos |
| Concurrencia | Stock, cuotas, correlativos y cobros |
| Arquitectura | Dependencias permitidas |
| Operación | Despliegue, respaldo y restauración |

Escenarios prioritarios:

- Dos ventas compiten por la última unidad.
- Una solicitud se repite después de perder la respuesta.
- Dos operaciones intentan cobrar la misma salida.
- La generación mensual de cuotas se ejecuta dos veces.
- Un usuario solicita información de otra sucursal.
- El envío electrónico pierde conexión después de ser recibido.
- Un lote se reenvasa y se rastrea hasta sus ventas.
- Una copia de seguridad se restaura en un entorno limpio.

Las pruebas de integración usarán SQL Server real. Los servicios externos tendrán simuladores reproducibles y verificaciones separadas en sus entornos de prueba.

Los objetivos de rendimiento se fijarán sobre datos, concurrencia y hardware documentados. Se medirán consultas antes de optimizarlas.

## 15. CI/CD y operación

### Backend por pull request

- Restauración y compilación.
- Formato y análisis estático.
- Pruebas unitarias, de arquitectura e integración.
- Comprobación del contrato y dependencias.

### Frontend por pull request

- Instalación reproducible.
- Lint y comprobación de tipos.
- Pruebas y compilación.
- Comprobación del cliente generado.
- Pruebas de navegador en el entorno integrado.

### Entornos

| Entorno | Propósito |
| --- | --- |
| Local | Desarrollo y demostración |
| CI | Validación automática |
| Staging | Integración y aceptación |
| Producción | Operación con configuración real |

### Despliegue

- Artefactos versionados para ambos repositorios.
- API y worker con la misma versión backend.
- Migraciones ejecutadas como paso controlado.
- Copias antes de cambios relevantes.
- Health checks y logs correlacionados.
- Métricas de errores y trabajos pendientes.
- Reversión compatible con las migraciones y los datos.
- Almacenamiento persistente de comprobantes y adjuntos.
- Restauración probada y documentada.

La infraestructura compartida y el arranque integrado vivirán en olvanta-api/deploy. El frontend aportará su artefacto y configuración. Una entrega registrará las versiones de imágenes, API, web y esquema necesarias para reproducirla.

## 16. Fases de implementación

Cada fase entregará una porción funcional con interfaz, backend, persistencia y verificaciones.

| Fase | Trabajo | Criterio de cierre |
| --- | --- | --- |
| 0. Definición | Destino independiente, alcance, glosario, reglas, backlog y ADR iniciales | Requisitos trazables y estructura acordada |
| 1. Fundaciones | Dos repositorios, Gitflow, proyectos, SQL Server, CI, API y shell web | Arranque reproducible y comunicación web–API |
| 2. Seguridad y maestros | Usuarios, permisos, sucursales, clientes, proveedores y catálogo | Operación protegida y aislamiento por sucursal |
| 3. Compras e inventario | Recepción, almacenes, movimientos, reservas y kardex | Saldos reconciliados y concurrencia comprobada |
| 4. Ventas y caja | Cotización, venta, despacho, cobro, crédito y cierre | Flujo comercial completo y saldos correctos |
| 5. Facturación | Simulación, XML, firma, envío de pruebas y seguimiento | Evidencia técnica y recuperación de fallos |
| 6. Alquileres | Locales, contratos, cuotas y cobros | Ciclo mensual sin cargos duplicados |
| 7. Estacionamientos | Entrada, tarifa, salida, cobro y ocupación | Jornada y cierre verificables |
| 8. Aceitunas | Pesos, lotes, presentaciones, reenvasado y trazabilidad | Seguimiento desde recepción hasta venta |
| 9. Mantenimiento e integraciones | Importaciones, cambios, correcciones y SQL | Evolución sin romper flujos previos |
| 10. Versión integrada | Reportes, rendimiento, despliegue, manuales y recuperación | Release demostrable y reproducible |

La emisión tributaria real tendrá un criterio adicional: recursos del emisor, modalidad, credenciales, certificado y validación operativa correspondientes. Si faltan, la entrega identificará claramente qué opera en simulación o pruebas.

### Primer ciclo de trabajo

Al autorizar la implementación se ejecutarán primero las fases 0 y 1:

1. Establecer la carpeta independiente sin alterar GarantiaTrack.
2. Convertir este plan en backlog con criterios de aceptación.
3. Registrar decisiones de arquitectura, autenticación y contrato.
4. Inicializar los dos repositorios y sus ramas.
5. Fijar versiones y configuración reproducible.
6. Levantar SQL Server, API y frontend.
7. Incorporar health checks y CI inicial.
8. Documentar y verificar el arranque desde un entorno limpio.

El primer hito comercial posterior será comprar → recibir stock → vender → cobrar → cerrar caja, seguido de facturación.

## 17. Práctica de mantenimiento

Después de estabilizar los módulos iniciales se trabajará con incidencias y ampliaciones concretas:

- Incorporar varios almacenes a un flujo inicialmente limitado a uno.
- Modificar descuentos conservando ventas históricas.
- Añadir una presentación de aceitunas.
- Importar clientes desde CSV o XML con validación previa.
- Incorporar un adaptador de servicio web.
- Optimizar un reporte mediante medición, índices y planes de ejecución.
- Corregir redondeos con pruebas de regresión.
- Evolucionar la API manteniendo compatibilidad.

Cada intervención incluirá requisito o reproducción del problema, solución, pruebas y versión. Los ejercicios con errores intencionales se aislarán del código publicado.

## 18. Definición de terminado

Una funcionalidad estará terminada cuando:

- Resuelva su caso de uso completo desde la interfaz.
- Persista datos y aplique reglas en backend.
- Controle permisos y acceso a recursos.
- Gestione errores, duplicados y concurrencia cuando corresponda.
- Supere verificaciones relevantes.
- Actualice contrato y documentación.
- Incluya migraciones cuando cambien los datos.
- Tenga commits y pull request trazables.
- Pueda demostrarse con datos reproducibles.

La versión integrada deberá demostrar:

> Recibir un lote de aceitunas, ingresarlo al almacén, vender una cantidad, registrar el cobro y procesar su comprobante; generar y cobrar una cuota de alquiler; registrar y cobrar una estancia de estacionamiento; cerrar caja y consultar los resultados.

## 19. Esfuerzo y dependencias

Estimación inicial del alcance completo: **400–650 horas**. Es una referencia de planificación, no un compromiso de calendario. Se reestimará al cerrar cada fase.

Principales fuentes de incertidumbre:

- Reglas tributarias y modalidad de integración.
- Credenciales y certificado del emisor.
- Políticas de tarifas, mora, prorrateo y descuentos.
- Conversiones y valoración del inventario.
- Infraestructura y licencia de producción.
- Cambios de alcance y necesidad de migrar datos reales.

Estos puntos se resolverán antes de implementar las funcionalidades que dependan de ellos. El resto del trabajo podrá avanzar de forma independiente.

## 20. Referencias técnicas

Referencias consultadas para el plan; se revisarán nuevamente al implementar las integraciones y fijar dependencias:

- [Política de soporte de .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [SQL Server en contenedores](https://learn.microsoft.com/sql/linux/quickstart-install-connect-docker)
- [React: aplicación desde cero con herramientas como Vite](https://react.dev/learn/build-a-react-app-from-scratch)
- [ASP.NET Core Identity para aplicaciones SPA](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0)
- [Protección CSRF en ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)
- [Conventional Commits](https://www.conventionalcommits.org/en/)
- [SUNAT: servicios web de pruebas, producción y consulta](https://orientacion.sunat.gob.pe/guias-manuales-y-servicios-web)
- [SUNAT: anexo técnico UBL 2.1 publicado en 2026](https://www.sunat.gob.pe/legislacion/superin/2026/anexo-000033-2026.pdf)
