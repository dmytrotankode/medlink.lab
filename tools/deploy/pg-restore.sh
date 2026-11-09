#!/usr/bin/env bash
# Відновлення БД ЛІС із дампа pg-backup.sh (на сервері стенда, від root).
#   Перевірка відновлення (БЕЗПЕЧНО, живу БД не чіпає): створює тимчасову БД, відновлює, показує кількості рядків, видаляє її:
#       medlink-lab-pg-restore --verify /var/backups/medlink-lab/daily/medlink_lab-YYYYmmdd-HHMMSS.dump
#   Відновлення на місце (ЗУПИНЯЄ службу medlink-lab, перед цим робить страхову копію поточної БД, потім стартує службу):
#       medlink-lab-pg-restore --replace-live /var/backups/medlink-lab/daily/<файл>.dump
# Без аргументу файла береться найновіший daily-дамп.
set -euo pipefail
MODE="${1:---verify}"; FILE="${2:-}"
LIVE="${DB:-medlink_lab}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/medlink-lab}"
[ -n "$FILE" ] || FILE="$(ls -1t "$BACKUP_DIR"/daily/${LIVE}-*.dump | head -1)"
[ -f "$FILE" ] || { echo "Немає файла дампа: $FILE" >&2; exit 1; }
psqlp() { runuser -u postgres -- psql -v ON_ERROR_STOP=1 -X -q "$@"; }
COUNTS="SELECT 'lab_order' t, count(*) FROM public.lab_order UNION ALL SELECT 'mis_patient_card', count(*) FROM public.mis_patient_card UNION ALL SELECT 'lab_test_result', count(*) FROM public.lab_test_result UNION ALL SELECT 'lab_audit_log', count(*) FROM public.lab_audit_log UNION ALL SELECT 'guide.gd_scenario', count(*) FROM guide.gd_scenario"

case "$MODE" in
  --verify)
    TMPDB="${LIVE}_restorecheck_$$"
    trap 'psqlp -d postgres -c "DROP DATABASE IF EXISTS \"$TMPDB\"" || true' EXIT
    psqlp -d postgres -c "CREATE DATABASE \"$TMPDB\""
    runuser -u postgres -- pg_restore --no-owner --exit-on-error --dbname="$TMPDB" < "$FILE"
    echo "== Відновлено у тимчасову БД $TMPDB із $FILE"; psqlp -d "$TMPDB" -At -F' = ' -c "$COUNTS"
    echo "== Для порівняння, жива БД $LIVE:"; psqlp -d "$LIVE" -At -F' = ' -c "$COUNTS" || true
    echo "Перевірку завершено; тимчасову БД буде видалено."
    ;;
  --replace-live)
    echo "УВАГА: БД $LIVE буде замінено вмістом $FILE. Служба medlink-lab зупиняється."
    [ "${ASSUME_YES:-}" = "1" ] || { read -r -p "Продовжити? (yes) " a; [ "$a" = "yes" ] || exit 1; }
    SAFE="$BACKUP_DIR/before-restore-$(date +%Y%m%d-%H%M%S).dump"
    systemctl stop medlink-lab.service
    runuser -u postgres -- pg_dump --format=custom --no-owner --dbname="$LIVE" > "$SAFE" && echo "Страхова копія: $SAFE"
    OWNER="$(psqlp -d postgres -At -c "SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname='$LIVE'")"
    psqlp -d postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$LIVE' AND pid <> pg_backend_pid()" >/dev/null
    psqlp -d postgres -c "DROP DATABASE \"$LIVE\"" -c "CREATE DATABASE \"$LIVE\" OWNER \"$OWNER\""
    runuser -u postgres -- pg_restore --no-owner --role="$OWNER" --exit-on-error --dbname="$LIVE" < "$FILE" || {
      echo "ПОМИЛКА відновлення. Страхова копія: $SAFE" >&2; exit 1; }
    systemctl start medlink-lab.service
    echo "Готово. Перевірка:"; psqlp -d "$LIVE" -At -F' = ' -c "$COUNTS"
    ;;
  *) echo "Режими: --verify | --replace-live [файл.dump]" >&2; exit 2 ;;
esac
