# Decisiones de Diseño — POS Integrado con Odoo 20 Community (Semana 4)

Este documento consolida las decisiones arquitectónicas y de diseño reales del sistema. Se prioriza la simplicidad verificable y la separación de responsabilidades sobre el uso artificial de patrones.

---

## Decisión 1: Aislamiento de la integración con Odoo 20 (Adapter / Anti-Corruption Layer)

1. **Problema concreto**  
   Odoo 20 expone RPC JSON-2 (`search_read`, `sync_from_ui`) con dominios en forma de arreglos heterogéneos, comandos relacionales (`[0, 0, {...}]`, `[6, 0, ids]`), reglas propias de redondeo fiscal (`round_globally` vs `round_per_line`) y formatos de fecha/moneda específicos. Acoplar los casos de uso o el frontend a ese contrato rompería el dominio ante cambios de Odoo e impediría probar reglas de negocio sin una instancia levantada.
2. **Alternativas investigadas**  
   - *Sin patrón:* llamar `HttpClient` y manipular `JsonElement` directamente desde `SalesController` o `SaleWorkflow`. Descartado por acoplamiento fuerte con el transporte HTTP y el esquema de Odoo.
   - *Con patrón:* definir puertos estrechos en Application (`ISaleGateway`, `ISaleHistoryReader`, `IProductCatalog`, `IPosSessionGateway`) e implementarlos en Infrastructure mediante adaptadores dedicados (`OdooSaleGateway`, `OdooJson2Client`).
3. **Solución elegida y categoría**  
   **Adapter / Anti-Corruption Layer** (Patrón estructural GoF + Clean Architecture / DIP).  
   *(Nota: en un CRUD simple sin traducción de esquema ni reglas fiscales, un servicio HTTP directo podría bastar; aquí el patrón sí aporta valor por la complejidad del contrato POS de Odoo).*
4. **Consecuencias y compromisos**  
   - *Ventajas:* los casos de uso y el dominio permanecen 100 % independientes de Odoo y JSON-2; permite pruebas unitarias deterministas con dobles en memoria e intercambio del adaptador (P10).
   - *Compromiso:* requiere mantener DTOs y mapeos explícitos entre Infrastructure y Application (`MapCore`, `BuildOdooOrderPayload`).
5. **Clases y módulos implicados**  
   - Puerto: [SalesContracts.cs](file:///c:/Proyecto_P/backend/src/Pos.Application/Sales/SalesContracts.cs) (`ISaleGateway`)
   - Adaptador: [OdooSaleGateway.cs](file:///c:/Proyecto_P/backend/src/Pos.Infrastructure/Odoo/OdooSaleGateway.cs), [OdooSaleGateway.History.cs](file:///c:/Proyecto_P/backend/src/Pos.Infrastructure/Odoo/OdooSaleGateway.History.cs), [OdooJson2Client.cs](file:///c:/Proyecto_P/backend/src/Pos.Infrastructure/Odoo/OdooJson2Client.cs)
6. **Prueba o demostración que lo verifica**  
   - `OdooSaleGatewayTests.cs` y `OdooAdaptersTests.cs` (mapeo y redondeo fiscal).
   - [P10ExtensibilityTests.cs](file:///c:/Proyecto_P/backend/tests/Pos.UnitTests/Application/P10ExtensibilityTests.cs) (`Swapping_sale_gateway_adapter_works_without_modifying_workflow_or_domain`).

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
   - Contrato y estrategias: [IDiscountPolicy.cs](file:///c:/Proyecto_P/backend/src/Pos.Domain/Discounts/IDiscountPolicy.cs), [DiscountPolicies.cs](file:///c:/Proyecto_P/backend/src/Pos.Domain/Discounts/DiscountPolicies.cs), [DiscountPolicyRegistry.cs](file:///c:/Proyecto_P/backend/src/Pos.Domain/Discounts/DiscountPolicyRegistry.cs)
   - Consumidor: [PreviewSaleUseCase.cs](file:///c:/Proyecto_P/backend/src/Pos.Application/Sales/PreviewSaleUseCase.cs)
6. **Prueba o demostración que lo verifica**  
   - `DiscountPolicyTests.cs` y `PreviewSaleUseCaseTests.cs` (política no autorizada y política defectuosa `GreedyPolicy`).
   - [P10ExtensibilityTests.cs](file:///c:/Proyecto_P/backend/tests/Pos.UnitTests/Application/P10ExtensibilityTests.cs) (`Adding_new_discount_policy_works_in_preview_and_workflow_without_modifying_use_cases`).

---

## Decisión 3: Idempotencia y recuperación ante fallos (Diario durable de operaciones)

1. **Problema concreto**  
   En un cobro real, un timeout de red, doble clic o reinicio del servidor tras enviar `sync_from_ui` a Odoo podría provocar que un reintento duplique la venta y el pago en efectivo (RF-16, RN-08) o muestre un falso éxito (RF-17, RN-07).
2. **Alternativas investigadas**  
   - *Sin patrón:* diccionario en memoria (`ConcurrentDictionary`) o reenvío automático del `POST`. Descartado porque un reinicio pierde la memoria y el reenvío ciego puede duplicar cobros.
   - *SQLite local:* descartado para evitar dependencias adicionales en un despliegue de un solo host.
   - *Con patrón:* diario durable por UUID en sistema de archivos (`CreateNew` + `Flush(true)`) previo al envío externo, con huella SHA-256 del payload canónico y conciliación de solo lectura.
3. **Solución elegida y categoría**  
   **Idempotent Operation Journal / Durable Claim** (Patrón de integración empresarial + persistencia técnica en Infrastructure).
4. **Consecuencias y compromisos**  
   - *Ventajas:* garantiza al menos en un host que una misma intención (`operationId`) nunca emite un segundo `sync_from_ui` ante reintentos, concurrencia o reinicios; responde `202 Accepted` (`uncertain`) en vez de falso éxito.
   - *Compromiso:* requiere conservar la ruta `Sales:JournalPath` fuera de `bin/` en producción y no soporta exclusión distribuida entre múltiples servidores sin un almacén compartido.
5. **Clases y módulos implicados**  
   - Puerto y orquestación: [SalesContracts.cs](file:///c:/Proyecto_P/backend/src/Pos.Application/Sales/SalesContracts.cs) (`ISaleOperationStore`), [SaleWorkflow.cs](file:///c:/Proyecto_P/backend/src/Pos.Application/Sales/SaleWorkflow.cs)
   - Implementación durable: [FileSaleOperationStore.cs](file:///c:/Proyecto_P/backend/src/Pos.Infrastructure/Sales/FileSaleOperationStore.cs)
   - Cliente frontend: [pendingSale.js](file:///c:/Proyecto_P/frontend/src/infrastructure/pendingSale.js), [useCases.js](file:///c:/Proyecto_P/frontend/src/application/useCases.js)
6. **Prueba o demostración que lo verifica**  
   - `SaleWorkflowTests.cs` (`Concurrent_requests_send_once`, `Repeated_request_after_store_reopen_sends_once`, `Timeout_reconciles_without_resending`).
   - [sales.test.js](file:///c:/Proyecto_P/frontend/src/application/sales.test.js) (`keeps identity on timeout and reconciles without POST`).

---

## Decisión 4: Modelado simple de estados y separación Presentador/UI en Frontend

1. **Problema concreto**  
   La venta tiene pocos estados (`Draft`/`Confirmed` en dominio; `confirmed`/`uncertain` en operación) y la UI de React requiere gestionar cotización, bloqueo de doble clic, búsqueda con debounce y errores claros sin acoplar las reglas a componentes JSX.
2. **Alternativas investigadas**  
   - *State GoF en backend:* crear una jerarquía de clases de estado (`ISaleState`, `DraftState`, `ConfirmedState`). Descartado por sobre-ingeniería dado que solo existen dos estados y transiciones simples (en este caso State GoF no aporta valor).
   - *Lógica directa en componentes React:* descartado porque dificulta pruebas unitarias rápidas sin navegador y mezcla presentación con contratos HTTP.
3. **Solución elegida y categoría**  
   **Enum + invariantes de dominio** en backend (`SaleStatus`) y **Presenter desacoplado** en frontend (`salesPresenter.js` + `salesErrors.js`) siguiendo Clean Architecture / Clean Code.
4. **Consecuencias y compromisos**  
   - *Ventajas:* código conciso, fácil de explicar en defensa y 138 pruebas rápidas en Vitest sin necesidad de automatizar navegador.
   - *Compromiso:* si en el futuro la venta incorporara múltiples estados intermedios complejos (crédito, reservas parciales, anulaciones), convendría migrar `SaleStatus` a State GoF.
5. **Clases y módulos implicados**  
   - Dominio backend: [SaleStatus.cs](file:///c:/Proyecto_P/backend/src/Pos.Domain/Sales/SaleStatus.cs), [Sale.cs](file:///c:/Proyecto_P/backend/src/Pos.Domain/Sales/Sale.cs)
   - Presentación frontend: [salesPresenter.js](file:///c:/Proyecto_P/frontend/src/ui/salesPresenter.js), [salesErrors.js](file:///c:/Proyecto_P/frontend/src/ui/salesErrors.js)
6. **Prueba o demostración que lo verifica**  
   - `SaleTests.cs` (inmutabilidad tras `Confirm`).
   - `quality.test.js`, `guidedFlow.test.jsx`, `rigorous.test.jsx`.
