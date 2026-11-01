# POS integrado con Odoo 20 Community — Proyecto integrador de Patrones de Software

Estado: **Semana 2** (carrito, descuentos con Strategy, login del operador y frontend React) sobre la **semana 1**
completada (requisitos, investigación inicial, diseño arquitectónico, instalación de Odoo, primera consulta integrada y
prueba temprana de registro de venta).

```
Frontend React        →  API REST ASP.NET Core (.NET 10)  →  Odoo 20 Community (JSON-2)  →  PostgreSQL
```

## Estructura

| Carpeta | Contenido |
|---|---|
| `backend/src/Pos.Domain` | Entidades y reglas (`Product`, `PosSession`). Sin dependencias. |
| `backend/src/Pos.Application` | Casos de uso y puertos (`IProductCatalog`, `IPosSessionGateway`). Depende solo de Domain. |
| `backend/src/Pos.Infrastructure` | Adaptadores de Odoo (`OdooJson2Client`, `OdooProductCatalog`, `OdooPosSessionGateway`). |
| `backend/src/Pos.Api` | Controladores REST, manejo uniforme de errores (ProblemDetails), composición. |
| `backend/tests` | Pruebas unitarias (dobles de prueba) y de integración contra Odoo real. |
| `infra` | `docker-compose.yml` de Odoo 20 + PostgreSQL y script de configuración del POS. |
| `scripts` | Inicialización de Odoo y prueba temprana de venta (P09). |
| `docs` | Informe de la semana 1 y evidencias de peticiones/respuestas. |

## Requisitos previos

- Docker Desktop
- .NET SDK 10
- Git Bash (Windows) o una shell POSIX para los scripts

## 1. Preparar Odoo 20 Community

```bash
cp infra/.env.example infra/.env
```

Edite `infra/.env` con una contraseña propia de PostgreSQL y luego ejecute:

```bash
bash scripts/odoo-init.sh
```

El script crea la base `pos_db` con `point_of_sale`, el impuesto IVA 15 %, cuatro productos, el método de pago
**Efectivo**, el punto de venta **Caja Principal**, el usuario de integración `pos_integration` (solo grupo
*Usuario POS*), abre una sesión y genera una API key válida por 90 días. Copie la línea `APIKEY` mostrada
(solo se muestra una vez). Odoo queda en http://localhost:8069 (usuario `admin` / `admin`: cámbielo).

## 2. Configurar el backend (sin secretos en el repositorio)

```bash
dotnet user-secrets set "Odoo:ApiKey" "<api-key>" --project backend/src/Pos.Api
```

Además, la semana 2 exige una clave para firmar los tokens y al menos un operador (también en user-secrets):

```bash
dotnet user-secrets set "Jwt:Key" "<texto-aleatorio-de-32-o-mas-caracteres>" --project backend/src/Pos.Api
dotnet user-secrets set "Auth:Operators:0:Username" "cajero1" --project backend/src/Pos.Api
dotnet user-secrets set "Auth:Operators:0:Password" "<contraseña>" --project backend/src/Pos.Api
```

Variables disponibles (`appsettings.json` o variables de entorno con `__`):

| Clave | Por defecto | Descripción |
|---|---|---|
| `Odoo:BaseUrl` | `http://localhost:8069` | URL de Odoo |
| `Odoo:Database` | `pos_db` | Base de datos (cabecera `X-Odoo-Database`) |
| `Odoo:ApiKey` | — | API key del usuario de integración (obligatoria) |
| `Odoo:PosConfigId` | `null` | Limita la sesión activa a un punto de venta |
| `Odoo:TimeoutSeconds` | `15` | Tiempo máximo de espera hacia Odoo |

## 3. Ejecutar la API

```bash
dotnet run --project backend/src/Pos.Api --launch-profile http
```

| Método | Ruta | Estado |
|---|---|---|
| GET | `/api/products?search=&limit=` | Implementado (RF-01, RF-02) |
| GET | `/api/pos/sessions/active` | Implementado |
| POST | `/api/auth/login` | Implementado (semana 2): devuelve un JWT; el resto de endpoints lo exige |
| GET | `/api/discounts` | Implementado (semana 2, RF-06 a RF-08): políticas de descuento autorizadas |
| POST | `/api/sales/preview` | Implementado (semana 2, RF-03 a RF-08): total provisional con precios de Odoo y descuento |
| POST | `/api/sales`; GET `/api/sales`, `/api/sales/{id}` | Semana 3 |

## 4. Pruebas

Unitarias (no requieren Odoo):

```bash
dotnet test backend/tests/Pos.UnitTests
```

Integración contra Odoo real (se omiten si `Odoo__ApiKey` no está definida):

```bash
Odoo__ApiKey="<api-key>" dotnet test backend/tests/Pos.IntegrationTests
```

Prueba temprana de venta con dos productos y efectivo (P09), que guarda las evidencias en `docs/evidencias`:

```bash
ODOO_API_KEY="<api-key>" bash scripts/prueba-temprana-venta.sh
```

Resultado de la semana 1: 21 pruebas unitarias y 4 de integración superadas; venta `Caja Principal - 000001`
registrada en estado `paid` por 7,76 USD y verificada en Odoo.

## 5. Frontend (React, semana 2)

Con la API en ejecución (perfil `http`, puerto 5038):

```bash
cd frontend
npm install
npm run dev      # http://localhost:5173 (origen permitido por CORS)
npm test         # pruebas del dominio, casos de uso y adaptador HTTP
```

Si la API usa otro puerto, copie `frontend/.env.example` a `frontend/.env` y ajuste `VITE_API_URL`.

El frontend sigue Clean Architecture: `src/domain` (carrito), `src/application` (casos de uso con puertos `api` y `session`),
`src/infrastructure` (adaptadores `fetch` y `sessionStorage`) y `src/ui` (componentes React). Las tres primeras carpetas no
importan React ni `fetch`, por lo que se prueban sin navegador (RNF-02).

## Solución de problemas

**`The ApiKey field is required` / `Falta Odoo:ApiKey` al iniciar la API.** Los user-secrets solo se cargan cuando
`ASPNETCORE_ENVIRONMENT=Development`. En Visual Studio seleccione el perfil **http** o **https** junto al botón
de ejecución (no ejecute el `.exe` directamente). Fuera de Development, defina la variable de entorno `Odoo__ApiKey`.
