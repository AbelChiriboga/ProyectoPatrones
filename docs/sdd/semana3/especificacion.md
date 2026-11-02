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


## SDD-03.4 — Calidad y fallos (contrato previo a implementación)

Árbol limpio sobre be9fedf; se conservan los cuatro commits de consolidación. Sin cambios de contratos/backend, secretos ni escrituras reales.

| Observación demostrable | RF/RN | Corrección prevista | Aceptación |
|---|---|---|---|
| Cada tecla convierte a número y modifica carrito; borrar produce NaN/error global inmediato | RF-15, RN-02 | Borrador local por campo; commit en blur/Enter contra dominio; error vinculado al producto | CA-01: vacío transitorio, inválidos/stock y corrección sin alterar subtotal válido |
| Quote solo se limpia por efecto; respuesta tardía puede restaurarla y al fallar recotización queda la anterior | RF-09/15, RN-03/05 | Vincular quote a versión de carrito/descuento, invalidar antes de consultar y descartar respuesta tardía | CA-02: edición/recotización fallida impide confirmación obsoleta |
| Revisión muestra solo importe; errores y éxito comparten texto genérico; historial vacío no se distingue de no consultado | RF-10/12–14/17, RN-07 | Revisión de líneas/importes API, estados semánticos y mensajes accionables; navegación por anclas Venta/Historial/Pendientes | CA-03–07: doble acción, rechazo, incertidumbre y lectura real de DTO mediante dobles |

Se extraen presentadores de estado de UI sin React/HTTP para probar acciones asincrónicas sin navegador ni dependencias nuevas; los casos de uso existentes mantienen persistencia/idempotencia. UI usa el mismo presentador probado. CA-08: regresión .NET/React/lanzador y build. La imagen de orden 000002 por USD 9,66 es evidencia reportada por el usuario, no creada ni verificada por Codex. Pendiente manual: teclado, foco, anchuras, errores visibles; no confirmar ventas nuevas.


### Resultado SDD-03.4

| Problema / RF-RN | Código aplicado | Pruebas y resultado | Pendiente manual |
|---|---|---|---|
| Tipeo y validación local / RF-15, RN-02 | `QuantityInput.jsx`, `quantityEditor.js`, callback estable en `PosPage.jsx`; reglas siguen en Cart/casos de uso | `quality.test.js` CA-01: vacío sin validación prematura, inválido/stock sin modificar carrito y corrección; aprobadas | Borrar/reemplazar, Enter/blur y leer error vinculado al campo |
| Quote obsoleta, respuesta tardía y recotización fallida / RF-09/15, RN-03/05 | `salesPresenter.js` enlaza snapshot y clave del carrito/descuento; `SalesPanel.jsx` oculta quote si difiere del carrito actual, sincroniza antes de confirmar | CA-02: cantidad/producto/descuento, respuesta tardía y fallo de refresh; aprobadas | Editar tras cotizar y comprobar desaparición del total verificado |
| Doble clic, rechazo e incertidumbre / RF-16/17, RN-07/08 | Presentador observable sin React/HTTP; consume quote antes de enviar, bloqueo síncrono compartido, almacenamiento pendiente del caso de uso intacto, conciliación solo explícita | CA-03–05: una llamada, rechazo accionable, timeout/uncertain sin paid ni reenvío y consulta posterior; aprobadas con dobles, no ventas reales | Solo revisar estados disponibles; no provocar nuevas ventas ni timeouts de cobro |
| Historial/detalle/navegación / RF-12–14, RNF-02/08 | Acciones del mismo presentador para historial/detalle, importes/estado provenientes del DTO, vacío distinguible, anclas Venta/Historial/Pendientes y foco visible | CA-06: acciones preservan referencia/importe/estado API y paginación; SSR existente monta UI. CA-07 parcial: enlaces/lectores conectados, teclado y foco pendientes del usuario | Abrir historial/detalle existente; recorrer anclas y teclado en anchos habituales |

Verificación actual: **51 React (6 archivos), 76 unitarias .NET, 4 integraciones Odoo de solo lectura y 6 del lanzador aprobadas; 0 fallidas y 0 omitidas**. Builds React y .NET correctos; la compilación .NET inicial sobre `bin` encontró DLL bloqueados por la API activa. Se repitió con `dotnet build backend/PosOdoo.slnx --artifacts-path <carpeta temporal>` y `dotnet test backend/PosOdoo.slnx --artifacts-path <misma carpeta> --no-build --no-restore`: 0 advertencias/errores, sin detener servicios. `npm --prefix frontend test`, `npm --prefix frontend run build` y `python -B scripts/test_run_week3.py` correctos. No se ejecutó `POST /api/sales`, no hubo ventas reales nuevas ni prueba visual. Los tests de presentación ejercitan acciones y transiciones del presentador usado por React; no son simulaciones de Odoo ni pruebas interactivas de navegador.

Fuera de alcance: nuevas formas de pago, vuelto, impuestos complejos, reportes, clientes y rediseño. Se conserva el diario de un host y la revisión manual cuando una operación no puede conciliarse; no se cambia su garantía.

Comprobación manual segura: (1) iniciar sesión en la copia de semana 3; (2) agregar un producto y borrar/reemplazar cantidad, probar cero y corregir sin confirmar venta; (3) consultar quote, cambiar cantidad/descuento y comprobar que exige nueva cotización; (4) revisar líneas, descuentos e impuestos sin pulsar Confirmar venta y efectivo; (5) navegar Historial y detalle de una venta existente; (6) revisar Pendientes (solo consultar si ya existe una operación), foco por teclado y anchuras. No registrar ventas para obtener capturas. La evidencia reportada de orden 000002 por USD 9,66 corresponde al usuario.


## SDD-03.4-B — Entradas estrictas y vistas exclusivas (2026-11-02)

Baseline limpio 3343654, sin reescribir commits. DEF-01: `inputMode` es solo una sugerencia de teclado; el presentador aceptaba cualquier texto y `Number` convertía notación científica/hexadecimal. El blur ignoraba cambios programáticos sin evento change. Corrección prevista (RF-15, RN-02/05): gramática ASCII decimal sin signo en el borde visual, vacío transitorio, rechazo íntegro con error accesible, revalidación del valor real en blur/Enter y entero seguro en Cart; conservar cantidad válida y stock, sin máximos comerciales inventados. DEF-02: anclas desplazaban la página pero no controlaban qué se renderizaba. DEF-03: checkout, historial y pendientes permanecían en una tarjeta conjunta. Corrección prevista (RF-09–14, CA-01): vista hash activa con navegación atrás/adelante, tres contenidos excluyentes, carrito y presentador conservados en el componente padre; revisión de efectivo en forma de ticket sin nuevos datos comerciales. Tests `rigorous.test.jsx` escritos antes de corregir: entradas adversariales, mutación al commit, protección numérica del dominio y ausencia de controles ajenos a cada vista. Regresiones de quote/doble envío/incertidumbre se conservan. Sin GUI, nuevas dependencias, credenciales modificadas ni ventas reales.


### Cierre y evidencia de regresión SDD-03.4-B

| Defecto / requisitos | Corrección aplicada | Evidencia automatizada | Manual pendiente |
|---|---|---|---|
| DEF-01 / RF-15, RN-02/05 | `quantityEditor.js` rechaza íntegramente letras/símbolos/signos/decimales/exponentes/hex/espacios y números no representables exactamente; no trunca. `QuantityInput.jsx` controla change/pegado y relee el valor real tanto en Enter como blur mediante `commitQuantityControl`. Se permite vacío transitorio; al salir inválido se restaura confirmado y mensaje local accesible. `Cart` exige `Number.isSafeInteger` además de positividad/stock; backend y su límite existente no se modifican. | 13 regresiones iniciales de gramática/mutación/dominio fallaron antes; ahora pasan. Pruebas adicionales de Enter/blur, cero/vacío/corrección, no doble commit y carrito/subtotal/quote/payload intactos. | Teclado, pegar caracteres, vacío, Enter, blur y mensaje junto al producto; no confirmar ventas. |
| DEF-02 / RF-09–14, CA-01 | `viewNavigation.js` adapta hashchange sin router nuevo. `PosPage` renderiza catálogo/carrito solo en Venta; `SalesPanel` permanece montado con contenido excluyente según vista. Volver y atrás/adelante no reinicializan carrito, descuento ni presentador. | Dos pruebas SSR que detectaban catálogo/cobro/historial/pending simultáneos fallaron antes; ahora pasan. Transiciones de hash incluyendo atrás/retorno, cancelación de suscripción y conservación de cart/quote probadas con puerto falso, sin navegador. | Navegar las tres vistas y volver con carrito válido; usar atrás/adelante. |
| DEF-03 / RF-10/12–14, RN-07 | Encabezado activo, foco en título, ticket de revisión solo en Venta con líneas/importes DTO; historial/detalle en Historial y conciliación en Pendientes. `resultSale` separa resultado de operación del detalle histórico para evitar mostrar una consulta como resultado de cobro. Aviso en Venta si una intención pendiente bloquea el cobro. | Prueba que el detalle consultado no contamina resultado de operación; controles ajenos ausentes en cada vista y acciones de conexión/recuperación. Regresiones existentes de quote invalidada/doble clic/incertidumbre siguen pasando. | Jerarquía, contraste, foco y anchos habituales; sin referencias visuales A/B adjuntas no se afirma fidelidad a imágenes no disponibles. |

Pruebas nuevas antes de corregir: `npm --prefix frontend test -- --run src/ui/rigorous.test.jsx` → **15 fallidas**. Después: **74 React aprobadas en 7 archivos (23 casos nuevos), 76 unitarias .NET, 4 integraciones reales de lectura y 6 del lanzador aprobadas; 0 fallidas/omitidas**. `npm --prefix frontend run build`: correcto. .NET compilado con `dotnet build backend/PosOdoo.slnx --artifacts-path <temporal> --verbosity quiet`, luego `dotnet test backend/PosOdoo.slnx --artifacts-path <misma temporal> --no-build --no-restore`: correcto, 0 advertencias/errores, sin parar API. Lanzador: `python -B scripts/test_run_week3.py`. No nuevas dependencias, cambios fiscales, secretos modificados ni escrituras de venta. La evidencia automatizada es de presentadores/SSR y puertos de navegación; no equivale a interacción visual de navegador. El usuario debe comprobar manualmente los controles reales.

Pasos manuales seguros: abrir localhost:5173/#venta en la copia de semana 3; agregar un producto, intentar `2a`, `@`, `1e2`, `-1`, `1.5`, cero y pegado inválido; comprobar cantidad/subtotal confirmados y corregir. Cotizar sin confirmar, navegar a Historial y Pendientes (solo datos/consultas), regresar a Venta y comprobar conservación. Modificar cantidad/descuento válido debe invalidar cotización. Revisar ticket/errores y teclado. No pulsar Confirmar venta y efectivo para obtener evidencia.


## SDD-03.4-C — Dirección A y UX adversarial (contrato previo)

Baseline limpio f16fc72. Se inspeccionó estáticamente el HTML `../paquete-sdd-semana3-inicio/semana3-sdd/POS Odoo - 3 direcciones visuales (A, B, C).html`, extrayendo el template A del manifiesto comprimido con JSON/base64/gzip, sin ejecutar scripts ni usar navegador. A usa fondo #f3f5f8, panel blanco/radio 14px, borde #dde3ea, azul #0b5cab, controles 44px y dos columnas cercanas a 1:1.05; se reimplementará en React/CSS con tipografía del sistema. Se excluyen fuentes/scripts/componentes del prototipo, atajo Enter para agregar, tercer paso obligatorio Historial, datos fijos y todos los extras de B/C.

| Defecto | Causa | Contrato de corrección / evidencia prevista |
|---|---|---|
| DEF-04 (RF-02/15) | UI manda cualquier texto, sin política antes de HTTP y sin distinguir estados | Búsqueda de hasta 100 unidades UTF-16 (mismo límite existente del backend), texto Unicode imprimible con letras/números, sin controles/formato/separadores de línea ni consultas compuestas solo de símbolos. Se permiten tildes, espacios, guiones y signos de nombres/códigos sin whitelist arbitraria. Rechazo íntegro conserva búsqueda válida previa; sin peticiones rechazadas; estados vacío/sin coincidencias/inválido/red separados. |
| DEF-05 (RF-15, RN-02) | Latch `rejected` bloquea commit de valor real válido tras rechazo | Revalidación del valor real sin latch persistente; error desaparece con corrección/condición resuelta; vacío transitorio; Enter/blur/IME y +/- pasan por el mismo dominio. Sin límites comerciales nuevos. |
| DEF-06 (RF-13/14) | No hay acción de entrada al historial y títulos repetidos | Entrada de vista carga automáticamente; estados carga/vacío/error/reintento, detalle de API y etiquetas de estado comprensibles conservando el código Odoo. Sin botón previo Ver historial. |
| DEF-07 (CA-01, RF-09–12) | Jerarquía de formulario y checkout en Venta | A: cabecera compacta, pasos 1 Venta → 2 Cobro, vistas independientes; Venta con catálogo/ticket y +/-; Cobro con cotización real, totales derivados de sus líneas (solo suma de importes proporcionados, sin reglas fiscales en React) y confirmación deliberada; preservación de carrito y quote por clave. |

Pruebas iniciales `guided.test.jsx` agregadas antes de corrección para DEF-04/05/06 y estructura DEF-07; no demuestran fidelidad visual por píxeles, reservada al usuario. Se mantienen todas las restricciones de Odoo, efectivo exacto, secretos, idempotencia y no ventas nuevas.


### Resultado SDD-03.4-C y límites de evidencia

| Problema → criterio | Código aplicado | Prueba → resultado | Evidencia pendiente |
|---|---|---|---|
| DEF-04 → búsqueda válida o rechazo explicado sin red inválida | `application/catalogSearch.js` en el caso de uso; `catalogPresenter.js` para debounce, cancelación lógica de respuestas tardías, texto anterior conservado y errores independientes; `CatalogPanel.jsx` etiqueta resultados conservados | `guided.test.jsx` tres consultas inválidas fallaban antes. `guidedFlow.test.jsx`: tildes, códigos, puntuación legítima, vacío, controles invisibles, solo símbolos, longitud 100/101, pegado, corrección en la misma interacción, ausencia de coincidencias, red y retry; todas pasan | Copiar/pegar desde teclado real y comprobar foco/lectura accesible |
| DEF-05 → cantidad confirmada separada del draft y error recuperable | Se retira latch `rejected`; se revalida actualValue. Al restaurar un valor válido, `restored` presenta explicación informativa sin marcarlo rojo/aria-invalid. `QuantityInput` añade +/- y composición IME, con el dominio/stock y safe integer existentes como autoridad | Fallo previo reproducido: rechazo x seguido de commit real válido 1 mantenía error. Ahora pasa. Escritura gradual/reemplazo 3→5, vacío/blur, reintento, límites de stock, 100→101 cuando no hay stock fiable, IME y controles accesibles comprobados sin browser | Probar teclas, selección, blur, IME y +/- reales; sin confirmar venta |
| DEF-06 → historial al entrar con carga/vacío/error/reintento | `salesPresenter.enterView`, canal de historial independiente y guardia de petición en curso; `HistoryView` sin Ver historial ni títulos repetidos, detalle y estado traducido conservando código | Fallo previo: no existía acción enterView. Ahora carga una vez, tolera efecto repetido y error síncrono/asíncrono, retry y reentrada; lista/detalle proyectan referencia/importes reales del DTO | Ver historial existente y detalle; comprobar actualización al regresar |
| DEF-07 → A guiado y vistas excluyentes | `PosPage` separa 1 Venta/2 Cobro y dos columnas catálogo/ticket; `SalesPanel` conserva estado y consulta quote al entrar a Cobro. `SalesViews` revisión y totales de líneas backend (sin fórmula tributaria), método efectivo exacto y checkbox de revisión previa. `styles.css` reproduce colores/espacios/radios/controles de A, responsive; se usan fuentes del sistema, no fuentes o scripts del prototipo | Fallo estructural previo: no existía #cobro ni separación de checkout. Ahora SSR comprueba estructura/exclusión; acciones prueban Venta→Cobro→Venta, carrito preservado, quote/revisión invalidadas, una sola confirmación y UUID pendiente sin reenvío. Esto NO verifica apariencia por píxeles | Comparar manualmente con A a escritorio/móvil; alineación, jerarquía y navegación Tab/Shift+Tab |

Evidencia antes de corregir: `npm --prefix frontend test -- --run src/ui/guided.test.jsx` → **6 fallidas**. La fidelidad visual DEF-07 solo se reprodujo como diferencia estructural de markup; no se afirma validación visual automática. Después: **108 React aprobadas en 9 archivos, 76 unitarias .NET, 4 integraciones reales de lectura y 6 pruebas del lanzador; 0 fallidas y 0 omitidas**. Se conservan los casos previos ajustando únicamente su expectativa de nuevo paso Cobro, revisión explícita y carga automática; no se eliminan pruebas. Las cifras anteriores de 74 corresponden a SDD-03.4-B.

Comandos medidos: `npm --prefix frontend test`; `npm --prefix frontend run build`; `dotnet build backend/PosOdoo.slnx --artifacts-path <temporal> --verbosity quiet`; `dotnet test backend/PosOdoo.slnx --artifacts-path <misma temporal> --no-build --no-restore --verbosity quiet` (variables Odoo locales cargadas sin imprimir valores); `python -B scripts/test_run_week3.py`. Builds correctos; .NET 0 advertencias/errores. HTTP del módulo PosPage servido en localhost:5173: 200 y referencia al paso Cobro. API 5038 y Vite 5173 ya estaban activos y no se reiniciaron. No se llamó POST /api/sales ni se registraron ventas; no se alteraron backend, secretos ni datos Odoo. Pruebas UI son de presentadores/SSR sin navegador, no pruebas visuales.

Aceptación visual A sigue abierta para el usuario. Exclusiones explícitas: tarjetas, transferencias, monto recibido/vuelto, clientes, inventario, facturación, reportes/auditoría adicional, indicadores de conexión inventados, teclado táctil C, atajos de confirmación y Enter para agregar productos. Historial no es paso obligatorio. La gramática de búsqueda no limita los nombres de productos ni prohíbe signos imprimibles cuando acompañan letras/números. El diario durable de un host y la conciliación manual de resultados no demostrables conservan sus límites previos.

Guía manual segura (sin confirmar ventas):
1. En #venta, comparar columnas/paneles/cabecera/pasos con A; estrechar ventana y recorrer foco con Tab/Shift+Tab.
2. Buscar `Café`, `CAFE-001` y un nombre normal; Enter no agrega ningún producto.
3. Pegar más de 100 caracteres, símbolos solos o texto con salto de línea: ver explicación y búsqueda anterior conservada; corregir y comprobar que desaparece el error. Una consulta válida inexistente debe mostrar ausencia de coincidencias, no error.
4. Agregar un producto; intentar letra/símbolos/0/vacío y salir del campo: cantidad válida conservada con explicación informativa. Reescribir un entero válido elimina el aviso sin reload.
5. Reemplazar cantidad y usar +/- respetando stock fiable; subtotal solo debe cambiar con cantidad confirmada.
6. Continuar a #cobro, revisar productos/descuentos/impuestos/total API y efectivo exacto; volver a Venta, modificar y entrar otra vez: exige cotización vigente y revisión nueva. No pulsar Confirmar venta y efectivo.
7. Entrar a #historial: carga automática sin Ver historial, abrir orden existente y comprobar referencia/importes/estado; no crear órdenes para probarlo.
8. Abrir #operaciones: solo consultar si ya hay una intención pendiente; nunca reenviar ni generar una venta para provocar incertidumbre.


## SDD-03.5 — Historial escalable y confirmaciones seguras (contrato previo)

Baseline limpio f31fba0. El usuario reporta diseño/entradas validados manualmente; Codex no usa navegador. Problemas: navegación de consulta poco visible; listado legacy obtiene detalle por cada orden (N+1), carece de búsqueda/fecha y solo permite avanzar; detalle se inserta bajo lista; Quitar muta inmediatamente.

Decisión RF-13/14: conservar `GET /api/sales` y su array por compatibilidad; añadir `GET /api/sales/history?search=&beforeId=0&limit=20` con `{items:[{id,reference,dateUtc?,total,currency,state}],hasMore,nextBeforeId}`. Puerto de lectura Application independiente; una búsqueda Odoo `pos.order/search_read`, proyección de campos de cabecera y limit+1, scope POS obligatorio, orden id desc. Búsqueda nombre/referencia POS con ilike y máximo 100 caracteres sin controles. No descargar todo el historial ni contar toda la base; cliente mantiene solo cursores para Anterior/Siguiente. `date_order` se interpreta UTC de Odoo, sin inventar fechas ausentes. El detalle conserva el endpoint existente.

Decisión RF-04/15: componente `<dialog>` modal reutilizable; foco inicial seguro, trap de Tab/Shift+Tab y restauración con preventScroll, bloqueo de scroll de fondo, Escape solo cancelación/cierre. Backdrop nunca confirma ni cierra. Detalle abre inmediatamente con carga/error/retry; versión de petición impide respuesta tardía tras cerrar/cambiar orden. Eliminación solicita producto seleccionado y ejecuta el caso de uso solo al confirmar, con guardia síncrona contra duplicados; Cancelar conserva identidad del carrito/quote. No eliminación de catálogo/ERP ni cambios de stock.

Navegación: secciones Venta/Historial/Pendientes visibles y activas; Venta incluye pasos 1 Venta → 2 Cobro, manteniendo estados en padre. Aceptación: páginas remotas buscables, estado/error/retry, fecha real o No informada; detalle en modal sin mover lista, clic exterior ignorado, cierre explícito/foco, quitar/cancelar/confirmar una vez e invalidación de quote. Pruebas sin browser con puertos falsos de foco/teclado y presentadores, SSR semántico, unitarias del contrato/adaptador y lecturas reales si hay entorno. La aceptación visual/foco nativo final corresponde al usuario. Sin ventas nuevas, secretos modificados ni cambios tributarios/idempotencia.

### Resultado y aceptación SDD-03.5

| Causa / requisitos | Corrección y archivos principales | Evidencia automatizada |
|---|---|---|
| Accesos secundarios poco visibles / RF-13/14/15 | `PosPage.jsx`, `styles.css`: tres secciones principales, `aria-current`, pasos de venta conservados; carrito permanece en padre | SSR de las cuatro vistas, regresiones de navegación real mediante puerto hash y conservación del carrito |
| Listado legacy lee líneas/pagos por orden y carece de búsqueda/fecha / RF-13 | `SearchSalesHistoryUseCase.cs`, `OdooSaleGateway.History.cs`, DI y `SalesController.cs`: endpoint aditivo, ámbito POS obligatorio, búsqueda name/pos_reference, límite 1–100, consulta única con limit+1 | Unitarias de validación, scope y proyección sin N+1; integración JSON-2 real y HTTP autenticado; contrato array legacy preservado |
| Consulta sin retroceso ni búsqueda remota / RF-13/15 | `historyQuery.js`, `useCases.js`, `httpPosApi.js`, `salesPresenter.js`, `SalesViews.jsx`, `SalesPanel.jsx`, `salesViewModel.js`: páginas de 20, pila de cursores y una sola página de registros en memoria, fecha UTC disponible, carga/vacío/error/retry | Búsqueda remota codificada, Anterior/Siguiente, query aplicada frente a borrador, retry del cursor fallido y descarte de páginas tardías |
| Detalle inline y respuestas asincrónicas obsoletas / RF-14/15 | `SafeDialog.jsx`, `dialogBehavior.js`, `SalesViews.jsx`, `salesPresenter.js`: modal con encabezado/pie fijos y scroll interno, cierre seguro, control de foco y versión de petición | SSR de título/descripción/acciones; puertos DOM de apertura, Tab/Shift+Tab, backdrop, Escape, restauración de foco/scroll y fallback si desaparece el abridor; respuesta tardía/ID erróneo no se muestra |
| Quitar muta sin confirmar / RF-04/15 | `removalPresenter.js`, `RemoveProductDialog.jsx`, `PosPage.jsx`: confirmación sobre producto estable; Cancelar intacto; caso de uso existente solo al confirmar; guardia síncrona | Carrito/quote idénticos al cancelar, una única línea retirada, quote/revisión invalidadas, doble clic/reentrada bloqueados y edición crítica rechazada |

Verificación: **131 React en 10 archivos, 89 unitarias .NET, 5 integraciones Odoo de solo lectura y 6 pruebas del lanzador aprobadas; 0 fallidas/omitidas**. Builds React y .NET correctos, .NET con 0 advertencias/errores. Las regresiones existentes se conservan; sus dobles de historial se adaptaron al nuevo sobre paginado. Los controles del diálogo se prueban mediante puertos DOM y SSR en Node, sin navegador ni emulador de navegación: esto verifica las políticas de interacción, no sustituye la prueba manual del comportamiento nativo.

Comandos: `npm --prefix frontend test`; `npm --prefix frontend run build`; `dotnet build backend/PosOdoo.slnx --artifacts-path <temporal> --verbosity quiet`; `dotnet test backend/PosOdoo.slnx --artifacts-path <misma temporal> --no-build --no-restore --verbosity quiet`, con configuración local Odoo cargada en memoria sin imprimir; `python -B scripts/test_run_week3.py`. Artifacts temporales evitan sobrescribir DLL de la API activa.

HTTP real verificado primero en API temporal y después en `http://localhost:5038`: login, historial resumido, búsqueda de referencia existente, fecha UTC, siguiente cursor y detalle devuelven 200; contrato legacy sigue siendo array/200; búsquedas/límites inválidos devuelven 400 y sin JWT devuelve 401. Solo se reinició la instancia ASP.NET identificada por PID/ruta de este repositorio para cargar la nueva ruta; Vite en 5173 sigue sirviendo este frontend y devuelve el módulo actualizado con HTTP 200. No se ejecutó `POST /api/sales`, no se crearon ventas ni se modificaron secretos, configuración o datos de Odoo. Sin push/merge/PR ni cambios en main.

Para ejecutar desde la raíz del repositorio, en dos terminales: `python -B scripts/run-week3.py` y `npm --prefix frontend run dev -- --host localhost --port 5173 --strictPort`. El lanzador conserva la precedencia de User Secrets y variables explícitas. Si los puertos ya están ocupados por estas instancias, utilizar las que están activas. URL de validación manual: `http://localhost:5173/#historial`.

Validaciones manuales pendientes del usuario, sin registrar ventas:
1. Recorrer Venta/Historial/Operaciones pendientes, comprobar sección activa, dos pasos de venta y carrito conservado; revisar anchuras y foco visible con teclado.
2. Entrar al historial, buscar una referencia existente y otra sin coincidencias; recorrer Anterior/Siguiente si existen varias páginas. Se pagina sin conteo total ni descarga completa; las fechas se indican explícitamente en UTC.
3. Abrir una orden existente: comprobar datos, scroll interno y botón Cerrar detalle. Clic exterior debe conservar el diálogo; Tab/Shift+Tab no deben salir; Escape solo cierra. Al cerrar, foco y posición de historial deben restaurarse.
4. En una venta en preparación con dos productos y cotización vigente, pulsar Quitar y cancelar mediante botón o Escape: carrito y cotización intactos. Clic exterior no debe cerrar. Confirmar después un único producto: solo esa línea desaparece, la cotización anterior deja de autorizar cobro y el foco retorna a un destino visible.
5. No confirmar venta/efectivo, generar incertidumbre ni crear órdenes para probar paginación. El diseño A previamente aceptado corresponde al reporte del usuario; Codex no realizó validación visual.


## SDD-03.5-B — Búsqueda dinámica (contrato previo)

Defecto RF-13/15: el campo solo actualizaba el borrador; la consulta dependía del submit del formulario. Se reutiliza el endpoint paginado sin cambios, con debounce de 300 ms en el presentador, invalidación de peticiones desde la edición, reinicio de cursores y restauración del historial al limpiar. Se mantienen registros anteriores visibles durante la consulta, con estado de carga y etiqueta de consulta aplicada; detalle, autenticación y navegación conservados. Pruebas con reloj falso de debounce, carreras, errores, limpieza y paginación; aceptación visual pendiente del usuario, sin navegador ni ventas nuevas.

Resultado B: `npm --prefix frontend test` → **138 aprobadas en 11 archivos**, incluyendo siete pruebas nuevas en `liveHistory.test.jsx` con reloj falso: coalescencia/300 ms/sin duplicados, reset/limpieza, respuestas tardías incluso durante debounce, tabla estable/vacío, error/recuperación, validación/referencias legítimas y navegación/dispose/modal. La regresión anterior de búsqueda manual se adapta a la interacción dinámica sin retirar sus aserciones de retry y validación. `npm --prefix frontend run build` correcto. `dotnet test backend/tests/Pos.UnitTests --artifacts-path <artefactos SDD-03.5> --no-build --no-restore --verbosity quiet` → **89 aprobadas, 0 fallidas/omitidas**; backend y contratos no cambiaron, se reutilizaron sus artefactos compilados. Las cinco integraciones reales y HTTP de SDD-03.5 son evidencia previa, no repetida en B.

Archivos B: `salesPresenter.js` implementa debounce/cancelación lógica/deduplicación y limpieza; `SalesPanel.jsx` libera solicitudes al desmontar; `SalesViews.jsx` elimina submit/Búsqueda explícita y conserva tabla con aria-busy durante carga. Casos de uso, adaptador HTTP, validación de referencias y backend inspeccionados y reutilizados sin cambios. Seguimiento y pruebas actualizados.

Pendiente manual: en Historial escribir una referencia sin Enter, comprobar actualización tras pausa breve, modificar/limpiar, recorrer páginas y comprobar reset, abrir/cerrar detalle, regresar entre secciones y verificar ausencia de parpadeo/foco/diseño. No crear ventas para validar. Sin navegador/GUI, secretos ni datos Odoo modificados.
