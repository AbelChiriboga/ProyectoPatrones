# POS integrado con Odoo 20 Community — Proyecto integrador de Patrones de Software

Estado: **Semana 1 completada** (requisitos, investigación inicial, diseño arquitectónico, instalación de Odoo,
primera consulta integrada y prueba temprana de registro de venta). El frontend se desarrolla a partir de la semana 2.

```
Frontend (semana 2)  →  API REST ASP.NET Core (.NET 10)  →  Odoo 20 Community (JSON-2)  →  PostgreSQL
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
| POST | `/api/auth/login`, `/api/sales`; GET `/api/sales`, `/api/sales/{id}` | Semanas 2–3 |

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

## Solución de problemas

**`The ApiKey field is required` / `Falta Odoo:ApiKey` al iniciar la API.** Los user-secrets solo se cargan cuando
`ASPNETCORE_ENVIRONMENT=Development`. En Visual Studio seleccione el perfil **http** o **https** junto al botón
de ejecución (no ejecute el `.exe` directamente). Fuera de Development, defina la variable de entorno `Odoo__ApiKey`.
