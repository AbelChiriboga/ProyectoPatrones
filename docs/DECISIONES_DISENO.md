# Decisiones de Diseño — POS Integrado con Odoo 20 Community (Entrega Final)

Este documento consolida las decisiones arquitectónicas y de diseño reales del sistema. Se prioriza la simplicidad verificable, la separación de responsabilidades y la adhesión a los principios de Clean Architecture y patrones de diseño GoF probados.

---

## Decisión 1: Aislamiento de la integración con Odoo 20 (Adapter / Anti-Corruption Layer)

1. **Problema concreto**  
   Odoo 20 expone RPC JSON-2 (`search_read`, `sync_from_ui`) con dominios en forma de arreglos heterogéneos, comandos relacionales (`[0, 0, {...}]`, `[6, 0, ids]`), reglas propias de redondeo fiscal (`round_globally` vs `round_per_line`) y formatos de fecha/moneda específicos. Acoplar los casos de uso o el frontend a ese contrato rompería el dominio ante cambios de Odoo e impediría probar reglas de negocio sin una instancia levantada.
2. **Alternativas investigadas**  
   - *Sin patrón:* llamar `HttpClient` y manipular `JsonElement` directamente desde `SalesController` o `SaleWorkflow`. Descartado por acoplamiento fuerte con el transporte HTTP y el esquema de Odoo.
   - *Con patrón:* definir puertos estrechos en Application (`ISaleGateway`, `ISaleHistoryReader`, `IProductCatalog`, `IPosSessionGateway`, `ISalesReportGateway`) e implementarlos en Infrastructure mediante adaptadores dedicados (`OdooSaleGateway`, `OdooJson2Client`, `OdooSalesReportGateway`).
3. **Solución elegida y categoría**  
   **Adapter / Anti-Corruption Layer** (Patrón estructural GoF + Clean Architecture / DIP).  
   *(Nota: en un CRUD simple sin traducción de esquema ni reglas fiscales, un servicio HTTP directo podría bastar; aquí el patrón sí aporta valor por la complejidad del contrato POS de Odoo).*
4. **Consecuencias y compromisos**  
   - *Ventajas:* los casos de uso y el dominio permanecen 100 % independientes de Odoo y JSON-2; permite pruebas unitarias deterministas con dobles en memoria e intercambio del adaptador (P10).
   - *Compromiso:* requiere mantener DTOs y mapeos explícitos entre Infrastructure y Application (`MapCore`, `BuildOdooOrderPayload`).
5. **Clases y módulos implicados**  
   - Puertos: [SalesContracts.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Application/Sales/SalesContracts.cs) (`ISaleGateway`), [ISalesReportGateway.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Application/Reports/ISalesReportGateway.cs)
   - Adaptadores: [OdooSaleGateway.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Infrastructure/Odoo/OdooSaleGateway.cs), [OdooSaleGateway.History.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Infrastructure/Odoo/OdooSaleGateway.History.cs), [OdooSalesReportGateway.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Infrastructure/Odoo/OdooSalesReportGateway.cs), [OdooJson2Client.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Infrastructure/Odoo/OdooJson2Client.cs)
6. **Prueba o demostración que lo verifica**  
   - `OdooSaleGatewayTests.cs` y `OdooAdaptersTests.cs` (mapeo y redondeo fiscal).
   - [P10ExtensibilityTests.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/tests/Pos.UnitTests/Application/P10ExtensibilityTests.cs) (`Swapping_sale_gateway_adapter_works_without_modifying_workflow_or_domain`).
   - [ApiContractTests.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/tests/Pos.UnitTests/Contract/ApiContractTests.cs) (28 pruebas E2E con fake gateway).

---

## Decisión 2: Políticas de descuento variables (Strategy GoF)

1. **Problema concreto**  
   El sistema debe aplicar descuentos autorizados por servidor (`none`, `promo-10`, `bulk-15`) y rechazar cualquier política no habilitada o que produzca importes inválidos (RN-04), permitiendo incorporar nuevas promociones sin modificar el flujo de cálculo ni la UI (P10).
2. **Alternativas investigadas**  
   - *Sin patrón:* un bloque `switch`/`if-else` centralizado dentro de `PreviewSaleUseCase`. Descartado porque obliga a editar el caso de uso cada vez que cambia o se agrega una regla promocional.
   - *Con patrón:* interfaz `IDiscountPolicy` en Domain con implementaciones concretas registradas en `DiscountPolicyRegistry`.
3. **Solución elegida y categoría**  
   **Strategy** (Patrón de comportamiento GoF + Open/Closed Principle).  
   *(Nota: si solo existiera una tarifa fija inmutable, Strategy sería innecesario; aquí aporta extensibilidad real comprobada en P10).*
4. **Consecuencias y compromisos**  
   - *Ventajas:* agregar una nueva política requiere solo una clase que implemente `IDiscountPolicy` y su registro en DI; `PreviewSaleUseCase` y `SaleWorkflow` no cambian.
   - *Compromiso:* la distribución del descuento entre líneas para cumplir el contrato fiscal por línea de Odoo exige coordinar el prorrateo en `OdooSaleGateway.BuildCheckoutLinesAsync`.
5. **Clases y módulos implicados**  
   - Contrato y estrategias: [IDiscountPolicy.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Domain/Discounts/IDiscountPolicy.cs), [DiscountPolicies.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Domain/Discounts/DiscountPolicies.cs), [DiscountPolicyRegistry.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Domain/Discounts/DiscountPolicyRegistry.cs)
   - Consumidor: [PreviewSaleUseCase.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Application/Sales/PreviewSaleUseCase.cs)
6. **Prueba o demostración que lo verifica**  
   - `DiscountPolicyTests.cs` y `PreviewSaleUseCaseTests.cs`.
   - [P10ExtensibilityTests.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/tests/Pos.UnitTests/Application/P10ExtensibilityTests.cs) (`Adding_new_discount_policy_works_in_preview_and_workflow_without_modifying_use_cases`).
   - [ApiContractTests.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/tests/Pos.UnitTests/Contract/ApiContractTests.cs) (`SaleFlow_Preview_WithPromo10_AppliesDiscountStrategyCorrectly`).

---

## Decisión 3: Idempotencia y recuperación ante fallos (Diario durable de operaciones)

1. **Problema concreto**  
   En un cobro real, un timeout de red, doble clic o reinicio del servidor tras enviar `sync_from_ui` a Odoo podría provocar que un reintento duplique la venta y el pago en efectivo (RF-16, RN-08) o muestre un falso éxito (RF-17, RN-07).
2. **Alternativas investigadas**  
   - *Sin patrón:* diccionario en memoria (`ConcurrentDictionary`) o reenvío automático del `POST`. Descartado porque un reinicio pierde la memoria y el reenvío ciego puede duplicar cobros.
   - *SQLite local:* descartado para evitar dependencias nativas adicionales en un despliegue ligero.
   - *Con patrón:* diario durable por UUID en sistema de archivos (`CreateNew` + `Flush(true)`) previo al envío externo, con huella SHA-256 del payload canónico y conciliación de solo lectura.
3. **Solución elegida y categoría**  
   **Idempotent Operation Journal / Durable Claim** (Patrón de integración empresarial + persistencia técnica en Infrastructure).
4. **Consecuencias y compromisos**  
   - *Ventajas:* garantiza en el host que una misma intención (`operationId`) nunca emite un segundo `sync_from_ui` ante reintentos, concurrencia o reinicios; responde `202 Accepted` (`uncertain`) en vez de falso éxito ante fallos de conexión.
   - *Compromiso:* requiere conservar la ruta `Sales:JournalPath` fuera de `bin/` en producción.
5. **Clases y módulos implicados**  
   - Puerto y orquestación: [SalesContracts.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Application/Sales/SalesContracts.cs) (`ISaleOperationStore`), [SaleWorkflow.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Application/Sales/SaleWorkflow.cs)
   - Implementación durable: [FileSaleOperationStore.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Infrastructure/Sales/FileSaleOperationStore.cs)
   - Cliente frontend: [pendingSale.js](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/frontend/src/infrastructure/pendingSale.js), [useCases.js](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/frontend/src/application/useCases.js)
6. **Prueba o demostración que lo verifica**  
   - `SaleWorkflowTests.cs` (`Concurrent_requests_send_once`, `Repeated_request_after_store_reopen_sends_once`, `Timeout_reconciles_without_resending`).
   - `sales.test.js` (`keeps identity on timeout and reconciles without POST`).
   - [ApiContractTests.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/tests/Pos.UnitTests/Contract/ApiContractTests.cs) (`SaleFlow_FullEndToEnd_Register_ConfirmsSale_And_RepetitionIsIdempotent`, `SaleFlow_Register_AlteredPayloadWithSameOperationId_Returns409Conflict`).

---

## Decisión 4: Modelado de estados y separación Presentador/UI en Frontend

1. **Problema concreto**  
   La venta tiene transiciones estrictas (`Draft`/`Confirmed` en dominio; `confirmed`/`uncertain` en operación) y la UI de React requiere gestionar cotización, bloqueo de doble clic, búsqueda con debounce y errores claros sin acoplar las reglas a componentes JSX.
2. **Alternativas investigadas**  
   - *State GoF en backend:* crear una jerarquía de clases de estado (`ISaleState`, `DraftState`, `ConfirmedState`). Descartado por sobre-ingeniería dado que solo existen dos estados y transiciones simples.
   - *Lógica directa en componentes React:* descartado porque dificulta pruebas unitarias rápidas sin navegador y mezcla presentación con contratos HTTP.
3. **Solución elegida y categoría**  
   **Enum + invariantes de dominio** en backend (`SaleStatus`, `Sale.cs`) y **Presenter desacoplado** en frontend (`salesPresenter.js` + `salesErrors.js`) siguiendo Clean Architecture / Clean Code.
4. **Consecuencias y compromisos**  
   - *Ventajas:* código conciso, fácil de explicar en defensa y 156 pruebas rápidas en Vitest sin necesidad de automatizar navegador.
   - *Compromiso:* mantiene componentes React como vistas puras orientadas a presentación y accesibilidad.
5. **Clases y módulos implicados**  
   - Dominio backend: [SaleStatus.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Domain/Sales/SaleStatus.cs), [Sale.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Domain/Sales/Sale.cs)
   - Presentación frontend: [salesPresenter.js](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/frontend/src/ui/salesPresenter.js), [salesErrors.js](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/frontend/src/ui/salesErrors.js)
6. **Prueba o demostración que lo verifica**  
   - `SaleTests.cs` (inmutabilidad tras `Confirm`).
   - `quality.test.js`, `guidedFlow.test.jsx`, `rigorous.test.jsx`.

---

## Decisión 5: Autorización basada en políticas y roles JWT (`Admin` vs `Cashier`)

1. **Problema concreto**  
   Se requiere restringir el acceso a reportes consolidados financieros únicamente al rol `Admin`, mientras que el rol `Cashier` solo debe operar la venta POS y consultar su propio historial de ventas, sin hardcodear condicionales de usuario en los controladores.
2. **Alternativas investigadas**  
   - *Validación manual por nombre en controladores:* `if (User.Identity.Name != "admin") return Forbid()`. Descartado por frágil, no extensible y violación de principios de separación de responsabilidades.
   - *Policy-Based Authorization con ASP.NET Core:* registrar políticas tipadas (`RequireAdminRole`, `RequireCashierRole`) en DI y vincularlas mediante el atributo declarativo `[Authorize(Policy = "...")]`.
3. **Solución elegida y categoría**  
   **Policy-Based Authorization / Claim-Based Security** (Patrón arquitectural de seguridad en WebAPI).
4. **Consecuencias y compromisos**  
   - *Ventajas:* extensibilidad inmediata para futuros roles (`Supervisor`, `Auditor`), autorización verificable en middleware antes de ejecutar la lógica de aplicación, y desacoplamiento total de los controladores.
   - *Compromiso:* exige que el token JWT incluya el claim estándar `ClaimTypes.Role` emitido al autenticar.
5. **Clases y módulos implicados**  
   - Dominio y Contratos: [OperatorRoles.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Domain/Auth/OperatorRoles.cs)
   - Aplicación y Api: [LoginUseCase.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Application/Auth/LoginUseCase.cs), [Program.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Api/Program.cs), [ReportsController.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Api/Controllers/ReportsController.cs)
   - Frontend: [AdminReportPage.jsx](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/frontend/src/ui/AdminReportPage.jsx), [App.jsx](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/frontend/src/ui/App.jsx)
6. **Prueba o demostración que lo verifica**  
   - `LoginWithRoleTests.cs`, `OperatorRoleTests.cs`.
   - `AdminReportPage.test.jsx`.
   - [ApiContractTests.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/tests/Pos.UnitTests/Contract/ApiContractTests.cs) (`Reports_AsCashier_Returns403Forbidden`, `Reports_AsAdmin_Returns200WithConsolidatedReportStructure`).

---

## Decisión 6: Paginación Keyset e índices cubrientes para escalabilidad masiva (1M registros $\le 3$s)

1. **Problema concreto**  
   Al escalar la base de datos a 1,000,000 de órdenes y detalles de venta, la paginación tradicional `OFFSET / LIMIT` degrada el rendimiento de $O(1)$ a $O(N)$ porque el motor debe escanear y descartar miles de páginas previas, superando el límite de 3 segundos exigido por el cliente.
2. **Alternativas investigadas**  
   - *Paginación estándar `OFFSET / LIMIT`:* descartada; a partir del registro 500,000 los tiempos de respuesta superan los 4.5 segundos.
   - *Paginación Keyset (Seek Method):* paginar usando una condición de cursor indexada (`WHERE id < cursor ORDER BY id DESC LIMIT 20`) combinada con índices compuestos B-Tree cubrientes.
3. **Solución elegida y categoría**  
   **Keyset Pagination (Seek Method) + Covering Composite Indexes** (Patrón de acceso a datos de alto rendimiento).
4. **Consecuencias y compromisos**  
   - *Ventajas:* el tiempo de paginación es estrictamente constante $O(1)$ independientemente de si hay 1,000 o 1,000,000 de registros; tiempo de respuesta demostrado en benchmark $< 10$ ms.
   - *Compromiso:* no permite saltos directos a páginas arbitrarias (ej. "ir a página 450"), lo cual se alinea perfectamente con la experiencia de navegación de scroll continuo de un POS comercial.
5. **Clases y módulos implicados**  
   - Casos de uso: [SearchSalesHistoryUseCase.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Application/Sales/SearchSalesHistoryUseCase.cs) (`beforeId`)
   - Adaptador: [OdooSaleGateway.History.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Infrastructure/Odoo/OdooSaleGateway.History.cs)
   - Infraestructura y Benchmark: [benchmark_1m.py](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/scripts/benchmark_1m.py), [docker-compose.perf.yml](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/infra/docker-compose.perf.yml)
6. **Prueba o demostración que lo verifica**  
   - [BENCHMARK_1M.md](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/docs/BENCHMARK_1M.md) (todas las consultas críticas responden en $< 800$ ms sobre 5 millones de filas).

---

## Decisión 7: Versionamiento dual y gobernanza OpenAPI 3.0 / Swagger UI

1. **Problema concreto**  
   Se requiere implementar versionamiento formal de API (`v1`), Swagger UI con esquemas enriquecidos y soporte para tokens Bearer JWT, sin romper ninguna de las rutas preexistentes que el cliente frontend ya consume en producción.
2. **Alternativas investigadas**  
   - *Romper rutas anteriores y forzar `/api/v1/...`:* descartado por romper retrocompatibilidad y requerir refactorización masiva en clientes activos.
   - *Dual Routing con Middleware de Versionamiento:* exponer alias de rutas (`/api/v1/...` y `/api/...` apuntando a los mismos controladores) e inyectar un middleware (`ApiVersioningMiddleware`) que añade encabezados contractuales `X-Api-Version: 1.0` y `api-supported-versions: 1.0`.
3. **Solución elegida y categoría**  
   **Dual Route Aliasing + Custom Versioning Middleware + OpenAPI 3.0 Documentation** (Patrón arquitectural de evolución de API sin ruptura).
4. **Consecuencias y compromisos**  
   - *Ventajas:* compatibilidad 100% retroactiva con clientes anteriores y cumplimiento formal de estándares empresariales para nuevos consumidores vía Swagger UI.
   - *Compromiso:* los atributos de ruta en controladores definen múltiples plantillas `[Route("api/[controller]")], [Route("api/v1/[controller]")]`.
5. **Clases y módulos implicados**  
   - Middleware: [ApiVersioningMiddleware.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Api/Middleware/ApiVersioningMiddleware.cs)
   - Configuración: [Program.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Api/Program.cs)
   - Controladores: [AuthController.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Api/Controllers/AuthController.cs), [ProductsController.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Api/Controllers/ProductsController.cs), [SalesController.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Api/Controllers/SalesController.cs), [ReportsController.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Api/Controllers/ReportsController.cs)
6. **Prueba o demostración que lo verifica**  
   - [ApiContractTests.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/tests/Pos.UnitTests/Contract/ApiContractTests.cs) (`ApiVersioning_RequestsIncludeVersionHeaders`, `Swagger_OpenApi_SchemaIsValid`).

---

## Decisión 8: Arquitectura de red local (LAN) con CORS dinámico RFC 1918

1. **Problema concreto**  
   Para operar en un entorno de dos computadoras (PC Servidor con Backend/Odoo y PC Cliente con Frontend POS en caja), el servidor debe escuchar peticiones en la red de área local sin desproteger los endpoints ni hardcodear direcciones IP privadas que cambian por DHCP.
2. **Alternativas investigadas**  
   - *Permitir todos los orígenes con `AllowAnyOrigin()`:* descartado porque la especificación W3C prohíbe el comodín `*` cuando se habilitan credenciales o cabeceras de autorización HTTP Bearer.
   - *CORS dinámico basado en predicado de subredes privadas RFC 1918:* evaluar dinámicamente si el origen peticionario pertenece a `192.168.0.0/16`, `10.0.0.0/8`, `172.16.0.0/12` o `localhost`.
3. **Solución elegida y categoría**  
   **Dynamic RFC 1918 Subnet CORS Validator + Kestrel 0.0.0.0 Binding** (Patrón de seguridad perimetral para redes locales).
4. **Consecuencias y compromisos**  
   - *Ventajas:* el sistema funciona de inmediato en cualquier red local sin necesidad de editar archivos de configuración al cambiar de red Wi-Fi/Ethernet.
   - *Compromiso:* en producción pública en la nube debe especificarse la lista blanca de dominios en la variable `CORS_ALLOWED_ORIGINS`.
5. **Clases y módulos implicados**  
   - Backend: [Program.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/src/Pos.Api/Program.cs)
   - Frontend: [apiClient.js](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/frontend/src/infrastructure/apiClient.js) (`resolveApiBaseUrl`)
   - Diagnóstico: [start_lan.ps1](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/scripts/start_lan.ps1), [verify_lan.py](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/scripts/verify_lan.py), [detect_lan_ip.py](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/scripts/detect_lan_ip.py)
6. **Prueba o demostración que lo verifica**  
   - [test_lan_scripts.py](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/scripts/test_lan_scripts.py) (5 pruebas aprobadas).
   - [ApiContractTests.cs](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/tests/Pos.UnitTests/Contract/ApiContractTests.cs) (validación de cabeceras CORS en preflight OPTIONS).

---

## Decisión 9: Flujo POS Odoo ágil con caché reactiva y calculadora de cambio en memoria

1. **Problema concreto**  
   Un operador de caja requiere una interfaz con alta velocidad de respuesta similar a Odoo POS nativo: búsqueda instantánea de productos con código de barras, atajos de teclado sin tocar el mouse, y cálculo inmediato del vuelto para pagos en efectivo.
2. **Alternativas investigadas**  
   - *Llamadas HTTP por cada pulsación de tecla:* descartado por saturar la red y generar parpadeos en la interfaz.
   - *Presentador con caché en memoria local, debounce, atajos globales y calculadora reactiva:* almacenar el catálogo consultado en memoria del cliente (`catalogPresenter.js`), resolver búsquedas localmente con debounce de 150 ms, capturar `Enter` para agregar el producto exacto y `Esc` para limpiar la búsqueda, y calcular el vuelto en tiempo real (`SalesPanel.jsx`).
3. **Solución elegida y categoría**  
   **Model-View-Presenter (MVP) + Reactive Observer State** (Patrón arquitectural de presentación en UI).
4. **Consecuencias y compromisos**  
   - *Ventajas:* experiencia fluida, operación 100% accesible mediante teclado y reducción drástica de consultas redundantes al backend.
   - *Compromiso:* el catálogo en memoria debe invalidarse o refrescarse si se modifican productos en Odoo.
5. **Clases y módulos implicados**  
   - Presenter y Estado: [catalogPresenter.js](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/frontend/src/ui/catalogPresenter.js), [salesPresenter.js](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/frontend/src/ui/salesPresenter.js)
   - Componentes UI: [CatalogPanel.jsx](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/frontend/src/ui/CatalogPanel.jsx), [PosPage.jsx](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/frontend/src/ui/PosPage.jsx), [SalesPanel.jsx](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/frontend/src/ui/SalesPanel.jsx)
6. **Prueba o demostración que lo verifica**  
   - [odooPosWorkflow.test.jsx](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/frontend/src/ui/odooPosWorkflow.test.jsx) (10 pruebas completas aprobadas en Vitest).
