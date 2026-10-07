<#
.SYNOPSIS
  MedLink LIS Analyzer Connector — видалення служби Windows.
.DESCRIPTION
  Зупиняє та видаляє службу, прибирає правила брандмауера. Файли програми та каталог data
  (ключ API, офлайн-буфер, журнали) видаляються лише з параметром -RemoveFiles / -RemoveData.
.EXAMPLE
  .\uninstall-windows.ps1
  .\uninstall-windows.ps1 -RemoveFiles -RemoveData
#>
[CmdletBinding()]
param(
    [string]$InstallDir = "$env:ProgramFiles\MedLink\LabConnector",
    [switch]$RemoveFiles,
    [switch]$RemoveData
)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ServiceName = 'MedLinkLabConnector'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { throw "Запустіть PowerShell від імені Адміністратора." }

$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc) {
    Write-Host "==> Зупинка та видалення служби $ServiceName" -ForegroundColor Cyan
    if ($svc.Status -ne 'Stopped') { Stop-Service $ServiceName -Force; Start-Sleep -Seconds 2 }
    & sc.exe delete $ServiceName | Out-Null
} else { Write-Host "Служба $ServiceName не знайдена." }

Write-Host "==> Видалення правил брандмауера" -ForegroundColor Cyan
& netsh advfirewall firewall delete rule name="MedLink LabConnector" | Out-Null

if ($RemoveFiles -and (Test-Path $InstallDir)) {
    Write-Host "==> Видалення файлів програми з $InstallDir" -ForegroundColor Cyan
    Get-ChildItem $InstallDir -Force | Where-Object { $_.Name -ne 'data' -or $RemoveData } | Remove-Item -Recurse -Force
    if ($RemoveData -or -not (Get-ChildItem $InstallDir -Force)) { Remove-Item $InstallDir -Recurse -Force -ErrorAction SilentlyContinue }
}
Write-Host "Готово." -ForegroundColor Green
