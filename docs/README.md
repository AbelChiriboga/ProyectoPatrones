# Documentación del Proyecto POS Odoo 20 — Semana 4 / Cierre Final

Este directorio consolida la documentación técnica, arquitectónica y de verificación del sistema POS integrado con Odoo 20 Community.

---

## Índice Documental

1. **[Matriz de Trazabilidad Integral](MATRIZ_TRAZABILIDAD.md)**  
   Mapeo exhaustivo de los 21 Requisitos Funcionales (RF-01 a RF-21), 8 Reglas de Negocio (RN-01 a RN-08), Requisitos No Funcionales (Rendimiento 1M, Red LAN, Versionamiento/OpenAPI, Seguridad, Usabilidad), Pruebas Mínimas P01 a P15, patrones de diseño aplicados y commits del repositorio.

2. **[Decisiones de Diseño](DECISIONES_DISENO.md)**  
   Fundamentación de las 9 decisiones arquitectónicas del sistema, incluyendo análisis de problemas, alternativas descartadas, consecuencias, trade-offs y pruebas que las validan:
   - Decisión 1: Adapter / Anti-Corruption Layer con Odoo 20.
   - Decisión 2: Strategy GoF para políticas de descuento.
   - Decisión 3: Idempotent Operation Journal durable ante fallos de red.
   - Decisión 4: Enum de dominio y Presenter MVP en frontend.
   - Decisión 5: Policy-Based Authorization para roles JWT (Admin / Cashier).
   - Decisión 6: Keyset Pagination e índices cubrientes para 1M registros.
   - Decisión 7: Versionamiento dual (/api/v1 y /api) y OpenAPI 3.0 / Swagger UI.
   - Decisión 8: Arquitectura de red local (LAN) con CORS dinámico RFC 1918.
   - Decisión 9: Flujo POS Odoo ágil con caché reactiva y calculadora de cambio.

3. **[Benchmark Masivo 1M de Registros](BENCHMARK_1M.md)**  
   Evidencia empírica de rendimiento y planes de ejecución bajo 5,000,000 de filas en PostgreSQL aislado:
   - Búsqueda en catálogo POS: 18.2 ms
   - Paginación de historial: 8.4 ms
   - Reporte consolidado financiero: 680.5 ms
   - Todas las consultas responden en menos de 1 segundo ($\ll 3$ s requerido).

4. **[Guía de Despliegue en Red Local (LAN)](GUIA_DESPLIEGUE_LAN.md)**  
   Checklist de conectividad, configuración de firewall de Windows/Linux, comandos de arranque (`start_lan.ps1`) y script automatizado de verificación (`verify_lan.py`) para operar en esquema multi-computadora (Servidor y Caja).

5. **[Especificación SDD](sdd/semana3/especificacion.md) y [Seguimiento de Progreso](sdd/semana3/PROGRESS.md)**  
   Bitácora técnica ágil y especificación contractual del sistema.

---

## Resumen de Verificación Automatizada

- **Total de pruebas automatizadas aprobadas:** **306 pruebas (100% OK)**
  - 139 pruebas .NET 10 (`backend/tests/Pos.UnitTests`)
  - 156 pruebas React (`frontend/src`)
  - 11 pruebas Python (`scripts/test_run_week3.py`, `scripts/test_lan_scripts.py`)
- **Compilación de producción:** Backend y Frontend limpios (0 errores).
