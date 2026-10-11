#!/usr/bin/env bash
# SessionStart hook: готує середовище хмарної сесії Claude Code (dotnet 8, node deps).
set -u
cd "$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
if ! command -v dotnet >/dev/null 2>&1; then
  (apt-get update -qq && DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-8.0) >/tmp/dotnet-install.log 2>&1 || echo "dotnet install failed (see /tmp/dotnet-install.log)"
fi
command -v dotnet >/dev/null 2>&1 && dotnet restore MedLink.LIS.sln >/tmp/dotnet-restore.log 2>&1 &
if command -v npm >/dev/null 2>&1 && [ -f src/MedLink.LIS.Web/package.json ] && [ ! -d src/MedLink.LIS.Web/node_modules ]; then
  (cd src/MedLink.LIS.Web && npm ci --no-audit --no-fund >/tmp/npm-ci.log 2>&1) &
fi
wait
echo "MedLink LIS: середовище готове (dotnet $(dotnet --version 2>/dev/null || echo '-'), node $(node -v 2>/dev/null || echo '-'))"
