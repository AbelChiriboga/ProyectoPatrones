# Preparación reproducible de Odoo 20 Community para el POS del proyecto.
# Uso: docker compose exec -T odoo odoo shell -d pos_db --db_host=db --db_user=... --db_password=... --no-http < odoo/setup_pos.py
# Es idempotente: si los registros ya existen los reutiliza.
from datetime import datetime, timedelta

company = env.company
company.write({'name': 'POS Patrones de Software'})

iva = env['account.tax'].search([('type_tax_use', '=', 'sale'), ('amount', '=', 15.0)], limit=1)

products_data = [
    ('CAFE-001', 'Café americano', 1.50),
    ('SAND-001', 'Sánduche de pollo', 3.75),
    ('JUGO-001', 'Jugo natural de naranja', 2.25),
    ('GALL-001', 'Galletas de avena', 0.90),
]
for code, name, price in products_data:
    product = env['product.product'].search([('default_code', '=', code)], limit=1)
    if not product:
        env['product.product'].create({
            'name': name,
            'default_code': code,
            'list_price': price,
            'sale_ok': True,
            'available_in_pos': True,
            'taxes_id': [(6, 0, iva.ids)],
        })

cash_journal = env['account.journal'].search([('type', '=', 'cash'), ('company_id', '=', company.id)], limit=1)
if not cash_journal:
    cash_journal = env['account.journal'].create({'name': 'Caja POS', 'type': 'cash', 'code': 'CPOS'})

cash_method = env['pos.payment.method'].search([('name', '=', 'Efectivo')], limit=1)
if not cash_method:
    cash_method = env['pos.payment.method'].create({'name': 'Efectivo', 'type': 'cash', 'journal_id': cash_journal.id})

config = env['pos.config'].search([('name', '=', 'Caja Principal')], limit=1)
if not config:
    config = env['pos.config'].create({
        'name': 'Caja Principal',
        'payment_method_ids': [(6, 0, cash_method.ids)],
    })

# Usuario de integración: solo permisos de usuario POS (mínimo privilegio).
integration_user = env['res.users'].search([('login', '=', 'pos_integration')], limit=1)
if not integration_user:
    integration_user = env['res.users'].create({
        'name': 'Integración POS .NET',
        'login': 'pos_integration',
        'group_ids': [(6, 0, [env.ref('base.group_user').id, env.ref('point_of_sale.group_pos_user').id])],
    })

session = config.current_session_id
if not session:
    session = env['pos.session'].create({'user_id': integration_user.id, 'config_id': config.id})
if session.state == 'opening_control':
    session.set_opening_control(0.0, 'Apertura de prueba - semana 1')

api_key = env['res.users.apikeys'].with_user(integration_user)._generate(
    'rpc', 'POS .NET backend', datetime.now() + timedelta(days=90))

env.cr.commit()
print('RESULT company=%s currency=%s' % (company.name, company.currency_id.name))
print('RESULT tax_id=%s config_id=%s session_id=%s session_state=%s cash_method_id=%s' % (
    iva.id, config.id, session.id, session.state, cash_method.id))
for p in env['product.product'].search([('available_in_pos', '=', True)]):
    print('RESULT product id=%s code=%s name=%s price=%s' % (p.id, p.default_code, p.name, p.list_price))
print('RESULT integration_user_id=%s' % integration_user.id)
print('APIKEY %s' % api_key)
