#!/bin/sh
set -eu

INDEX_FILE="/usr/share/nginx/html/index.html"

if [ -f "$INDEX_FILE" ]; then
  # Replace runtime placeholder with container env var.
  # The placeholder token must exist in the built index.html.
  # shellcheck disable=SC2001
  sed -i "s|__VITE_API_URL__|${VITE_API_URL:-}|g" "$INDEX_FILE"
fi

exec "$@"
