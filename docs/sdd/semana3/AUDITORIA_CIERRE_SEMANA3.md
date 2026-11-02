# AUD-03-FINAL — Auditoría de cierre de semana 3

## 1. Resumen ejecutivo y veredicto

**NO LISTA PARA PUBLICAR**, por un hallazgo P1 documental de preparación/reproducción (H01). La implementación funcional auditada supera las suites actuales y las lecturas HTTP reales; no se identificó un P0 en el alcance inspeccionado. No se corrigieron hallazgos ni se modificó código/configuración.

Auditoría del 2026-11-02, hora de máquina UTC−05:00 (America/Guayaquil). Ejecuciones principales entre 01:39:46 y 01:41:46. **238 pruebas aprobadas: 138 React + 89 unitarias .NET + 5 integraciones reales de lectura + 6 del lanzador; cero fallidas/omitidas.** Dos compilaciones correctas, excluidas del conteo.

El botón «Consultar total con impuestos» es **OBSERVACIÓN UX P2**, no bloqueante: la cotización automática al ingresar a Cobro ya existe y el botón permite recotizar/reintentar. No impide operar ni registra ventas.

Único archivo de entrega creado: este informe. No navegador/GUI, commits, push, merge, cambios de secretos, nuevas ventas, inicializaciones ni cambios en datos Odoo. Las compilaciones generan únicamente artefactos temporales o ignorados.

## 2. Identificación comprobada

- `pwd` y `git rev-parse --show-toplevel`: `C:/Users/User OGX/Documents/04_Universidad/patrones_software/patrones-software-semana3/ProyectoPatrones`.
- `git branch --show-current`: `semana3-ventas`.
- HEAD: `28afbbac515f6b2a7e61458a90772a8568469583` (`28afbba`).
- `main` y `git merge-base main HEAD`: `9c84c3e3230d58ff74cbf241a5d8d94a187ddca7`.
- `git status --porcelain=v1` inicial: vacío. Final esperado y comprobado tras redactar: únicamente `?? docs/sdd/semana3/AUDITORIA_CIERRE_SEMANA3.md`.
- Copia antigua no utilizada. Puertos inspeccionados sin detener ni duplicar servicios: API 5038 PID 11512, ejecutable dentro de este repositorio; Vite 5173 PID 26220, comando apunta a este `frontend/node_modules/.../vite.js`.

Fuentes revisadas: AGENTS.md completo; especificacion.md y PROGRESS.md; README/scripts; contratos/código/pruebas; evidencias P09; PDFs docentes en `../docs/`. El proyecto oficial (pp. 2–7) define RF/RN/P01–P10 y entrega; las indicaciones complementarias no sustituyen el frontend completo exigido allí. La validación técnica POS docente es antecedente diagnóstico, no receta de ajustes manuales de importes. Extracción de texto con PyMuPDF, sin validación gráfica de PDFs.

## 3. Matriz de requisitos, reglas y pruebas

Las rutas siguientes son relativas a la raíz comprobada. **AHORA** = EJECUTADO AHORA; **HIST** = REPORTADO PREVIAMENTE/evidencia persistida. VERIFICADO se refiere al alcance técnico de cada fila; no equivale a una prueba visual realizada por el agente.

| Requisito / estado | Implementación concreta | Prueba / resultado observable / evidencia | Riesgo o límite |
|---|---|---|---|
| RF-09 VERIFICADO | `backend/src/Pos.Application/Sales/SaleWorkflow.cs`: QuoteAsync/RegisterAsync; PreviewSaleUseCase; `OdooSaleGateway.QuoteAsync` | AHORA: SaleWorkflowTests y PreviewSaleUseCaseTests; cantidades, descuentos/método inválidos no envían; precios de catálogo, expectedTotal contrastado | P09 con descuento es cobertura de dobles/cálculo; no venta real adicional |
| RF-10 VERIFICADO | OdooSaleGateway.QuoteAsync filtra métodos de sesión con type=cash; SalesPanel itera quote.methods | AHORA: `Finds_open_pos_session_with_payment_methods`, `Empty_closed_disabled_and_invalid_method_never_send`; P09 pago efectivo HIST | Solo efectivo exacto; no inventar métodos ni vuelto |
| RF-11 VERIFICADO con evidencia histórica | OdooSaleGateway.SendAsync → JSON-2 `pos.order/sync_from_ui` | HIST: semana3-p09.json, orden 2, dos líneas/un pago. AHORA: GET detalle de orden existente | No nueva escritura; no se atribuye P09 a esta auditoría |
| RF-12 VERIFICADO | SaleWorkflow.StatusAsync; resultSale/VerifiedDetail en UI | AHORA: GET detalle 200 con ID/referencia/importes reales; `Unpaid_response_never_confirms`, prueba UI de resultado verificado | Estado remoto puede evolucionar paid→done |
| RF-13 VERIFICADO | SearchSalesHistoryUseCase, OdooSaleGateway.History.cs/SearchAsync; salesPresenter.editHistorySearch/loadHistory; httpPosApi.salesHistoryPage | AHORA: siete casos liveHistory.test.jsx, OdooHistoryReaderTests e integración `Reads_bounded_history_pages_and_reference_search_without_writing_orders`; HTTP búsqueda/cursor 200 | Búsqueda name/pos_reference; no fechas/cliente inventados. Páginas 20, limit+1, scope POS; no descarga total |
| RF-14 PARCIAL (técnica verificada) | SalesViews.SaleDetailDialog; SafeDialog; dialogBehavior.activateDialog/containDialogFocus | AHORA: historyDialogs.test.jsx: carga, ID erróneo/tardío, foco/scroll mediante puertos, backdrop/Escape; GET detalle 200 | Scroll/foco nativos y presentación pertenecen a aceptación manual; no navegador |
| RF-15 VERIFICADO en pruebas | quantityEditor, Cart; parseCatalogSearch/parseHistorySearch; PreviewSaleUseCase y SearchSalesHistoryUseCase | AHORA: rigorous/guidedFlow/quality/liveHistory; validación backend antes del reader; error corregible sin contaminar subtotal/payload | No garantiza stock externo; máximo de cantidades backend existente conservado |
| RF-16 / RN-08 VERIFICADO en alcance de un host | FileSaleOperationStore.TryCreate/Confirm; SaleWorkflow.RegisterAsync; pendingSale; presenter.confirm | AHORA: `Repeated_request_after_store_reopen_sends_once`, `Concurrent_requests_send_once`, colisión dueño/payload, claim corrupto, doble confirmación UI. HIST: uuidCount=1 tras repetición P09 | Requiere diario conservado y UUID reutilizado; no exclusión distribuida ni protección entre intenciones nuevas |
| RF-17 / RN-07 VERIFICADO | RegisterAsync/StatusAsync; useCases.confirmSale/reconcileSale y presenter.result | AHORA: `Timeout_reconciles_without_resending`, `Unpaid_response_never_confirms`, calidad UI timeout/uncertain | Claim incierto/404 no habilita cobro ciego; revisión manual si no concilia |
| RN-01 VERIFICADO | SaleWorkflow.QuoteAsync y Sale.Confirm | Empty_closed_disabled_and_invalid_method_never_send y SaleTests: venta vacía rechazada | Preview vacío permitido para preparar carrito, no para registrar |
| RN-02 VERIFICADO | SaleWorkflow, Sale/SaleLine y Cart | Invalid_quantity_never_sends (0, −1, 100001), SaleTests, rigorous.test.jsx | No afirmar política comercial de stock completo |
| RN-03 VERIFICADO | PreviewSaleUseCase.ExecuteAsync y gateway fiscal | Calculates_lines_and_subtotal_with_catalog_prices; comprobación expectedTotal y fuente externa | Configuraciones fiscales complejas fallan cerradas |
| RN-04 VERIFICADO | DiscountPolicyRegistry e IDiscountPolicy; asignación fiscal gateway | Applies_an_authorized_percentage_discount, Rejects_a_discount_that_is_not_authorized/Rejects_a_policy_that_discounts_more_than_the_subtotal | Alcance fiscal simple documentado |
| RN-05 VERIFICADO | SaleWorkflow.StatusAsync; quote ligada a saleKey | Pruebas de estado no pagado, quote tardía/invalidada y recotización fallida | Recalcular al confirmar evita autoridad de importes del cliente |
| RN-06 VERIFICADO | Sale confirmada no editable; UI carrito nuevo tras resultado confirmado; no endpoint de edición remota | SaleTests y pruebas UI resultado separado del detalle | No restringe administración posterior legítima en Odoo |
| P01 VERIFICADO | SaleTests / SaleWorkflowTests | Empty_closed_disabled_and_invalid_method_never_send: cero envíos | Dobles, no escrituras |
| P02 VERIFICADO | SaleTests / SaleWorkflowTests / Cart | Invalid_quantity_never_sends; entradas adversariales React | Dobles |
| P03 VERIFICADO | GetActiveSessionUseCaseTests / SaleWorkflowTests | Sesión Closed rechazada, cero envíos | Carrera de cierre posterior depende de validación Odoo; error nunca equivale a éxito |
| P04 VERIFICADO | PreviewSaleUseCase y filtros OdooProductCatalog | Unknown_product_is_reported_as_not_found; catálogo deshabilitado en workflow → cero envíos | Producto no habilitado; no confundir con stock cero |
| P05 VERIFICADO | Cliente JSON-2/workflow | Timeout e integración inválida; unpaid nunca confirmed | No se provocó fallo comercial real |
| P06 VERIFICADO | Diario/workflow y casos frontend | Reapertura, concurrencia, colisión, timeout y resultado incierto | Límite de un host |
| P07 VERIFICADO | domain/cart.test.js, useCases.test.js, quality/rigorous | Carrito/cantidades/totales y cancelación conservados | No prueba visual |
| P08 VERIFICADO | httpPosApi.test.js y presentadores UI | Fallos/401/uncertain, recuperación y ausencia de falso éxito | SSR/puertos, no navegador |
| P09 VERIFICADO histórico; lectura actual corroborada | docs/evidencias/semana3-p09.json | Dos líneas/pago, repetición igual, uuidCount=1; AHORA GET ID 2 conserva datos salvo state=done | Sin escritura nueva ni confirmación visual Odoo ahora |
| P10 PARCIAL / transferencia semana 4 | DiscountPolicyRegistry, PreviewSaleUseCaseTests, puertos inyectables | Políticas variables y política greedy inyectada rechazada sin alterar caso de uso; gateway sustituido en tests | Preparar demostración positiva de cambio controlado y explicación individual para entrega grupal |

**ACEPTACIÓN MANUAL DEL USUARIO:** el encargo de auditoría reporta validación favorable de entradas, búsqueda dinámica, historial y modales. Se conserva como reporte del usuario, sin atribuir al agente capturas, casos interactivos ni certificación de foco nativo. Los documentos previos todavía marcan algunos de estos puntos pendientes; precisar el acta de aceptación en la entrega grupal.

## 4. Suites y compilaciones ejecutadas ahora

Preinspección: Vitest utiliza pruebas de dominio/presentadores/SSR/dobles y puertos de foco; ninguna controla navegador. SaleWorkflowTests usa gateway falso y diario temporal, no Odoo. Las cinco OdooIntegrationTests solo llaman catálogo/sesión/historial de lectura. test_run_week3 importa build_environment con credenciales sintéticas y no inicia servicios.

| Comando exacto desde raíz | Hora local 2026-11-02 | Resultado | Salida |
|---|---|---|---|
| `npm --prefix frontend test` | inicio 01:39:48 | 138 aprobadas / 11 archivos; 0 fallidas/omitidas | 0 |
| `npm --prefix frontend run build` | final antes de 01:39:53 | Correcto, 38 módulos | 0 |
| `python -B scripts/test_run_week3.py` | final 01:39:53 −05:00 | 6 aprobadas; 0 fallidas/omitidas | 0 |
| `dotnet build backend/PosOdoo.slnx --artifacts-path C:\Users\USEROG~1\AppData\Local\Temp\pos-aud03-f9go2_vx --verbosity quiet` | 01:39:46–01:40:04 (bloque .NET) | 0 errores/advertencias | 0 |
| `dotnet test backend/PosOdoo.slnx --artifacts-path C:\Users\USEROG~1\AppData\Local\Temp\pos-aud03-f9go2_vx --no-build --no-restore --verbosity quiet` | termina 01:40:04.924 −05:00 | 89 unitarias + 5 integraciones reales, 0 fallidas/omitidas | 0 |

Los dos comandos .NET se ejecutaron desde un envoltorio Python en memoria que creó la salida mediante tempfile.mkdtemp, compiló y cargó solo las claves `Odoo__*` del archivo local ignorado al entorno del subproceso de tests. Se comprobó loopback/pos_db; no se imprimieron valores. No se sobrescribieron las DLL de la API activa.

**HTTP AHORA:** peticiones Python urllib en memoria a API5038, configuración efectiva obtenida con las funciones del lanzador (variables explícitas/User Secrets/fallback). Login 200; historial limit=1 200; búsqueda por referencia existente 200; página siguiente con IDs menores 200; detalle 200; contrato legacy array 200; sin JWT 401. El token nunca se imprimió. Primera comparación estricta P09 terminó en AssertionError (salida 1): la segunda lectura diagnóstica (salida 0, 01:41:46) aisló únicamente `/state`: paid histórico frente a done actual. No se ocultó esa divergencia ni se exigió que un registro remoto conserve su estado histórico. Ambas situaciones son aceptadas por StatusAsync. No se llamó POST /api/sales, ni se ejecutó el verificador de escrituras.

**REPORTADO PREVIAMENTE:** las cifras 131 React y 231 pruebas totales eran anteriores a 03.5-B; no son el resultado actual. P09 pertenece a su fecha de evidencia, no a esta ejecución.

## 5. Hallazgos y decisión de cotización

| ID / prioridad | Causa y evidencia | Impacto / propuesta mínima (NO aplicada) |
|---|---|---|
| H01 **P1 BLOQUEANTE PARA PUBLICAR** | README.md inicia con Estado Semana 2 y guía `scripts/odoo-init.sh`, publica valores por defecto de administrador y pide cambiarlos después; al final Semana 3 dice no usar ese inicializador. El script antiguo inicializa la base/abre servicios y muestra APIKEY. Además la sección nueva ubica credenciales del operador solo en el fallback local, aunque run-week3.py prioriza User Secrets. Inspección de README, odoo-init.sh y run-week3.py, no ejecución del inicializador. | El punto de entrada de la entrega puede inducir preparación insegura/incorrecta y login con credencial de otra fuente. Unificar inicio del README alrededor de semana 3, relegar lo anterior explícitamente a histórico, explicar precedencia y reemplazar instrucciones actuales peligrosas. Verificar esa guía antes de publicar. No se afirma haber demostrado un fallo en una instalación limpia. |
| H02 **P2 OBSERVACIÓN UX** | SalesPanel.jsx siempre muestra «Consultar total con impuestos». enterView('cobro') ya llama quote si no hay quote/pending; quote obtiene sesión y total del servidor. | Acción redundante en recorrido normal, útil como reintento/actualización. Propuesta: conservar recotización explícita solo como Actualizar/Reintentar cuando corresponda. No eliminar botón ni cambiar estados ahora; automatismo solicitado ya existe. |
| H03 **P2 documentación/extensibilidad** | README conserva conteos antiguos y no enumera el endpoint history; especificacion.md contiene fases previas/tentativas (SQLite/RegisterSaleUseCase) y luego decisiones finales reales. | Aclarar versión/fecha de resultados y vincular decisiones finales (archivos/SaleWorkflow) al entregar. Hay al menos tres decisiones registradas; falta pulir su presentación y demostrar P10 en grupo. |

No se identificó P0 en la inspección/pruebas. H01 basta para impedir veredicto de publicación aprobada. No se usa P2 para exigir nuevas funcionalidades.

Cotización: al editar productos/cantidad/descuento, setSale invalida quote/revisión; key distinta oculta quote inmediatamente y una respuesta tardía se ignora. Antes de confirmar se sincroniza la venta y backend recotiza; sin quote/método/revisión, busy o pending no se confirma. Error de recotización consume la anterior; incertidumbre exige conciliación. Selector muestra exclusivamente métodos devueltos por gateway, filtrados a efectivo, no opciones cosméticas ficticias. Estos puntos tienen regresiones quality/guidedFlow y sales.test.js aprobadas.

Historial conserva tabla durante loading/error con etiqueta de consulta aplicada; resultados vacíos se distinguen de error, retry conserva cursor y consulta. Hay debounce de 300 ms e invalidación desde la edición; salir de sección/desmontar cancela lógicamente. Detalle y Quitar usan diálogo nativo con ARIA, acciones seguras, bloqueo exterior, Tab/Shift+Tab y restauración de foco/scroll probados mediante puertos. Cancelar no muta carrito/quote; confirmar quita una línea una vez e invalida cotización. Ninguna inspección estática se presenta como prueba gráfica.

## 6. Arquitectura, patrones y semana 4

Dependencias .csproj: Domain sin referencias a otras capas; Application → Domain; Infrastructure → Application; Api compone ambas. Application define ISaleGateway/ISaleHistoryReader/ISaleOperationStore. Controllers delegan y usan ProblemDetails/JWT. Frontend Domain/Application no importan React/fetch en código productivo; infraestructura implementa puertos y UI compone. La referencia a infraestructura encontrada en application/sales.test.js es composición de prueba, no dependencia productiva.

SOLID observable: responsabilidad de fiscalidad/integración en gateway frente a orquestación de workflow; DIP por puertos; políticas de descuento sustituibles sin modificar consumidores (OCP). No se certifica cumplimiento absoluto de SOLID: métodos/test densos y gateway con varias responsabilidades son candidatos de revisión grupal, sin refactorizar durante auditoría.

Decisiones documentadas en especificacion.md (secciones 8/10 y posteriores):

| Problema / categoría | Alternativas / elección | Compromiso / código / prueba |
|---|---|---|
| Integración cambiante / Adapter | Acoplar JSON a casos de uso frente a gateway | OdooSaleGateway/OdooJson2Client detrás de ISaleGateway; costo de mapeo/validación; pruebas de adaptador y cinco lecturas reales |
| Variación descuentos / Strategy GoF | Condicional central frente a IDiscountPolicy/registry | Extensible con registro explícito; distribución fiscal conserva reglas; DiscountPolicyTests/PreviewSaleUseCaseTests |
| Caída y repetición / patrón de integración idempotente | Caché RAM, UUID remoto, SQLite tentativo frente a diario durable elegido | FileSaleOperationStore y conciliación, sin reenvío de claims; concurrencia/reapertura/corrupción probadas; solo un host |
| Pocos estados / alternativa sencilla | State GoF frente a transiciones explícitas | SaleStatus y resultado confirmado/incierto, sin jerarquía innecesaria; pruebas de timeout/no pagado |

Para semana 4: consolidar tres fichas finales con problema/alternativas/compromisos/código/prueba; demostración positiva P10 de cambio controlado, revisión Clean Code y defensa individual. No confundir Clean Architecture con patrón GoF ni exigir tres GoF. La evidencia de puertos y políticas existe; la defensa de cada integrante no puede certificarse por esta auditoría.

## 7. Git, secretos, dependencias y scripts

Consultas ejecutadas, sin cambiar rama: `git status --porcelain=v1`; `git log --oneline --decorate -n 20`; `git merge-base main HEAD`; `git log --oneline main..HEAD`; `git diff --stat main...HEAD`; `git diff --check main...HEAD`; `git remote -v` capturado y saneado antes de mostrarlo. Todas las consultas Git terminaron con salida 0; diff --check no produjo incidencias. Origen: https://github.com/AbelChiriboga/ProyectoPatrones.git, sin token incrustado observado.

**14 commits locales por encima de main**, sin reescritura:

| Fase | Commits comprobados (orden de dependencia) |
|---|---|
| 03.1 ventas/infra y 03.2 integración UI/arranque | 0d1565c feat(sales): add POS checkout, reconciliation and durable idempotency; f545c0b feat(frontend): connect cash checkout, sale history and pending operations; 54affb6 fix(dev): prepare isolated Odoo and preserve User Secrets on startup; be9fedf docs(sdd): consolidate week 3 specification, policies and P09 evidence |
| 03.3 | Consolidación de los anteriores en historial local; no hay commit con etiqueta 03.3 que inventar |
| 03.4 | ba71f25 fix(ui): guard cart edits and checkout failure states; 3343654 docs(sdd): record POS quality checks and manual acceptance |
| 03.4-B | a66f1ba fix(ui): enforce strict quantities and exclusive POS views; f16fc72 docs(sdd): record adversarial input and navigation regressions |
| 03.4-C | c08760c feat(ui): implement guided A checkout and resilient POS inputs; f31fba0 docs(sdd): record direction A and adversarial UX evidence |
| 03.5 | d988672 feat(sales): add scoped searchable history pages; 6d5ed9e feat(ui): add paged history and safe modal confirmations; 11691f2 docs(sdd): record history and dialog acceptance checks |
| 03.5-B | **28afbba fix(history): implement debounced live order search** |

Diff: **55 archivos, 2988 inserciones, 217 eliminaciones**. Sin binarios nuevos en diff, sin marcadores de conflicto detectados. Cambios afectan alcance ventas, soporte carrito validado, UI, pruebas, preparación y SDD; no modificación comercial ajena identificada. Existen desde main `semana2-cambios.zip` y `docs/Informe_Semana1_POS_Odoo20.docx`; no son generados nuevos de semana 3.

Seguridad: inventario de **129 archivos tracked**, inspección por archivo de texto/archivo comprimido y rutas del diff; patrones de asignación sensible, URL con credenciales, claves privadas, JWT literales y tokens largos. Revisión de candidatos: README usa placeholders/variables, pruebas usan constantes sintéticas, preparador genera valores aleatorios en runtime; cadenas largas halladas en SDD son hashes Git/digest. Sin `.env` real, secrets.json, certificados privados, dumps, bases, bin/obj/node_modules/dist versionados detectados. Se revisaron miembros ZIP/DOCX sin extraer a disco; ninguna coincidencia con las credenciales locales actuales comparadas en memoria. No se detectó contraseña/API key/JWT signing key real ni dato personal nuevo en el diff. Esto no constituye escaneo exhaustivo de todos los objetos históricos ni garantiza que toda cadena arbitraria sea inocua.

`infra/.env`, backend.env.local, odoo.env.local y compose.env.local existen y están ignorados (git check-ignore). User Secrets se leyó solo para login; no se imprimió ni modificó. Identidad de autores Git y metadatos históricos del grupo no se redistribuyen en este informe. Paquetes/lockfile no cambiaron respecto a main; no se instaló dependencia ni se ejecutó auditoría de vulnerabilidades en línea. Versiones declaradas y referencias inspeccionadas, builds actuales correctos; no afirmar ausencia de vulnerabilidades por eso.

Scripts: run-week3.py solo compone entorno/inicia API; pruebas del lanzador solo sintéticas. prepare-week3.py escribe configuración/DB/credenciales y no se ejecutó. odoo-init.sh/setup_pos.py y prueba-temprana-venta.sh escriben en Odoo y no se ejecutaron. verify-week3.py, aun sin --write, compila, inicia API5049 y produce log; con --write puede enviar POST y actualizar evidencia: no ejecutado. Se usaron suites inspeccionadas y HTTP de lectura sobre API existente. No comprobación destructiva ni arranque limpio sobre volúmenes reales.

## 8. P09 histórico y trazabilidad actual

`docs/evidencias/semana3-p09.json`, verificado históricamente a `2026-11-02T02:23:02Z`: UUID `0f21ba7c-8be1-4ed2-a053-4cf4132e4064`, orden 2, referencia Caja Principal - 000001, total/pagado 2,76 USD e impuesto 0,36. Dos líneas, un pago efectivo; result==repeatResult y detail==result.sale; uuidCount=1 y historyContainsSale. HTTP/JSON2 de orden/líneas/pagos son evidencia persistida, no capturas actuales de interfaz Odoo.

AHORA se leyó el detalle ID 2 mediante la API real: los campos coinciden con el histórico, incluyendo UUID, líneas y método, salvo state=done frente a paid. No se atribuye causa del cambio ni una fecha no disponible. No implica pérdida de pago: paid/total siguen 2,76 y workflow admite done. No se repitió POST ni el conteo remoto UUID como prueba de concurrencia actual.

`semana3-p09-rejected.json` conserva el rechazo histórico HTTP422, reconciledUuidCount=0 y causa de redondeo; no es una venta exitosa adicional. Las evidencias `20261025-145140-*` son semana 1, no resultados actuales. La referencia manual de una orden posterior aportada por el usuario no sustituye P09 ni se registra como venta creada por el auditor.

## 9. Riesgos residuales y transferencia

- Diario local: conservar ruta durable y respaldos; no borrar claims para resolver incertidumbre. Si falta/corrompe un claim/resultado, revisión manual; sin promesa distribuida o entre UUID diferentes.
- Fiscalidad limitada: centavos, efectivo exacto, porcentaje excluido simple, sin listas/posiciones fiscales complejas ni vuelto; backend rechaza configuraciones no soportadas.
- Stock cero: **TRANSFERENCIA SEMANA 4**, condicionado al comportamiento real de Odoo y a una política de inventario acordada. No afirmar bloqueo de venta por stock si Odoo no lo garantiza. P04 actual sí verifica rechazo de producto deshabilitado y no requiere inventario integral.
- Aceptación manual reportada favorable; conservar evidencia del usuario sobre teclado/foco/anchuras. Ninguna certificación gráfica realizada por Codex.
- Pruebas finales de escritura, recuperación destructiva o instalación desde cero requieren entorno y autorización específicos; no se provocaron para esta auditoría.

## 10. Puerta de publicación y pasos del usuario

Verificados: rama correcta, 14 commits preservados, búsqueda dinámica confirmada en HEAD, 238 pruebas aprobadas, builds correctos, lecturas HTTP y P09 trazables, main intacta, sin secretos reales detectados en alcance revisado ni escrituras comerciales de auditoría.

**Bloqueo previo a publicar: resolver H01 mediante una intervención posterior autorizada sobre README y revisar la guía resultante.** Después, reevaluar la puerta. H02/H03 no exigen ampliar negocio ni rehacer el flujo. P10/defensa/pruebas finales corresponden a coordinación de semana 4.

Arranque correcto si no hay servicios activos (en este equipo ya están activos): desde raíz `python -B scripts/run-week3.py`; otra terminal `npm --prefix frontend run dev -- --host localhost --port 5173 --strictPort`. No volver a ejecutar inicializadores para usar los servicios existentes.

Comandos sugeridos para el usuario **solo después de resolver el bloqueo y autorizar publicación**, no ejecutados por el auditor:

```powershell
git status --short --branch
git log --oneline main..semana3-ventas
git diff --check main...semana3-ventas
# Revisar y confirmar selectivamente el informe y las correcciones autorizadas antes del envío.
git push origin semana3-ventas
```

No se propone push a main ni merge automático. Este informe queda sin commit para revisión del usuario; código, configuración, main y HEAD conservados.


## 11. Reevaluación documental SDD-03.6 — 2026-11-02

**Veredicto posterior: LISTA CON OBSERVACIONES NO BLOQUEANTES.** H01 (P1) queda resuelto para la preparación documental de la rama. No se detectó un bloqueo adicional en esta intervención. La publicación sigue siendo una acción posterior del usuario: no se ejecutó push ni merge.

Las secciones 1–10 conservan íntegramente la auditoría original y su veredicto **NO LISTA PARA PUBLICAR** como evidencia histórica. Su contenido previo a esta adición tiene SHA-256 `c37602778cd4d53afbc85848d9bd60919608fc03e88aeb9bacabc1c7336d40a6`. Sus expresiones «AHORA», estado Git sin commit y conteos describen aquella ejecución, no esta reevaluación. El archivo de auditoría estaba sin registrar al comenzar SDD-03.6; se incluye ahora en el commit documental autorizado junto al README.

### Resolución del bloqueo original

| Contradicción H01 | Corrección aplicada en README.md | Evidencia de correspondencia |
|---|---|---|
| Estado inicial Semana 2 y dos guías de arranque incompatibles | Una guía vigente de semana 3; separación de preparación inicial, ejecución habitual, recuperación y operaciones con efectos | Rutas de scripts comprobadas; run-week3.py, prepare-week3.py y compose inspeccionados completos |
| Inicializador antiguo presentado como primer paso, con defaults administrativos | odoo-init.sh y prueba-temprana-venta.sh quedan únicamente como antecedentes no ejecutables del arranque habitual; no se publican credenciales administrativas | No se ejecutó ninguno; el flujo vigente usa prepare-week3 solo para instancia nueva y override local para servicios preparados |
| Uso normal mezclado con reinicialización | Uso habitual de Compose con proyecto/override conservados; control previo de puertos/PID y Vite strictPort; recuperación de archivos en vez de borrar base/credenciales | `docker compose ... config --quiet`: salida 0, sin iniciar ni modificar servicios |
| Credenciales POS atribuidas exclusivamente al archivo de respaldo | Tabla de precedencia por clave y distinción entre operador, integración, firma JWT y administración Odoo | build_environment/read_user_secrets; Program.cs; csproj/UserSecretsId; ConfiguredOperatorAuthenticator; LoginUseCase; JwtOptions |
| Información antigua confundida con estado actual | Conteos identificados por fecha de auditoría, rutas REST actuales e instrucciones de pruebas sin repetir P09 | Enlaces, proyectos y scripts npm comprobados; sin prometer validación visual o instalación desde cero |

Precedencia comprobada leyendo código: con **run-week3.py**, variables explícitas heredadas > User Secrets en Development > respaldo backend.env.local inyectado por clave ausente > appsettings del entorno > appsettings base. El lanzador normaliza `__`/`:` y mayúsculas; excluye DevelopmentAdminPassword. DOTNET_ENVIRONMENT prevalece sobre ASPNETCORE_ENVIRONMENT y el lanzador fija Development solo si ASPNETCORE_ENVIRONMENT falta. Fuera de Development no protege claves de User Secrets. La regla general .NET incluye argumentos de línea de comandos sobre entorno; el lanzador actual solo pasa la URL y no reenvía argumentos arbitrarios. El archivo local no es un proveedor JSON agregado a Program.cs: lo transforma el lanzador en variables. Los archivos appsettings.*.local.json no se cargan automáticamente.

La inspección de verify-week3.py mostró una diferencia ya existente, ahora documentada: ese verificador actualiza directamente el entorno con el respaldo local y no reproduce la precedencia del lanzador habitual. No se recomienda como arranque ni se ejecutó en esta fase. Esta distinción no requiere cambiar código/configuración.

### Verificaciones ejecutadas en SDD-03.6

Fecha/hora de comprobación de máquina: **2026-11-02 01:51:40 −05:00**. Repositorio correcto, rama semana3-ventas; HEAD inicial 28afbbac515f6b2a7e61458a90772a8568469583 y main 9c84c3e3230d58ff74cbf241a5d8d94a187ddca7.

| Comando / comprobación | Resultado |
|---|---|
| `python -B scripts/test_run_week3.py` | 6 pruebas existentes aprobadas, 0 fallidas/omitidas, salida 0; solo datos sintéticos, sin iniciar API |
| `docker compose -p patrones-semana3 --env-file infra/.env -f infra/docker-compose.yml -f infra/compose.env.local config --quiet` | Salida 0; comprobación de configuración en silencio, sin imprimir secretos ni operar contenedores |
| Verificador Python en memoria de README | Salida 0: existen scripts/proyectos, destinos Markdown y comandos dev/test/build de package.json; no credenciales locales actuales ni patrones de JWT/clave privada/URL autenticada en el texto |
| `git diff --check` | Salida 0, sin incidencias |
| Alcance de Git antes del commit | Solo README.md modificado y auditoría sin registrar; staging explícito de esos dos documentos |
| Preservación del informe inicial | Prefijo de bytes íntegro comprobado por SHA-256 antes y después de anexar esta sección |

No se repitieron las suites React/.NET, compilaciones ni HTTP/P09: el cambio es exclusivamente documental. Los **238 casos y builds correctos** de las secciones anteriores siguen siendo resultados de AUD-03-FINAL, no ejecuciones nuevas de SDD-03.6. Tampoco se certificó un aprovisionamiento nuevo: se contrastaron comandos con implementación y se validó Compose sin iniciar servicios.

### Alcance y riesgos restantes

Archivos corregidos: README.md y esta auditoría (solo sección añadida). Sin cambios en código fuente, tests, contratos, AGENTS.md, secretos, configuración o datos de Odoo. Los commits anteriores y main se conservan; se autoriza únicamente un commit local `docs(week3): resolve README audit blocker and close release review`.

H02 permanece **P2 no bloqueante**: botón de recotización, sin modificación UX. H03 mejora parcialmente al actualizar README/fechas/contratos; fichas finales de decisiones, P10 y defensa se coordinan en semana 4. Stock cero sigue reservado a esa fase, condicionado a Odoo; no se incorpora ni se promete una regla nueva. La protección de volúmenes, archivos locales y diario, límites fiscales y garantía idempotente de un host permanecen vigentes. La validación gráfica pertenece al usuario, no fue realizada por el agente.

Puerta documental: H01 resuelto y guía consistente con el código inspeccionado; no P0/P1 pendientes identificados dentro de SDD-03.6. Rama apta para publicación **con las observaciones no bloqueantes registradas**, conservando las condiciones y responsabilidades de semana 4. Ninguna publicación o integración de ramas se ejecuta automáticamente.
