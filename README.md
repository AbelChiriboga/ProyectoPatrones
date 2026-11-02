# POS integrado con Odoo 20 Community — Semana 3

Rama de entrega: `semana3-ventas`. Implementa ventas, efectivo exacto, conciliación, historial con búsqueda dinámica/paginación en servidor y detalle, conservando catálogo, carrito y descuentos. El frontend obtiene los datos por la API .NET; nunca recibe la API key de Odoo.

```text
React → ASP.NET Core (.NET 10) → Odoo 20 Community (JSON-2) → PostgreSQL
```

Alcance: efectivo por el total verificado, sin tarjetas, transferencias ni vuelto. No incluye inventario integral, clientes, facturación o reportes nuevos. La publicación no ejecuta por sí misma ninguna venta.

## 1. Prerrequisitos y carpeta correcta

- Git; .NET SDK 10; Python 3; Node.js y npm compatibles con el lockfile y Vite del proyecto.
- Docker Desktop con motor iniciado y Docker Compose con soporte para la etiqueta `!override` usada por el preparador.
- PowerShell para los comandos de esta guía. Git Bash no es necesario para el arranque actual.
- Puertos habituales: Odoo 8069, API 5038 y React 5173.

Todos los comandos parten de la raíz **ProyectoPatrones** de la copia de semana 3. En el equipo de desarrollo:

```powershell
Set-Location 'C:\Users\User OGX\Documents\04_Universidad\patrones_software\patrones-software-semana3\ProyectoPatrones'
git rev-parse --show-toplevel
git branch --show-current
```

Otro integrante debe usar la ruta de su clon y comprobar la rama `semana3-ventas`. No usar la copia antigua ubicada fuera de `patrones-software-semana3`.

## 2. Preparación inicial — solo instancia nueva y aislada

Si el entorno ya fue preparado, ir a **Ejecución habitual**. No volver a inicializar Odoo para iniciar la aplicación.

Antes de preparar, ejecutar `docker compose ls` y comprobar que el proyecto fijo `patrones-semana3` no pertenece a otra instalación que deba conservarse. Conservar bases, volúmenes, credenciales y diario existentes; si hay una instalación previa, recuperar su configuración antes de continuar.

En una instancia realmente nueva:

1. Si no existe `infra/.env`, copiar la plantilla. **No sobrescribir un archivo existente**:

   ```powershell
   if (-not (Test-Path -LiteralPath infra/.env)) {
       Copy-Item -LiteralPath infra/.env.example -Destination infra/.env
   }
   ```

2. Editar localmente `infra/.env`: mantener `POSTGRES_USER` y establecer una contraseña propia para `POSTGRES_PASSWORD`, sin conservar el marcador de la plantilla. El preparador lee líneas simples `CLAVE=valor`; usar valores sin comillas. No publicar el archivo ni compartir su contenido en consola/capturas. No cambiar contraseñas de una base ya inicializada para intentar recuperarla.
3. Revisar que no hay una base existente pendiente de recuperación y ejecutar:

   ```powershell
   python -B scripts/prepare-week3.py
   ```

El preparador usa el proyecto Docker `patrones-semana3`, PostgreSQL 16 y Odoo 20. Crea configuración local, inicia la base, espera disponibilidad e inicializa POS sin HTTP. Configura el entorno de desarrollo y genera claves/contraseñas aleatorias sin imprimirlas. Publica Odoo solo en `127.0.0.1:8069` mediante el override local.

Archivos locales ignorados que deben conservarse y protegerse:

| Archivo | Propósito |
|---|---|
| `infra/.env` | Configuración de PostgreSQL para Compose |
| `infra/odoo.env.local` | Conexión de Odoo a PostgreSQL y clave del gestor de bases |
| `infra/compose.env.local` | Override de loopback y montaje de configuración |
| `infra/backend.env.local` | Respaldo de configuración API/operador y contraseña administrativa generada |

Si `backend.env.local` existe, el preparador termina sin inicializar ni rotar claves; eso no comprueba que todos los contenedores/archivos estén completos. Si detecta `pos_db` existente sin ese archivo, rechaza su inicialización: recuperar las credenciales, **no borrar la base ni el archivo para forzar el proceso**. Antes de esa comprobación puede haber escrito configuración local e iniciado PostgreSQL.

Instalar dependencias frontend una vez en el clon preparado, sin servidor Vite activo:

```powershell
npm --prefix frontend ci
```

## 3. Ejecución habitual — conservar datos

### Comprobar servicios antes de iniciarlos

```powershell
docker compose -p patrones-semana3 --env-file infra/.env -f infra/docker-compose.yml -f infra/compose.env.local ps
Get-NetTCPConnection -LocalPort 5038,5173 -State Listen -ErrorAction SilentlyContinue |
    Select-Object LocalAddress,LocalPort,OwningProcess
```

Para un PID ocupado, comprobar su ruta/comando antes de decidir qué hacer (sustituir el número por el observado):

```powershell
Get-CimInstance Win32_Process -Filter 'ProcessId = 12345' |
    Select-Object ProcessId,ExecutablePath,CommandLine
```

Inspeccionar localmente sin compartir líneas que contengan datos sensibles. Si las instancias ya pertenecen a este clon y funcionan, utilizarlas. No ejecutar otro servidor ni detener servicios ajenos. Si un puerto pertenece a otra copia, resolver esa instancia identificada antes de continuar; no matar procesos por nombre indiscriminadamente.

### Odoo y PostgreSQL preparados

Con los cuatro archivos locales conservados y el proyecto correcto identificado:

```powershell
docker compose -p patrones-semana3 --env-file infra/.env -f infra/docker-compose.yml -f infra/compose.env.local up -d
```

Este comando inicia/reutiliza los servicios y volúmenes del proyecto; no ejecuta el inicializador POS. Usar siempre el override preparado, pues el compose base por sí solo publica Odoo en todas las interfaces.

### Backend — terminal 1, puerto 5038 libre

```powershell
python -B scripts/run-week3.py
```

El lanzador ejecuta `dotnet run --no-launch-profile --project backend/src/Pos.Api --urls http://localhost:5038` con la configuración descrita más abajo. El comando `dotnet run` directo no lee `backend.env.local` por sí mismo; no sustituir el lanzador esperando su mismo respaldo local.

### Frontend — terminal 2, puerto 5173 libre

```powershell
npm --prefix frontend run dev -- --host localhost --port 5173 --strictPort
```

`--strictPort` evita que Vite cambie silenciosamente de puerto. No reinstalar dependencias para arrancar cada vez. Usar `localhost`, que es el origen permitido por CORS.

| Servicio | URL |
|---|---|
| POS React | http://localhost:5173 |
| Historial | http://localhost:5173/#historial |
| API | http://localhost:5038 |
| OpenAPI JSON (Development) | http://localhost:5038/openapi/v1.json |
| Odoo local | http://localhost:8069 |

`App.jsx` usa `VITE_API_URL` si está definida; en otro caso usa `http://localhost:5038`. Esta variable es pública, solo contiene la URL. No poner contraseñas ni claves del backend en variables `VITE_*`. Para el arranque habitual no hace falta crear otro archivo `.env` de frontend.

Flujo: login POS → preparar carrito → pasar a Cobro (cotización automática del servidor) → revisar y confirmar explícitamente el efectivo exacto. «Consultar total con impuestos» permite recotizar/reintentar. Ante resultado incierto, conservar el identificador y consultar Operaciones pendientes; no volver a cobrar. Las comprobaciones manuales sin autorización de venta deben detenerse antes de confirmar.

## 4. Configuración efectiva y autenticación

### Precedencia del lanzador actual, por cada clave

Comprobada en `scripts/run-week3.py` (`build_environment`, `read_user_secrets`) y `Program.cs` (`WebApplication.CreateBuilder`). De mayor a menor, al usar **este lanzador**:

| Orden | Fuente | Comportamiento |
|---|---|---|
| 1 | Variables explícitas heredadas por el proceso | No son sustituidas por el archivo local; sobreescriben User Secrets y JSON |
| 2 | .NET User Secrets, solo en Development | El lanzador reconoce claves planas/anidadas y evita inyectar para ellas el respaldo local |
| 3 | `infra/backend.env.local` | Solo claves ausentes en fuentes anteriores; se inyectan como variables de entorno del proceso hijo |
| 4 | `appsettings.{Environment}.json` | Configuración del entorno seleccionado |
| 5 | `appsettings.json` | Valores base; claves sensibles vacías y operadores vacíos en el repositorio |

Las claves se comparan sin distinguir mayúsculas/minúsculas; `__` en variables equivale a `:` en configuración .NET. La precedencia se resuelve **por clave**, no por archivo completo: usuario y contraseña podrían provenir de fuentes distintas si se configuraron parcialmente.

.NET normalmente sitúa los argumentos de línea de comandos sobre las variables de entorno, después User Secrets y los JSON. Aquí el respaldo tiene prioridad sobre JSON porque el lanzador lo convierte en entorno y protege previamente las claves de User Secrets. El lanzador no reenvía argumentos arbitrarios de Python; pasa únicamente la URL indicada. No hay un proveedor personalizado que cargue automáticamente cualquier `appsettings.*.local.json` o `.env` en ASP.NET Core.

El lanzador establece `ASPNETCORE_ENVIRONMENT=Development` solo si falta. **`DOTNET_ENVIRONMENT` prevalece sobre `ASPNETCORE_ENVIRONMENT`** con WebApplication y es el que el lanzador toma primero. Fuera de Development no se consideran User Secrets: se usan variables explícitas, respaldo local disponible y JSON. El respaldo es de desarrollo local, no una recomendación de almacenamiento para producción.

`read_user_secrets` obtiene el ID del archivo `backend/src/Pos.Api/Pos.Api.csproj`. En Windows lo busca bajo `%APPDATA%/Microsoft/UserSecrets/<UserSecretsId>/secrets.json`; sin APPDATA usa `~/.microsoft/usersecrets`. No copiar ese archivo al repositorio. Consultar las fuentes localmente con el responsable, sin volcar `dotnet user-secrets list`, valores de entorno o JSON sensible en informes/logs.

### Credenciales distintas para propósitos distintos

- **Login POS:** `Auth:Operators:<índice>:Username` y `Password`, efectivos según la tabla anterior. `ConfiguredOperatorAuthenticator` no autentica contra cuentas Odoo: compara usuario sin distinguir mayúsculas y contraseña exacta; el caso de uso recorta espacios del usuario, no de la contraseña.
- **Conexión backend → Odoo:** `Odoo:ApiKey`; permanece en servidor. `Odoo:PosConfigId` delimita las ventas/historial al POS configurado.
- **JWT:** `Jwt:Key` firma tokens, requiere al menos 32 caracteres; no es la contraseña de login. Issuer/Audience provienen de la configuración API.
- **Administración de Odoo:** `DevelopmentAdminPassword` generada por el preparador está en el respaldo local y se excluye del entorno API. No es la contraseña del operador POS ni la clave del gestor de bases en `odoo.env.local`.

No asumir que el par del archivo local es el efectivo si existen User Secrets o variables explícitas. No cambiar contraseñas para resolver una discrepancia de precedencia; identificar primero fuente, entorno y proceso correctos. Tras un cambio de configuración expresamente autorizado, reiniciar solo la instancia API correspondiente para volver a cargar sus opciones.

| Clave | Necesidad / valor público base |
|---|---|
| `Odoo:BaseUrl`, `Odoo:Database` | Base: http://localhost:8069 y pos_db |
| `Odoo:ApiKey` | Obligatoria; sin valor confidencial en Git |
| `Odoo:PosConfigId` | ID real del POS; base null, ventas/historial fallan cerradas sin él |
| `Odoo:TimeoutSeconds` | Base 15 |
| `Auth:Operators` | Operadores configurados; base vacía |
| `Jwt:Key` | Obligatoria; base vacía |
| `Sales:JournalPath` | Diario durable; por defecto `.pos-operations` junto al ejecutable |

Conservar el diario entre reinicios. En despliegue definir una ruta absoluta persistente y protegerla; no borrar claims para desbloquear cobros. La garantía idempotente actual requiere un host/diario compartido y reutilizar el UUID; no promete exclusión distribuida.

## 5. Recuperación y diagnóstico

| Síntoma | Comprobación / acción |
|---|---|
| Docker no responde | Iniciar el motor de Docker Desktop y comprobar el contexto. No borrar volúmenes |
| Falta override/configuración local | Recuperar los archivos del entorno correcto. No arrancar solo con el compose base ni ejecutar el inicializador antiguo |
| `!override` no reconocido | Comprobar compatibilidad de Docker Compose antes de preparar; no quitar la restricción de loopback |
| Puerto ocupado o interfaz antigua | Ver PID/ruta con los comandos anteriores; comprobar el clon. Vite debe estar en 5173 y servir este frontend |
| Faltan Odoo:ApiKey / Jwt:Key | Usar el lanzador; comprobar entorno efectivo (incluido DOTNET_ENVIRONMENT), fuentes y permisos de lectura sin imprimir valores |
| 401 `auth.invalid_credentials` | Verificar operador/contraseña efectivos por clave; distinguir User Secrets de respaldo y de cuenta administrativa Odoo. No rotar secretos a ciegas |
| JWT expirado | Volver a iniciar sesión; no desactivar validación |
| API no conecta a Odoo | Revisar servicios, URL/base y validez/permisos de la API key con el responsable; no recrear base ni generar claves por ensayo |
| Sesión POS cerrada | Coordinar su apertura por el procedimiento operativo del grupo; no abrir/cambiar sesiones como diagnóstico automatizado |
| Historial/cotización fallan | Revisar conexión, ámbito POS y mensaje; usar reintento/recotización. No sustituir datos reales por simulaciones |
| DLL bloqueadas al compilar | Mantener API activa y usar salida temporal de compilación, como en la sección de pruebas |
| Resultado de venta incierto | Conservar UUID/diario y consultar la operación; no reenviar ni crear otra intención para resolverlo |

Revisar archivos/logs locales sin publicarlos: pueden contener información sensible. Si el preparador falla, puede dejar `infra/prepare-error.env.local`; conservarlo localmente para diagnóstico, sin copiarlo íntegro al chat ni a Git. No borrar archivos de credenciales para forzar una nueva preparación.

## 6. Pruebas y contratos actuales

Comandos sin escritura comercial, con dependencias ya disponibles:

```powershell
npm --prefix frontend test
npm --prefix frontend run build
python -B scripts/test_run_week3.py
$week3Artifacts = Join-Path ([System.IO.Path]::GetTempPath()) ('pos-week3-' + [guid]::NewGuid().ToString('N'))
dotnet build backend/PosOdoo.slnx --artifacts-path $week3Artifacts
dotnet test backend/tests/Pos.UnitTests --artifacts-path $week3Artifacts --no-build --no-restore
```

Las integraciones de `backend/tests/Pos.IntegrationTests` son de lectura. Para ejecutarlas con los mismos artefactos: `dotnet test backend/tests/Pos.IntegrationTests --artifacts-path $week3Artifacts --no-build --no-restore`. Requieren variables `Odoo__ApiKey`, `Odoo__BaseUrl`, `Odoo__Database` y configuración apropiada del entorno aislado, proporcionadas de forma privada. No cargan User Secrets automáticamente ni usan el entorno construido por run-week3.py; sin API key se omiten. No contabilizar una omisión como integración aprobada.

`scripts/verify-week3.py` es una herramienta de verificación controlada, no el lanzador habitual: ejecuta pruebas, inicia otra API en 5049 y escribe un log local. Su entorno prioriza el respaldo local al actualizar las variables directamente; **no tiene la misma precedencia que run-week3.py**. No ejecutarlo junto a instancias/compilaciones incompatibles. No usar `--write` sin autorización explícita: puede enviar POST de venta y modificar evidencia. Para leer P09 existente basta consultar los documentos y endpoints GET autorizados.

Resultados **de la auditoría AUD-03-FINAL del 2026-11-02**, no nuevas ejecuciones atribuibles a esta guía: 138 React, 89 unitarias .NET, 5 integraciones reales de lectura y 6 del lanzador = **238 aprobadas**, builds correctos. La reevaluación documental y su alcance están al final de [la auditoría](docs/sdd/semana3/AUDITORIA_CIERRE_SEMANA3.md). La validación visual corresponde al usuario; el agente no usa navegador.

Todos los endpoints siguientes exigen JWT salvo login:

| Método y ruta | Propósito |
|---|---|
| POST `/api/auth/login` | Token del operador |
| GET `/api/products?search=&limit=` | Catálogo real |
| GET `/api/discounts` | Políticas autorizadas |
| GET `/api/pos/sessions/active` | Sesión activa |
| POST `/api/sales/preview` | Total provisional sin impuestos |
| POST `/api/sales/quote` | Cotización real para revisión, sin registrar venta |
| POST `/api/sales` | Registro de venta y efectivo; requiere autorización operativa |
| GET `/api/sales/operations/{uuid}` | Consulta/conciliación de la operación del operador |
| GET `/api/sales/history?search=&beforeId=0&limit=20` | Resumen paginado, búsqueda por referencia de orden/POS |
| GET `/api/sales?beforeId=0&limit=20` | Contrato histórico compatible, array de detalles |
| GET `/api/sales/{id}` | Detalle real de una orden |

## 7. Operaciones con efectos e historial

La preparación inicial crea/configura datos POS y credenciales; no es una operación de recuperación habitual. El borrado de volúmenes/bases, reinicialización, cambio de contraseñas o eliminación del diario puede destruir datos o garantías y requiere una intervención específica autorizada. Esta guía no prescribe esos pasos.

Los scripts `scripts/odoo-init.sh` y `scripts/prueba-temprana-venta.sh` son antecedentes de semanas previas, **fuera del arranque actual**: inicializan o escriben ventas y pueden mostrar claves. Se conservan como evidencia, no como comandos que ejecutar en esta instalación. No usar defaults históricos de administrador como credenciales vigentes.

Evidencia real histórica: `docs/evidencias/semana3-p09.json` (dos productos, efectivo y un registro por UUID); no repetir P09 creando órdenes. Alcance técnico y decisiones finales: [especificación SDD](docs/sdd/semana3/especificacion.md); seguimiento: [PROGRESS](docs/sdd/semana3/PROGRESS.md). Backend mantiene Domain/Application/Infrastructure/Api; frontend mantiene domain/application/infrastructure/ui.

Pendientes de semana 4: demostración P10, fichas finales de decisiones, defensa individual y prueba condicionada de stock cero. No prometer bloqueo por stock si Odoo no lo garantiza. La observación UX sobre el botón de recotización es no bloqueante y no cambia el arranque ni las reglas de cobro.
