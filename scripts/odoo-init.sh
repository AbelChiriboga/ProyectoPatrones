#!/usr/bin/env bash
# Crea la base pos_db con el módulo point_of_sale y aplica la configuración del POS (infra/odoo/setup_pos.py).
# Al final imprime la API key del usuario de integración UNA sola vez: guárdela con dotnet user-secrets.
set -euo pipefail
cd "$(dirname "$0")/../infra"
source .env
ARGS=(--db_host=db --db_user="$POSTGRES_USER" --db_password="$POSTGRES_PASSWORD")
docker compose up -d
docker compose exec -T odoo odoo -d pos_db -i point_of_sale --without-demo=all --load-language=es_EC --stop-after-init "${ARGS[@]}"
docker compose exec -T odoo odoo shell -d pos_db --no-http "${ARGS[@]}" < odoo/setup_pos.py 2>/dev/null | grep -E '^(RESULT|APIKEY)'
docker compose restart odoo
