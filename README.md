# POS Integrado con Odoo 20 Community — Entrega Final y Cierre de Calidad

Sistema POS web de grado empresarial con **Clean Architecture** en **React + ASP.NET Core (.NET 10)** integrado de forma nativa con **Odoo 20 Community** vía RPC JSON-2. Implementa catálogo de productos en tiempo real, carrito interactivo reactivo, cálculo de descuentos mediante **Strategy GoF**, cotización fiscal autoritativa, cobro en efectivo con cálculo dinámico de cambio, garantía de **idempotencia durable** ante reintentos/concurrencia, conciliación en dos fases, historial de ventas con paginación keyset $O(1)$, control de acceso basado en roles (**Admin** y **Cajero**), reportes consolidados financieros y despliegue multi-PC en red local (LAN).

```text
┌──────────────┐       HTTP / JSON       ┌────────────────────────┐      RPC JSON-2      ┌─────────────────────────┐
│ React (Vite) │ ◄─────────────────────► │ ASP.NET Core (.NET 10) │ ◄──────────────────► │ Odoo 20 Community (POS) │
└──────────────┘   Bearer JWT (Roles)    └────────────────────────┘   API Key (Segura)   └─────────────────────────┘
                                                     │                                                │
                                            Diario Durable (.claim)                             PostgreSQL 16
```

---

## 1. Documentación Clave del Proyecto

- **[Matriz de Trazabilidad Integral](docs/MATRIZ_TRAZABILIDAD.md):** Auditoría 100% de requisitos funcionales (RF-01 a RF-21), reglas de negocio (RN-01 a RN-08), requisitos no funcionales, pruebas P01 a P15 y commits del repositorio.
- **[Decisiones de Diseño](docs/DECISIONES_DISENO.md):** Fundamentación técnica de las 9 decisiones arquitectónicas y patrones GoF (Adapter, Strategy, Durable Claim, MVP, Keyset Pagination, CORS RFC 1918, etc.).
- **[Benchmark Masivo 1M](docs/BENCHMARK_1M.md):** Demostración empírica de tiempos de respuesta $\le 3$ s (consultas en 8 a 680 ms) sobre 5,000,000 de registros en PostgreSQL.
- **[Guía de Despliegue en Red Local (LAN)](docs/GUIA_DESPLIEGUE_LAN.md):** Checklist de conectividad, configuración de firewall y verificación paso a paso para entorno multi-PC.
- **[Especificación SDD Semana 3](docs/sdd/semana3/especificacion.md)** y **[Progreso SDD](docs/sdd/semana3/PROGRESS.md):** Especificación de arquitectura y trazabilidad ágil.

---

## 2. Prerrequisitos y Estructura del Repositorio

- **Git**, **.NET 10 SDK**, **Python 3.10+**, **Node.js 20+** y **Docker Desktop** con soporte para Compose v2.
- Shell recomendada: **PowerShell**.
- Puertos estándar:
  - Backend API: `5038` (escucha en `0.0.0.0:5038` para LAN y localhost)
  - Frontend UI: `5173` (escucha en `0.0.0.0:5173` para LAN y localhost)
  - Odoo 20 POS: `8069` (loopback `127.0.0.1:8069`)
  - PostgreSQL Desarrollo: `5432` / Benchmark Rendimiento: `5433`

---

## 3. Guía de Ejecución Rápida

### A. Modo Local Estándar (Una Sola Máquina)

1. **Iniciar Odoo y PostgreSQL en Docker:**
   ```powershell
   docker compose -p patrones-semana3 --env-file infra/.env -f infra/docker-compose.yml -f infra/compose.env.local up -d
   ```

2. **Iniciar Backend API (.NET 10):**
   ```powershell
   python -B scripts/run-week3.py
   ```
   *La API estará disponible en `http://localhost:5038` y Swagger UI en `http://localhost:5038/swagger`.*

3. **Iniciar Frontend React:**
   ```powershell
   npm --prefix frontend run dev -- --host localhost --port 5173 --strictPort
   ```
   *Acceso en `http://localhost:5173`.*

### B. Modo Red Local (LAN Multi-PC)

Para operar con el Servidor en una PC y el Frontend POS en otra PC de la red local:

1. **En PC Servidor:**
   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts/start_lan.ps1
   ```
   *El script detectará automáticamente la IP privada (ej. `192.168.1.100`) e iniciará backend y frontend vinculados a `0.0.0.0`.*

2. **En PC Cliente (Caja):**
   - Abrir el navegador en: `http://<IP_SERVIDOR>:5173`
   - La aplicación detectará automáticamente el origen y conectará al backend en `http://<IP_SERVIDOR>:5038`.

3. **Verificación automatizada de conectividad:**
   ```powershell
   python -B scripts/verify_lan.py --server-ip <IP_SERVIDOR> --client-ip <IP_CLIENTE>
   ```

---

## 4. Endpoints de la API y Control de Acceso

Todos los endpoints implementan **versionamiento dual** (`/api/v1/...` formal y `/api/...` retrocompatible) e inyectan las cabeceras `X-Api-Version: 1.0` y `api-supported-versions: 1.0`.

| Método | Ruta | Rol Requerido | Propósito / Contrato | Códigos de Estado |
|---|---|---|---|---|
| `POST` | `/api/v1/auth/login` | Público | Autenticación de operador y emisión de Bearer JWT con claims | `200`, `400`, `401` |
| `GET` | `/api/v1/pos/sessions/active` | `Cashier`, `Admin` | Consulta de la sesión activa de Odoo POS | `200`, `401`, `502` |
| `GET` | `/api/v1/products?search=&limit=` | `Cashier`, `Admin` | Catálogo de productos vendibles (`sale_ok`, `available_in_pos`) | `200`, `401` |
| `GET` | `/api/v1/discounts` | `Cashier`, `Admin` | Lista de políticas de descuento activas (`none`, `promo-10`, `bulk-15`) | `200`, `401` |
| `POST` | `/api/v1/sales/preview` | `Cashier`, `Admin` | Cálculo provisional de subtotal y descuento en backend | `200`, `400`, `401` |
| `POST` | `/api/v1/sales/quote` | `Cashier`, `Admin` | Cotización oficial en Odoo con desglose fiscal de impuestos | `200`, `400`, `401`, `409`, `502` |
| `POST` | `/api/v1/sales` | `Cashier`, `Admin` | Registro de venta en Odoo e idempotencia con diario durable | `200`, `202`, `400`, `401`, `409`, `502` |
| `GET` | `/api/v1/sales/operations/{uuid}` | `Cashier`, `Admin` | Conciliación de estado de operación por identificador único | `200`, `401`, `404` |
| `GET` | `/api/v1/sales/history?search=&beforeId=` | `Cashier`, `Admin` | Historial paginado keyset filtrado por el cajero autenticado | `200`, `401` |
| `GET` | `/api/v1/sales/{id}` | `Cashier`, `Admin` | Detalle fiscal y líneas de una orden confirmada | `200`, `401`, `404` |
| `GET` | `/api/v1/reports/sales?from=&to=` | **`Admin`** | Reporte consolidado financiero (ventas totales, métodos, cajeros) | `200`, `401`, `403` |
| `GET` | `/openapi/v1.json` / `/swagger` | Público | Documentación OpenAPI 3.0 interactiva con soporte Bearer JWT | `200` |

---

## 5. Resumen de Calidad y Pruebas Automatizadas

La solución cuenta con **306 pruebas automatizadas aprobadas al 100%**, ejecutadas sin requerir dependencias externas ni servicios gráficos:

```powershell
# 1. Pruebas Unitarias y de Componentes Frontend (156 pruebas en 14 archivos)
npm --prefix frontend test

# 2. Compilación de Producción Frontend (0 errores)
npm --prefix frontend run build

# 3. Pruebas Unitarias y de Contrato Backend .NET 10 (139 pruebas)
$artifacts = Join-Path ([System.IO.Path]::GetTempPath()) ('pos-test-' + [guid]::NewGuid().ToString('N'))
dotnet test backend/tests/Pos.UnitTests --artifacts-path $artifacts

# 4. Pruebas Automatizadas Python (11 pruebas)
py -B scripts/test_run_week3.py
py -B scripts/test_lan_scripts.py
```

### Tabla de Resultados de Verificación

| Suite de Pruebas | Entorno / Motor | Pruebas | Estado |
|---|---|---|---|
| [Pos.UnitTests](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/tests/Pos.UnitTests) (Dominio, Aplicación, Infraestructura) | .NET 10 SDK / xUnit | 111 | **100% Aprobadas** |
| [ApiContractTests](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/tests/Pos.UnitTests/Contract/ApiContractTests.cs) (Contratos HTTP, Versionamiento, E2E) | WebApplicationFactory / xUnit | 28 | **100% Aprobadas** |
| [frontend/src](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/frontend/src) (Dominio, Aplicación, MVP UI, Flujo Odoo POS) | Vitest / Testing Library | 156 | **100% Aprobadas** |
| [test_run_week3.py](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/scripts/test_run_week3.py) (Precedencia de configuración) | Python unittest | 6 | **100% Aprobadas** |
| [test_lan_scripts.py](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/scripts/test_lan_scripts.py) (Diagnóstico y verificación LAN) | Python unittest | 5 | **100% Aprobadas** |
| **Total de Pruebas Automatizadas** | | **306** | **100% Aprobadas** |
| [Pos.IntegrationTests](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/backend/tests/Pos.IntegrationTests) (Integración real con Odoo) | .NET 10 / xUnit | 5 | Omitidas (sin Odoo activo) |
| [semana3-p09.json](file:///d:/PATRONES DE SOTFWARE/ProyectoPatrones/docs/evidencias/semana3-p09.json) (Venta real en Odoo) | Integración Real | 1 | Histórica Verificada |

---

## 6. Patrones de Diseño Aplicados y Verificados

1. **Adapter / Anti-Corruption Layer (GoF Estructural):** Aísla el dominio de los formatos RPC y esquemas relacionales heterogéneos de Odoo 20 (`OdooSaleGateway`, `OdooSalesReportGateway`).
2. **Strategy (GoF Comportamiento):** Extensibilidad desacoplada de promociones y descuentos (`IDiscountPolicy`, `Promo10DiscountPolicy`, `Bulk15DiscountPolicy`).
3. **Idempotent Operation Journal / Durable Claim (Integración Empresarial):** Garantiza atomicidad y no duplicación ante fallos de red y concurrencia mediante claims con huella SHA-256 (`FileSaleOperationStore`).
4. **Model-View-Presenter (MVP) y Reactive Observer:** Desacopla la lógica de presentación del renderizado React (`salesPresenter`, `catalogPresenter`, `salesErrors`).
5. **Keyset Pagination (Patrón de Datos):** Paginación por cursor indexado que mantiene tiempo de respuesta constante $O(1)$ sobre millones de registros.
6. **Policy-Based Authorization:** Control de acceso declarativo basado en claims de rol (`RequireAdminRole`, `RequireCashierRole`) sin hardcodeo en controladores.
