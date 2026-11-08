#!/usr/bin/env bash
# Лише ЧИТАННЯ: стан стендів /lab і /lab-v2 на сервері. Нічого не змінює і не перезапускає.
# Запуск з ПК розробника (Git Bash):
#   ssh -i ~/.ssh/medone_ed25519 root@94.130.180.117 'bash -s' < tools/deploy/stand-diagnose.sh > stand-report.txt 2>&1
# Потім надішліть вміст stand-report.txt (паролів у ньому немає).
set -u
hr() { printf '\n===== %s =====\n' "$1"; }

hr "Служби"
for s in medlink-lab medlink-lab-v2 nginx; do printf '%s: ' "$s"; systemctl is-active "$s" 2>&1; done

hr "Порти (5190 /lab, 5191 /lab-v2)"
ss -ltnp 2>/dev/null | grep -E ':(5190|5191|5444|6391)\b' | awk '{print $1,$4,$6}'

hr "Контейнери lab-v2"
docker ps -a --format '{{.Names}}  {{.Status}}' 2>&1 | grep -i 'lab-v2'

hr "Health"
curl -s -m 5 -o /dev/null -w "/lab    %{http_code}\n" http://127.0.0.1:5190/lab/health
curl -s -m 5 -o /dev/null -w "/lab-v2 %{http_code}\n" http://127.0.0.1:5191/lab-v2/health

hr "Журнал medlink-lab-v2 (останні 40 рядків, помилки/попередження)"
journalctl -u medlink-lab-v2 -n 400 --no-pager 2>&1 | grep -E -i 'warn|fail|error|exception|crit' | tail -40 | cut -c1-260

hr "Журнал medlink-lab (останні 25 рядків, помилки)"
journalctl -u medlink-lab -n 300 --no-pager 2>&1 | grep -E -i 'warn|fail|error|exception|crit' | tail -25 | cut -c1-260

hr "Версії зібраних застосунків"
for d in /srv/medlink-lab/app /srv/medlink-lab-v2/app; do
  printf '%s: ' "$d"; stat -c '%y' "$d/MedLink.LIS.Api.dll" 2>&1
done

hr "Дані lab-v2: замовлення та підрозділи (без персональних даних)"
if docker ps --format '{{.Names}}' | grep -qx medlink-lab-v2-pg; then
  docker exec medlink-lab-v2-pg psql -U postgres -d lis -At -c \
   "select 'orders', count(*), count(department_id) as with_department from lab_order;" 2>&1 | head -3
  docker exec medlink-lab-v2-pg psql -U postgres -d lis -At -c \
   "select 'order_tests', count(*) from lab_order_test;" 2>&1 | head -3
else
  echo "контейнер medlink-lab-v2-pg не запущений"
fi

hr "Диск і пам'ять"
df -h / | tail -1; free -m | sed -n 2p
