# MedLink LIS — скидання демо-бази: зупиняє сервер, зберігає поточну БД у резервну копію, наступний запуск створить чисту демо-БД
$dataDir = Join-Path $env:LOCALAPPDATA 'MedLink LIS\data'
& (Join-Path $PSScriptRoot 'Stop-MedLinkLIS.ps1')
Start-Sleep -Seconds 1
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
Get-ChildItem $dataDir -Filter 'medlink_lis.db*' -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike '*.bak' } | ForEach-Object { Rename-Item $_.FullName "$($_.Name).$stamp.bak" }
[System.Reflection.Assembly]::LoadWithPartialName('System.Windows.Forms') | Out-Null
[System.Windows.Forms.MessageBox]::Show("Демо-базу скинуто (резервна копія збережена в $dataDir). Запустіть MedLink LIS знову.", 'MedLink LIS') | Out-Null
