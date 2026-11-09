#!/usr/bin/env bash
# Прогін тестів ЛІС на PostgreSQL на стенді (БД medlink_lab_test поряд із СУБД: без повільного тунелю).
# Запуск з кореня репозиторію у Git Bash:
#   bash tools/deploy/pg-test-on-server.sh [фільтр dotnet test]     — лише PostgreSQL (LIS_TEST_PG), повний журнал: /tmp/lab-test-pg.log на сервері
#   bash tools/deploy/pg-test-on-server.sh --both [фільтр]          — ОБИДВА провайдери: спочатку SQLite (dev/CI), потім PostgreSQL; код виходу ≠ 0, якщо впав будь-який
# Пароль ролі lab_stand береться з ~/.medlink/lab_pg_pw.txt і на екран не виводиться.
# Увага: тести ПОВНІСТЮ перестворюють схеми public і guide у БД medlink_lab_test (не в основній medlink_lab!).
set -euo pipefail
cd "$(dirname "$0")/../.."
KEY=~/.ssh/medone_ed25519
HOST=root@94.130.180.117
BOTH=0
if [ "${1:-}" = "--both" ]; then BOTH=1; shift; fi
FILTER="${1:-}"
PW="$(cat ~/.medlink/lab_pg_pw.txt)"
TMP="$(mktemp -d)"

echo "==> архів вихідного коду (без bin/obj/node_modules/wwwroot)"
tar --exclude=App_Data --exclude=bin --exclude=obj --exclude=node_modules --exclude=wwwroot --exclude=.git --exclude=.quasar --exclude=dist -czf "$TMP/src.tgz" src tests db docs/swagger.json MedLink.LIS.sln Directory.Build.props 2>/dev/null || tar --exclude=App_Data --exclude=bin --exclude=obj --exclude=node_modules --exclude=wwwroot --exclude=.git --exclude=.quasar --exclude=dist -czf "$TMP/src.tgz" src tests db MedLink.LIS.sln Directory.Build.props
scp -q -i "$KEY" "$TMP/src.tgz" "$HOST:/tmp/lab-src.tgz"

echo "==> тести на сервері ($([ "$BOTH" = 1 ] && echo 'SQLite + PostgreSQL' || echo PostgreSQL))"
set +e
ssh -i "$KEY" "$HOST" "PGPW='$PW' FILTER='$FILTER' BOTH='$BOTH' bash -s" <<'REMOTE'
set -e
rm -rf /tmp/lab-src && mkdir -p /tmp/lab-src && tar -xzf /tmp/lab-src.tgz -C /tmp/lab-src && cd /tmp/lab-src
export DOTNET_ROOT=/opt/dotnet8-lab PATH=/opt/dotnet8-lab:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_ROLL_FORWARD=LatestPatch
FARGS=(); [ -n "$FILTER" ] && FARGS=(--filter "$FILTER")
rc=0
run() { # $1 = назва, $2 = файл журналу
  dotnet test tests/MedLink.LIS.Tests "${FARGS[@]}" --nologo --logger "console;verbosity=normal" > "$2" 2>&1 || rc=1
  echo "----- $1: підсумок"; grep -E "^\s*(Total tests|Passed:|Failed:|Skipped:)|error CS" "$2" | head -20
  echo "----- $1: впалі тести (перші повідомлення; повний журнал: $2)"
  grep -E "^\s+Failed |Error Message" -A2 "$2" | grep -vE "^--$|Stack Trace" | cut -c1-300 | head -${FAILLINES:-120}
}
if [ "$BOTH" = 1 ]; then
  unset LIS_TEST_PG
  run "SQLite" /tmp/lab-test-sqlite.log
fi
export LIS_TEST_PG="Host=127.0.0.1;Port=5432;Database=medlink_lab_test;Username=lab_stand;Password=$PGPW;Include Error Detail=true"
run "PostgreSQL" /tmp/lab-test-pg.log
exit $rc
REMOTE
rc=$?
rm -rf "$TMP"
exit $rc
