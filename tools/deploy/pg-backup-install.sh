#!/usr/bin/env bash
# Встановлює на сервері стенда щоденне резервне копіювання БД ЛІС (systemd timer). Запуск з кореня репозиторію (Git Bash):
#     bash tools/deploy/pg-backup-install.sh
# Створює лише ресурси лабораторії: /usr/local/sbin/medlink-lab-pg-{backup,restore}, medlink-lab-backup.{service,timer}, /var/backups/medlink-lab.
set -euo pipefail
cd "$(dirname "$0")"
KEY=~/.ssh/medone_ed25519; HOST=root@94.130.180.117
scp -q -i "$KEY" pg-backup.sh pg-restore.sh "$HOST:/tmp/"
ssh -i "$KEY" "$HOST" 'bash -s' <<'REMOTE'
set -e
install -m 0755 /tmp/pg-backup.sh /usr/local/sbin/medlink-lab-pg-backup
install -m 0755 /tmp/pg-restore.sh /usr/local/sbin/medlink-lab-pg-restore
rm -f /tmp/pg-backup.sh /tmp/pg-restore.sh
install -d -m 0700 /var/backups/medlink-lab
cat > /etc/systemd/system/medlink-lab-backup.service <<'UNIT'
[Unit]
Description=MedLink LIS: резервна копія БД medlink_lab (pg_dump)
After=postgresql.service

[Service]
Type=oneshot
ExecStart=/usr/local/sbin/medlink-lab-pg-backup
Nice=10
IOSchedulingClass=idle
UNIT
cat > /etc/systemd/system/medlink-lab-backup.timer <<'UNIT'
[Unit]
Description=MedLink LIS: щоденна резервна копія БД (02:40 UTC)

[Timer]
OnCalendar=*-*-* 02:40:00
RandomizedDelaySec=300
Persistent=true

[Install]
WantedBy=timers.target
UNIT
systemctl daemon-reload
systemctl enable --now medlink-lab-backup.timer
systemctl list-timers medlink-lab-backup.timer --no-pager
REMOTE
