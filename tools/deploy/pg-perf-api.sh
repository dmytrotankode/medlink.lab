#!/usr/bin/env bash
# Заміри часу відповіді API на ТЕСТОВІЙ БД medlink_lab_test (після pg-test-on-server.sh і pg-perf-seed.sql).
# Запуск з ПК (Git Bash):  bash tools/deploy/pg-perf-api.sh
# На сервері піднімає ТИМЧАСОВИЙ екземпляр зібраного API (/tmp/lab-src/.../bin/Debug) на 127.0.0.1:5291 проти medlink_lab_test,
# міряє curl-ом основні маршрути (по 5 викликів, мс) і зупиняє екземпляр. Службу medlink-lab і основну БД не чіпає.
set -euo pipefail
KEY=~/.ssh/medone_ed25519; HOST=root@94.130.180.117
PW="$(cat ~/.medlink/lab_pg_pw.txt)"
ssh -i "$KEY" "$HOST" "PGPW='$PW' bash -s" <<'REMOTE'
set -e
cd /tmp/lab-src/src/MedLink.LIS.Api/bin/Debug/net8.0
export DOTNET_ROOT=/opt/dotnet8-lab PATH=/opt/dotnet8-lab:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ROLL_FORWARD=LatestPatch
export ASPNETCORE_ENVIRONMENT=Production Lab__SeedDemoData=false Kestrel__Endpoints__Http__Url=http://127.0.0.1:5291
export Database__Provider=Postgres "Database__ConnectionString=Host=127.0.0.1;Port=5432;Database=medlink_lab_test;Username=lab_stand;Password=$PGPW"
ss -ltn | grep -q ':5291 ' && { echo "порт 5291 зайнятий"; exit 1; }
nohup dotnet MedLink.LIS.Api.dll > /tmp/lab-perf-api.log 2>&1 &
PID=$!
trap 'kill $PID 2>/dev/null || true' EXIT
for i in $(seq 1 90); do curl -sf http://127.0.0.1:5291/health >/dev/null && break; sleep 1; done
echo "старт API: ~${i} с"
B=http://127.0.0.1:5291/api/v1/lab
t() { # назва, url
  local best=99999 sum=0
  for i in 1 2 3 4 5; do
    ms=$(curl -s -o /dev/null -w "%{time_total}" -H "X-MedLink-Employee-Id: 0e000000-0000-0000-0000-000000000001" "$2" | awk '{printf "%d", $1*1000}')
    sum=$((sum+ms)); [ "$ms" -lt "$best" ] && best=$ms
  done
  echo "$1: мін ${best} мс, середнє $((sum/5)) мс"
}
t "список замовлень (стор. 50)"            "$B/orders?pageSize=50"
t "список замовлень, статус NEW,RECEIVED"  "$B/orders?status=NEW,RECEIVED,IN_PROGRESS&pageSize=50"
t "пошук замовлення за номером (фрагмент)" "$B/orders?search=0054321&pageSize=50"
t "пошук замовлення за ПІБ"                "$B/orders?search=%D0%BA%D1%80%D0%B0%D0%B2%D1%87%D0%B5%D0%BD%D0%BA%D0%BE-777&pageSize=50"
t "пошук пацієнта за ПІБ (нижній регістр)" "$B/patients?search=%D0%BA%D1%80%D0%B0%D0%B2%D1%87%D0%B5%D0%BD%D0%BA%D0%BE-1234&pageSize=50"
t "список пацієнтів за абеткою (стор. 50)" "$B/patients?pageSize=50"
t "worklist (500)"                         "$B/worklist?pageSize=500"
t "health"                                 "http://127.0.0.1:5291/health"
grep -c -i "error\|exception" /tmp/lab-perf-api.log | sed 's/^/помилок у журналі API: /'
REMOTE
