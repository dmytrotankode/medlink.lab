<#
.SYNOPSIS
  MedLink LIS — збірка інсталятора автономного екземпляра (Windows x64).
  1) фронтенд (Quasar) → wwwroot API; 2) dotnet publish API self-contained win-x64;
  3) документація (ТЗ HTML з Markdown, контракт API, swagger, скріншоти, міграції);
  4) інсталятор коннектора аналізаторів (якщо ще не зібрано); 5) ISCC → Output\MedLink_LIS_Setup_v<версія>.exe
.EXAMPLE
  .\build-installer.ps1
  .\build-installer.ps1 -Version 4.1.0 -SkipFrontend
#>
[CmdletBinding()]
param(
    [string]$Version = '4.1.0',
    [switch]$SkipFrontend,
    [string]$Iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$web = Join-Path $root 'src\MedLink.LIS.Web'
$api = Join-Path $root 'src\MedLink.LIS.Api'
$out = Join-Path $root 'artifacts\medlink-lis\win-x64'
$docs = Join-Path $root 'artifacts\medlink-lis\docs'

if (-not $SkipFrontend) {
    Write-Host '==> Фронтенд (quasar build)' -ForegroundColor Cyan
    Push-Location $web
    if (-not (Test-Path 'node_modules')) { npm install --no-audit --no-fund; if ($LASTEXITCODE) { throw 'npm install' } }
    npm run build; if ($LASTEXITCODE) { throw 'npm run build' }
    Pop-Location
    $www = Join-Path $api 'wwwroot'
    Get-ChildItem $www -Exclude '.gitkeep' | Remove-Item -Recurse -Force
    Copy-Item (Join-Path $web 'dist\spa\*') $www -Recurse -Force
}

Write-Host '==> dotnet publish (self-contained win-x64)' -ForegroundColor Cyan
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $api 'MedLink.LIS.Api.csproj') -c Release -r win-x64 --self-contained true -o $out -p:Version=$Version -p:DebugType=embedded -nologo -v minimal
if ($LASTEXITCODE) { throw "dotnet publish: $LASTEXITCODE" }
Remove-Item (Join-Path $out 'appsettings.Development.json') -ErrorAction SilentlyContinue
Remove-Item (Join-Path $out 'App_Data') -Recurse -Force -ErrorAction SilentlyContinue

Write-Host '==> Документація' -ForegroundColor Cyan
if (Test-Path $docs) { Remove-Item $docs -Recurse -Force }
New-Item -ItemType Directory -Force -Path $docs | Out-Null
python (Join-Path $root 'tools\render_tz_html.py')
if ($LASTEXITCODE) { throw 'render_tz_html.py' }
Copy-Item (Join-Path $root 'docs\*.md'), (Join-Path $root 'docs\*.html'), (Join-Path $root 'docs\swagger.json') $docs
Copy-Item (Join-Path $root 'docs\screenshots') (Join-Path $docs 'screenshots') -Recurse
Copy-Item (Join-Path $root 'db\migrations') (Join-Path $docs 'migrations') -Recurse
Copy-Item (Join-Path $root 'db\postgres\medlink_lis_schema.sql'), (Join-Path $root 'db\postgres\README.md') (Join-Path $docs 'migrations')
$video = Join-Path $root 'artifacts\video\MedLink_LIS_demo_UA.mp4'
if (Test-Path $video) { Copy-Item $video $docs }

$connector = Join-Path $root 'installers\labconnector\Output\MedLink_LabConnector_Setup_v4.0.0.exe'
if (-not (Test-Path $connector)) {
    Write-Host '==> Інсталятор коннектора' -ForegroundColor Cyan
    & (Join-Path $root 'installers\labconnector\publish.ps1') -Rid win-x64 -BuildInstaller
}

Write-Host '==> Inno Setup' -ForegroundColor Cyan
& $Iscc "/DMyAppVersion=$Version" (Join-Path $PSScriptRoot 'MedLinkLIS_Setup.iss')
if ($LASTEXITCODE) { throw "ISCC: $LASTEXITCODE" }
Write-Host "Готово: $(Join-Path $PSScriptRoot "Output\MedLink_LIS_Setup_v$Version.exe")" -ForegroundColor Green
