#!/usr/bin/env bash
# Встановлення/оновлення ІМІТАТОРА Identity на стенді MedLink: https://mis.med-one.avowiz.com/id/ (OIDC для Lab API, поки немає справжнього).
# Запуск з ПК розробника (архів збірки вже завантажено в /tmp/idsim-app.tgz; без архіву — лише конфігурація):
#   ssh -i ~/.ssh/medone_ed25519 root@94.130.180.117 'bash -s' < tools/deploy/medlink-idsim-install.sh
# Що робить (ідемпотентно):
#   1. Служба medlink-idsim на 127.0.0.1:5210, каталог /srv/medlink-idsim (app/, data/ з ключем підпису, secrets.env 0600 root).
#   2. Пароль стенда Sim__Password генерується один раз і ніде не виводиться; підключення до БД МІС — роль lab_app (лише SELECT)
#      з /srv/medlink-lab-mis/secrets.env. Drop-in sim.conf — 0600 root.
#   3. nginx: сніпет snippets/medlink-idsim.conf (location /id/ без basic-auth: імітатор має власний вхід), підключений у сайті
#      mis.med-one.avowiz.com (копія конфіга до правки, nginx -t, reload, без restart). Сніпет /lab/ теж без basic-auth (Authorization: Bearer).
# НЕ чіпає: medlink-lab (5190), medlink-lab-v2 (5191), medlink-api, medone, medlink-fin, інші сайти nginx.
# Пароль стенда для власника: cat /srv/medlink-idsim/secrets.env (на сервері, під root).
set -euo pipefail

APP=/srv/medlink-idsim
SVC=medlink-idsim
PORT=5210
PGHOST_PORT=5434
DB=evomis
SITE=/etc/nginx/sites-available/mis.med-one.avowiz.com
ISSUER="${ISSUER:-https://mis.med-one.avowiz.com/id}"
LAB_SECRETS=/srv/medlink-lab-mis/secrets.env

if ss -ltn | grep -q "127.0.0.1:$PORT " && ! systemctl is-active --quiet "$SVC"; then
  echo "ПОМИЛКА: порт $PORT зайнятий не службою $SVC" >&2; exit 1
fi
[ -f "$LAB_SECRETS" ] || { echo "ПОМИЛКА: немає $LAB_SECRETS (спершу встановіть medlink-lab-mis)" >&2; exit 1; }

id medlinkidsim >/dev/null 2>&1 || useradd --system --home "$APP" --shell /usr/sbin/nologin medlinkidsim
mkdir -p "$APP/app" "$APP/data"

# --- секрет: пароль стенда (один раз) ---
if [ ! -f "$APP/secrets.env" ]; then
  umask 077
  printf 'SIM_PASSWORD=%s\n' "$(head -c 18 /dev/urandom | od -An -tx1 | tr -d ' \n')" > "$APP/secrets.env"
  umask 022
fi
chmod 600 "$APP/secrets.env"; chown root:root "$APP/secrets.env"
# shellcheck disable=SC1090
. "$APP/secrets.env"
PG_APP=$(. "$LAB_SECRETS"; printf '%s' "$PG_APP")

# --- застосунок ---
if [ -f /tmp/idsim-app.tgz ]; then
  systemctl stop "$SVC.service" 2>/dev/null || true
  if [ -f "$APP/app/MedLink.IdentitySim.dll" ]; then rm -rf "$APP/app.prev"; cp -a "$APP/app" "$APP/app.prev"; fi
  find "$APP/app" -mindepth 1 -delete
  tar -xzf /tmp/idsim-app.tgz -C "$APP/app"
  rm -f /tmp/idsim-app.tgz
fi
chown -R root:root "$APP/app"; chmod -R go-w "$APP/app"
chown medlinkidsim:medlinkidsim "$APP/data"; chmod 700 "$APP/data"
chmod 755 "$APP"

cat > /etc/systemd/system/$SVC.service <<UNIT
[Unit]
Description=MedLink Identity Simulator (стенд mis.med-one /id)
After=network.target

[Service]
User=medlinkidsim
WorkingDirectory=$APP/app
ExecStart=/usr/share/dotnet/dotnet $APP/app/MedLink.IdentitySim.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_ROLL_FORWARD=Major
Environment=ASPNETCORE_URLS=http://127.0.0.1:$PORT
Restart=on-failure
RestartSec=5
MemoryMax=512M
NoNewPrivileges=true
ProtectSystem=strict
ReadWritePaths=$APP/data

[Install]
WantedBy=multi-user.target
UNIT

D=/etc/systemd/system/$SVC.service.d
mkdir -p "$D"; chmod 755 "$D"
umask 077
# Search Path=public: у ролі lab_app за замовчуванням search_path=lab, а таблиці МІС лежать у public
cat > "$D/sim.conf" <<CONF
[Service]
Environment=Sim__Issuer=$ISSUER
Environment=Sim__Password=$SIM_PASSWORD
Environment=Sim__KeyPath=$APP/data/signing-key.pem
Environment="Sim__MedlinkConnectionString=Host=127.0.0.1;Port=$PGHOST_PORT;Database=$DB;Username=lab_app;Password=$PG_APP;Search Path=public;Timeout=10"
CONF
chmod 600 "$D"/*.conf
umask 022

# --- nginx ---
cat > /etc/nginx/snippets/medlink-idsim.conf <<NGX
location = /id { return 301 /id/; }
location /id/ {
  auth_basic off;
  include snippets/medone-proxy.conf;
  proxy_set_header X-Forwarded-Proto https;
  proxy_pass http://127.0.0.1:$PORT/;
}
NGX
# /lab/: токен Identity йде в Authorization, basic-auth сайту конфліктує з ним
if ! grep -q "auth_basic off" /etc/nginx/snippets/medlink-lab-mis.conf; then
  sed -i 's#^location /lab/ {#location /lab/ {\n  auth_basic off;#' /etc/nginx/snippets/medlink-lab-mis.conf
fi
if ! grep -q "snippets/medlink-idsim.conf" "$SITE"; then
  cp -a "$SITE" "/root/mis.med-one.avowiz.com.bak.idsim.$(date +%Y%m%d%H%M%S)"
  sed -i 's#^\(\s*\)include snippets/medlink-lab-mis.conf;#&\n\1include snippets/medlink-idsim.conf;#' "$SITE"
fi

systemctl daemon-reload
systemctl enable "$SVC.service" >/dev/null 2>&1
systemctl restart "$SVC.service"
nginx -t && systemctl reload nginx

for i in $(seq 1 30); do
  curl -sf -m 3 "http://127.0.0.1:$PORT/.well-known/openid-configuration" >/dev/null && break
  sleep 1
done
echo "discovery (локально): $(curl -s -m 5 http://127.0.0.1:$PORT/.well-known/openid-configuration | head -c 160)"
