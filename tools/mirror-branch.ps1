# Дзеркало гілки з GitLab MedLink у GitHub для рев'ю з хмарної сесії.
# Використання: .\tools\mirror-branch.ps1 [-Branch tenancy]
# Логін і токен GitLab вводяться у стандартному вікні Windows при першому запуску
# і зберігаються менеджером облікових даних; у скрипті та в чаті вони не з'являються.
param([string]$Branch = "tenancy")
$ErrorActionPreference = "Stop"
$Work = "C:\__MEDLINK___\LABA_MEDLINK_GIT"
$GitLab = "https://gitlab.phrm.pro/it4med/laboratory/laboratory.git"
$GitHub = "https://github.com/dmytrotankode/medlink.lab"
Push-Location $Work
try {
  git remote set-url origin $GitLab
  # прибрати застарілі збережені дані, щоб Windows запитав актуальні
  cmdkey /delete:git:https://gitlab.phrm.pro 2>$null | Out-Null
  cmdkey /delete:git:https://oauth2@gitlab.phrm.pro 2>$null | Out-Null
  git -c credential.helper=manager -c credential.interactive=always fetch origin $Branch
  if ($LASTEXITCODE -ne 0) { throw "fetch failed" }
  git push $GitHub ("origin/{0}:refs/heads/review/{0}" -f $Branch) --force
  Write-Host ("Done: branch {0} mirrored to GitHub as review/{0}" -f $Branch)
} finally { Pop-Location }
