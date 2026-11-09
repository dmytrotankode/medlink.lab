<#
.SYNOPSIS
  Достаёт одно значение из хранилища секретов лаборатории (для инструментов и скриптов; в чат не печатать).

.EXAMPLE
  $pw = & .\get-lab-secret.ps1 -Section evomis -Field password
  & .\get-lab-secret.ps1 -Section hoco -Field host
#>
param(
  [Parameter(Mandatory)][ValidateSet('evomis', 'medlink', 'ehealth', 'hoco')][string]$Section,
  [Parameter(Mandatory)][string]$Field
)
$ErrorActionPreference = 'Stop'
$store = Join-Path $env:USERPROFILE '.medlink\lab_secrets.json.dpapi'
if (-not (Test-Path $store)) { throw "Хранилище не создано. Запустите tools\secrets\setup-lab-secrets.ps1" }
$sec = Get-Content $store | ConvertTo-SecureString
$json = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec))
$obj = $json | ConvertFrom-Json
$s = $obj.$Section
if ($null -eq $s) { throw "Раздел '$Section' не заполнен." }
$v = $s.$Field
if ($null -eq $v) { throw "Поля '$Field' нет в разделе '$Section'." }
$v
