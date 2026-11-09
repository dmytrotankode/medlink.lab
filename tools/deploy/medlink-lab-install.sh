#!/usr/bin/env bash
# Встановлення/оновлення демо-стенда ЛІС у підпапці https://med-one.avowiz.com/lab/
# Запуск з ПК розробника (архів збірки вже завантажено в /tmp/lab-app.tgz):
#   ssh -i ~/.ssh/medone_ed25519 root@94.130.180.117 'bash -s' < tools/deploy/medlink-lab-install.sh
# Що робить: окремий системний користувач medlinklab, каталог /srv/medlink-lab, служба medlink-lab
# на 127.0.0.1:5190 (PathBase=/lab, БД — PostgreSQL medlink_lab; SQLite більше не використовується), сніпет nginx /lab/.
# Наявні служби (med-one, /fin/, PostgreSQL 17, контейнери medlink-*) не змінюються.
# PostgreSQL обов'язковий: передайте пароль ролі lab_stand змінною PGPW (перший запуск) або залиште наявний drop-in postgres.conf.
# Резервні копії БД: bash tools/deploy/pg-backup-install.sh (таймер medlink-lab-backup.timer).
set -euo pipefail

APP=/srv/medlink-lab
PORT=5190

[ -n "${PGPW:-}" ] || [ -f /etc/systemd/system/medlink-lab.service.d/postgres.conf ] || { echo "ПОМИЛКА: PostgreSQL — основна СУБД ЛІС. Задайте PGPW (пароль ролі lab_stand) або створіть drop-in postgres.conf" >&2; exit 1; }
id medlinklab >/dev/null 2>&1 || useradd --system --home "$APP" --shell /usr/sbin/nologin medlinklab
mkdir -p "$APP/app" "$APP/data"
if [ -f /tmp/lab-app.tgz ]; then
  systemctl stop medlink-lab.service 2>/dev/null || true
  find "$APP/app" -mindepth 1 -delete
  tar -xzf /tmp/lab-app.tgz -C "$APP/app"
  rm -f /tmp/lab-app.tgz
fi
chown -R medlinklab:medlinklab "$APP"

cat > /etc/systemd/system/medlink-lab.service <<UNIT
[Unit]
Description=MedLink LIS (лабораторія, демо-стенд /lab)
After=network.target

[Service]
User=medlinklab
WorkingDirectory=$APP/app
ExecStart=/usr/share/dotnet/dotnet $APP/app/MedLink.LIS.Api.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_ROLL_FORWARD=Major
Environment=Urls=http://127.0.0.1:$PORT
Environment=Kestrel__Endpoints__Http__Url=http://127.0.0.1:$PORT
Environment=PathBase=/lab
Restart=on-failure
RestartSec=5
MemoryMax=1G
NoNewPrivileges=true

[Install]
WantedBy=multi-user.target
UNIT

# nginx: окремий сніпет, підключений у server-блок med-one.avowiz.com поруч із medlink-fin.conf
cat > /etc/nginx/snippets/medlink-lab.conf <<NGX
location = /lab { return 301 /lab/; }
location /lab/ {
  include snippets/medone-proxy.conf;
  client_max_body_size 20m;
  proxy_pass http://127.0.0.1:$PORT;
}
NGX
SITE=/etc/nginx/sites-enabled/medone-web
if ! grep -q "snippets/medlink-lab.conf" "$SITE"; then
  cp "$SITE" "/root/medone-web.bak.$(date +%Y%m%d%H%M%S)"
  sed -i 's#^\(\s*\)include snippets/medlink-fin.conf;#&\n\1include snippets/medlink-lab.conf;#' "$SITE"
fi

# PostgreSQL (БД medlink_lab, роль lab_stand): drop-in з паролем лише для root (0600); без PGPW наявний drop-in не чіпається.
if [ -n "${PGPW:-}" ]; then
  mkdir -p /etc/systemd/system/medlink-lab.service.d
  umask 077
  cat > /etc/systemd/system/medlink-lab.service.d/postgres.conf <<PGCONF
[Service]
Environment=Database__Provider=Postgres
Environment="Database__ConnectionString=Host=127.0.0.1;Port=5432;Database=medlink_lab;Username=lab_stand;Password=${PGPW}"
PGCONF
  chmod 600 /etc/systemd/system/medlink-lab.service.d/postgres.conf
  umask 022
fi

# Режим визначення співробітника: на цьому dev-стенді ролі перемикаються заголовком X-MedLink-Employee-Id (Auth:Mode=Header).
# Для бойового середовища задайте AUTH_MODE=None і підключіть вхід (docs/AUTH_PLAN.md). Наявний drop-in не перезаписується.
mkdir -p /etc/systemd/system/medlink-lab.service.d
if [ ! -f /etc/systemd/system/medlink-lab.service.d/auth.conf ]; then
  cat > /etc/systemd/system/medlink-lab.service.d/auth.conf <<AUTHCONF
[Service]
Environment=Auth__Mode=${AUTH_MODE:-Header}
AUTHCONF
fi

systemctl daemon-reload
systemctl enable --now medlink-lab.service
systemctl restart medlink-lab.service
nginx -t && systemctl reload nginx

for i in $(seq 1 90); do
  curl -sf -m 3 "http://127.0.0.1:$PORT/lab/health" >/dev/null && break
  sleep 2
done
echo "health: $(curl -s -m 5 http://127.0.0.1:$PORT/lab/health)"
echo "public: $(curl -s -o /dev/null -w '%{http_code}' -m 10 https://med-one.avowiz.com/lab/)"
