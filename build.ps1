# MedLink LIS 4.0 — збірка фронтенду (Quasar v1 SPA) та копіювання у wwwroot API (Windows).
# Використання: .\build.ps1 [-SkipInstall]
param([switch]$SkipInstall)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Web = Join-Path $Root 'src\MedLink.LIS.Web'
$WwwRoot = Join-Path $Root 'src\MedLink.LIS.Api\wwwroot'

Push-Location $Web
try {
  if (-not $SkipInstall) {
    Write-Host '==> npm install' -ForegroundColor Cyan
    npm install --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'npm install failed' }
  }
  Write-Host "==> quasar build (Node $(node -v))" -ForegroundColor Cyan
  npm run build
  if ($LASTEXITCODE -ne 0) { throw 'quasar build failed' }
} finally {
  Pop-Location
}

Write-Host "==> копіювання dist/spa -> $WwwRoot" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $WwwRoot | Out-Null
Get-ChildItem -Path $WwwRoot -Force | Where-Object { $_.Name -ne '.gitkeep' } | Remove-Item -Recurse -Force
Copy-Item -Path (Join-Path $Web 'dist\spa\*') -Destination $WwwRoot -Recurse -Force
New-Item -ItemType File -Force -Path (Join-Path $WwwRoot '.gitkeep') | Out-Null

$count = (Get-ChildItem -Path $WwwRoot -Recurse -File).Count
Write-Host "==> готово: $count файлів у wwwroot" -ForegroundColor Green
