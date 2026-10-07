<#
.SYNOPSIS
  MedLink LIS Analyzer Connector — встановлення на Windows без Inno Setup.
.DESCRIPTION
  Копіює опубліковані файли (publish win-x64) у каталог інсталяції, виконує реєстрацію
  на сервері ЛІС (setup --server … --install-key …), реєструє службу Windows
  (delayed-auto, автоперезапуск), відкриває порти у брандмауері.
  Запускати від імені Адміністратора (PowerShell 5.1+ або 7+).
.EXAMPLE
  .\install-windows.ps1 -ServerUrl https://lis.clinic.ua -InstallKey ABCD-1234 -Name "Лабораторія ПК1"
  .\install-windows.ps1 -ServerUrl https://lis.clinic.ua -InstallKey ABCD-1234 -PrinterName "ZDesigner GK420t"
  .\install-windows.ps1 -SkipSetup   # лише служба, реєстрацію виконати пізніше
#>
[CmdletBinding()]
param(
    [string]$ServerUrl,
    [string]$InstallKey,
    [string]$Name = $env:COMPUTERNAME,
    [string]$InstallDir = "$env:ProgramFiles\MedLink\LabConnector",
    [string]$SourceDir = (Join-Path $PSScriptRoot "..\..\artifacts\labconnector\win-x64"),
    [int]$StatusPort = 5088,
    [string]$PrinterName,
    [string]$PrinterHost,
    [switch]$SkipSetup,
    [switch]$Insecure
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ServiceName = 'MedLinkLabConnector'
$DisplayName = 'MedLink LIS Analyzer Connector'

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { throw "Запустіть PowerShell від імені Адміністратора." }

if (-not $SkipSetup) {
    if (-not $ServerUrl) { $ServerUrl = Read-Host "Адреса сервера ЛІС (напр. https://lis.clinic.ua)" }
    if (-not $InstallKey) { $InstallKey = Read-Host "Ключ інсталяції коннектора (з розділу «Коннектори» у ЛІС)" }
}

if (-not (Test-Path (Join-Path $SourceDir 'MedLink.LabConnector.exe'))) {
    # дозволяємо запуск із самого каталогу публікації
    if (Test-Path (Join-Path $PSScriptRoot 'MedLink.LabConnector.exe')) { $SourceDir = $PSScriptRoot }
    else { throw "Не знайдено MedLink.LabConnector.exe у $SourceDir. Спочатку виконайте publish.ps1 -Rid win-x64." }
}

Write-Step "Зупинка попередньої служби (якщо є)"
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc) {
    if ($svc.Status -ne 'Stopped') { Stop-Service $ServiceName -Force; Start-Sleep -Seconds 2 }
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
}

Write-Step "Копіювання файлів у $InstallDir"
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $InstallDir 'data\logs') | Out-Null
# data\ (ключ API, кеш, буфер) не перезаписуємо
Get-ChildItem -Path $SourceDir -Force | Where-Object { $_.Name -ne 'data' } | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $InstallDir -Recurse -Force
}
$exe = Join-Path $InstallDir 'MedLink.LabConnector.exe'

if ($PrinterName -or $PrinterHost) {
    Write-Step "Налаштування принтера етикеток"
    $printing = @{ Printing = @{} }
    if ($PrinterName) { $printing.Printing.DefaultLabelPrinter = $PrinterName }
    if ($PrinterHost) { $printing.Printing.Host = $PrinterHost; $printing.Printing.Port = 9100 }
    $printing | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $InstallDir 'appsettings.Printing.json') -Encoding UTF8
}

if (-not $SkipSetup) {
    Write-Step "Реєстрація на сервері $ServerUrl"
    $args = @('setup', '--server', $ServerUrl, '--install-key', $InstallKey, '--name', $Name, '--status-port', $StatusPort)
    if ($Insecure) { $args += '--insecure' }
    & $exe @args
    if ($LASTEXITCODE -ne 0) { Write-Warning "Реєстрація не вдалася (код $LASTEXITCODE). Службу буде встановлено; повторіть: `"$exe`" setup --server … --install-key …" }
}

Write-Step "Реєстрація служби Windows $ServiceName"
& sc.exe create $ServiceName binPath= "`"$exe`" run" start= delayed-auto DisplayName= "$DisplayName" | Out-Null
& sc.exe description $ServiceName "Коннектор лабораторних аналізаторів MedLink LIS (ASTM/HL7). ТОВ «МедЛінк»." | Out-Null
& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

Write-Step "Правила брандмауера"
& netsh advfirewall firewall delete rule name="MedLink LabConnector" | Out-Null
& netsh advfirewall firewall add rule name="MedLink LabConnector" dir=in action=allow program="$exe" enable=yes profile=any | Out-Null
$ports = @($StatusPort)
$cache = Join-Path $InstallDir 'data\config.cache.json'
if (Test-Path $cache) {
    try {
        $cfg = Get-Content $cache -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($a in $cfg.analyzers) { if ($a.connection.isServer -and $a.connection.port) { $ports += [int]$a.connection.port } }
    } catch { Write-Warning "Не вдалося прочитати config.cache.json: $_" }
}
$portList = ($ports | Sort-Object -Unique) -join ','
& netsh advfirewall firewall add rule name="MedLink LabConnector" dir=in action=allow protocol=TCP localport=$portList enable=yes profile=any | Out-Null
Write-Host "    відкрито TCP-порти: $portList"

Write-Step "Запуск служби"
Start-Service $ServiceName
Start-Sleep -Seconds 3
$svc = Get-Service $ServiceName
Write-Host ""
Write-Host "Готово. Служба $ServiceName: $($svc.Status)" -ForegroundColor Green
Write-Host "Сторінка статусу: http://localhost:$StatusPort/"
Write-Host "Каталог даних і журналів: $InstallDir\data"
