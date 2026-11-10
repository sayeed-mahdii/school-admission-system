#!/bin/sh
set -eu

API_URL_VALUE="${API_URL:-${VITE_API_URL:-http://localhost:8080}}"
ESCAPED_API_URL=$(printf '%s' "$API_URL_VALUE" | sed 's/\\/\\\\/g; s/"/\\"/g')

cat <<EOF > /usr/share/nginx/html/config.js
window.__APP_CONFIG__ = { API_URL: "${ESCAPED_API_URL}" };
EOF

