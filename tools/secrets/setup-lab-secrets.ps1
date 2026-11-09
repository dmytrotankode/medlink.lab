<#
.SYNOPSIS
  Ввод секретов доступа для проекта лаборатории MedLink. Значения в чат и в репозиторий не попадают.

.DESCRIPTION
  Секреты хранятся одним зашифрованным файлом (Windows DPAPI, только для текущего пользователя Windows):
      %USERPROFILE%\.medlink\lab_secrets.json.dpapi
  Файл лежит вне репозитория. Скрипт спрашивает значения скрытым вводом и ничего не печатает на экран.

  Разделы:
    evomis   — тестовая база evomis-test (PostgreSQL): сервер, порт, база, пользователь, пароль
    medlink  — тестовый пользователь MedLink (Test_owner1): пароль
    ehealth  — подключение к eHealth (SimplexSMD@ukr.net), вход по КЭП: путь к файлу ключа и пароль ключа
    hoco     — Харьковский Онко (ТОЛЬКО ЧТЕНИЕ): берётся автоматически из хранилища проекта Аккураси,
               вводить ничего не нужно

.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\secrets\setup-lab-secrets.ps1
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\secrets\setup-lab-secrets.ps1 -Only evomis,ehealth
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\secrets\setup-lab-secrets.ps1 -Status

.NOTES
  Из cmd.exe:  powershell -NoProfile -ExecutionPolicy Bypass -File "C:\__MEDLINK___\LABA\tools\secrets\setup-lab-secrets.ps1"
  Доступ к 192.168.255.1 — это частный адрес, нужен VPN/офисная сеть.
#>
param(
  [ValidateSet('evomis', 'medlink', 'ehealth', 'hoco')]
  [string[]]$Only,
  [switch]$Status
)

$ErrorActionPreference = 'Stop'
$store = Join-Path $env:USERPROFILE '.medlink\lab_secrets.json.dpapi'
$accuracyStore = Join-Path $env:USERPROFILE '.claude\secrets\accuracy.json.dpapi'

function Read-Store {
  if (-not (Test-Path $store)) { return [ordered]@{} }
  $sec = Get-Content $store | ConvertTo-SecureString
  $json = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec))
  $obj = $json | ConvertFrom-Json
  $h = [ordered]@{}
  foreach ($p in $obj.PSObject.Properties) {
    $inner = [ordered]@{}
    foreach ($q in $p.Value.PSObject.Properties) { $inner[$q.Name] = $q.Value }
    $h[$p.Name] = $inner
  }
  return $h
}

function Write-Store($data) {
  $dir = Split-Path $store
  if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
  $json = $data | ConvertTo-Json -Depth 5
  ConvertTo-SecureString $json -AsPlainText -Force | ConvertFrom-SecureString | Set-Content -Encoding ascii $store
}

function Ask([string]$prompt, [string]$default = '') {
  $suffix = if ($default) { " [$default]" } else { '' }
  $v = Read-Host "$prompt$suffix"
  if ([string]::IsNullOrWhiteSpace($v)) { return $default }
  return $v.Trim()
}

function Ask-Secret([string]$prompt) {
  $s = Read-Host $prompt -AsSecureString
  $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s))
  return $plain
}

$data = Read-Store

if ($Status) {
  Write-Host "Файл: $store" -ForegroundColor Cyan
  foreach ($name in 'evomis', 'medlink', 'ehealth', 'hoco') {
    if ($data.Contains($name)) {
      $fields = ($data[$name].Keys | ForEach-Object { $_ }) -join ', '
      Write-Host ("  {0,-8} заполнен: {1}" -f $name, $fields) -ForegroundColor Green
    } else {
      Write-Host ("  {0,-8} НЕ заполнен" -f $name) -ForegroundColor Yellow
    }
  }
  exit 0
}

$sections = if ($Only) { $Only } else { @('evomis', 'medlink', 'ehealth', 'hoco') }

if ($sections -contains 'evomis') {
  Write-Host "`n=== Тестовая база evomis-test (PostgreSQL) ===" -ForegroundColor Cyan
  $old = if ($data.Contains('evomis')) { $data['evomis'] } else { @{} }
  $e = [ordered]@{}
  $e.host     = Ask 'Сервер' $(if ($old.host) { $old.host } else { '192.168.255.1' })
  $e.port     = Ask 'Порт' $(if ($old.port) { $old.port } else { '5432' })
  $e.database = Ask 'База данных' $(if ($old.database) { $old.database } else { 'evomis-test' })
  $e.user     = Ask 'Пользователь (dev_office или user — как у вас в DBeaver)' $(if ($old.user) { $old.user } else { 'dev_office' })
  $pw = Ask-Secret 'Пароль (ввод скрыт; Enter — оставить прежний)'
  $e.password = if ($pw) { $pw } elseif ($old.password) { $old.password } else { '' }
  $data['evomis'] = $e
  Write-Host 'Раздел evomis сохранён.' -ForegroundColor Green
}

if ($sections -contains 'medlink') {
  Write-Host "`n=== Тестовый пользователь MedLink ===" -ForegroundColor Cyan
  $old = if ($data.Contains('medlink')) { $data['medlink'] } else { @{} }
  $m = [ordered]@{}
  $m.login = Ask 'Логин' $(if ($old.login) { $old.login } else { 'Test_owner1' })
  $pw = Ask-Secret 'Пароль (ввод скрыт; Enter — оставить прежний)'
  $m.password = if ($pw) { $pw } elseif ($old.password) { $old.password } else { '' }
  $data['medlink'] = $m
  Write-Host 'Раздел medlink сохранён.' -ForegroundColor Green
}

if ($sections -contains 'ehealth') {
  Write-Host "`n=== Подключение к eHealth: вход по КЭП ===" -ForegroundColor Cyan
  Write-Host 'Сам файл ключа никуда не копируется — сохраняется только путь к нему и пароль ключа.' -ForegroundColor DarkGray
  $old = if ($data.Contains('ehealth')) { $data['ehealth'] } else { @{} }
  $h = [ordered]@{}
  $h.login  = Ask 'Пользователь' $(if ($old.login) { $old.login } else { 'SimplexSMD@ukr.net' })
  $h.method = 'KEP'
  $path = Ask 'Путь к файлу ключа КЭП (.dat / .pfx / .jks / .pk8)' $(if ($old.key_path) { $old.key_path } else { '' })
  if ($path -and -not (Test-Path $path)) { Write-Host "Внимание: файл не найден: $path" -ForegroundColor Yellow }
  $h.key_path = $path
  $h.ca = Ask 'Центр сертификации (необязательно, Enter — определить автоматически)' $(if ($old.ca) { $old.ca } else { '' })
  $pw = Ask-Secret 'Пароль ключа КЭП (ввод скрыт; Enter — оставить прежний)'
  $h.key_password = if ($pw) { $pw } elseif ($old.key_password) { $old.key_password } else { '' }
  $data['ehealth'] = $h
  Write-Host 'Раздел ehealth сохранён.' -ForegroundColor Green
}

if ($sections -contains 'hoco') {
  Write-Host "`n=== Харьковский Онко (только чтение) ===" -ForegroundColor Cyan
  if (-not (Test-Path $accuracyStore)) {
    Write-Host "Не найдено хранилище проекта Аккураси: $accuracyStore" -ForegroundColor Red
  } else {
    $sec = Get-Content $accuracyStore | ConvertTo-SecureString
    $json = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec))
    $acc = $json | ConvertFrom-Json
    $o = $acc.mysql_simplex_oco
    if ($null -eq $o) {
      Write-Host 'В хранилище Аккураси нет записи mysql_simplex_oco.' -ForegroundColor Red
    } else {
      $data['hoco'] = [ordered]@{
        host = $o.host; port = $o.port; database = $o.db; user = $o.user; password = $o.password
        readonly = $true
        note = 'Харьковский Онко. Только SELECT/SHOW/DESCRIBE. Источник: хранилище проекта Аккураси (mysql_simplex_oco).'
      }
      Write-Host ("Перенесено: {0}:{1}/{2}  (режим: только чтение)" -f $o.host, $o.port, $o.db) -ForegroundColor Green
    }
  }
}

Write-Store $data
Write-Host "`nГотово. Файл: $store" -ForegroundColor Cyan
Write-Host 'Проверка заполненности без показа значений:  ...\setup-lab-secrets.ps1 -Status' -ForegroundColor DarkGray
