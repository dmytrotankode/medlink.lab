#!/usr/bin/env bash
# Встановлення/оновлення Lab API на стенді MedLink: https://mis.med-one.avowiz.com/lab/ (поруч із medlink-api).
# Запуск з ПК розробника (архів збірки вже завантажено в /tmp/lab-mis-app.tgz; без архіву — лише конфігурація):
#   ssh -i ~/.ssh/medone_ed25519 root@94.130.180.117 'bash -s' < tools/deploy/medlink-lab-mis-install.sh
# Що робить (ідемпотентно):
#   1. БД evomis (контейнер medlink-pg): схема lab (власник lab_migrator), ролі lab_migrator / lab_app (не superuser),
#      SELECT у public лише на таблиці з розділу 3 docs/MEDLINK_INTEGRATION_PLAN.md. Паролі генеруються один раз
#      у /srv/medlink-lab-mis/secrets.env (0600 root) і ніде не виводяться.
#   2. Каталог /srv/medlink-lab-mis, служба medlink-lab-mis на 127.0.0.1:5200 (PathBase=/lab), drop-in-и 0600 root.
#   3. nginx: сніпет snippets/medlink-lab-mis.conf, підключений у сайті mis.med-one.avowiz.com (з копією конфіга).
# НЕ чіпає: medlink-lab (5190), medlink-lab-v2 (5191), medlink-api, medlink-pg (окрім БД evomis: схема lab і дві ролі), інші сайти nginx.
# Бекап БД перед першим запуском — окремий крок: pg_dump -Fc evomis > /srv/medlink/backups/evomis_<дата>.dump (див. docs).
set -euo pipefail

APP=/srv/medlink-lab-mis
SVC=medlink-lab-mis
PORT=5200
PG=medlink-pg
DB=evomis
PGHOST_PORT=5434
SITE=/etc/nginx/sites-available/mis.med-one.avowiz.com
PUBLIC_BASE_URL="${PUBLIC_BASE_URL:-https://mis.med-one.avowiz.com/lab}"
AUTH_MODE="${AUTH_MODE:-Header}"

# Порт має бути вільним або зайнятим самою службою
if ss -ltn | grep -q "127.0.0.1:$PORT " && ! systemctl is-active --quiet "$SVC"; then
  echo "ПОМИЛКА: порт $PORT зайнятий не службою $SVC" >&2; exit 1
fi

id medlinklab >/dev/null 2>&1 || useradd --system --home /srv/medlink-lab --shell /usr/sbin/nologin medlinklab
mkdir -p "$APP/app"

# --- секрети (генеруються один раз) ---
if [ ! -f "$APP/secrets.env" ]; then
  umask 077
  gen() { head -c 24 /dev/urandom | od -An -tx1 | tr -d ' \n'; }
  printf 'PG_MIGRATOR=%s\nPG_APP=%s\nVERIFY=%s\n' "$(gen)" "$(gen)" "$(gen)" > "$APP/secrets.env"
  umask 022
fi
chmod 600 "$APP/secrets.env"; chown root:root "$APP/secrets.env"
# shellcheck disable=SC1091
. "$APP/secrets.env"

# --- БД: схема lab і ролі ---
docker exec -i "$PG" psql -v ON_ERROR_STOP=1 -q -U postgres -d "$DB" >/dev/null <<SQL
DO \$\$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='lab_migrator') THEN
    CREATE ROLE lab_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='lab_app') THEN
    CREATE ROLE lab_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
  END IF;
END \$\$;
ALTER ROLE lab_migrator PASSWORD '$PG_MIGRATOR';
ALTER ROLE lab_app PASSWORD '$PG_APP';
GRANT CONNECT ON DATABASE $DB TO lab_migrator, lab_app;
-- CREATE на базі потрібен для CREATE SCHEMA IF NOT EXISTS при старті (схема вже існує, нових схем роль не створює)
GRANT CREATE ON DATABASE $DB TO lab_migrator;
CREATE SCHEMA IF NOT EXISTS lab AUTHORIZATION lab_migrator;
ALTER SCHEMA lab OWNER TO lab_migrator;
GRANT USAGE ON SCHEMA lab TO lab_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA lab TO lab_app;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA lab TO lab_app;
ALTER DEFAULT PRIVILEGES FOR ROLE lab_migrator IN SCHEMA lab GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO lab_app;
ALTER DEFAULT PRIVILEGES FOR ROLE lab_migrator IN SCHEMA lab GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO lab_app;
-- public: лише читання переліку таблиць МІС. У PG14 PUBLIC має CREATE на public — знімаємо, щоб ролі lab_* не створювали там об'єктів.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO lab_migrator, lab_app;
GRANT SELECT ON public.cmn_person, public.org_employee, public.org_department, public.mis_patient_card,
  public.sys_user_profile, public.sys_profile, public.sys_profile_role, public.sys_role,
  public.cmn_enum_record, public.ehe_outgoing_medical_referral TO lab_migrator, lab_app;
ALTER ROLE lab_migrator SET search_path = lab;
ALTER ROLE lab_app SET search_path = lab;
SQL

# --- застосунок ---
if [ -f /tmp/lab-mis-app.tgz ]; then
  systemctl stop "$SVC.service" 2>/dev/null || true
  # попередня збірка — для відкату
  if [ -f "$APP/app/MedLink.LIS.Api.dll" ]; then rm -rf "$APP/app.prev"; cp -a "$APP/app" "$APP/app.prev"; fi
  find "$APP/app" -mindepth 1 -delete
  tar -xzf /tmp/lab-mis-app.tgz -C "$APP/app"
  rm -f /tmp/lab-mis-app.tgz
fi
chown -R medlinklab:medlinklab "$APP/app"
[ -d "$APP/app.prev" ] && chown -R medlinklab:medlinklab "$APP/app.prev"
chmod 755 "$APP"

cat > /etc/systemd/system/$SVC.service <<UNIT
[Unit]
Description=MedLink LIS (Lab API, стенд mis.med-one /lab)
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

# drop-in-и з секретами: 0600, лише root (systemd читає як root). Режим входу — окремий файл, наявний не перезаписується.
D=/etc/systemd/system/$SVC.service.d
mkdir -p "$D"; chmod 755 "$D"
umask 077
cat > "$D/postgres.conf" <<CONF
[Service]
Environment=Database__Provider=Postgres
Environment=Database__Schema=lab
Environment=Lab__SeedDemoData=false
Environment="Database__ConnectionString=Host=127.0.0.1;Port=$PGHOST_PORT;Database=$DB;Username=lab_migrator;Password=$PG_MIGRATOR;Search Path=lab"
Environment="Medlink__ConnectionString=Host=127.0.0.1;Port=$PGHOST_PORT;Database=$DB;Username=lab_app;Password=$PG_APP"
CONF
cat > "$D/app.conf" <<CONF
[Service]
Environment=Lab__PublicBaseUrl=$PUBLIC_BASE_URL
Environment=Lab__VerifySecret=$VERIFY
CONF
if [ ! -f "$D/auth.conf" ]; then
  cat > "$D/auth.conf" <<CONF
[Service]
Environment=Auth__Mode=$AUTH_MODE
CONF
fi
chmod 600 "$D"/*.conf
umask 022

# --- nginx: сніпет + підключення в сайті mis.med-one (за тим самим basic-auth) ---
cat > /etc/nginx/snippets/medlink-lab-mis.conf <<NGX
location = /lab { return 301 /lab/; }
location /lab/ {
  auth_basic off;
  include snippets/medone-proxy.conf;
  proxy_set_header X-Forwarded-Proto https;
  client_max_body_size 20m;
  proxy_pass http://127.0.0.1:$PORT;
}
NGX
if ! grep -q "snippets/medlink-lab-mis.conf" "$SITE"; then
  cp -a "$SITE" "/root/mis.med-one.avowiz.com.bak.$(date +%Y%m%d%H%M%S)"
  # перед загальним location / (інші location не змінюються)
  sed -i 's#^\(\s*\)location / { try_files#\1include snippets/medlink-lab-mis.conf;\n&#' "$SITE"
fi

systemctl daemon-reload
systemctl enable "$SVC.service" >/dev/null 2>&1
systemctl restart "$SVC.service"
nginx -t && systemctl reload nginx

for i in $(seq 1 90); do
  curl -sf -m 3 "http://127.0.0.1:$PORT/lab/health" >/dev/null && break
  sleep 2
done
echo "health: $(curl -s -m 5 http://127.0.0.1:$PORT/lab/health | head -c 300)"
