#!/usr/bin/env bash
# Щоденне резервне копіювання БД ЛІС (PostgreSQL) на сервері стенда. Запускається таймером systemd medlink-lab-backup.timer
# (встановлення: bash tools/deploy/pg-backup-install.sh з ПК розробника) або вручну від root:  /usr/local/sbin/medlink-lab-pg-backup
# Що робить: pg_dump -Fc (стиснений, придатний для pg_restore) БД medlink_lab (схеми public і guide) → /var/backups/medlink-lab,
# перевіряє дамп (pg_restore --list), робить ротацію: останні KEEP_DAILY днів + по одному тижневому (неділя) KEEP_WEEKLY тижнів.
# Змінні: DB (medlink_lab), BACKUP_DIR, KEEP_DAILY (14), KEEP_WEEKLY (8). Код виходу ≠ 0 при будь-якій помилці (unit стає failed).
set -euo pipefail
DB="${DB:-medlink_lab}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/medlink-lab}"
KEEP_DAILY="${KEEP_DAILY:-14}"
KEEP_WEEKLY="${KEEP_WEEKLY:-8}"
umask 077
mkdir -p "$BACKUP_DIR/daily" "$BACKUP_DIR/weekly"
stamp="$(date +%Y%m%d-%H%M%S)"
tmp="$BACKUP_DIR/daily/.${DB}-${stamp}.dump.part"
out="$BACKUP_DIR/daily/${DB}-${stamp}.dump"
trap 'rm -f "$tmp"' EXIT

# pg_dump від імені postgres (peer-автентифікація): пароль не потрібен і ніде не зберігається
runuser -u postgres -- pg_dump --format=custom --compress=6 --no-owner --dbname="$DB" > "$tmp"
size=$(stat -c %s "$tmp")
[ "$size" -gt 10000 ] || { echo "ПОМИЛКА: дамп підозріло малий ($size байт)" >&2; exit 1; }
# цілісність: каталог дампа має читатися, а таблиця замовлень — бути в ньому
runuser -u postgres -- pg_restore --list < "$tmp" > "$tmp.list"
grep -q " lab_order " "$tmp.list" || { echo "ПОМИЛКА: у дампі немає lab_order" >&2; rm -f "$tmp.list"; exit 1; }
rm -f "$tmp.list"
mv "$tmp" "$out"
trap - EXIT
echo "OK: $out ($(numfmt --to=iec "$size"))"

# тижневий знімок — копія недільного дампа
if [ "$(date +%u)" = "7" ]; then cp -p "$out" "$BACKUP_DIR/weekly/$(basename "$out")"; fi

# ротація
{ ls -1t "$BACKUP_DIR"/daily/${DB}-*.dump 2>/dev/null || true; } | tail -n +$((KEEP_DAILY + 1)) | xargs -r rm -f --
{ ls -1t "$BACKUP_DIR"/weekly/${DB}-*.dump 2>/dev/null || true; } | tail -n +$((KEEP_WEEKLY + 1)) | xargs -r rm -f --
date -u +%FT%TZ > "$BACKUP_DIR/last_ok"
logger -t medlink-lab-backup "OK $out $size bytes"
