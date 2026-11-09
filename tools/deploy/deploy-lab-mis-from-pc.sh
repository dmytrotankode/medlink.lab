#!/usr/bin/env bash
# Оновлення Lab API на стенді MedLink: https://mis.med-one.avowiz.com/lab/ (служба medlink-lab-mis, :5200, схема lab у БД evomis).
# Запуск з кореня репозиторію у Git Bash:   bash tools/deploy/deploy-lab-mis-from-pc.sh
# Dev-стенд /lab на med-one.avowiz.com цим скриптом НЕ чіпається.
set -euo pipefail
cd "$(dirname "$0")/../.."
KEY=~/.ssh/medone_ed25519
HOST=root@94.130.180.117
OUT="$(mktemp -d)"

echo "==> 1/4 фронтенд (підпапка /lab/)"
MSYS_NO_PATHCONV=1 MSYS2_ENV_CONV_EXCL=LIS_PUBLIC_PATH LIS_PUBLIC_PATH=/lab/ powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./build.ps1 -SkipInstall

echo "==> 2/4 dotnet publish (linux-x64)"
dotnet publish src/MedLink.LIS.Api/MedLink.LIS.Api.csproj -c Release -r linux-x64 --self-contained false -o "$OUT/app" --nologo -v q

git checkout src/MedLink.LIS.Api/wwwroot/index.html src/MedLink.LIS.Web/.quasar/client-entry.js 2>/dev/null || true
echo "==> 3/4 архів і завантаження"
tar -czf "$OUT/lab-mis-app.tgz" -C "$OUT/app" .
scp -i "$KEY" "$OUT/lab-mis-app.tgz" "$HOST:/tmp/lab-mis-app.tgz"

echo "==> 4/4 встановлення (служба medlink-lab-mis; попередня збірка лишається в /srv/medlink-lab-mis/app.prev)"
ssh -i "$KEY" "$HOST" "bash -s" < tools/deploy/medlink-lab-mis-install.sh

echo "==> перевірка"
ssh -i "$KEY" "$HOST" 'curl -s -m 10 -o /dev/null -w "127.0.0.1:5200/lab/health -> %{http_code}\n" http://127.0.0.1:5200/lab/health'
rm -rf "$OUT"
