#!/usr/bin/env bash
# MedLink LIS 4.0 — збірка фронтенду (Quasar v1 SPA) та копіювання у wwwroot API.
# Використання: ./build.sh [--skip-install]
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WEB="$ROOT/src/MedLink.LIS.Web"
WWWROOT="$ROOT/src/MedLink.LIS.Api/wwwroot"

if [[ "${1:-}" != "--skip-install" ]]; then
  echo "==> npm install ($WEB)"
  (cd "$WEB" && npm install --no-audit --no-fund)
fi

echo "==> quasar build (Node $(node -v), NODE_OPTIONS=--openssl-legacy-provider через cross-env)"
(cd "$WEB" && npm run build)

echo "==> копіювання dist/spa → $WWWROOT"
mkdir -p "$WWWROOT"
# очищення (залишаємо .gitkeep)
find "$WWWROOT" -mindepth 1 ! -name '.gitkeep' -exec rm -rf {} + 2>/dev/null || true
cp -R "$WEB/dist/spa/." "$WWWROOT/"
touch "$WWWROOT/.gitkeep"

echo "==> готово: $(find "$WWWROOT" -type f | wc -l) файлів у wwwroot"
