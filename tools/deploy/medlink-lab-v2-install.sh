#!/usr/bin/env bash
# Демо-стенд лінії tenancy (Артем) у підпапці https://med-one.avowiz.com/lab-v2/
# Запуск з ПК розробника після завантаження /tmp/v2-app.tgz і /tmp/v2-migrations.sql:
#   ssh -i ~/.ssh/medone_ed25519 root@94.130.180.117 'bash -s' < tools/deploy/medlink-lab-v2-install.sh
# Ізоляція від продуктиву: власний контейнер PostgreSQL 16 (127.0.0.1:5444) і Redis (127.0.0.1:6391),
# служба medlink-lab-v2 на 127.0.0.1:5191. PostgreSQL 17 хоста та контейнери medlink-* не змінюються.
# Паролі генеруються на сервері в /srv/medlink-lab-v2/secrets.env (0600) і ніде не виводяться.
set -euo pipefail

APP=/srv/medlink-lab-v2
PORT=5191
PG=medlink-lab-v2-pg
RD=medlink-lab-v2-redis

id medlinklab >/dev/null 2>&1 || useradd --system --home /srv/medlink-lab --shell /usr/sbin/nologin medlinklab
mkdir -p "$APP/app" "$APP/pgdata"

if [ ! -f "$APP/secrets.env" ]; then
  umask 077
  gen() { head -c 24 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 28; }
  printf 'PG_SUPER=%s\nPG_APP=%s\nPG_MIGRATOR=%s\nINTROSPECTION=%s\nVERIFY=%s\n' "$(gen)" "$(gen)" "$(gen)" "$(gen)" "$(gen)" > "$APP/secrets.env"
fi
# shellcheck disable=SC1091
. "$APP/secrets.env"

# --- PostgreSQL 16 і Redis у власних контейнерах (лише localhost) ---
if ! docker ps -a --format '{{.Names}}' | grep -qx "$PG"; then
  docker run -d --name "$PG" --restart unless-stopped -p 127.0.0.1:5444:5432 \
    -e POSTGRES_PASSWORD="$PG_SUPER" -e POSTGRES_DB=lis -v "$APP/pgdata:/var/lib/postgresql/data" postgres:16 >/dev/null
fi
if ! docker ps -a --format '{{.Names}}' | grep -qx "$RD"; then
  docker run -d --name "$RD" --restart unless-stopped -p 127.0.0.1:6391:6379 redis:7-alpine >/dev/null
fi
for i in $(seq 1 30); do docker exec "$PG" pg_isready -U postgres -d lis >/dev/null 2>&1 && break; sleep 2; done

# --- Ролі та схема (EF-міграції Артема, ідемпотентний скрипт) ---
docker exec -i "$PG" psql -v ON_ERROR_STOP=1 -U postgres -d lis >/dev/null <<SQL
DO \$\$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='lab_app') THEN CREATE ROLE lab_app LOGIN NOBYPASSRLS; END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='lab_migrator') THEN CREATE ROLE lab_migrator LOGIN NOBYPASSRLS; END IF;
END \$\$;
ALTER ROLE lab_app PASSWORD '$PG_APP';
ALTER ROLE lab_migrator PASSWORD '$PG_MIGRATOR';
GRANT CONNECT ON DATABASE lis TO lab_app, lab_migrator;
GRANT USAGE, CREATE ON SCHEMA public TO lab_migrator;
GRANT USAGE ON SCHEMA public TO lab_app;
SQL
if [ -f /tmp/v2-migrations.sql ]; then
  docker exec -i "$PG" psql -v ON_ERROR_STOP=1 -q -U postgres -d lis < /tmp/v2-migrations.sql >/dev/null
  rm -f /tmp/v2-migrations.sql
fi
# Дефект лінії tenancy (РЕВІЗІЯ 8.2): міграція 0005 додала NOT NULL-колонки аудиту, яких немає в EF-моделі,
# тож сід на чистій БД падає (тести ховають це DEFAULT-ами в TestDatabase.cs). Ті самі DEFAULT-и, id — gen_random_uuid().
docker exec -i "$PG" psql -v ON_ERROR_STOP=1 -q -U postgres -d lis >/dev/null <<'SQL'
ALTER TABLE tn_tenant ALTER COLUMN created_by SET DEFAULT '00000000-0000-4000-8000-000000000000', ALTER COLUMN modified_by SET DEFAULT '00000000-0000-4000-8000-000000000000';
ALTER TABLE tn_settings ALTER COLUMN created_by SET DEFAULT '00000000-0000-4000-8000-000000000000', ALTER COLUMN modified_by SET DEFAULT '00000000-0000-4000-8000-000000000000', ALTER COLUMN time_zone SET DEFAULT 'Europe/Kyiv', ALTER COLUMN report_language SET DEFAULT 'uk';
ALTER TABLE tn_requisites ALTER COLUMN created_by SET DEFAULT '00000000-0000-4000-8000-000000000000', ALTER COLUMN modified_by SET DEFAULT '00000000-0000-4000-8000-000000000000';
ALTER TABLE id_membership ALTER COLUMN created_by SET DEFAULT '00000000-0000-4000-8000-000000000000', ALTER COLUMN modified_by SET DEFAULT '00000000-0000-4000-8000-000000000000';
ALTER TABLE id_membership_division ALTER COLUMN created_by SET DEFAULT '00000000-0000-4000-8000-000000000000', ALTER COLUMN modified_by SET DEFAULT '00000000-0000-4000-8000-000000000000', ALTER COLUMN id SET DEFAULT gen_random_uuid();
SQL

# --- Застосунок ---
if [ -f /tmp/v2-app.tgz ]; then
  systemctl stop medlink-lab-v2.service 2>/dev/null || true
  find "$APP/app" -mindepth 1 -delete
  tar -xzf /tmp/v2-app.tgz -C "$APP/app"
  rm -f /tmp/v2-app.tgz
fi
chown -R medlinklab:medlinklab "$APP/app"
chown root:medlinklab "$APP/secrets.env"; chmod 0640 "$APP/secrets.env"

cat > "$APP/app.env" <<ENV
ASPNETCORE_ENVIRONMENT=Production
DOTNET_ROLL_FORWARD=Major
Urls=http://127.0.0.1:$PORT
Kestrel__Endpoints__Http__Url=http://127.0.0.1:$PORT
PathBase=/lab-v2
ConnectionStrings__DefaultConnection=Host=127.0.0.1;Port=5444;Database=lis;Username=lab_app;Password=$PG_APP
ConnectionStrings__MigratorConnection=Host=127.0.0.1;Port=5444;Database=lis;Username=lab_migrator;Password=$PG_MIGRATOR
ConnectionStrings__Redis=127.0.0.1:6391
Identity__IntrospectionClientSecret=$INTROSPECTION
Identity__Authority=https://med-one.avowiz.com/lab-v2/stand-idp
Identity__RedisActiveTokenFallbackToIntrospection=false
Cors__AllowedOrigins__0=https://med-one.avowiz.com
Lab__VerifySecret=$VERIFY
Stand__LoginEnabled=true
ENV
chown root:medlinklab "$APP/app.env"; chmod 0640 "$APP/app.env"

cat > /etc/systemd/system/medlink-lab-v2.service <<UNIT
[Unit]
Description=MedLink LIS — лінія tenancy (демо-стенд /lab-v2)
After=network.target docker.service

[Service]
User=medlinklab
WorkingDirectory=$APP/app
ExecStart=/usr/share/dotnet/dotnet $APP/app/MedLink.LIS.Api.dll
EnvironmentFile=$APP/app.env
Restart=on-failure
RestartSec=5
MemoryMax=1G
NoNewPrivileges=true

[Install]
WantedBy=multi-user.target
UNIT

cat > /etc/nginx/snippets/medlink-lab-v2.conf <<NGX
location = /lab-v2 { return 301 /lab-v2/; }
location /lab-v2/ {
  include snippets/medone-proxy.conf;
  client_max_body_size 20m;
  proxy_pass http://127.0.0.1:$PORT;
}
NGX
SITE=/etc/nginx/sites-enabled/medone-web
if ! grep -q "snippets/medlink-lab-v2.conf" "$SITE"; then
  cp "$SITE" "/root/medone-web.bak.$(date +%Y%m%d%H%M%S)"
  sed -i 's#^\(\s*\)include snippets/medlink-lab.conf;#&\n\1include snippets/medlink-lab-v2.conf;#' "$SITE"
fi

systemctl daemon-reload
systemctl enable medlink-lab-v2.service >/dev/null 2>&1
systemctl restart medlink-lab-v2.service
nginx -t && systemctl reload nginx

for i in $(seq 1 40); do
  curl -sf -m 3 "http://127.0.0.1:$PORT/lab-v2/health" >/dev/null && break
  sleep 3
done
echo "health: $(curl -s -m 5 http://127.0.0.1:$PORT/lab-v2/health)"
echo "public: $(curl -s -o /dev/null -w '%{http_code}' -m 10 https://med-one.avowiz.com/lab-v2/)"
