#!/usr/bin/env bash
# Прогін тестів ЛІС на PostgreSQL на стенді (БД medlink_lab_test поряд із СУБД: без повільного тунелю).
# Запуск з кореня репозиторію у Git Bash:   bash tools/deploy/pg-test-on-server.sh [фільтр dotnet test]
# Пароль ролі lab_stand береться з ~/.medlink/lab_pg_pw.txt і на екран не виводиться.
set -euo pipefail
cd "$(dirname "$0")/../.."
KEY=~/.ssh/medone_ed25519
HOST=root@94.130.180.117
FILTER="${1:-}"
PW="$(cat ~/.medlink/lab_pg_pw.txt)"
TMP="$(mktemp -d)"

echo "==> архів вихідного коду (без bin/obj/node_modules/wwwroot)"
tar --exclude=bin --exclude=obj --exclude=node_modules --exclude=wwwroot --exclude=.git --exclude=.quasar --exclude=dist -czf "$TMP/src.tgz" src tests db docs/swagger.json 2>/dev/null || tar --exclude=bin --exclude=obj --exclude=node_modules --exclude=wwwroot --exclude=.git --exclude=.quasar --exclude=dist -czf "$TMP/src.tgz" src tests db
scp -q -i "$KEY" "$TMP/src.tgz" "$HOST:/tmp/lab-src.tgz"

echo "==> тести на сервері (PostgreSQL)"
ssh -i "$KEY" "$HOST" "PGPW='$PW' FILTER='$FILTER' bash -s" <<'REMOTE'
set -e
rm -rf /tmp/lab-src && mkdir -p /tmp/lab-src && tar -xzf /tmp/lab-src.tgz -C /tmp/lab-src && cd /tmp/lab-src
export DOTNET_ROOT=/opt/dotnet8-lab PATH=/opt/dotnet8-lab:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_ROLL_FORWARD=LatestPatch
export LIS_TEST_PG="Host=127.0.0.1;Port=5432;Database=medlink_lab_test;Username=lab_stand;Password=$PGPW;Include Error Detail=true"
if [ -n "$FILTER" ]; then dotnet test tests/MedLink.LIS.Tests --filter "$FILTER" --nologo --logger "console;verbosity=normal" 2>&1 | grep -vE "warning|^\s*$" | tail -60
else dotnet test tests/MedLink.LIS.Tests --nologo --logger "console;verbosity=normal" 2>&1 | grep -vE "warning|^\s*$" | tail -400; fi
REMOTE
rm -rf "$TMP"
