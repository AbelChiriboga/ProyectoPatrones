-- ============================================================================
-- Esquema de base de datos para pruebas de carga y rendimiento (1,000,000 registros)
-- Representa las tablas críticas del POS integrado respetando contratos de Odoo/PostgreSQL
-- ============================================================================

-- 1. Tabla de Productos (product_product)
CREATE TABLE IF NOT EXISTS product_product (
    id SERIAL PRIMARY KEY,
    default_code VARCHAR(64) NOT NULL,
    name VARCHAR(256) NOT NULL,
    barcode VARCHAR(64),
    lst_price NUMERIC(12, 2) NOT NULL DEFAULT 0.00,
    available_in_pos BOOLEAN NOT NULL DEFAULT TRUE,
    sale_ok BOOLEAN NOT NULL DEFAULT TRUE,
    is_storable BOOLEAN NOT NULL DEFAULT FALSE,
    qty_available NUMERIC(12, 2) DEFAULT 0.00,
    taxes_id JSONB DEFAULT '[]'::jsonb
);

-- 2. Tabla de Clientes / Contactos (res_partner)
CREATE TABLE IF NOT EXISTS res_partner (
    id SERIAL PRIMARY KEY,
    name VARCHAR(256) NOT NULL,
    email VARCHAR(128),
    vat VARCHAR(32),
    phone VARCHAR(32),
    active BOOLEAN NOT NULL DEFAULT TRUE,
    create_date TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 3. Tabla de Ventas / Órdenes POS (pos_order)
CREATE TABLE IF NOT EXISTS pos_order (
    id SERIAL PRIMARY KEY,
    uuid UUID NOT NULL UNIQUE,
    name VARCHAR(64) NOT NULL,
    pos_reference VARCHAR(64) NOT NULL,
    session_id INTEGER NOT NULL,
    config_id INTEGER NOT NULL DEFAULT 1,
    user_id INTEGER NOT NULL,
    employee_id INTEGER,
    partner_id INTEGER REFERENCES res_partner(id),
    date_order TIMESTAMP WITH TIME ZONE NOT NULL,
    amount_total NUMERIC(12, 2) NOT NULL,
    amount_tax NUMERIC(12, 2) NOT NULL DEFAULT 0.00,
    amount_paid NUMERIC(12, 2) NOT NULL DEFAULT 0.00,
    amount_return NUMERIC(12, 2) NOT NULL DEFAULT 0.00,
    state VARCHAR(32) NOT NULL DEFAULT 'paid',
    currency_id VARCHAR(8) NOT NULL DEFAULT 'USD'
);

-- 4. Tabla de Líneas de Venta (pos_order_line)
CREATE TABLE IF NOT EXISTS pos_order_line (
    id SERIAL PRIMARY KEY,
    order_id INTEGER NOT NULL REFERENCES pos_order(id) ON DELETE CASCADE,
    product_id INTEGER NOT NULL REFERENCES product_product(id),
    qty NUMERIC(12, 2) NOT NULL,
    price_unit NUMERIC(12, 2) NOT NULL,
    discount NUMERIC(5, 2) NOT NULL DEFAULT 0.00,
    price_subtotal NUMERIC(12, 2) NOT NULL,
    price_subtotal_incl NUMERIC(12, 2) NOT NULL
);

-- 5. Tabla de Pagos POS (pos_payment)
CREATE TABLE IF NOT EXISTS pos_payment (
    id SERIAL PRIMARY KEY,
    pos_order_id INTEGER NOT NULL REFERENCES pos_order(id) ON DELETE CASCADE,
    payment_method_id INTEGER NOT NULL,
    payment_method_name VARCHAR(64) NOT NULL DEFAULT 'Efectivo',
    amount NUMERIC(12, 2) NOT NULL,
    payment_date TIMESTAMP WITH TIME ZONE NOT NULL
);

-- ============================================================================
-- ÍNDICES OPTIMIZADOS (Compuestos, Cubrientes y Keyset)
-- ============================================================================

-- Optimización Consulta 1: Búsqueda en catálogo POS
-- Índice compuesto sobre disponibilidad y ordenamiento por nombre
CREATE INDEX IF NOT EXISTS idx_products_pos_search 
ON product_product (sale_ok, available_in_pos, name);

-- Índice por código de producto para búsqueda rápida exacta / prefijo
CREATE INDEX IF NOT EXISTS idx_products_code 
ON product_product (default_code) 
WHERE sale_ok = TRUE AND available_in_pos = TRUE;

-- Optimización Consulta 2: Historial de ventas con paginación Keyset (id < cursor) y filtro por cajero
-- Índice compuesto cubriente (config_id, user_id, id DESC)
CREATE INDEX IF NOT EXISTS idx_pos_order_history_keyset 
ON pos_order (config_id, user_id, id DESC);

-- Índice para búsqueda de referencia en historial
CREATE INDEX IF NOT EXISTS idx_pos_order_reference 
ON pos_order (config_id, pos_reference);

-- Optimización Consulta 3: Reportes globales consolidados (rango de fechas + agregación por cajero)
CREATE INDEX IF NOT EXISTS idx_pos_order_reports_date 
ON pos_order (config_id, date_order, amount_total, user_id);

-- Optimización Consulta 4: Pagos para reporte consolidado por método de pago
CREATE INDEX IF NOT EXISTS idx_pos_payment_report 
ON pos_payment (pos_order_id, payment_method_id, amount);
