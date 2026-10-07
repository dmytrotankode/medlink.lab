<#
.SYNOPSIS
  MedLink LIS Analyzer Connector — публікація self-contained збірок (Windows-хост).
.EXAMPLE
  .\publish.ps1                       # win-x64, linux-x64, linux-arm64
  .\publish.ps1 -Rid win-x64          # лише один RID
  .\publish.ps1 -Rid win-x64 -SingleFile
  .\publish.ps1 -Rid win-x64 -BuildInstaller   # + ISCC.exe MedLink_LabConnector_Setup.iss
#>
[CmdletBinding()]
param(
    [string[]]$Rid = @('win-x64', 'linux-x64', 'linux-arm64'),
    [switch]$SingleFile,
    [switch]$BuildInstaller,
    [string]$Iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $root 'src\MedLink.LabConnector\MedLink.LabConnector.csproj'
$outBase = Join-Path $root 'artifacts\labconnector'
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { $version = '4.0.0' }

Write-Host "MedLink LIS Analyzer Connector v$version — публікація: $($Rid -join ', ')" -ForegroundColor Cyan
foreach ($r in $Rid) {
    $out = Join-Path $outBase $r
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    Write-Host "==> $r → $out"
    dotnet publish $project -c Release -r $r --self-contained true -o $out `
        -p:PublishSingleFile=$($SingleFile.IsPresent.ToString().ToLower()) -p:PublishReadyToRun=false -p:DebugType=embedded -p:Version=$version -nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish ($r) завершився з кодом $LASTEXITCODE" }
    Remove-Item (Join-Path $out 'appsettings.Development.json') -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $out 'data') -Recurse -Force -ErrorAction SilentlyContinue
    if ($r -like 'win-*') { Copy-Item (Join-Path $PSScriptRoot 'install-windows.ps1'), (Join-Path $PSScriptRoot 'uninstall-windows.ps1') $out }
    else { Copy-Item (Join-Path $PSScriptRoot 'install-linux.sh'), (Join-Path $PSScriptRoot 'medlink-labconnector.service') $out }
    Copy-Item (Join-Path $PSScriptRoot 'README_UA.md'), (Join-Path $PSScriptRoot 'license.txt') $out
    Compress-Archive -Path "$out\*" -DestinationPath (Join-Path $outBase "medlink-labconnector_${version}_$r.zip") -Force
}

if ($BuildInstaller) {
    if (-not (Test-Path $Iscc)) { throw "Inno Setup 6 не знайдено: $Iscc (https://jrsoftware.org/isdl.php)" }
    $publishDir = Join-Path $outBase 'win-x64'
    if (-not (Test-Path (Join-Path $publishDir 'MedLink.LabConnector.exe'))) { throw "Спочатку опублікуйте win-x64" }
    Write-Host "==> Inno Setup" -ForegroundColor Cyan
    & $Iscc "/DPublishDir=$publishDir" "/DMyAppVersion=$version" (Join-Path $PSScriptRoot 'MedLink_LabConnector_Setup.iss')
    if ($LASTEXITCODE -ne 0) { throw "ISCC завершився з кодом $LASTEXITCODE" }
    Write-Host "Інсталятор: $(Join-Path $PSScriptRoot 'Output')" -ForegroundColor Green
}
Write-Host "Готово: $outBase" -ForegroundColor Green
