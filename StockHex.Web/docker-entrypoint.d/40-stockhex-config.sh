#!/bin/sh
# Escribe la configuración que lee el navegador. Se ejecuta en cada arranque del
# contenedor, así que la misma imagen sirve contra cualquier API sin recompilar.
set -eu

CONFIG=/usr/share/nginx/html/config.js

# Vacío = mismo origen, que es el caso normal: nginx hace de proxy de /api.
# Se define API_URL sólo si el frontend debe pegarle a una API en otro dominio.
API_URL="${API_URL:-}"

cat > "$CONFIG" <<JS
window.__STOCKHEX_CONFIG__ = { apiUrl: "${API_URL}" };
JS

echo "stockhex: config.js escrito con apiUrl=\"${API_URL}\" (vacío = mismo origen)"
