@echo off
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
