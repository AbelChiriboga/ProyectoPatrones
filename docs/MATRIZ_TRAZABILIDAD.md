# Matriz de Trazabilidad Final (RF / RN / RNF / CA / P)

> **Criterio de honestidad:** se distinguen explícitamente las pruebas unitarias ejecutadas en esta entrega (91 .NET + 138 React + 6 lanzador), las pruebas de integración reales omitidas por requerir instancia Odoo activa (5 .NET) y la evidencia histórica P09 de Semana 3.

## 1. Requisitos Funcionales (RF-01 a RF-17)

| ID | Requisito | Estado | Evidencia (archivo / clase / prueba) |
|---|---|---|---|
| **RF-01** | Autenticación de operador POS | Cumplido | `AuthController.cs`, `LoginUseCase.cs`, `ConfiguredOperatorAuthenticator.cs`, `JwtTokenIssuer.cs`; `LoginUseCaseTests.cs` |
| **RF-02** | Consulta de sesión POS activa | Cumplido | `PosSessionsController.cs`, `GetActiveSessionUseCase.cs`, `OdooPosSessionGateway.cs`; `GetActiveSessionUseCaseTests.cs` |
| **RF-03** | Búsqueda y listado de productos reales | Cumplido | `ProductsController.cs`, `SearchProductsUseCase.cs`, `OdooProductCatalog.cs`; `SearchProductsUseCaseTests.cs`, `CatalogPanel.jsx` |
| **RF-04** | Filtrado por productos vendibles/activos en POS | Cumplido | `OdooProductCatalog.cs` (`sale_ok`, `available_in_pos`); `OdooAdaptersTests.cs` |
| **RF-05** | Carrito editable (agregar, editar cantidad, quitar) | Cumplido | `frontend/src/domain/cart.js`, `QuantityInput.jsx`, `RemoveProductDialog.jsx`; `cart.test.js`, `quality.test.js` |
| **RF-06** | Cálculo provisional en backend | Cumplido | `SalesController.Preview`, `PreviewSaleUseCase.cs`, `Sale.cs`; `PreviewSaleUseCaseTests.cs` |
| **RF-07 / RF-08** | Consulta y aplicación de descuentos autorizados | Cumplido | `DiscountsController.cs`, `ListDiscountsUseCase.cs`, `DiscountPolicyRegistry.cs`; `DiscountPolicyTests.cs` |
| **RF-09** | Confirmar líneas, precios y descuentos antes de cobrar | Cumplido | `SaleWorkflow.QuoteAsync`, `SaleWorkflow.RegisterAsync`, `OdooSaleGateway.QuoteAsync`; `SaleWorkflowTests.cs` |
| **RF-10** | Selección de método de pago configurado (efectivo) | Cumplido | `OdooSaleGateway.ReadCashMethodsAsync` (`type = cash`), `SalesPanel.jsx`; `guidedFlow.test.jsx` |
| **RF-11** | Registrar venta y pago en Odoo | Cumplido (histórico real / unitarias) | `OdooSaleGateway.SendAsync` (`pos.order/sync_from_ui`); histórico `docs/evidencias/semana3-p09.json` |
| **RF-12** | Mostrar identificador Odoo, estado y resultado real | Cumplido | `SaleWorkflow.StatusAsync`, `salesPresenter.js`, `SalesViews.jsx` |
| **RF-13** | Consultar historial paginado y buscable | Cumplido | `SearchSalesHistoryUseCase.cs`, `OdooSaleGateway.History.cs`; `SearchSalesHistoryTests.cs`, `liveHistory.test.jsx` |
| **RF-14** | Consultar detalle real de una venta | Cumplido | `SalesController.Detail`, `OdooSaleGateway.DetailAsync`, `SalesViews.jsx`; `historyDialogs.test.jsx` |
| **RF-15** | Validar entradas antes de enviar | Cumplido | `SaleWorkflow.ValidateCheckoutLines`, `quantityEditor.js`, `catalogSearch.js`, `historyQuery.js`; `rigorous.test.jsx` |
| **RF-16** | Evitar duplicación de ventas ante reintentos/concurrencia | Cumplido (1 host) | `FileSaleOperationStore.cs`, `SaleWorkflow.RegisterAsync`, `pendingSale.js`; `SaleWorkflowTests.cs`, `sales.test.js` |
| **RF-17** | Manejo de fallos de comunicación sin falso éxito | Cumplido | `SaleWorkflow.RegisterAsync` (HTTP 202 `uncertain`), `salesErrors.js`; `httpPosApi.test.js` |

## 2. Reglas de Negocio (RN-01 a RN-08)

| ID | Regla | Estado | Evidencia |
|---|---|---|---|
| **RN-01** | Venta confirmada requiere al menos una línea | Cumplido | `Sale.Confirm`, `SaleWorkflow.ValidateCheckoutLines`; `SaleTests.cs`, `SaleWorkflowTests.cs` |
| **RN-02** | Cantidades enteras positivas y dentro de límite | Cumplido | `SaleLine`, `SaleWorkflow.ValidateCheckoutLines`, `cart.js`; `SaleTests.cs`, `cart.test.js` |
| **RN-03** | Precios autoritativos provienen de Odoo | Cumplido | `PreviewSaleUseCase`, `OdooSaleGateway.QuoteAsync`, `expectedTotal` check (`409 sale.total_changed`) |
| **RN-04** | Solo políticas de descuento habilitadas | Cumplido | `DiscountPolicyRegistry.Get`; `DiscountPolicyTests.cs`, `PreviewSaleUseCaseTests.cs` |
| **RN-05** | Coherencia de sesión abierta, productos, impuestos y efectivo | Cumplido | `OdooSaleGateway.ReadValidatedCheckoutContextAsync`, `SaleWorkflow.RegisterAsync` |
| **RN-06** | Venta confirmada no es editable | Cumplido | `Sale.EnsureEditable`, ausencia de endpoint de edición; `SaleTests.cs` |
| **RN-07** | Pago exitoso solo tras verificar respuesta en Odoo | Cumplido | `SaleWorkflow.StatusAsync` + `MatchesOperation` |
| **RN-08** | Misma operación (`operationId`) no genera otra venta | Cumplido (1 host) | `FileSaleOperationStore.TryCreate` + huella SHA-256; `SaleWorkflowTests.cs` |

## 3. Requisitos No Funcionales y Clean Architecture (RNF / CA)

| Código | Aspecto | Estado | Evidencia |
|---|---|---|---|
| **CA-BE** | Clean Architecture Backend (dependencias hacia adentro) | Cumplido | `Pos.Domain.csproj` (0 refs), `Pos.Application.csproj` (solo Domain), `Pos.Infrastructure.csproj` (Application), `Pos.Api.csproj` |
| **CA-FE** | Clean Architecture Frontend (dependencias hacia adentro) | Cumplido | `domain/` y `application/` sin imports de React/fetch/infrastructure (incluyendo `sales.test.js` refactorizado) |
| **RNF-Seguridad** | API key solo en backend, JWT, CORS y `ProblemDetails` | Cumplido | `Program.cs`, `OdooJson2Client.cs`, `PosExceptionHandler.cs`, `.gitignore` |
| **RNF-Usabilidad** | Bloqueo de doble clic, errores claros y diálogos accesibles | Cumplido (técnico) | `salesPresenter.js`, `salesErrors.js`, `SafeDialog.jsx`, `dialogBehavior.js` |

## 4. Pruebas Mínimas Oficiales (P01 a P10)

| Prueba | Descripción | Tipo / Estado | Evidencia |
|---|---|---|---|
| **P01** | Venta vacía rechazada | Unitaria / **Aprobada** | `SaleTests.cs`, `SaleWorkflowTests.cs` |
| **P02** | Cantidad inválida (0, negativo, exceso) | Unitaria / **Aprobada** | `SaleTests.cs`, `PreviewSaleUseCaseTests.cs`, `SaleWorkflowTests.cs` |
| **P03** | Sesión inválida o cerrada | Unitaria / **Aprobada** | `GetActiveSessionUseCaseTests.cs`, `SaleWorkflowTests.cs` |
| **P04** | Producto inexistente o deshabilitado | Unitaria / **Aprobada** | `PreviewSaleUseCaseTests.cs`, `OdooAdaptersTests.cs`, `SaleWorkflowTests.cs` |
| **P05** | Fallo o timeout de comunicación sin falso éxito | Unitaria / **Aprobada** | `SaleWorkflowTests.cs`, `OdooSaleGatewayTests.cs` |
| **P06** | Repetición idempotente y concurrencia | Unitaria / **Aprobada** | `SaleWorkflowTests.cs`, `sales.test.js` |
| **P07** | Carrito, cantidades y total provisional en frontend | Unitaria / **Aprobada** | `cart.test.js`, `useCases.test.js`, `quality.test.js` |
| **P08** | Error comprensible en frontend cuando falla API | Unitaria / **Aprobada** | `httpPosApi.test.js`, `guidedFlow.test.jsx`, `rigorous.test.jsx` |
| **P09** | Venta real en Odoo (2 productos + efectivo) | Integración real / **Histórica** | Evidencia previa [semana3-p09.json](file:///c:/Proyecto_P/docs/evidencias/semana3-p09.json); guía reproducible en FASE 2 para nueva corrida |
| **P10** | Cambio controlado sin afectar componentes no relacionados | Unitaria / **Aprobada** | [P10ExtensibilityTests.cs](file:///c:/Proyecto_P/backend/tests/Pos.UnitTests/Application/P10ExtensibilityTests.cs) |
