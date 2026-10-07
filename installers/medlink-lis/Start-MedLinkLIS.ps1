# =============================================================================
# MedLink LIS — запуск автономного екземпляра на ПК аналітика.
# Сервер (ASP.NET Core, self-contained) запускається у фоні, БД — у профілі користувача,
# ЕСОЗ — мок (реальні виклики API eHealth обслуговує вбудований мок), демо-дані — так.
# Після готовності (/health) відкривається браузер (Microsoft Edge або браузер за замовчуванням).
# =============================================================================
$ErrorActionPreference = 'Stop'
$app = $PSScriptRoot
$dataDir = Join-Path $env:LOCALAPPDATA 'MedLink LIS'
$logDir = Join-Path $dataDir 'logs'
New-Item -ItemType Directory -Force -Path (Join-Path $dataDir 'data'), $logDir | Out-Null

$port = 5055
$url = "http://localhost:$port"

function Test-Lis {
    # 127.0.0.1, а не localhost: Kestrel слухає IPv4 0.0.0.0, а localhost у PowerShell 5.1 спершу йде на ::1
    try { $r = Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:$port/health" -TimeoutSec 3; return $r.Content -match '"status"\s*:\s*"Healthy"' } catch { return $false }
}

function Open-Browser {
    $edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($edge) { Start-Process $edge "--new-window $url/" } else { Start-Process "$url/" }
}

if (-not (Test-Lis)) {
    $busy = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if ($busy) {
        [System.Reflection.Assembly]::LoadWithPartialName('System.Windows.Forms') | Out-Null
        [System.Windows.Forms.MessageBox]::Show("Порт $port зайнятий іншою програмою. Закрийте її або змініть порт у Start-MedLinkLIS.ps1.", 'MedLink LIS') | Out-Null
        exit 1
    }
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:ASPNETCORE_URLS = $url
    $env:Kestrel__Endpoints__Http__Url = "http://0.0.0.0:$port"   # appsettings.json задає Kestrel-ендпоінт — перевизначаємо порт
    $env:ASPNETCORE_CONTENTROOT = $app
    $env:Database__SqlitePath = Join-Path $dataDir 'data\medlink_lis.db'
    $env:Lab__SeedDemoData = 'true'
    $env:Lab__PublicBaseUrl = $url
    $env:Ehealth__Mode = 'Mock'
    $exe = Join-Path $app 'MedLink.LIS.Api.exe'
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    Start-Process -FilePath $exe -WorkingDirectory $app -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $logDir "server-$stamp.log") -RedirectStandardError (Join-Path $logDir "server-$stamp.err.log")
    # Перший запуск створює БД і демо-дані (до 1–2 хв)
    $deadline = (Get-Date).AddMinutes(4)
    while (-not (Test-Lis)) {
        if ((Get-Date) -gt $deadline) {
            [System.Reflection.Assembly]::LoadWithPartialName('System.Windows.Forms') | Out-Null
            [System.Windows.Forms.MessageBox]::Show("Сервер MedLink LIS не запустився. Журнали: $logDir", 'MedLink LIS') | Out-Null
            exit 1
        }
        Start-Sleep -Seconds 2
    }
}
Open-Browser
