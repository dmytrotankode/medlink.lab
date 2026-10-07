inst_dir = "C:/__MEDLINK___/LABA/MedLink.LabConnector/Installers"

service_content = """[Unit]
Description=MedLink LIS Analyzer Driver Connector Daemon
Documentation=https://medlink.ua/docs/lis/connector
After=network.target

[Service]
Type=notify
WorkingDirectory=/opt/medlink/labconnector
ExecStart=/opt/medlink/labconnector/MedLink.LabConnector
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=medlink-labconnector
User=medlink
Group=medlink
Environment=DOTNET_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

# Security hardening
ProtectSystem=full
ProtectHome=true
NoNewPrivileges=true
PrivateTmp=true

[Install]
WantedBy=multi-user.target
"""

install_linux_content = """#!/usr/bin/env bash
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
"""

inno_setup_content = """#define MyAppName "MedLink LIS Analyzer Connector"
#define MyAppVersion "3.0.0"
#define MyAppPublisher "ТОВ 'МедЛінк' (MedLink LLC)"
#define MyAppURL "https://medlink.ua"
#define MyAppExeName "MedLink.LabConnector.exe"

[Setup]
AppId={{5C8F6E22-98AA-4791-B831-29EFA0502123}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/support
AppUpdatesURL={#MyAppURL}/downloads
DefaultDirName={autopf}\\MedLink\\LabConnector
DefaultGroupName=MedLink LIS
AllowNoIcons=yes
LicenseFile=license.txt
OutputDir=Output
OutputBaseFilename=MedLink_LabConnector_Setup_v3.0
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin

[Languages]
Name: "ukrainian"; MessagesFile: "compiler:Languages\\Ukrainian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
ukrainian.WelcomeLabel1=Ласкаво просимо до майстра встановлення MedLink LIS Analyzer Connector
ukrainian.WelcomeLabel2=Ця програма встановить драйверний коннектор лабораторних аналізаторів MedLink на ваш комп'ютер.%n%nВсі авторські та інтелектуальні права на продукт належать ТОВ "МедЛінк".

[Files]
Source: "..\\bin\\Release\\net6.0\\publish\\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\\{#MyAppName} Конфігурація"; Filename: "{app}\\appsettings.json"
Name: "{group}\\Деінсталювати {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
; Встановлення Windows Service
Filename: "{sys}\\sc.exe"; Parameters: "create MedLinkLabConnector binPath= \"\"{app}\\{#MyAppExeName}\"\" start= auto DisplayName= \"MedLink LIS Analyzer Connector Service\""; Flags: runhidden; StatusMsg: "Реєстрація Windows Service..."
Filename: "{sys}\\sc.exe"; Parameters: "description MedLinkLabConnector \"Служба двосторонньої інтеграції лабораторних приладів з хмарною МІС/ЛІС MedLink. Copyright (c) MedLink.\""; Flags: runhidden
Filename: "{sys}\\net.exe"; Parameters: "start MedLinkLabConnector"; Flags: runhidden; StatusMsg: "Запуск служби..."

[UninstallRun]
Filename: "{sys}\\net.exe"; Parameters: "stop MedLinkLabConnector"; Flags: runhidden
Filename: "{sys}\\sc.exe"; Parameters: "delete MedLinkLabConnector"; Flags: runhidden
"""

install_windows_cmd = """@echo off
chcp 65001 > nul
echo ==============================================================================
echo  Встановлення MedLink LIS Analyzer Driver Connector (Windows Service)
echo  Copyright (c) 2026 MedLink LLC. Всі майнові права належать ТОВ "МедЛінк".
echo ==============================================================================

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ПОМИЛКА: Запустіть цей скрипт від імені Адміністратора!
    pause
    exit /b 1
)

set SERVICE_NAME=MedLinkLabConnector
set BIN_PATH=%~dp0MedLink.LabConnector.exe

echo [1/3] Зупинка існуючої служби...
net stop %SERVICE_NAME% >nul 2>&1
sc delete %SERVICE_NAME% >nul 2>&1

echo [2/3] Створення служби Windows %SERVICE_NAME%...
sc create %SERVICE_NAME% binPath= "%BIN_PATH%" start= auto DisplayName= "MedLink LIS Analyzer Connector"
sc description %SERVICE_NAME% "Драйвер зв'язку лабораторного обладнання з медичною платформою MedLink. Права захищено ТОВ 'МедЛінк'."

echo [3/3] Запуск служби...
net start %SERVICE_NAME%

echo ==============================================================================
echo  Службу успішно встановлено та запущено!
echo  Логи та статус доступні в журналі подій Windows (Event Viewer) або консолі.
echo ==============================================================================
pause
"""

license_content = """ЛІЦЕНЗІЙНА УГОДА КІНЦЕВОГО КОРИСТУВАЧА (EULA)
ПРОГРАМНОГО ЗАБЕЗПЕЧЕННЯ "MedLink LIS Analyzer Connector"

1. ПРЕДМЕТ УГОДИ
Дане програмне забезпечення "MedLink LIS Analyzer Connector", включаючи драйвери зв'язку з приладами (ASTM, HL7, MLLP, Serial/COM, TCP/IP), бібліотеки обробки даних, служби Windows та Linux daemons, а також будь-яка супровідна документація, є об'єктом виключних майнових та немайнових інтелектуальних прав ТОВ "МедЛінк" (MedLink LLC).

2. ПРАВОВИЙ СТАТУС ТА АВТОРСЬКІ ПРАВА
Всі права на програму захищені законодавством України та міжнародними договорами про захист авторського права. 
Користувачеві надається невиключна, обмежена ліцензія на використання даного модуля виключно у складі або взаємодії з лабораторною та медичною інформаційною системою MedLink.

3. ЗАБОРОНЕНІ ДІЇ
Забороняється реверс-інжиніринг, декомпіляція, модифікація, субліцензування, перепродаж або використання драйверів з конкуруючими медичними системами без прямої письмової згоди правовласника - ТОВ "МедЛінк".

Copyright (c) 2026 MedLink. Всі права захищені.
"""

with open(f"{inst_dir}/medlink-labconnector.service", "w", encoding="utf-8") as f: f.write(service_content)
with open(f"{inst_dir}/install-linux.sh", "w", encoding="utf-8") as f: f.write(install_linux_content)
with open(f"{inst_dir}/MedLink_LabConnector_Setup.iss", "w", encoding="utf-8") as f: f.write(inno_setup_content)
with open(f"{inst_dir}/install-windows.cmd", "w", encoding="utf-8") as f: f.write(install_windows_cmd)
with open(f"{inst_dir}/license.txt", "w", encoding="utf-8") as f: f.write(license_content)

print("Installers generated successfully")
