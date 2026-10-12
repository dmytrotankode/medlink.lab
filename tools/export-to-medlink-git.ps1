# Експорт проміжних результатів (тільки вихідний код + ТЗ) у git MedLink.
# Використання (PowerShell, з кореня робочого репозиторію):
#   .\tools\export-to-medlink-git.ps1 [-Target C:\__MEDLINK___\LABA_MEDLINK_GIT] [-Remote <url>] [-Message "..."]
param(
  [string]$Target  = "C:\__MEDLINK___\LABA_MEDLINK_GIT",
  [string]$Remote  = "https://gitlab.phrm.pro/it4med/laboratory/laboratory",
  [string]$Branch  = "main",
  [string]$Message = ("Проміжні результати Medlink-LAB " + (Get-Date -Format "yyyy-MM-dd HH:mm"))
)
$ErrorActionPreference = "Stop"
$Src = Split-Path -Parent $PSScriptRoot

# 1. Підготовка цільової папки (зберігаємо її .git між запусками)
if (-not (Test-Path $Target)) { New-Item -ItemType Directory -Path $Target | Out-Null }
Get-ChildItem -Path $Target -Force | Where-Object { $_.Name -ne ".git" } | Remove-Item -Recurse -Force

# 2. Копіювання: тільки вихідний код і документація (ТЗ)
$include = @("src", "tests", "docs", "MedLink.LIS.sln", "Directory.Build.props", "build.sh", "build.ps1", "run.sh", "run.cmd", ".gitignore", ".gitattributes", "CLAUDE.md", "README.md")
$excludeDirs = @("bin", "obj", "node_modules", "dist", "wwwroot", "App_Data", "playwright-report", "test-results", ".vite", "data", "analysis")
foreach ($item in $include) {
  $from = Join-Path $Src $item
  if (-not (Test-Path $from)) { continue }
  if ((Get-Item $from).PSIsContainer) {
    robocopy $from (Join-Path $Target $item) /E /NFL /NDL /NJH /NJS /NP /XD $excludeDirs /XF "*.db" "*.db-wal" "*.db-shm" | Out-Null
  } else {
    Copy-Item $from (Join-Path $Target $item) -Force
  }
}
# wwwroot потрібен лише як порожня папка (SPA збирається build.ps1)
New-Item -ItemType Directory -Force -Path (Join-Path $Target "src\MedLink.LIS.Api\wwwroot") | Out-Null
New-Item -ItemType File -Force -Path (Join-Path $Target "src\MedLink.LIS.Api\wwwroot\.gitkeep") | Out-Null

# 3. Прибрати згадки інших git-хостів з документації (у git MedLink шляхи інших репозиторіїв не вказуються)
Get-ChildItem -Path $Target -Recurse -Include *.md | ForEach-Object {
  $t = Get-Content $_.FullName -Raw -Encoding UTF8
  $n = ($t -split "`n" | Where-Object { $_ -notmatch "github\.com|claude\.ai" }) -join "`n"
  if ($n -ne $t) { Set-Content $_.FullName $n -Encoding UTF8 }
}

# 4. git: ініціалізація, коміт, push
Push-Location $Target
try {
  if (-not (Test-Path ".git")) {
    git init -q
    git checkout -q -b $Branch
    git remote add origin $Remote
  } else {
    git remote set-url origin $Remote
  }
  git add -A
  git -c core.safecrlf=false commit -q -m $Message 2>$null
  git push -u origin $Branch
  Write-Host "Готово: $Target -> $Remote ($Branch)"
} finally { Pop-Location }
