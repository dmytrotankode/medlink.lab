#define MyAppName "MedLink LIS Analyzer Connector"
#define MyAppVersion "3.0.0"
#define MyAppPublisher "ТОВ 'МедЛінк' (MedLink LLC)"
#define MyAppURL "https://medlink.ua"
#define MyAppExeName "MedLink.LabConnector.exe"

[Setup]
AppId={{5C8F6E22-98AA-4791-B831-29EFA0502123}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/support
AppUpdatesURL={#MyAppURL}/downloads
DefaultDirName={autopf}\MedLink\LabConnector
DefaultGroupName=MedLink LIS
AllowNoIcons=yes
LicenseFile=license.txt
OutputDir=Output
OutputBaseFilename=MedLink_LabConnector_Setup_v3.0
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin

[Languages]
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
ukrainian.WelcomeLabel1=Ласкаво просимо до майстра встановлення MedLink LIS Analyzer Connector
ukrainian.WelcomeLabel2=Ця програма встановить драйверний коннектор лабораторних аналізаторів MedLink на ваш комп'ютер.%n%nВсі авторські та інтелектуальні права на продукт належать ТОВ "МедЛінк".

[Files]
Source: "..\bin\Release\net6.0\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName} Конфігурація"; Filename: "{app}\appsettings.json"
Name: "{group}\Деінсталювати {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
; Встановлення Windows Service
Filename: "{sys}\sc.exe"; Parameters: "create MedLinkLabConnector binPath= ""{app}\{#MyAppExeName}"" start= auto DisplayName= "MedLink LIS Analyzer Connector Service""; Flags: runhidden; StatusMsg: "Реєстрація Windows Service..."
Filename: "{sys}\sc.exe"; Parameters: "description MedLinkLabConnector "Служба двосторонньої інтеграції лабораторних приладів з хмарною МІС/ЛІС MedLink. Copyright (c) MedLink.""; Flags: runhidden
Filename: "{sys}\net.exe"; Parameters: "start MedLinkLabConnector"; Flags: runhidden; StatusMsg: "Запуск служби..."

[UninstallRun]
Filename: "{sys}\net.exe"; Parameters: "stop MedLinkLabConnector"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "delete MedLinkLabConnector"; Flags: runhidden
