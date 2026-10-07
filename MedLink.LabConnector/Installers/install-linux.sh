#!/usr/bin/env bash
# =============================================================================
# MedLink LIS: Analyzer Driver Connector Linux Installer
# Всі майнові та інтелектуальні права належать ТОВ "МедЛінк" (MedLink LLC).
# Copyright (c) 2026 MedLink. Всі права захищені.
# =============================================================================

set -e

echo "================================================================"
echo " Встановлення MedLink LIS Analyzer Driver Connector (Linux)    "
echo " Copyright (c) 2026 MedLink. Всі права захищені.              "
echo "================================================================"

if [ "$EUID" -ne 0 ]; then
  echo "Помилка: запустіть інсталятор з правами root (sudo ./install-linux.sh)"
  exit 1
fi

INSTALL_DIR="/opt/medlink/labconnector"
SERVICE_FILE="/etc/systemd/system/medlink-labconnector.service"

echo "[1/5] Створення системного користувача medlink..."
if ! id -u medlink > /dev/null 2>&1; then
    useradd -r -s /bin/false -d /opt/medlink medlink
fi

# Надання доступу до послідовних портів dialout / tty
usermod -a -G dialout,tty medlink

echo "[2/5] Створення каталогу програми $INSTALL_DIR..."
mkdir -p "$INSTALL_DIR"

echo "[3/5] Копіювання бінарних файлів та конфігурації..."
cp -r ../* "$INSTALL_DIR/" || true
chown -R medlink:medlink "$INSTALL_DIR"
chmod +x "$INSTALL_DIR/MedLink.LabConnector" || true

echo "[4/5] Реєстрація systemd служби..."
cp medlink-labconnector.service "$SERVICE_FILE"
systemctl daemon-reload

echo "[5/5] Активація та запуск служби..."
systemctl enable medlink-labconnector
systemctl restart medlink-labconnector

echo "================================================================"
echo " Інсталяцію успішно завершено! Служба активна.                 "
echo " Перевірка статусу: systemctl status medlink-labconnector       "
echo " Перегляд логів:   journalctl -u medlink-labconnector -f       "
echo " Власник продукту: ТОВ 'МедЛінк'                                "
echo "================================================================"
