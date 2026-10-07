# MedLink LIS — зупинка автономного сервера (процес MedLink.LIS.Api з каталогу встановлення)
$app = $PSScriptRoot
Get-Process -Name 'MedLink.LIS.Api' -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($app, [StringComparison]::OrdinalIgnoreCase) } | Stop-Process -Force
