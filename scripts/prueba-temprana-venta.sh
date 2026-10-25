#!/usr/bin/env bash
# Prueba temprana (semana 1): registra en Odoo 20 una venta POS de dos productos con pago en efectivo
# mediante JSON-2 -> pos.order.sync_from_ui, y luego la verifica leyendo pos.order, líneas y pagos.
# Requiere: ODOO_URL, ODOO_DB, ODOO_API_KEY (variables de entorno; la clave nunca se versiona).
# Parámetros opcionales: SESSION_ID, PAYMENT_METHOD_ID, PRODUCT_A, PRODUCT_B, TAX_ID.
set -euo pipefail
: "${ODOO_URL:=http://localhost:8069}" "${ODOO_DB:=pos_db}" "${ODOO_API_KEY:?Defina ODOO_API_KEY}"
: "${SESSION_ID:=2}" "${PAYMENT_METHOD_ID:=3}" "${PRODUCT_A:=11}" "${PRODUCT_B:=12}" "${TAX_ID:=1}"
OUT_DIR="$(dirname "$0")/../docs/evidencias"
mkdir -p "$OUT_DIR"
UUID="$(cat /proc/sys/kernel/random/uuid 2>/dev/null || powershell -NoProfile -Command '[guid]::NewGuid().ToString()' | tr -d '\r')"
NOW="$(date -u '+%Y-%m-%d %H:%M:%S')"

call() { # $1 = modelo/método, $2 = cuerpo JSON
  curl -sS -X POST "$ODOO_URL/json/2/$1" \
    -H "Authorization: bearer $ODOO_API_KEY" -H "X-Odoo-Database: $ODOO_DB" \
    -H "Content-Type: application/json" -d "$2" -w '\nHTTP %{http_code}\n'
}

# Café americano 2 x 1.50 = 3.00 ; Sánduche 1 x 3.75 = 3.75 ; base 6.75 ; IVA 15 % = 1.0125 ; total 7.76
REQUEST=$(cat <<JSON
{"orders": [{
  "uuid": "$UUID", "session_id": $SESSION_ID, "state": "paid", "date_order": "$NOW",
  "amount_tax": 1.01, "amount_total": 7.76, "amount_paid": 7.76, "amount_return": 0,
  "lines": [
    [0, 0, {"product_id": $PRODUCT_A, "qty": 2, "price_unit": 1.50, "discount": 0,
            "price_subtotal": 3.00, "price_subtotal_incl": 3.45, "tax_ids": [[6, 0, [$TAX_ID]]]}],
    [0, 0, {"product_id": $PRODUCT_B, "qty": 1, "price_unit": 3.75, "discount": 0,
            "price_subtotal": 3.75, "price_subtotal_incl": 4.31, "tax_ids": [[6, 0, [$TAX_ID]]]}]
  ],
  "payment_ids": [[0, 0, {"payment_method_id": $PAYMENT_METHOD_ID, "amount": 7.76, "payment_date": "$NOW"}]]
}]}
JSON
)

STAMP="$(date '+%Y%m%d-%H%M%S')"
echo "$REQUEST" > "$OUT_DIR/$STAMP-01-sync_from_ui-request.json"
echo "== 1) sync_from_ui (uuid $UUID)"
call pos.order/sync_from_ui "$REQUEST" | tee "$OUT_DIR/$STAMP-02-sync_from_ui-response.txt" | head -c 600; echo

echo "== 2) Repetición con el mismo uuid (no debe crear una segunda venta)"
call pos.order/sync_from_ui "$REQUEST" > "$OUT_DIR/$STAMP-03-sync_from_ui-repeticion.txt"
call pos.order/search_count "{\"domain\": [[\"uuid\", \"=\", \"$UUID\"]]}" | tee "$OUT_DIR/$STAMP-04-conteo-por-uuid.txt"

echo "== 3) Verificación de la orden registrada"
call pos.order/search_read "{\"domain\": [[\"uuid\", \"=\", \"$UUID\"]], \"fields\": [\"id\", \"name\", \"pos_reference\", \"state\", \"amount_total\", \"amount_tax\", \"amount_paid\", \"session_id\", \"lines\", \"payment_ids\"]}" \
  | tee "$OUT_DIR/$STAMP-05-orden-verificada.txt"
call pos.order.line/search_read "{\"domain\": [[\"order_id.uuid\", \"=\", \"$UUID\"]], \"fields\": [\"product_id\", \"qty\", \"price_unit\", \"price_subtotal\", \"price_subtotal_incl\"]}" \
  | tee "$OUT_DIR/$STAMP-06-lineas.txt"
call pos.payment/search_read "{\"domain\": [[\"pos_order_id.uuid\", \"=\", \"$UUID\"]], \"fields\": [\"payment_method_id\", \"amount\", \"payment_date\"]}" \
  | tee "$OUT_DIR/$STAMP-07-pagos.txt"
