; =============================================================================
; MedLink LIS Analyzer Connector — інсталятор Windows (Inno Setup 6.x)
; Майстер запитує адресу сервера ЛІС та ключ інсталяції, виконує
;   MedLink.LabConnector.exe setup --server … --install-key … --name …
; реєструє службу Windows (sc.exe, delayed-auto, автоперезапуск), відкриває порти
; у брандмауері, створює ярлики на сторінку статусу та каталог конфігурації.
; Збірка: ISCC.exe MedLink_LabConnector_Setup.iss  (попередньо publish.ps1 -Rid win-x64)
; Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
; =============================================================================

#define MyAppName "MedLink LIS Analyzer Connector"
#ifndef MyAppVersion
  #define MyAppVersion "4.0.0"
#endif
#define MyAppPublisher "ТОВ «МедЛінк» (MedLink LLC)"
#define MyAppURL "https://medlink.ua"
#define MyAppExeName "MedLink.LabConnector.exe"
#define MyServiceName "MedLinkLabConnector"
#ifndef PublishDir
  #define PublishDir "..\..\artifacts\labconnector\win-x64"
#endif

[Setup]
AppId={{5C8F6E22-98AA-4791-B831-29EFA0502123}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/support
AppUpdatesURL={#MyAppURL}/downloads
DefaultDirName={autopf}\MedLink\LabConnector
DefaultGroupName=MedLink LIS
AllowNoIcons=yes
LicenseFile=license.txt
OutputDir=Output
OutputBaseFilename=MedLink_LabConnector_Setup_v{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
PrivilegesRequired=admin
CloseApplications=yes
RestartApplications=no
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
MinVersion=10.0

[Languages]
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
ukrainian.WelcomeLabel1=Ласкаво просимо до майстра встановлення {#MyAppName}
ukrainian.WelcomeLabel2=Програма встановить коннектор лабораторних аналізаторів MedLink LIS (ASTM E1381/E1394, HL7 v2 MLLP, текстові протоколи) як службу Windows.%n%nДля зв'язку з сервером знадобляться адреса ЛІС та одноразовий ключ інсталяції, який видає адміністратор у розділі «Аналізатори → Коннектори».

[CustomMessages]
ukrainian.ServerPageCaption=Підключення до сервера ЛІС
ukrainian.ServerPageDescription=Вкажіть адресу сервера MedLink LIS та ключ інсталяції коннектора
ukrainian.ServerUrlLabel=Адреса сервера ЛІС (напр. https://lis.clinic.ua):
ukrainian.InstallKeyLabel=Ключ інсталяції (з розділу «Коннектори» у ЛІС):
ukrainian.ConnectorNameLabel=Назва цього коннектора (напр. «Лабораторія, ПК №1»):
ukrainian.SkipSetupLabel=Пропустити реєстрацію зараз (виконаю setup вручну пізніше)
ukrainian.PrinterPageCaption=Друк етикеток (необов'язково)
ukrainian.PrinterPageDescription=Принтер штрихкод-етикеток для локального агента друку (http://localhost:5088/print/zpl)
ukrainian.PrinterNameLabel=Ім'я принтера Windows (RAW/ZPL), напр. «ZDesigner GK420t» — або залиште порожнім:
ukrainian.PrinterHostLabel=…або мережевий принтер (IP-адреса, порт 9100):
ukrainian.StatusPortLabel=Порт локальної сторінки статусу:
ukrainian.RegisteringStatus=Реєстрація коннектора на сервері ЛІС...
ukrainian.ServiceStatus=Реєстрація служби Windows...
ukrainian.FirewallStatus=Налаштування брандмауера Windows...
ukrainian.SetupFailed=Реєстрацію на сервері не виконано (код %1). Перевірте адресу та ключ; службу встановлено — повторіть пізніше командою:%n%n"%2" setup --server … --install-key …
ukrainian.ServerUrlRequired=Вкажіть адресу сервера або позначте «Пропустити реєстрацію».
ukrainian.InstallKeyRequired=Вкажіть ключ інсталяції або позначте «Пропустити реєстрацію».
english.ServerPageCaption=LIS server connection
english.ServerPageDescription=Enter the MedLink LIS server address and the connector install key
english.ServerUrlLabel=LIS server URL (e.g. https://lis.clinic.ua):
english.InstallKeyLabel=Install key (from "Connectors" in LIS):
english.ConnectorNameLabel=Name of this connector (e.g. "Lab PC 1"):
english.SkipSetupLabel=Skip registration now (I will run setup manually later)
english.PrinterPageCaption=Label printing (optional)
english.PrinterPageDescription=Barcode label printer for the local print agent (http://localhost:5088/print/zpl)
english.PrinterNameLabel=Windows printer name (RAW/ZPL), e.g. "ZDesigner GK420t" — or leave empty:
english.PrinterHostLabel=…or a network printer (IP address, port 9100):
english.StatusPortLabel=Local status page port:
english.RegisteringStatus=Registering connector on LIS server...
english.ServiceStatus=Registering Windows service...
english.FirewallStatus=Configuring Windows Firewall...
english.SetupFailed=Server registration failed (code %1). Check URL and key; the service is installed — retry later with:%n%n"%2" setup --server … --install-key …
english.ServerUrlRequired=Enter the server URL or check "Skip registration".
english.InstallKeyRequired=Enter the install key or check "Skip registration".

[Dirs]
Name: "{app}\data"; Permissions: users-modify
Name: "{app}\data\logs"; Permissions: users-modify

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "appsettings.Development.json,data\*"
Source: "README_UA.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Статус коннектора MedLink"; Filename: "http://localhost:{code:GetStatusPort}/"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Каталог конфігурації та журналів"; Filename: "{app}\data"
Name: "{group}\Інструкція (README_UA)"; Filename: "{app}\README_UA.md"
Name: "{group}\Деінсталювати {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\Статус коннектора MedLink"; Filename: "http://localhost:{code:GetStatusPort}/"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Run]
; 1. Реєстрація на сервері (пишe data\appsettings.local.json). Помилка не зупиняє інсталяцію — див. [Code].
; 2. Служба Windows: delayed-auto + автоперезапуск при збоях.
Filename: "{sys}\sc.exe"; Parameters: "create {#MyServiceName} binPath= ""\""{app}\{#MyAppExeName}\"" run"" start= delayed-auto DisplayName= ""{#MyAppName}"""; StatusMsg: "{cm:ServiceStatus}"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "description {#MyServiceName} ""Коннектор лабораторних аналізаторів MedLink LIS (ASTM/HL7). ТОВ «МедЛінк»."""; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "failure {#MyServiceName} reset= 86400 actions= restart/5000/restart/10000/restart/30000"; Flags: runhidden
; 3. Брандмауер: вхідні TCP для порту статусу/друку та для TCP-серверних портів приладів (порти з конфігурації додає [Code])
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""MedLink LabConnector"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""MedLink LabConnector"" dir=in action=allow program=""{app}\{#MyAppExeName}"" enable=yes profile=any"; StatusMsg: "{cm:FirewallStatus}"; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""MedLink LabConnector"" dir=in action=allow protocol=TCP localport={code:GetFirewallPorts} enable=yes profile=any"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "start {#MyServiceName}"; Flags: runhidden
Filename: "http://localhost:{code:GetStatusPort}/"; Description: "Відкрити сторінку статусу коннектора"; Flags: postinstall shellexec skipifsilent nowait

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden; RunOnceId: "StopSvc"
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden; RunOnceId: "DelSvc"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""MedLink LabConnector"""; Flags: runhidden; RunOnceId: "DelFw"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\data\logs"

[Code]
var
  ServerPage: TInputQueryWizardPage;
  SkipSetupCheck: TNewCheckBox;
  PrinterPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  ServerPage := CreateInputQueryPage(wpSelectDir, CustomMessage('ServerPageCaption'), CustomMessage('ServerPageDescription'), '');
  ServerPage.Add(CustomMessage('ServerUrlLabel'), False);
  ServerPage.Add(CustomMessage('InstallKeyLabel'), False);
  ServerPage.Add(CustomMessage('ConnectorNameLabel'), False);
  ServerPage.Add(CustomMessage('StatusPortLabel'), False);
  ServerPage.Values[0] := GetPreviousData('ServerUrl', 'https://');
  ServerPage.Values[2] := GetPreviousData('ConnectorName', GetComputerNameString);
  ServerPage.Values[3] := GetPreviousData('StatusPort', '5088');
  SkipSetupCheck := TNewCheckBox.Create(ServerPage);
  SkipSetupCheck.Parent := ServerPage.Surface;
  SkipSetupCheck.Top := ServerPage.Edits[3].Top + ServerPage.Edits[3].Height + ScaleY(12);
  SkipSetupCheck.Width := ServerPage.SurfaceWidth;
  SkipSetupCheck.Caption := CustomMessage('SkipSetupLabel');

  PrinterPage := CreateInputQueryPage(ServerPage.ID, CustomMessage('PrinterPageCaption'), CustomMessage('PrinterPageDescription'), '');
  PrinterPage.Add(CustomMessage('PrinterNameLabel'), False);
  PrinterPage.Add(CustomMessage('PrinterHostLabel'), False);
  PrinterPage.Values[0] := GetPreviousData('PrinterName', '');
  PrinterPage.Values[1] := GetPreviousData('PrinterHost', '');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = ServerPage.ID then
  begin
    if not SkipSetupCheck.Checked then
    begin
      if (Trim(ServerPage.Values[0]) = '') or (Trim(ServerPage.Values[0]) = 'https://') then
      begin
        MsgBox(CustomMessage('ServerUrlRequired'), mbError, MB_OK);
        Result := False;
        Exit;
      end;
      if Trim(ServerPage.Values[1]) = '' then
      begin
        MsgBox(CustomMessage('InstallKeyRequired'), mbError, MB_OK);
        Result := False;
        Exit;
      end;
    end;
    if StrToIntDef(Trim(ServerPage.Values[3]), 0) <= 0 then ServerPage.Values[3] := '5088';
  end;
end;

procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  SetPreviousData(PreviousDataKey, 'ServerUrl', ServerPage.Values[0]);
  SetPreviousData(PreviousDataKey, 'ConnectorName', ServerPage.Values[2]);
  SetPreviousData(PreviousDataKey, 'StatusPort', ServerPage.Values[3]);
  SetPreviousData(PreviousDataKey, 'PrinterName', PrinterPage.Values[0]);
  SetPreviousData(PreviousDataKey, 'PrinterHost', PrinterPage.Values[1]);
end;

function GetStatusPort(Param: String): String;
begin
  Result := Trim(ServerPage.Values[3]);
  if Result = '' then Result := '5088';
end;

{ Порти TCP-серверів приладів читаються з data\config.cache.json ("isServer":true … "port":NNNN);
  якщо кеш ще не отримано — лише порт статусу. }
function GetFirewallPorts(Param: String): String;
var
  Json, Ports: String;
  P, Q: Integer;
  PortStr: String;
begin
  Result := GetStatusPort('');
  if LoadStringFromFile(ExpandConstant('{app}\data\config.cache.json'), Json) then
  begin
    Ports := '';
    P := Pos('"port":', Json);
    while P > 0 do
    begin
      Delete(Json, 1, P + 6);
      Q := 1;
      PortStr := '';
      while (Q <= Length(Json)) and (Json[Q] in ['0'..'9', ' ']) do
      begin
        if Json[Q] <> ' ' then PortStr := PortStr + Json[Q];
        Q := Q + 1;
      end;
      if (PortStr <> '') and (Pos(',' + PortStr + ',', ',' + Ports + ',') = 0) then
      begin
        if Ports <> '' then Ports := Ports + ',';
        Ports := Ports + PortStr;
      end;
      P := Pos('"port":', Json);
    end;
    if Ports <> '' then Result := Result + ',' + Ports;
  end;
end;

procedure WritePrinterSettings;
var
  Content: String;
  PrinterName, PrinterHost: String;
  LocalFile: String;
begin
  PrinterName := Trim(PrinterPage.Values[0]);
  PrinterHost := Trim(PrinterPage.Values[1]);
  if (PrinterName = '') and (PrinterHost = '') then Exit;
  { setup уже записав data\appsettings.local.json; принтер пишемо в окремий файл, який читає хост }
  LocalFile := ExpandConstant('{app}\appsettings.Printing.json');
  Content := '{' + #13#10 + '  "Printing": {' + #13#10;
  if PrinterName <> '' then Content := Content + '    "DefaultLabelPrinter": "' + PrinterName + '"';
  if (PrinterName <> '') and (PrinterHost <> '') then Content := Content + ',' + #13#10;
  if PrinterHost <> '' then Content := Content + '    "Host": "' + PrinterHost + '", "Port": 9100';
  Content := Content + #13#10 + '  }' + #13#10 + '}' + #13#10;
  SaveStringToFile(LocalFile, Content, False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  Params, Exe: String;
begin
  if CurStep = ssPostInstall then
  begin
    WritePrinterSettings;
    if not SkipSetupCheck.Checked then
    begin
      Exe := ExpandConstant('{app}\{#MyAppExeName}');
      Params := 'setup --server "' + Trim(ServerPage.Values[0]) + '" --install-key "' + Trim(ServerPage.Values[1]) + '"' +
                ' --name "' + Trim(ServerPage.Values[2]) + '" --status-port ' + GetStatusPort('');
      WizardForm.StatusLabel.Caption := CustomMessage('RegisteringStatus');
      if not Exec(Exe, Params, ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
        MsgBox(FmtMessage(CustomMessage('SetupFailed'), [IntToStr(ResultCode), Exe]), mbInformation, MB_OK);
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    { data\appsettings.local.json (ключ API) та офлайн-буфер лишаються — їх видаляє адміністратор свідомо }
  end;
end;
