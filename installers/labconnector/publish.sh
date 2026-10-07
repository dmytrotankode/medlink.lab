#!/usr/bin/env bash
# =============================================================================
# MedLink LIS Analyzer Connector — публікація self-contained збірок
#   ./publish.sh                 # win-x64, linux-x64, linux-arm64
#   ./publish.sh linux-x64       # лише один RID
#   SINGLE_FILE=1 ./publish.sh   # один виконуваний файл (PublishSingleFile)
# Результат: artifacts/labconnector/<rid>/ (+ копія інсталяційних скриптів)
# Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
# =============================================================================
set -euo pipefail
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PROJECT="$ROOT/src/MedLink.LabConnector/MedLink.LabConnector.csproj"
OUT_BASE="$ROOT/artifacts/labconnector"
RIDS=("$@")
[[ ${#RIDS[@]} -eq 0 ]] && RIDS=(win-x64 linux-x64 linux-arm64)
SINGLE="${SINGLE_FILE:-0}"
VERSION="$(grep -oP '(?<=<Version>)[^<]+' "$PROJECT" | head -1 || echo 4.0.0)"

echo "MedLink LIS Analyzer Connector v$VERSION — публікація: ${RIDS[*]}"
for RID in "${RIDS[@]}"; do
  OUT="$OUT_BASE/$RID"
  rm -rf "$OUT"
  echo "==> $RID → $OUT"
  dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained true -o "$OUT" \
    -p:PublishSingleFile="$([[ $SINGLE == 1 ]] && echo true || echo false)" \
    -p:PublishReadyToRun=false -p:DebugType=embedded -p:Version="$VERSION" \
    -nologo -v minimal
  rm -f "$OUT/appsettings.Development.json"
  rm -rf "$OUT/data"
  # інсталяційні скрипти та інструкція поруч із бінарником
  case "$RID" in
    win-*) cp "$SCRIPT_DIR/install-windows.ps1" "$SCRIPT_DIR/uninstall-windows.ps1" "$OUT/";;
    linux-*) cp "$SCRIPT_DIR/install-linux.sh" "$SCRIPT_DIR/medlink-labconnector.service" "$OUT/"; chmod +x "$OUT/install-linux.sh" "$OUT/MedLink.LabConnector" 2>/dev/null || true;;
  esac
  cp "$SCRIPT_DIR/README_UA.md" "$SCRIPT_DIR/license.txt" "$OUT/"
  ( cd "$OUT_BASE" && tar -czf "medlink-labconnector_${VERSION}_${RID}.tar.gz" "$RID" ) 2>/dev/null || true
done
echo "Готово: $OUT_BASE"
ls -1 "$OUT_BASE"
