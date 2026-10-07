; =============================================================================
; MedLink LIS — інсталятор автономного екземпляра для ПК аналітика / демонстрації (Inno Setup 6.x)
; Встановлює: сервер ЛІС (self-contained, .NET не потрібен) з веб-інтерфейсом, демо-дані, мок ЕСОЗ,
; документацію (ТЗ, контракт API, скріншоти) та інсталятор коннектора аналізаторів.
; Ярлик «MedLink LIS» запускає сервер у фоні й відкриває браузер; БД — %LOCALAPPDATA%\MedLink LIS.
; Збірка: build-installer.ps1 (готує artifacts\medlink-lis\win-x64 і викликає ISCC)
; Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
; =============================================================================

#define MyAppName "MedLink LIS"
#ifndef MyAppVersion
  #define MyAppVersion "4.1.0"
#endif
#define MyAppPublisher "ТОВ «МедЛінк» (MedLink LLC)"
#ifndef PublishDir
  #define PublishDir "..\..\artifacts\medlink-lis\win-x64"
#endif
#ifndef DocsDir
  #define DocsDir "..\..\artifacts\medlink-lis\docs"
#endif
#ifndef ConnectorSetup
  #define ConnectorSetup "..\labconnector\Output\MedLink_LabConnector_Setup_v4.0.0.exe"
#endif

[Setup]
AppId={{9B2B6B0E-4D7C-4E2B-9E61-1E1F3B7A4C21}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\MedLink\LIS
DefaultGroupName=MedLink LIS
DisableProgramGroupPage=yes
LicenseFile=..\labconnector\license.txt
OutputDir=Output
OutputBaseFilename=MedLink_LIS_Setup_v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayName={#MyAppName} {#MyAppVersion}
CloseApplications=yes

[Languages]
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Ярлик «MedLink LIS» на робочому столі"; GroupDescription: "Ярлики:"
Name: "connector"; Description: "Одразу встановити коннектор аналізаторів (MedLink LabConnector)"; GroupDescription: "Додатково:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Start-MedLinkLIS.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stop-MedLinkLIS.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "Reset-DemoData.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "README_UA.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#DocsDir}\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ConnectorSetup}"; DestDir: "{app}\connector"; Flags: ignoreversion

[Icons]
Name: "{group}\MedLink LIS"; Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\Start-MedLinkLIS.ps1"""; WorkingDir: "{app}"; IconFilename: "{app}\MedLink.LIS.Api.exe"
Name: "{group}\Зупинити MedLink LIS"; Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\Stop-MedLinkLIS.ps1"""; WorkingDir: "{app}"
Name: "{group}\Скинути демо-базу"; Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\Reset-DemoData.ps1"""; WorkingDir: "{app}"
Name: "{group}\Технічне завдання (ТЗ)"; Filename: "{app}\docs\ТЗ_ЛІС_MedLink_v4.html"
Name: "{group}\Документація"; Filename: "{app}\docs"
Name: "{group}\API (Swagger)"; Filename: "http://localhost:5055/swagger"
Name: "{group}\Встановити коннектор аналізаторів"; Filename: "{app}\connector\MedLink_LabConnector_Setup_v4.0.0.exe"
Name: "{group}\Інструкція"; Filename: "{app}\README_UA.md"
Name: "{group}\Видалити MedLink LIS"; Filename: "{uninstallexe}"
Name: "{autodesktop}\MedLink LIS"; Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\Start-MedLinkLIS.ps1"""; WorkingDir: "{app}"; IconFilename: "{app}\MedLink.LIS.Api.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\connector\MedLink_LabConnector_Setup_v4.0.0.exe"; Description: "Встановити коннектор аналізаторів"; Flags: waituntilterminated; Tasks: connector
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\Start-MedLinkLIS.ps1"""; Description: "Запустити MedLink LIS"; Flags: postinstall nowait skipifsilent runhidden

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\Stop-MedLinkLIS.ps1"""; Flags: runhidden; RunOnceId: "StopLis"
