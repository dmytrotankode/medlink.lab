#!/usr/bin/env bash
# =============================================================================
# MedLink LIS Analyzer Connector — встановлення на Linux (systemd)
#   sudo ./install-linux.sh --server https://lis.clinic.ua --install-key ABCD-1234 [--name "Лаб ПК1"]
#   sudo ./install-linux.sh --skip-setup            # лише служба; setup виконати пізніше
#   sudo ./install-linux.sh --source /шлях/до/publish/linux-x64
# Створює користувача medlink (групи dialout, lp), копіює файли у /opt/medlink/labconnector,
# виконує реєстрацію на сервері, встановлює та вмикає systemd-службу.
# Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
# =============================================================================
set -euo pipefail

SERVER=""; KEY=""; NAME="$(hostname)"; SKIP_SETUP=0; INSECURE=0; STATUS_PORT=5088
PRINTER_NAME=""; PRINTER_HOST=""
INSTALL_DIR="/opt/medlink/labconnector"
DATA_DIR="/var/lib/medlink/labconnector"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARCH="$(uname -m)"; case "$ARCH" in aarch64|arm64) RID=linux-arm64;; *) RID=linux-x64;; esac
SOURCE_DIR="$SCRIPT_DIR/../../artifacts/labconnector/$RID"
SERVICE_FILE="/etc/systemd/system/medlink-labconnector.service"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --server) SERVER="$2"; shift 2;;
    --install-key) KEY="$2"; shift 2;;
    --name) NAME="$2"; shift 2;;
    --source) SOURCE_DIR="$2"; shift 2;;
    --install-dir) INSTALL_DIR="$2"; shift 2;;
    --data-dir) DATA_DIR="$2"; shift 2;;
    --status-port) STATUS_PORT="$2"; shift 2;;
    --printer-name) PRINTER_NAME="$2"; shift 2;;
    --printer-host) PRINTER_HOST="$2"; shift 2;;
    --skip-setup) SKIP_SETUP=1; shift;;
    --insecure) INSECURE=1; shift;;
    -h|--help) sed -n '2,12p' "$0"; exit 0;;
    *) echo "Невідомий параметр: $1"; exit 2;;
  esac
done

echo "================================================================"
echo " MedLink LIS Analyzer Connector — встановлення (Linux, $RID)"
echo " ТОВ «МедЛінк» © 2026"
echo "================================================================"
if [[ "$EUID" -ne 0 ]]; then echo "Помилка: запустіть з правами root (sudo)"; exit 1; fi

if [[ ! -x "$SOURCE_DIR/MedLink.LabConnector" ]]; then
  if [[ -x "$SCRIPT_DIR/MedLink.LabConnector" ]]; then SOURCE_DIR="$SCRIPT_DIR"; else
    echo "Не знайдено MedLink.LabConnector у $SOURCE_DIR. Спочатку: ./publish.sh $RID"; exit 1; fi
fi

if [[ $SKIP_SETUP -eq 0 ]]; then
  [[ -z "$SERVER" ]] && read -rp "Адреса сервера ЛІС (напр. https://lis.clinic.ua): " SERVER
  [[ -z "$KEY" ]] && read -rp "Ключ інсталяції коннектора: " KEY
fi

echo "[1/6] Системний користувач medlink (групи dialout, lp — доступ до COM-портів і принтерів)"
if ! id -u medlink >/dev/null 2>&1; then useradd -r -s /usr/sbin/nologin -d /var/lib/medlink medlink; fi
for g in dialout tty lp; do getent group "$g" >/dev/null && usermod -a -G "$g" medlink || true; done

echo "[2/6] Зупинка попередньої служби (якщо є)"
systemctl stop medlink-labconnector 2>/dev/null || true

echo "[3/6] Копіювання файлів у $INSTALL_DIR"
mkdir -p "$INSTALL_DIR" "$DATA_DIR/logs"
rsync -a --delete --exclude 'data/' "$SOURCE_DIR"/ "$INSTALL_DIR"/ 2>/dev/null || cp -r "$SOURCE_DIR"/. "$INSTALL_DIR"/
chmod +x "$INSTALL_DIR/MedLink.LabConnector"
if [[ -n "$PRINTER_NAME" || -n "$PRINTER_HOST" ]]; then
  {
    echo '{ "Printing": {'
    [[ -n "$PRINTER_NAME" ]] && printf '  "DefaultLabelPrinter": "%s"%s\n' "$PRINTER_NAME" "$([[ -n "$PRINTER_HOST" ]] && echo ,)"
    [[ -n "$PRINTER_HOST" ]] && printf '  "Host": "%s", "Port": 9100\n' "$PRINTER_HOST"
    echo '} }'
  } > "$INSTALL_DIR/appsettings.Printing.json"
fi
chown -R medlink:medlink "$INSTALL_DIR" "$DATA_DIR"

if [[ $SKIP_SETUP -eq 0 ]]; then
  echo "[4/6] Реєстрація на сервері $SERVER"
  EXTRA=(); [[ $INSECURE -eq 1 ]] && EXTRA+=(--insecure)
  if ! sudo -u medlink MEDLINK_CONNECTOR_DATA="$DATA_DIR" "$INSTALL_DIR/MedLink.LabConnector" setup --server "$SERVER" --install-key "$KEY" --name "$NAME" --status-port "$STATUS_PORT" --data-dir "$DATA_DIR" "${EXTRA[@]}"; then
    echo "УВАГА: реєстрація не вдалася. Службу буде встановлено; повторіть пізніше:"
    echo "  sudo -u medlink $INSTALL_DIR/MedLink.LabConnector setup --server … --install-key … --data-dir $DATA_DIR"
  fi
else
  echo "[4/6] Реєстрацію пропущено (--skip-setup)"
fi

echo "[5/6] systemd-служба"
sed -e "s#@INSTALL_DIR@#$INSTALL_DIR#g" -e "s#@DATA_DIR@#$DATA_DIR#g" "$SCRIPT_DIR/medlink-labconnector.service" > "$SERVICE_FILE" 2>/dev/null \
  || sed -e "s#@INSTALL_DIR@#$INSTALL_DIR#g" -e "s#@DATA_DIR@#$DATA_DIR#g" "$INSTALL_DIR/medlink-labconnector.service" > "$SERVICE_FILE"
systemctl daemon-reload
systemctl enable medlink-labconnector >/dev/null

echo "[6/6] Запуск"
systemctl restart medlink-labconnector
sleep 2
systemctl --no-pager --lines=5 status medlink-labconnector || true

echo "================================================================"
echo " Готово. Сторінка статусу: http://localhost:$STATUS_PORT/"
echo " Журнали: journalctl -u medlink-labconnector -f   та   $DATA_DIR/logs/"
echo " Конфігурація зв'язку: $DATA_DIR/appsettings.local.json"
echo " Якщо прилад на USB-COM: ls -l /dev/ttyUSB*  (користувач medlink у групі dialout)"
echo "================================================================"
