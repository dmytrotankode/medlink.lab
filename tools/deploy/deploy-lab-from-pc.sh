#!/usr/bin/env bash
# Оновлення стенда /lab (наша лінія; БД PostgreSQL medlink_lab, SQLite-файл лишається резервом) з ПК розробника: збірка → архів → завантаження → встановлення.
# Запуск з кореня репозиторію у Git Bash:   bash tools/deploy/deploy-lab-from-pc.sh
# /lab-v2 (лінія tenancy Артема) цим скриптом НЕ чіпається.
# Перед запуском: дерево git чисте за потрібним комітом (збірка бере те, що лежить у папці).
set -euo pipefail
cd "$(dirname "$0")/../.."
KEY=~/.ssh/medone_ed25519
HOST=root@94.130.180.117
OUT="$(mktemp -d)"

echo "==> 1/4 фронтенд (quasar build, підпапка /lab/) -> wwwroot"
MSYS_NO_PATHCONV=1 MSYS2_ENV_CONV_EXCL=LIS_PUBLIC_PATH LIS_PUBLIC_PATH=/lab/ powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./build.ps1 -SkipInstall

echo "==> 2/4 dotnet publish (linux-x64, framework-dependent)"
dotnet publish src/MedLink.LIS.Api/MedLink.LIS.Api.csproj -c Release -r linux-x64 --self-contained false -o "$OUT/app" --nologo -v q

echo "==> 3/4 архів і завантаження на сервер"
tar -czf "$OUT/lab-app.tgz" -C "$OUT/app" .
ls -lh "$OUT/lab-app.tgz"
scp -i "$KEY" "$OUT/lab-app.tgz" "$HOST:/tmp/lab-app.tgz"

echo "==> 4/4 встановлення (зупинка служби -> розпаковка -> перезапуск; файл SQLite у /srv/medlink-lab/data не чіпається)"
PGPW=""; [ -f ~/.medlink/lab_pg_pw.txt ] && PGPW="$(cat ~/.medlink/lab_pg_pw.txt)"
ssh -i "$KEY" "$HOST" "PGPW='$PGPW' bash -s" < tools/deploy/medlink-lab-install.sh

echo "==> перевірка"
curl -s -m 10 -o /dev/null -w "https://med-one.avowiz.com/lab/health -> %{http_code}\n" https://med-one.avowiz.com/lab/health
rm -rf "$OUT"
echo "Готово. Відкрийте https://med-one.avowiz.com/lab/ і перевірте колонки «Дослідження»/«Відділення» у черзі направлень."
