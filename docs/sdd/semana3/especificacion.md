# Semana 3: venta, efectivo e historial — SDD-03.0 / SDD-03.1

Preparación e implementación del 2026-11-01 (America/Guayaquil). Secciones 1–9: baseline histórico y propuesta SDD-03.0. La sección 10 registra el contrato final SDD-03.1 y prevalece sobre las propuestas y pendientes iniciales.

## 1. Fuentes y baseline

Repositorio clonado: https://github.com/AbelChiriboga/ProyectoPatrones.git. Rama local `semana3-ventas`, base `origin/main` = `9c84c3e3230d58ff74cbf241a5d8d94a187ddca7`. No había repositorio ni cambios locales que preservar en la carpeta nueva `ProyectoPatrones`.

Fuentes académicas disponibles en `../docs/`: `Proyecto_Integrador_Patrones_Software_POS_Odoo20_unlocked.pdf` (pp. 2–8: RF, RN, P01–P10, contratos y rúbrica); `Indicaciones para el Proyecto Final (Patrones de Software)_unlocked.pdf`; `Validacion_Tecnica_Odoo20_JSON2_POS_Patrones_Especifico_unlocked.pdf` (referencia histórica con ajustes manuales, no receta de cobro). Ante la indicación de interfaz mínima del segundo documento, prevalece el frontend completo del proyecto oficial. No se requiere repetir la auditoría docente ni implementar contabilidad, inventario completo, facturación o banca. Semana 4 conserva refactorización y defensa pendientes.

| Evidencia inspeccionada | Implementado / límite verificable |
|---|---|
| `backend/PosOdoo.slnx`, `backend/src/Pos.Api/Program.cs` | .NET 10, cuatro capas, JWT obligatorio salvo login, CORS y ProblemDetails. No hay acceso directo a PostgreSQL en el flujo inspeccionado. |
| `backend/src/Pos.Api/Controllers/SalesController.cs` | Solo `POST /api/sales/preview`; registro, historial y detalle están pendientes. |
| `backend/src/Pos.Application/Sales/PreviewSaleUseCase.cs` | `SaleLineRequest(ProductId, Quantity)`, precios del catálogo, hasta 100 líneas y políticas autorizadas; permite previsualización vacía. Total provisional sin impuestos; no es total cobrable. |
| `backend/src/Pos.Domain/Sales/{Sale,SaleLine,SaleStatus}.cs` | Cantidades positivas, combinación de líneas, rechazo de confirmación vacía y edición posterior. `Confirmed` actual es estado del dominio, no evidencia de registro ni pago en Odoo. |
| `backend/src/Pos.Domain/Discounts/`, `backend/src/Pos.Application/DependencyInjection.cs` | Strategy y registro: `none`, `promo-10`, `bulk-15`. Su representación fiscal en una venta Odoo no está resuelta. |
| `backend/src/Pos.Infrastructure/Odoo/` | `OdooJson2Client` con bearer, base, Content-Length y traducción de fallos; `OdooProductCatalog` filtra vendibles/POS; `OdooPosSessionGateway` obtiene sesión abierta y IDs de métodos. No existe gateway de registro de ventas. |
| `frontend/src/{domain/cart.js,application/useCases.js,infrastructure/httpPosApi.js,ui/}` | Login, catálogo, carrito y preview. Casos de uso independientes de React/HTTP; sin confirmación, pago, historial ni detalle. |
| `docs/evidencias/20261025-145140-*` | Histórico: orden 1, dos líneas, 7,76 USD, `paid`, pago efectivo; repetición HTTP 422 por diferencia del importe pagado y conteo UUID = 1. No prueba idempotencia concurrente, recuperación ni ejecución local actual. |
| `scripts/prueba-temprana-venta.sh` | Escritura real con datos fijos de ejemplo y repetición; NO ejecutado. El README documenta semana 2 y resultados anteriores; no sustituye los resultados actuales. |

## 2. Diagnóstico reproducible

| Herramienta | Resultado de esta ejecución |
|---|---|
| Git | 2.53.0.windows.1; clonación y creación de rama funcionales. Primer intento restringido falló; clonación autorizada fuera del sandbox correcta. |
| .NET | SDK 10.0.402 (también 9.0.319); runtime 10.0.13. `dotnet --info` encontró acceso restringido al gestor de servicios, pero versión, restauración, compilación y pruebas funcionaron fuera del sandbox. |
| Node / npm | v24.13.0 / 11.6.2; `npm ci` instalado dentro de `frontend` desde lockfile; auditoría: 0 vulnerabilidades reportadas. |
| Docker | CLI 29.9.2, Compose v5.6.1, Engine 29.9.2 funcional fuera del sandbox. No confundir denegación del pipe en sandbox con Engine ausente. |
| Odoo | No instalado ni operativo en esta preparación: sin contenedor/imagen Odoo, puerto 8069 sin conexión. `docker manifest inspect odoo:20.0` correcto: imagen publicada, no descargada ni ejecutada. Otro contenedor Mongo ajeno permanece intacto. |

Comandos ejecutados desde la raíz del repo (npm desde `frontend`):

| Comando | Resultado actual |
|---|---|
| `dotnet build backend/PosOdoo.slnx` | Correcto: 0 errores, 0 advertencias. |
| `dotnet test backend/tests/Pos.UnitTests --no-restore` | 62 aprobadas, 0 fallidas, 0 omitidas. |
| `dotnet test backend/tests/Pos.IntegrationTests --no-restore` | 0 aprobadas, 0 fallidas, 4 omitidas: falta `Odoo__ApiKey`. Suite invocada; integración real NO ejecutada. |
| `npm ci`; `npm test`; `npm run build` | Instalación correcta; 29 aprobadas en 3 archivos; build correcto. |
| `docker compose --env-file infra/.env -f infra/docker-compose.yml config --quiet` | Configuración válida. `.env` generado con contraseña aleatoria, confirmado ignorado mediante `git check-ignore infra/.env`; secreto no publicado. |

Preparación condicionada: `infra/docker-compose.yml` usa PostgreSQL 16, Odoo 20, volúmenes persistentes y puerto 8069 en todas las interfaces; no tiene healthcheck. `scripts/odoo-init.sh` no espera readiness, inicializa mientras el servicio está activo y no sustituye admin/admin. `infra/odoo/setup_pos.py` cambia empresa, crea productos/configuración/sesión y genera una clave nueva en cada ejecución; no es totalmente idempotente ni busca exclusivamente el efectivo de la misma compañía. Busca IVA 15 % pero no lo crea si falta. No se ejecuta este inicializador sin resolver esas condiciones; no se alteró seguridad existente para sortearlas.

Secuencia pendiente antes de escrituras empresariales: preparar override local ignorado que limite puerto a loopback y use proyecto Compose aislado; descargar imágenes y registrar digest/versión; iniciar DB y esperar disponibilidad; inicializar Odoo en proceso único sin HTTP; establecer contraseñas aleatorias de administrador y administrador de bases antes de exponer HTTP; adaptar localmente la preparación para no imprimir claves, validar impuesto/compañía/método y no regenerar API keys existentes. Guardar secretos fuera de Git, sin consola ni capturas. Después iniciar HTTP y verificar `/web/login`, `/doc`, catálogo y sesión con usuario POS mínimo. No modificar volúmenes ajenos ni ejecutar `down -v`. Esta secuencia NO se presenta como ejecutada.

## 3. Trazabilidad y alcance funcional

| Requisito oficial | Comportamiento exigido en SDD-03.1 | Evidencia prevista |
|---|---|---|
| RF-09 confirmar líneas/precios/descuentos | Backend revalida carrito completo antes del envío | Unitarias Application + P09 |
| RF-10 seleccionar pago configurado | Consultar métodos de la sesión; habilitar efectivo real | UI y consulta real |
| RF-11 registrar venta y pago | Gateway validado en instalación concreta; operación coherente | Orden, líneas y pagos leídos de Odoo |
| RF-12 identificador/estado/resultado real | Mostrar ID Odoo, estado original y confirmación o incertidumbre | UI + respuestas REST |
| RF-13 historial; RF-14 detalle | Lecturas paginadas y detalle desde Odoo con ámbito autorizado | Pruebas GET y UI |
| RF-15 datos válidos | Rechazar entradas inconsistentes antes de escribir | P01–P04 + descuentos/pago |
| RF-16 evitar duplicados | Persistencia de operación, exclusión concurrente y conciliación | P06 + reinicio/concurrencia |
| RF-17 comunicación | Timeout/error jamás significa pago exitoso | P05/P08 + integración negativa |

RN-01: al menos una línea; RN-02: cantidades enteras positivas (tipo actual), límites y suma sin overflow; RN-03: precios de Odoo verificados en backend; RN-04: solo políticas habilitadas; RN-05: sesión, productos, moneda, impuestos, descuentos y efectivo coherentes; RN-06: operación confirmada no editable; RN-07: pago solo exitoso tras verificar Odoo; RN-08: misma operación no genera otra venta. Las reglas actuales cubren partes de RN-01–06; RN-07–08 quedan por implementar en integración.

## 4. Contratos REST propuestos, aún no implementados

Todas las rutas requieren JWT de operador. Identidad/ámbito proceden del token y configuración de servidor, nunca del cuerpo. Mantener `preview` compatible. DTOs nuevos son orientativos: confirmar firma Odoo y reglas fiscales antes de fijarlos.

| Método/ruta | Entrada | Salida |
|---|---|---|
| `GET /api/pos/sessions/active` existente | Sin cambios incompatibles | DTO existente: `id,name,state,configId,currencyCode,paymentMethodIds`. |
| `GET /api/pos/payment-methods?sessionId={id}` nuevo | Sesión autorizada | `[{id,name,type}]` de métodos reales compatibles; efectivo seleccionado explícitamente. No etiquetar IDs como efectivo sin consultar tipo. |
| `POST /api/sales` | `{operationId:uuid,sessionId:int,lines:[{productId:int,quantity:int}],discountCode:string?,payment:{methodId:int,tenderedAmount:decimal},expectedTotal:decimal,currencyCode:string}` | Primera confirmación verificada: 201 + `Location: /api/sales/{odooId}`; repetición conciliada: 200 mismo resultado; incertidumbre: 202 + `Location: /api/sales/operations/{operationId}`. |
| `GET /api/sales/operations/{operationId}` nuevo | Operación autorizada | 200 `{operationId,status,odooId?,odooState?,message}`; concilia mediante solo lectura; 404 si no pertenece al ámbito. |
| `GET /api/sales?from=&to=&limit=20&cursor=` | Fechas ISO-8601 UTC opcionales; límite 1–100; cursor opaco validado | 200 `{items:[{id,reference,dateUtc,state,currencyCode,total}],nextCursor?}`; orden estable `date_order desc,id desc`, compañía/POS autorizado, fechas inclusiva/exclusiva. |
| `GET /api/sales/{id}` | ID Odoo positivo y autorizado | 200 detalle; 404 si ausente/fuera de ámbito. |

Resultado confirmado: `{operationId,status:"confirmed",id,reference,odooState,currencyCode,subtotal,discount,tax,total,paid,change,lines:[{productId,name,quantity,unitPrice,discount,subtotal,tax,total}],payments:[{methodId,name,amount}],dateUtc}`. Leer importes finales de Odoo, con proyección de campos permitidos, sin tokens ni trazas internas. La fecha de Odoo se normaliza explícitamente a UTC.

`expectedTotal` expresa consentimiento al total calculado por servidor, nunca autoridad de precio. Si el preview actual no puede mostrar total fiscal verificable, incorporar cotización de checkout compatible antes de cobrar; no reutilizar su total sin IVA. Primera entrega de efectivo: importe exacto, rechazar insuficiente o sobrepago con mensaje claro; `change=0`. Soportar vuelto solo tras validarlo expresamente, sin presentarlo como requisito ya resuelto. No hay endpoint separado de pago: venta y efectivo se solicitan juntos, evitando cobro doble por dos acciones.

Errores consistentes con ProblemDetails existente `{status,title,detail,code}` más `operationId` y `operationStatus` cuando corresponda: 400 forma/límites; 401/403 autenticación/permisos; 404 recurso; 409 clave reutilizada con otro payload/sesión o cotización cambiada; 422 regla de negocio. Preservar 502/503/504 para fallos de consulta o fallos inequívocamente previos al envío; tras un envío posiblemente recibido, persistir incertidumbre y devolver 202 si es posible. Si se corta la conexión .NET, frontend conserva UUID y consulta la operación al reconectar. No reenviar automáticamente un POST.

## 5. Estados e idempotencia

`Draft → confirmado en Odoo | resultado incierto | rechazo`. Estado técnico durable `Sending` previo a HTTP: cualquier reinicio en ese estado se recupera como incierto. `Sale.Confirm()` por sí solo no realiza la transición externa. Confirmado requiere una orden única con UUID esperado, estado pagado permitido por contrato validado (`paid`; estudiar `done`), líneas y efectivo vinculados y montos/moneda coherentes; HTTP 200 o una lista de IDs no basta. Un rechazo definitivo exige demostrar que no hubo escritura; error posterior al envío o lectura inconclusa conserva incertidumbre.

Identidad: frontend genera UUID una vez y lo conserva hasta resolver; servidor persiste antes del envío `{operationId,scope,operator,normalizedPayloadHash,odooUuid,lineUuids,paymentUuid,status,odooId?,timestamps,result}`. Índice único por operación/ámbito y adquisición atómica; mismo UUID con contenido distinto → 409. Mantener diario técnico durable de operaciones (tentativamente SQLite local del backend, fuera de Git), sin replicar libros de ventas de Odoo. No basta un diccionario en memoria ni un bloqueo por proceso.

Ante repetición: recuperar registro → devolver confirmación guardada o conciliar consultando UUID y ámbito en Odoo; jamás emitir otro `sync_from_ui` a ciegas. Consultas sin resultado mientras puede continuar la petición original NO prueban ausencia. Sin garantía de finalización/rollback, mantener incierto y escalar conciliación manual; no generar UUID nuevo ni permitir nueva confirmación del mismo carrito. Caída entre commit Odoo y guardado local se resuelve por lectura. Cancelación del navegador tampoco prueba rollback. Retención del diario debe impedir reutilización accidental tras reinicios/limpieza; definir política antes de producción.

## 6. Integración y cálculos: hipótesis que deben validarse

Se consultó el [código oficial candidato de Odoo 20](https://github.com/odoo/odoo/blob/20.0/addons/point_of_sale/models/pos_order.py): firma `sync_from_ui(self, orders)`; localiza órdenes por UUID y devuelve `read_pos_data`, con colecciones por modelo. Puede procesar pagos de órdenes pagadas; `_update_lines` contempla UUID de líneas. Esto explica por qué el UUID de orden aislado no garantiza reenvío seguro. Es una referencia remota mutable, NO verificación de versión instalada; registrar digest y firma del contenedor antes de implementarla. No se demostró unicidad concurrente del UUID en la instalación. La página de documentación externa 20.0 no pudo recuperarse por timeout; no se inventa su contenido.

En la instalación: inspeccionar `/doc` y código efectivo sin escribir; confirmar `orders`, campos requeridos, forma exacta de respuesta, ACL/reglas de compañía para usuario POS, lectura de impuestos/pagos y comportamiento transaccional. Candidato JSON-2: `POST /json/2/pos.order/sync_from_ui` con `{orders:[...]}`, bearer y `X-Odoo-Database` en backend. Después leer `pos.order`, `pos.order.line` y `pos.payment` por UUID/relaciones; conservar evidencia saneada. No usar acceso SQL ni secuencia de `create/write` sobre importes o `amount_paid` para simular cobro.

Application define tentativamente `ISaleGateway` (enviar, buscar por UUID, historial, detalle), `IPaymentMethodCatalog` e `ISaleOperationStore`; Infrastructure aporta `OdooSaleGateway` y diario durable; Domain concentra invariantes. JSON de Odoo y comandos relacionales no cruzan al frontend ni a políticas internas.

Precios/impuestos: reutilizar catálogo real, pero `lst_price` y IDs fiscales actuales no resuelven por sí solos lista de precios, impuestos incluidos/excluidos, posición fiscal ni redondeo de moneda/compañía. Consultar configuración efectiva y validar cálculo acordado con Odoo. Aplicar descuentos autorizados de forma trazable por línea con distribución y redondeo consistente; si una configuración no está soportada, rechazar explícitamente antes de enviar. No hardcodear IVA, moneda, sesión, método ni productos de las evidencias. Revalidar sesión aún abierta y productos activos en confirmación; impedir reasignación silenciosa a otra sesión. Contrastar total esperado, total fiscal y pago leído con precisión de moneda; diferencias no producen éxito automático.

## 7. Frontend y pruebas de aceptación

Extender puertos/casos de uso en `application/useCases.js` para cotizar checkout, seleccionar efectivo, confirmar, consultar operación, historial y detalle; `infrastructure/httpPosApi.js` gestiona REST/JWT y almacenamiento de identidad pendiente mediante puerto. UI conserva carrito ante error, bloquea doble clic mientras envía, muestra resultado incierto con acción de consultar, y limpia carrito solo tras confirmación verificada. Historial/detalle son navegables, con carga/vacío/error; mensajes accesibles con región de estado, foco y botones utilizables por teclado. React no calcula precios autoritativos ni contiene claves de Odoo.

| Prueba oficial | Evidencia actual y trabajo del bloque funcional |
|---|---|
| P01 venta vacía | `backend/tests/Pos.UnitTests/Domain/SaleTests.cs` rechaza `Confirm`; preview vacío permitido. Añadir rechazo de registro sin invocar gateway. |
| P02 cantidad inválida | `SaleTests`, `PreviewSaleUseCaseTests`; extender registro con cero, negativo, fracción, overflow y límites. |
| P03 sesión inválida/cerrada | `Application/GetActiveSessionUseCaseTests.cs`; añadir validación del checkout, cierre entre lectura/envío y ninguna escritura en caso inválido. |
| P04 producto deshabilitado | `PreviewSaleUseCaseTests` producto no encontrado y `Infrastructure/OdooAdaptersTests.cs` filtros; añadir registro que no envía producto retirado. |
| P05 fallo/timeout | Tests existentes del cliente HTTP; añadir timeout antes/después del envío, JSON inválido, HTTP 200 sin pago y cancelación: ningún falso éxito. |
| P06 repetición | Pendiente: misma operación secuencial/concurrente, otro contenido, reinicio, commit Odoo sin respuesta y lectura vacía transitoria; máximo una venta y un pago. |
| P07 carrito | `frontend/src/domain/cart.test.js`, `application/useCases.test.js`; conservar regresiones y añadir confirmación/cotización. |
| P08 mensajes | `frontend/src/infrastructure/httpPosApi.test.js`; añadir 202, incertidumbre, sesión JWT expirada, accesibilidad y conservación del carrito. |
| P09 real | Solo histórico `20261025-145140-*`; NO ejecutado ahora. Futuro: con autorización explícita y UUID trazable nuevo, dos productos/efectivo, verificar orden/líneas/pago y localizar en historial/detalle. Reenvío de escritura requiere autorización y entorno desechable. |
| P10 extensibilidad | `backend/tests/Pos.UnitTests/Domain/DiscountPolicyTests.cs` y prueba de política inyectada en preview; demostrar también gateway intercambiable sin tocar dominio/UI. |

Añadir negativos de descuento no autorizado, método ajeno a sesión, efectivo insuficiente, total cambiado, compañía ajena, permisos Odoo, historial paginado estable y detalle inexistente. Unitarias usan dobles de puertos, sin presentarlos como integración. Pruebas reales de lectura se ejecutan después de configurar Odoo; pruebas reales de escritura tienen autorización y evidencia propias. No aumentar el conteo de integración con suites omitidas.

## 8. Decisiones para la rúbrica

Al menos tres decisiones justificadas para el proyecto completo, sin exigir tres patrones GoF. Elecciones nuevas tentativas, sujetas a validación:

| Problema | Alternativas y elección | Compromiso / clases / prueba |
|---|---|---|
| Políticas variables de descuento | Condicional central vs Strategy existente; conservar Strategy (GoF) por variación ya presente. | Registro y distribución fiscal requieren coordinación. `IDiscountPolicy`, `DiscountPolicyRegistry`, preview/checkout; política adicional y descuento inválido (P10). |
| Contrato externo cambiante | JSON en controlador vs adaptador sobre puerto Application; extender Adapter/capa anticorrupción (estructural/integración). | Más mapeo, evita acoplar casos de uso a Odoo. `ISaleGateway`, `OdooSaleGateway`, `OdooJson2Client`; tests de mapeo y lectura real. |
| Repetición y caída de proceso | Solo UUID Odoo, caché en memoria o diario durable; elegir diario + adquisición atómica + conciliación (patrón de integración, no GoF). | Persistencia técnica y recuperación adicionales. `ISaleOperationStore`, caso `RegisterSaleUseCase`, SQLite tentativo; concurrencia y reinicio P06. |
| Pocos estados de operación | State GoF vs enum + transiciones explícitas; elegir solución simple, revisable si crece el flujo. | Disciplina en transiciones; no inventar jerarquía. `SaleStatus`, resultado Application/operación; tabla de transiciones y P05/P06. |

## 9. Criterios y bloque único SDD-03.1

Aceptación observable: (1) regresiones actuales siguen aprobadas y dependencias apuntan hacia Domain/Application en ambas apps; (2) operador selecciona efectivo real y confirma dos productos; (3) ID, estado, líneas, impuestos y pago coinciden con consultas Odoo; (4) historial y detalle localizan la misma venta con ámbito correcto; (5) RN-01–08 y P01–P10 tienen evidencia pertinente; (6) timeout/reinicio/reenvío nunca muestran éxito falso ni crean cobro adicional; (7) secretos fuera del navegador/Git y errores sin claves; (8) decisiones justificadas y reproducción documentadas. Los puntos reales siguen pendientes, no se declaran satisfechos por esta preparación.

Un solo bloque grande, autorizado aparte: resolver arranque seguro de Odoo y validar contrato/ACL/fiscalidad mediante lecturas → implementar diario, puertos, casos de uso y adaptador con conciliación → exponer REST/DTO/ProblemDetails → completar checkout/historial/detalle frontend → ejecutar unitarias/regresiones y lecturas reales → solicitar únicamente autorización aún ausente para P09/escrituras trazables → validar pago/duplicados reales y documentar resultados. Si la interfaz no soporta flujo verificable, conservar diagnóstico y consultar mecanismo uniforme del docente; no sustituir POS por ventas o simulación. No commit/push/PR ni cambios a main.

Riesgos y dependencias reales: Odoo local y API key aún ausentes; inicializador sin endurecimiento/readiness; firma/ACL instaladas sin verificar; fiscalidad y distribución de descuentos por resolver; reenvío histórico fallido; durabilidad/concurrencia por implementar. Las compilaciones y unitarias actuales están correctas. Esta fase no autoriza instalar software global, alterar seguridad existente, borrar bases/volúmenes ni registrar ventas.


## 10. Cierre SDD-03.1: contrato implementado y evidencia actual

### Entorno y verificación

Odoo 20.0-20261015 inicializado en proyecto Docker **patrones-semana3**, base **pos_db**, host `127.0.0.1:8069`, sesión/configuración reales 1/1. Imagen `odoo:20.0`, digest `sha256:d5e3a26baa044867c826dddc09058f10e2582706bf83c146ea04fea889720c76`. Código instalado inspeccionado: `sync_from_ui(self, orders)` y respuesta `read_pos_data`; usuario de integración POS pudo consultar configuración, compañía, impuestos, catálogo, sesión, orden, líneas y pagos por JSON-2. Administrador y gestor de bases con contraseñas aleatorias antes de exposición HTTP; host ligado solo a loopback. Mongo ajeno intacto. Configuración y credenciales están ignoradas por Git.

`python scripts/prepare-week3.py` reemplaza el arranque inseguro para esta entrega: DB aislada y readiness, inicialización sin HTTP, setup existente reutilizado, contraseñas aleatorias y clave capturada sin imprimir. Reutiliza credenciales existentes sin rotarlas; rechaza reinicializar una base existente si falta su archivo de credenciales. Para levantar de nuevo los servicios usar el override local, no el compose base aislado (ver README).

Validación final: `dotnet build backend/PosOdoo.slnx --no-restore`: 0 errores/0 advertencias; `dotnet test backend/PosOdoo.slnx --no-restore` con entorno local cargado por `scripts/verify-week3.py`: **76 unitarias + 4 integraciones reales aprobadas, 0 fallidas/omitidas**. Frontend: `npm test`: **33 aprobadas en 4 archivos**; `npm run build`: correcto. Baseline anterior 62/29 preservado. No se realizó prueba visual ni automatización de navegador: se verificaron casos de uso/adaptador, build y flujo API real.

P09 reproducible: `python scripts/verify-week3.py --write`. Ya existe evidencia, por lo que una nueva ejecución concilia la misma operación y repite solo la lógica idempotente de la API, sin enviar `sync_from_ui` otra vez. `docs/evidencias/semana3-p09.json`: operación `0f21ba7c-8be1-4ed2-a053-4cf4132e4064`, orden **2**, `Caja Principal - 000001`, estado **paid**, **2,76 USD**, impuesto **0,36**, pago efectivo **2,76**, dos productos reales. Incluye lectura directa de orden/líneas/pago, detalle e historial API y conteo UUID **1** después de repetir POST. El script no crea otra intención mientras exista esa evidencia.

Primer intento conservado en `docs/evidencias/semana3-p09-rejected.json`: UUID distinto, respuesta 422 «Order / is not fully paid», conteo posterior 0. Causa: compañía `round_globally` frente al cálculo inicial por línea (2,77). Se inspeccionó lógica instalada, confirmó rechazo sin persistencia y corrigió redondeo antes de una nueva intención trazable; nunca se reenviaron sus comandos ni se alteraron importes por `write`. El ID 2 refleja consumo de secuencia durante rollback; existe una sola venta pagada. El diario del primer UUID permanece incierto y retenido, sin reenvío automático.

### Contrato final (todos exigen JWT)

- `POST /api/sales/preview`: compatible con semana 2, sin cambios.
- `POST /api/sales/quote`: `{sessionId,lines:[{productId,quantity}],discountCode}` → `{sessionId,currency,total,lines:[{productId,quantity,unitPrice,discountPercent,net,tax,taxIds}],methods:[{id,name}]}`. Métodos son efectivo real de la sesión; no se necesita endpoint adicional de métodos.
- `POST /api/sales`: `{operationId:UUID,sale:{sessionId,lines,discountCode},paymentMethodId,expectedTotal}`. Backend recalcula antes del envío. Efectivo exacto igual al total; no hay campo de precio autoritativo, cobro separado ni vuelto. Respuesta 200 tanto inicial como repetida si se verificó; 202 con Location de operación cuando es incierta. Elegimos 200 uniforme para no exponer estado interno del diario como distinción 201/200.
- `GET /api/sales/operations/{uuid}`: solo dueño de la operación; `{operationId,status:"confirmed"|"uncertain",sale?}`. Conciliación de solo lectura. Ausente/otro operador → 404.
- `GET /api/sales?beforeId=0&limit=20`: array de detalles reales, orden `id desc`, límite 1–100; siguiente página con último ID. No implementa filtros de fechas ni cursor opaco de la propuesta inicial; RF-13 no los exige.
- `GET /api/sales/{id}`: detalle real `{id,uuid,reference,state,currency,total,tax,paid,sessionId,lines,methods}`. ID positivo y POS configurado; 404 fuera del ámbito. Cantidades fraccionarias/devoluciones no soportadas no se convierten silenciosamente a enteros.
- Sin `Odoo:PosConfigId` explícito, ventas/historial fallan cerradas. Todos los operadores autenticados configurados en esta instalación están autorizados al POS único del servidor; las consultas filtran `config_id`, además de las ACL del usuario POS. No se ofrece administración multi-POS ni permisos distintos por cajero.
- ProblemDetails existente se conserva; 409 `sale.conflict` ante identidad reutilizada y 409 `sale.total_changed` ante cambio de total previo al envío. Este último permite recotizar sin dejar una operación fantasma en React.

### Implementación y decisiones reales

`SaleWorkflow` reutiliza preview, catálogo, sesión y Strategy; valida RN-01–05 antes de escribir. `OdooSaleGateway` es Adapter sobre puerto `ISaleGateway`: JSON queda en Infrastructure. Lee la orden y sus líneas/pagos después de enviar; confirma solo UUID, sesión, estado pagado, total esperado, importe pagado y método coherentes, y productos/cantidades coincidentes. `Sale.Confirm()` no se usa como evidencia de pago externo. Estado externo confirmado se consulta, no se edita como borrador (RN-06/07).

Diario elegido: archivos por UUID con creación exclusiva (`CreateNew`), escritura y `Flush(true)` antes del envío, resultado temporal y publicación atómica sin sobrescribir el primer resultado. Alternativa SQLite descartada para no agregar paquetes a un diario mínimo de un solo host. Guarda dueño, huella SHA-256 de payload canónico (líneas ordenadas, descuento normalizado), solicitud y respuesta confirmada para recuperación. Ruta configurable `Sales:JournalPath`, defecto `.pos-operations` dentro del directorio ejecutable, ignorada por Git; fijar ruta absoluta estable y respaldarla al desplegar. No es contabilidad: Odoo sigue siendo fuente empresarial, el resultado es una caché idempotente.

Una colisión o archivo incompleto falla cerrado; timeout/cancelación/error después de envío conserva intención y devuelve incertidumbre. Repetición y reinicio nunca vuelven a invocar escritura, incluso si lectura Odoo aún está vacía. Concurrencia entre procesos se excluye mediante creación exclusiva en el mismo filesystem; no promete exclusión entre hosts/discos distintos. No eliminar claims para «resolver» un error. La garantía exige conservar el diario y reutilizar el UUID; no detecta dos intenciones nuevas que el operador cree con IDs diferentes.

Descuentos: se conserva Strategy existente y se distribuye su importe exacto por diferencia de acumulados redondeados; porcentaje por línea enviado al contrato POS. Impuestos de Odoo: como máximo un porcentaje excluido por producto, moneda de centavos, precios con hasta dos decimales, sin lista de precios ni posiciones fiscales ni redondeo de efectivo. Se lee modo de redondeo de compañía y se distribuye impuesto por acumulado para `round_globally`; `round_per_line` se calcula individualmente. Odoo recalcula y luego se contrasta el resultado; configuración no soportada se rechaza explícitamente. P09 sin descuento; cobro con descuentos no se ejecutó como venta real adicional.

React incorpora cotización fiscal, efectivo, confirmación, conciliación tras recarga, historial paginado y detalle. El puerto `pending` conserva UUID por operador en localStorage; el caso de uso lo guarda antes de HTTP. Bloquea doble envío y edición mientras está ocupado/pendiente; solo libera carrito tras pago verificado. Validación definitiva permite corregir; incertidumbre mantiene identidad. Ante una petición perdida antes de que el servidor cree el claim, un 404 de conciliación requiere revisión manual: no se inventa ausencia segura ni se cobra otra vez.

Pruebas nuevas: `SaleWorkflowTests` (vacía/cantidades, sesión cerrada, producto retirado, descuento no autorizado, método ajeno, propietario/payload, timeout, respuesta no pagada, concurrencia, diario reabierto/incompleto); `OdooSaleGatewayTests` (regresión de redondeo global, ámbito y JSON inválido); `frontend/src/application/sales.test.js` (identidad persistente, red perdida, incertidumbre y validación/cambio de total). P10 mantiene políticas sustituibles y puertos inyectables; no se agrega State GoF innecesario. Límites residuales: filesystem local durable, conciliación manual de claims corruptos o sin registro remoto, configuraciones fiscales complejas y pruebas visuales pendientes. No hay bloqueo externo para el flujo simple P09 validado.
