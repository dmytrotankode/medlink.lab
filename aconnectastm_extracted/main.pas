unit main;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes, Vcl.Graphics,
  Vcl.Controls, Vcl.Forms, Vcl.Dialogs, nrclasses, nrcomm, Vcl.StdCtrls, DateUtils,
  nrcommbox, nrdataproc, nrastm, nrlogfile, Data.DB, DBAccess, MyAccess, inifiles,
  MemDS, nrsocket, Vcl.ComCtrls, Vcl.Menus, System.Actions, Vcl.ActnList,
  Vcl.ExtCtrls;

type
  TfrmMain = class(TForm)
    memLog: TMemo;
    Memo2: TMemo;
    nrComm1: TnrComm;
    nrAstm1: TnrAstm;
    nrLogFile1: TnrLogFile;
    AccuracyMedConnect: TMyConnection;
    qryGet: TMyQuery;
    qryParse: TMyQuery;
    nrSocketClient: TnrSocket;
    PageControl1: TPageControl;
    tsCOM: TTabSheet;
    tsTcp: TTabSheet;
    Button1: TButton;
    nrDeviceBox1: TnrDeviceBox;
    CheckBox1: TCheckBox;
    cbProtocol: TComboBox;
    Label2: TLabel;
    ePort: TEdit;
    eHost: TEdit;
    Label1: TLabel;
    chActive: TCheckBox;
    chbxServer: TCheckBox;
    btnSend: TButton;
    Button5: TButton;
    PopupMenu1: TPopupMenu;
    pmAck: TMenuItem;
    genASTM: TMyQuery;
    MemoSend: TMemo;
    ActionList1: TActionList;
    actnTest: TAction;
    pmENQ: TMenuItem;
    pmSTX: TMenuItem;
    pmEtx: TMenuItem;
    Timer1: TTimer;
    Button2: TButton;
    Button3: TButton;
    actnStart: TAction;
    actnStop: TAction;
    TimerConnect: TTimer;
    nrSocketServer: TnrSocket;
    actnACK: TAction;
    Button4: TButton;
    pmSOT: TMenuItem;
    pmEOT: TMenuItem;
    pmASCII: TMenuItem;
    chbxAutostart: TCheckBox;
    qryExec: TMyCommand;
    Button6: TButton;
    procedure Button1Click(Sender: TObject);
    procedure CheckBox1Click(Sender: TObject);
    procedure nrAstm1BeginSession(Sender: TObject);
    procedure nrAstm1EndSession(Sender: TObject);
    procedure nrAstm1NACK(Sender: TObject);
    procedure nrAstm1ACK(Sender: TObject);
    procedure nrAstm1Record(Sender: TObject; dataRecord: string;
      var isAck: Boolean);
    procedure FormCreate(Sender: TObject);
    procedure chActiveClick(Sender: TObject);
    procedure nrSocketClientClose(Sender: TObject);
    procedure FormClose(Sender: TObject; var Action: TCloseAction);
    procedure btnSendClick(Sender: TObject);
    procedure Button5Click(Sender: TObject);
    procedure nrSocketClientAfterReceive(Com: TObject; Buffer: Pointer;
      Received: Cardinal);
    procedure pmAckClick(Sender: TObject);
    procedure nrSocketClientConnect(Sender: TObject);
    procedure nrAstm1FatalError(Sender: TObject; ErrorCode, Detail: Cardinal;
      ErrorMsg: string; var RaiseException: Boolean);
    procedure actnTestExecute(Sender: TObject);
    procedure pmENQClick(Sender: TObject);
    procedure pmSTXClick(Sender: TObject);
    procedure pmEtxClick(Sender: TObject);
    procedure Timer1Timer(Sender: TObject);
    procedure actnStartExecute(Sender: TObject);
    procedure TimerConnectTimer(Sender: TObject);
    procedure actnStopExecute(Sender: TObject);
    procedure nrComm1Close(Sender: TObject);
    procedure nrSocketServerClose(Sender: TObject);
    procedure nrSocketServerConnect(Sender: TObject);
    procedure nrSocketServerAfterReceive(Com: TObject; Buffer: Pointer;
      Received: Cardinal);
    procedure AccuracyMedConnectAfterConnect(Sender: TObject);
    procedure actnACKExecute(Sender: TObject);
    procedure Button4Click(Sender: TObject);
    procedure pmASCIIClick(Sender: TObject);
    procedure pmSOTClick(Sender: TObject);
    procedure pmEOTClick(Sender: TObject);
    procedure nrComm1AfterReceive(Com: TObject; Buffer: Pointer;
      Received: Cardinal);
    procedure FormShow(Sender: TObject);
    procedure Button6Click(Sender: TObject);
  private
    { Private declarations }
    last_recive: TDateTime;
    astmPacket: string;
    analyzer_id: integer;
    protocol_type: widestring;
    d: integer;
    slConnection: TStringList;
    text_answer: string;
    bop: string;
    eop: string;
    is_end_package: boolean;
    packagestate: integer;
    function GetVersion(var Major, Minor, Release, Build: integer): Boolean;
    function CalcCRC (data: string): string;
    procedure MemoSizeControl(vMemo:TMemo);
    procedure SendStr(s: string);
    procedure Parse;
  public
    { Public declarations }
    procedure AddConnectionTab(sockClient: TnrSocket);
    procedure DeleteConnectionTab(sockClient: TnrSocket);
    procedure DeleteConnectionAll;
  end;

var
  frmMain: TfrmMain;

implementation

{$R *.dfm}

function TfrmMain.GetVersion(var Major, Minor, Release, Build: integer): Boolean;
type
  TVerInfo=packed record
    Nevazhno: array[0..47] of byte; // ненужные нам 48 байт
    Minor,Major,Build,Release: word; // а тут версия
  end;
var
  s:TResourceStream;
  v:TVerInfo;
begin
  try
    s:=TResourceStream.Create(HInstance,'#1',RT_VERSION); // достаём ресурс
    if s.Size>0 then
    begin
      s.Read(v,SizeOf(v)); // читаем нужные нам байты
      Major := v.Major;
      Minor := v.Minor;
      Release := v.Release;
      Build := v.Build;
      result := True;
    end;
  s.Free;
  ///
  except;
     result := false;
  end;
end;

procedure TfrmMain.Parse;
var
  request_str, full_message: string;
  i: integer;
begin
    qryParse.Close;
    qryParse.ParamByName('p_analyzer_message').AsWideString := astmPacket;
    qryParse.ParamByName('p_analyzer_id').AsInteger := analyzer_id;
    qryParse.Open;
    memLog.Lines.Add('Start parsing');
    memLog.Lines.Add(qryParse.FieldByName('p_result_message').AsWideString);
    memLog.Lines.Add('End parsing');

    sleep(d);

    if qryParse.FieldByName('p_result_code').AsWideString = 'Q' then
    begin
        memLog.Lines.Add('Send order');
        genASTM.Close;
        genASTM.ParamByName('p_service_barcode').Value := qryParse.FieldByName('p_barcode').AsWideString;
        genASTM.ParamByName('p_analyzer_id').Value := analyzer_id;
        genASTM.ParamByName('p_mode').Value := '';
        genASTM.Open;

        if protocol_type = 'ASTM2' then  begin  SendStr(#05); sleep(d); end;

        while not genASTM.Eof do
        begin
            request_str := genASTM.FieldByName('result').AsString;
//                  request_str := request_str + calcCRC(request_str);
            if protocol_type = 'RAPID' then
             request_str := genASTM.FieldByName('result').AsString
            else if nrAstm1.CRCCheck then
              request_str := char(2) + request_str + calcCRC(request_str) + char(13)  + char(10)
            else
              request_str := char(2) + request_str;
            memLog.Lines.Add(request_str);
            SendStr(request_str);
//            nrAstm1.SendRecord(request_str);
            genASTM.Next;
            sleep(d);
        end;
        if protocol_type = 'ASTM2' then  begin  SendStr(#04); sleep(d); end;
    end
    else if qryParse.FieldByName('p_result_code').AsWideString = 'QR' then
    begin
        memLog.Lines.Add('Send message');
        full_message := qryParse.FieldByName('p_result_message').AsWideString;

        if protocol_type = 'RAPID' then
        begin
            Sendstr(full_message);
            memLog.Lines.Add(full_message);
        end
        else
        begin
            if protocol_type = 'ASTM2' then  begin  SendStr(#05); sleep(d); end;
            request_str := copy(full_message,1,239);
            i := 1;
            while full_message > '' do
            begin
                if request_str = full_message then
                  request_str := request_str + char(3)
                else
                  request_str := request_str + char(23);

                if i > 1 then  request_str := IntToStr(i) + request_str;

                if nrAstm1.CRCCheck then
                  request_str := char(2) + request_str + calcCRC(request_str) + char(13)  + char(10)
                else
                  request_str := char(2) + request_str;
                Sendstr(request_str);
                memLog.Lines.Add(request_str);
                sleep(d);
                full_message := copy(full_message,239+1, 10000);
                request_str := copy(full_message,1,239);
                i := i + 1;
            end;
            if protocol_type = 'ASTM2' then  begin  SendStr(#04); sleep(d); end;
        end;
    end
    else if qryParse.FieldByName('p_result_code').AsWideString = 'QRQ' then
    begin
        memLog.Lines.Add('Send message');
        qryGet.Close;
        qryGet.SQL.Text := 'select @qrq_message qrq_message';
        qryGet.Open;
        full_message := qryGet.FieldByName('qrq_message').AsString;
        qryGet.Close;
        Sendstr(full_message);
        memLog.Lines.Add(full_message);
    end;

    MemoSizeControl(memLog);
    MemoSizeControl(memo2);
end;


procedure TfrmMain.SendStr(s: string);
begin
    text_answer := '';
    if protocol_type = 'ASTM' then nrAstm1.SendMessage(s)
    else
    begin
        if nrComm1.Active then
        begin
            nrComm1.SendString(s);
        end
        else
        begin
            if chbxServer.Checked then
            begin
              if slConnection.Count > 0 then
                try
                  TnrSocket(StrToInt(slConnection[0])).SendString(s);
                  nrLogFile1.Add('nrSocket: Send data: ' + s);
                finally
                end
              else
              begin
                memLog.Lines.Add('No coneections!');
              end;
            end
            else
            begin
              nrSocketClient.SendString(s);
              nrLogFile1.Add('nrSocket: Send data: ' + s);
            end;
        end;
    end;
end;

function TfrmMain.CalcCRC (data: string): string;
var
  i,c: integer;
begin
    c:=0;
    for I := 1 to Length(data) do
     c := c + ord(data[i]);
    result := IntToHex( (c mod 256),2);
end;

procedure TfrmMain.Button1Click(Sender: TObject);
begin
  nrComm1.ConfigDialog;
end;

procedure TfrmMain.Button4Click(Sender: TObject);
begin
    PopupMenu1.Popup(Mouse.CursorPos.X, Mouse.CursorPos.Y);
end;

procedure TfrmMain.btnSendClick(Sender: TObject);
begin
//    nrAstm1.SessionStart;
    SendStr(MemoSend.Lines.Text);
end;

procedure TfrmMain.Button5Click(Sender: TObject);
begin
    SendStr(#06);
end;

procedure TfrmMain.Button6Click(Sender: TObject);
var
  request_str, full_message: string;
  i: integer;
begin
        full_message := MemoSend.Text;
        request_str := copy(full_message,1,239);
        i := 1;
        while full_message > '' do
        begin
            if request_str = full_message then
              request_str := request_str + char(3)
            else
              request_str := request_str + char(23);

            if i > 1 then  request_str := IntToStr(i) + request_str;

            if nrAstm1.CRCCheck then
              request_str := char(2) + request_str + calcCRC(request_str) + char(13)  + char(10)
            else
              request_str := char(2) + request_str;
            memLog.Lines.Add(request_str);
            sleep(d);
            full_message := copy(full_message,239+1, 10000);
            request_str := copy(full_message,1,239);
            i := i + 1;
        end;

end;

procedure TfrmMain.chActiveClick(Sender: TObject);
begin
;
end;

procedure TfrmMain.CheckBox1Click(Sender: TObject);
begin
  try
    nrComm1.Active := CheckBox1.Checked;
    nrAstm1.Active := CheckBox1.Checked;
  except
    on E: Exception do
    begin
        memLog.Lines.Add(E.Message);
        CheckBox1.Checked := nrComm1.Active;
        raise;
    end;
  end;
end;

procedure TfrmMain.FormClose(Sender: TObject; var Action: TCloseAction);
var
  ini: TMemIniFile;
begin
    slConnection.Free;
    Ini := TMemIniFile.Create( ExtractFilePath(Application.ExeName) + 'AconnectAstm.ini', TEncoding.Utf8 );
    try
      Ini.WriteInteger( 'connect', 'port', AccuracyMedConnect.Port);
      Ini.WriteString( 'connect', 'server', AccuracyMedConnect.Server);
      Ini.WriteString( 'connect', 'dbname', AccuracyMedConnect.Database);
      Ini.WriteInteger( 'connect', 'analyzer_id', analyzer_id);
      Ini.WriteBool( 'connect', 'auto_start', chbxAutostart.Checked);
    finally
      Ini.UpdateFile;
      Ini.Free;
    end;

end;

procedure TfrmMain.FormCreate(Sender: TObject);
var
  ini: TMemIniFile;
  err_msg : string;
  version_major, version_minor, version_release, version_build: integer;
begin
    astmPacket := '';
    GetVersion(version_major, version_minor, version_release, version_build);
    Caption := Caption + ' v.' + IntToStr(version_major) + '.' + IntToStr(version_minor) + '.' + IntToStr(version_release) + '.' + IntToStr(version_build);
    Ini := TMemIniFile.Create( ExtractFilePath(Application.ExeName) + 'AconnectAstm.ini', TEncoding.Utf8 );
    try
      chbxAutostart.Checked := Ini.ReadBool( 'connect', 'auto_start', false);
      analyzer_id := Ini.ReadInteger( 'connect', 'analyzer_id', 0);
      if AccuracyMedConnect.Connected then AccuracyMedConnect.Close;
      AccuracyMedConnect.Server := Ini.ReadString( 'connect', 'server', 'localhost');
      AccuracyMedConnect.Port := Ini.ReadInteger( 'connect', 'port', 3308);
      AccuracyMedConnect.Database := Ini.ReadString( 'connect', 'dbname', 'hospital');

      eHost.Text := Ini.ReadString( 'tcp', 'host', '');
      if eHost.Text = '' then
      begin
          PageControl1.ActivePage := tsCom;
          nrComm1.ComPortNo := Ini.ReadInteger( 'com', 'numb', 1);
          nrComm1.BaudRate := Ini.ReadInteger( 'com', 'BaudRate', 9600);
          nrComm1.ByteSize := Ini.ReadInteger( 'com', 'ByteSize', 8);
          if Ini.ReadString( 'com', 'Parity', 'pNone') = 'pEven' then
           nrComm1.Parity := pEven
          else if Ini.ReadString( 'com', 'Parity', 'pNone') = 'pMark' then
           nrComm1.Parity := pMark
          else if Ini.ReadString( 'com', 'Parity', 'pNone') = 'pNone' then
           nrComm1.Parity := pNone
          else if Ini.ReadString( 'com', 'Parity', 'pNone') = 'pOdd' then
           nrComm1.Parity := pOdd
          else if Ini.ReadString( 'com', 'Parity', 'pNone') = 'pSpace' then
           nrComm1.Parity := pSpace;

          if Ini.ReadString( 'com', 'StopBits', 'sbOne') = 'sbOne' then
           nrComm1.StopBits := sbOne
          else if Ini.ReadString( 'com', 'StopBits', 'sbOne') = 'sbOneAndHalf' then
           nrComm1.StopBits := sbOneAndHalf
          else if Ini.ReadString( 'com', 'StopBits', 'sbOne') = 'sbTwo' then
           nrComm1.StopBits := sbTwo;

          if Ini.ReadString( 'com', 'StreamProtocol', 'spNone') = 'spNone' then
           nrComm1.StreamProtocol := spNone
          else if Ini.ReadString( 'com', 'StreamProtocol', 'spNone') = 'spXonXoff' then
           nrComm1.StreamProtocol := spXonXoff
          else if Ini.ReadString( 'com', 'StreamProtocol', 'spNone') = 'spHardware' then
           nrComm1.StreamProtocol := spHardware;
      end
      else
      begin
          PageControl1.ActivePage := tsTCP;
          ePort.Text := Ini.ReadString( 'tcp', 'port', '23');
          chbxServer.Checked := Ini.ReadBool( 'tcp', 'server', true);
          cbProtocol.ItemIndex := Ini.ReadInteger( 'tcp', 'udp', 0);
      end;
      nrAstm1.CRCCheck := Ini.ReadBool( 'tcp', 'control_sum', false);

      AccuracyMedConnect.Username := 'analyzer';
      AccuracyMedConnect.Password := '12aconnect34';
      AccuracyMedConnect.Options.Direct := true;
      err_msg := '';

      try
        AccuracyMedConnect.Open;
      except
        on e: Exception do err_msg := e.Message;
      end;
      if not AccuracyMedConnect.Connected then
      begin
        AccuracyMedConnect.Options.Direct := false;
        try
          AccuracyMedConnect.Open;
        except
          on e: Exception do err_msg := e.Message;
        end;
      end;
      qryGet.SQL.Text := 'CALL list_analyzer(' + IntToStr(analyzer_id) + ')';
      qryGet.Open;
      Caption := Caption + ' - ' + qryGet.FieldByName('analyzer_name').AsWideString + ' (' + qryGet.FieldByName('protocol_type').AsWideString + ')';
      protocol_type := qryGet.FieldByName('protocol_type').AsWideString;
      d := qryGet.FieldByName('sleep').Asinteger;
      bop := qryGet.FieldByName('bop').AsString;
      eop := qryGet.FieldByName('eop').AsString;
//      AccuracyMedConnect. := Ini.ReadString( 'connect', 'protocol', 'TCP/IP');
//      tunnel := Ini.ReadString( 'connect', 'tunnel', '');
    finally
      Ini.Free;
    end;
    slConnection := TStringList.Create;
end;

procedure TfrmMain.FormShow(Sender: TObject);
begin
    if chbxAutostart.Checked then actnStart.Execute;

end;

procedure TfrmMain.pmASCIIClick(Sender: TObject);
var
  h: integer;
  hs: string;
begin
    if InputQuery('ASCII DEC number', 'Number', hs) then
    begin
        sendStr(char(StrToInt(hs)));
    end;
end;

procedure TfrmMain.nrAstm1BeginSession(Sender: TObject);
begin
  astmPacket := '';
  memLog.Lines.Add('ASTM Session Started');
end;

procedure TfrmMain.nrAstm1EndSession(Sender: TObject);
var
  request_str: string;
  nrSocket1: TnrSocket;
begin
    memLog.Lines.Add('ASTM Session Finished');
    qryParse.Close;
    qryParse.ParamByName('p_analyzer_message').AsWideString := astmPacket;
    qryParse.ParamByName('p_analyzer_id').AsInteger := analyzer_id;
    qryParse.Open;
    memLog.Lines.Add('Start parsing');
    memLog.Lines.Add(qryParse.FieldByName('p_result_message').AsWideString);
    memLog.Lines.Add('End parsing');

    sleep(d);

    if qryParse.FieldByName('p_result_code').AsWideString = 'Q' then
    begin
        memLog.Lines.Add('Send order');
        genASTM.Close;
        genASTM.ParamByName('p_service_barcode').Value := qryParse.FieldByName('p_barcode').AsWideString;
        genASTM.ParamByName('p_analyzer_id').Value := analyzer_id;
        genASTM.ParamByName('p_mode').Value := '';
        genASTM.Open;
        if chbxServer.Checked then
         nrSocket1 := nrSocketServer
        else
         nrSocket1 := nrSocketClient;
//        SendStr(#05);
//        sleep(d);
        nrAstm1.SessionStart;
        while not genASTM.Eof do
        begin
            request_str := genASTM.FieldByName('result').AsString;
//                  request_str := request_str + calcCRC(request_str);
            if nrAstm1.CRCCheck then
              request_str := char(2) + request_str + calcCRC(request_str) + char(13)  + char(10)
            else
              request_str := request_str;
            memLog.Lines.Add(request_str);
            SendStr(request_str);
//            nrAstm1.SendRecord(request_str);
            genASTM.Next;
//            while text_answer = '' do nrAstm1.Tag := 0;

            sleep(d);
        end;
//        sleep(d);
//        SendStr(#04);
        nrAstm1.SessionEnd;
        nrAstm1.ReplyACK;
    end
    else if qryParse.FieldByName('p_result_code').AsWideString = 'QR' then
    begin
        memLog.Lines.Add('Send order');
        Sendstr(qryParse.FieldByName('p_result_message').AsWideString);
        sleep(d);
        nrAstm1.ReplyACK;
    end;

    MemoSizeControl(memLog);
    MemoSizeControl(memo2);
end;

procedure TfrmMain.nrAstm1FatalError(Sender: TObject; ErrorCode,
  Detail: Cardinal; ErrorMsg: string; var RaiseException: Boolean);
begin
    RaiseException := false;
end;

procedure TfrmMain.nrAstm1ACK(Sender: TObject);
begin
  memLog.Lines.Add('ACK is receievd');
end;

procedure TfrmMain.nrAstm1NACK(Sender: TObject);
begin
  memLog.Lines.Add('NACK is receievd');
end;

procedure TfrmMain.nrAstm1Record(Sender: TObject; dataRecord: string;
  var isAck: Boolean);
begin
  memLog.Lines.Add('RECORD is received: [' + IntTOStr(Length(dataRecord)) + '] ' + dataRecord);
  astmPacket := astmPacket + dataRecord;
end;

procedure TfrmMain.nrComm1AfterReceive(Com: TObject; Buffer: Pointer;
  Received: Cardinal);
var i:integer;
    s:string;
    str1, strd : string;
begin
  last_recive := now;
  if Com is TnrSocket then
  begin
      if (Com as TnrSocket).ServerMode  then
       nrLogFile1.Add('============== SERVER RECEIVE ================')
      else
       nrLogFile1.Add('============== CLIENT RECEIVE ================');
  end
  else
    nrLogFile1.Add('============== COM RECEIVE ================');
  nrLogFile1.Add(PAnsiChar(Buffer));
  if (protocol_type = 'ASTM') then exit;
  if (protocol_type = 'HL7') then
  begin
    s:='';
    for i:=0 to Received-1 do
    begin
        Str1 := Char(Byte(PAnsiChar(Buffer)[i]));
        s := s + str1;
    end;
    if POS('MSH|', s) > 0 then
    begin
        astmPacket := '';
        memLog.Lines.Add(FormatDateTime('dd.mm.yyyy hh:nn:ss',now) + ' -- Begin of package -- ');
    end;
    memLog.Lines.Add(FormatDateTime('dd.mm.yyyy hh:nn:ss',now) + ' ' + s);
    astmPacket := astmPacket + s;
    if POS((char(13)+char(28)+char(13)), s) > 0 then
    begin
        memLog.Lines.Add(FormatDateTime('dd.mm.yyyy hh:nn:ss',now) + ' -- End of package -- ');
        Parse;
    end;
//    SendStr(#06);
  end
  else
  begin
    s:='';
    for i:=0 to Received-1 do
    begin
        str1 := Char(Byte(PAnsiChar(Buffer)[i]));
        if Str1 = bop then
        begin
          is_end_package := false;
          packagestate := 5;
          astmPacket := '';

  //        Analyzer.vMemo.Lines.SaveToFile(Analyzer.vLogFileName + '_' + FormatDateTime('yymmdd_hhnnss', now)+'.log');
  //        Analyzer.vMemo.Lines.Clear;
          memLog.Lines.Add(FormatDateTime('dd.mm.yyyy hh:nn:ss',now) + ' -- Begin of package -- ');
  //        vMemoSizeControl(Analyzer.vMemo);

        end
        else if Str1 = eop then
        begin
          is_end_package := packagestate = 5;

          memLog.Lines.Add(FormatDateTime('dd.mm.yyyy hh:nn:ss',now) + ' -- End of package -- ');
          if (packagestate = 5 ) then
          begin
            astmPacket := astmPacket + s;
            Parse;
            packagestate := 0;
            exit;
  //          is_parsed := true;
          end;
          packagestate := 0;
        end
        else if (packagestate = 5) then
        begin
           s := s + str1;
           memLog.Lines.Text := memLog.Lines.Text + str1;
        end;
    end;
    astmPacket := astmPacket + s;
    SendStr(#06);
  end;
//  Timer1.Enabled := true;
end;

procedure TfrmMain.nrComm1Close(Sender: TObject);
begin
  CheckBox1.Checked := nrComm1.Active;
end;

procedure TfrmMain.nrSocketClientAfterReceive(Com: TObject; Buffer: Pointer;
  Received: Cardinal);
var
  I: Integer;
  s : string;
begin
  OutputDebugString('nrSocket1AfterReceive( ..... ');
  s := '';
  for I := 0 to Received - 1 do begin
//    s := s + ' ' + IntToHex(byte(PAnsiChar(Buffer)[i]), 2);
    s := s + Char(byte(PAnsiChar(Buffer)[i]));
  end;

  astmPacket := astmPacket + s;
  text_answer := s;
  Timer1.Enabled := true;
  if length(s) >= 1 then memLog.Lines.Add(s);
  if (protocol_type = 'HL7') and (s = #05) then SendStr(#06);
end;

procedure TfrmMain.nrSocketClientClose(Sender: TObject);
begin
//  chActive.Checked := nrSocket1.Active;
  if Sender is TnrSocket then begin
    if TnrSocket(Sender).ServerMode then
    begin
         memLog.Lines.Add('============== SERVER STOPPED ================');
         DeleteConnectionAll;
    end
    else
    begin
        memLog.Lines.Add('============== REMOTE CLIENT DISCONNECTED (CLOSED) ================');
        DeleteConnectionTab(TnrSocket(Sender));
    end;
  end;
end;

procedure TfrmMain.nrSocketClientConnect(Sender: TObject);
begin
//  chActive.Checked := nrSocket1.Active;
  if Sender is TnrSocket then
  begin
    if TnrSocket(Sender).ServerMode then memLog.Lines.Add('============== SERVER STARTED ================')
    else
    begin
        memLog.Lines.Add('============== REMOTE CLIENT [' + IntToStr(TnrSocket(Sender).Handle) + '] CONNECTED (ACCEPTED) ================');
        //nrLogFile1.Add('============== REMOTE CLIENT CONNECTED DONE!!!! ================');

           AddConnectionTab(TnrSocket(Sender));
    end;

  end;
  actnACK.Execute;
end;

procedure TfrmMain.nrSocketServerAfterReceive(Com: TObject; Buffer: Pointer;
  Received: Cardinal);
var
  I: Integer;
  s : string;
begin
  OutputDebugString('nrSocket1AfterReceive( ..... ');
  s := '';
  for I := 0 to Received - 1 do begin
//    s := s + ' ' + IntToHex(byte(PAnsiChar(Buffer)[i]), 2);
    s := s + Char(byte(PAnsiChar(Buffer)[i]));
  end;
  astmPacket := astmPacket + s;
  text_answer := s;
  Timer1.Enabled := true;

{$IFDEF HAS_ANONYM}
    TThread.Synchronize(TThread.CurrentThread,
      procedure begin
{$ENDIF}
      if length(s) >= 1 then memLog.Lines.Add(s);
{$IFDEF HAS_ANONYM}
      end);
{$ENDIF}
   if (protocol_type = 'HL7') and (s = #05) then SendStr(#06);
end;

procedure TfrmMain.nrSocketServerClose(Sender: TObject);
begin
  if Sender is TnrSocket then begin
    if TnrSocket(Sender).ServerMode then
    begin
      OutputDebugString('============== SERVER STOPPED ================');
        nrLogFile1.Add('============== SERVER STOPPED ================');
        memLog.Lines.Add('============== SERVER STOPPED ================');
    end
    else
    begin
        OutputDebugString('============== REMOTE CLIENT DISCONNECTED (CLOSED) ================');
        OutputDebugString('============== REMOTE CLIENT CLOSED done!!!');
        nrLogFile1.Add('============== REMOTE CLIENT DISCONNECTED (CLOSED) ================');
        memLog.Lines.Add('============== REMOTE CLIENT DISCONNECTED (CLOSED) ================');
        nrLogFile1.Add('============== REMOTE CLIENT CLOSED done!!!');
        memLog.Lines.Add('============== REMOTE CLIENT CLOSED done!!!');
{$IFDEF HAS_ANONYM}
    TThread.Synchronize(TThread.CurrentThread,
      procedure begin
{$ENDIF}
        DeleteConnectionTab(TnrSocket(Sender));
{$IFDEF HAS_ANONYM}
      end);
{$ENDIF}

    end;
  end;
end;

procedure TfrmMain.nrSocketServerConnect(Sender: TObject);
begin
  if Sender is TnrSocket then begin
    if TnrSocket(Sender).ServerMode  then
    begin
         nrLogFile1.Add('============== SERVER STARTED ================');
         memLog.Lines.Add('============== SERVER STARTED ================');
    end
    else
    begin
        nrLogFile1.Add('============== REMOTE CLIENT [' + IntToStr(TnrSocket(Sender).Handle) + '] CONNECTED (ACCEPTED) ================');
        memLog.Lines.Add('============== REMOTE CLIENT [' + IntToStr(TnrSocket(Sender).Handle) + '] CONNECTED (ACCEPTED) ================');

{$IFDEF HAS_ANONYM}
    TThread.Synchronize(TThread.CurrentThread,
      procedure begin
{$ENDIF}

        AddConnectionTab(TnrSocket(Sender));

{$IFDEF HAS_ANONYM}
      end);
{$ENDIF}
    end;

  end;
  actnACK.Execute;
end;

procedure TfrmMain.pmENQClick(Sender: TObject);
begin
   SendStr(#05);
end;

procedure TfrmMain.pmEOTClick(Sender: TObject);
begin
   SendStr(#04);
end;

procedure TfrmMain.pmEtxClick(Sender: TObject);
begin
    SendStr(#03);
end;

procedure TfrmMain.Timer1Timer(Sender: TObject);
var
  request_str: string;
  nrSocket1: TnrSocket;
begin
  if protocol_type = 'HL7' then
  begin
      Timer1.Enabled := false;
      if (Pos('PID|',astmPacket) > 0) or (Pos('QRD|',astmPacket) > 0) then
      begin
          memLog.Lines.Add('HL7 Session Finished');
          memLog.Lines.Add('Start parsing');
          qryParse.Close;
          qryParse.ParamByName('p_analyzer_message').AsWideString := astmPacket;
          qryParse.ParamByName('p_analyzer_id').AsInteger := analyzer_id;
          qryParse.Open;
          memLog.Lines.Add(qryParse.FieldByName('p_result_message').AsWideString);

          astmPacket := '';

          if qryParse.FieldByName('p_result_code').AsWideString = 'QR' then
          begin
              memLog.Lines.Add('Send order');
              Sendstr(qryParse.FieldByName('p_result_message').AsWideString);
              sleep(d);
              Sendstr(#06);
          end
          else
          if qryParse.FieldByName('p_result_code').AsWideString = 'Q' then
          begin
              memLog.Lines.Add('Send order');

              genASTM.Close;
              genASTM.ParamByName('p_service_barcode').Value := qryParse.FieldByName('p_barcode').AsWideString;
              genASTM.ParamByName('p_analyzer_id').Value := analyzer_id;
              genASTM.ParamByName('p_mode').Value := '';
              genASTM.Open;
              genASTM.First;
              while not genASTM.Eof do
              begin
                  memLog.Lines.Add(genASTM.FieldByName('result').AsString);
                  Sendstr(genASTM.FieldByName('result').AsString);
                  sleep(d);
                  genASTM.Next;
              end;
          end;
          memLog.Lines.Add('End parsing');
          astmPacket := '';
//          Timer1.Enabled := false;
       end;
  end
  else
  if protocol_type = 'ASTM2' then
  begin
      Timer1.Enabled := false;
      if (Pos(char(5),astmPacket) > 0)  then
      begin
          astmPacket := '';
          memLog.Lines.Add('ASTM Session Started');
      end
      else
      if (Pos(char(4),astmPacket) > 0)  then
      begin
          memLog.Lines.Add('ASTM Session Finished');
          qryParse.Close;
          qryParse.ParamByName('p_analyzer_message').AsWideString := astmPacket;
          qryParse.ParamByName('p_analyzer_id').AsInteger := analyzer_id;
          qryParse.Open;
          astmPacket := '';
          memLog.Lines.Add('Start parsing');
          memLog.Lines.Add(qryParse.FieldByName('p_result_message').AsWideString);
          memLog.Lines.Add('End parsing');

          if qryParse.FieldByName('p_result_code').AsWideString = 'Q' then
          begin
              memLog.Lines.Add('Send order');
              genASTM.Close;
              genASTM.ParamByName('p_service_barcode').Value := qryParse.FieldByName('p_barcode').AsWideString;
              genASTM.ParamByName('p_analyzer_id').Value := analyzer_id;
              genASTM.ParamByName('p_mode').Value := '';
              genASTM.Open;
              if chbxServer.Checked then
               nrSocket1 := nrSocketServer
              else
               nrSocket1 := nrSocketClient;
      //        SendStr(#05);
      //        sleep(d);
              while not genASTM.Eof do
              begin
                  request_str := genASTM.FieldByName('result').AsString;
      //                  request_str := request_str + calcCRC(request_str);
                  if nrAstm1.CRCCheck then
                    request_str := char(2) + request_str + calcCRC(request_str) + char(13)  + char(10)
                  else
                    request_str := char(2) + request_str;
                  memLog.Lines.Add(request_str);
                  SendStr(request_str);
      //            nrAstm1.SendRecord(request_str);
                  genASTM.Next;
      //            while text_answer = '' do nrAstm1.Tag := 0;

                  sleep(d);
              end;
      //        sleep(d);
      //        SendStr(#04);
          end
          else if qryParse.FieldByName('p_result_code').AsWideString = 'QR' then
          begin
              memLog.Lines.Add('Send order');
              Sendstr(qryParse.FieldByName('p_result_message').AsWideString);
              sleep(d);
          end;
      end;
      Sendstr(#06);
  end
  else if protocol_type = 'CYAN' then
  begin
      Timer1.Enabled := false;
      if (Pos('Serial No.:',astmPacket) > 0) and (Pos(char(4),astmPacket) > 0) then
      begin
          memLog.Lines.Add('CYAN Session Finished');
          memLog.Lines.Add('Start parsing');
          qryParse.Close;
          qryParse.ParamByName('p_analyzer_message').AsWideString := astmPacket;
          qryParse.ParamByName('p_analyzer_id').AsInteger := analyzer_id;
          qryParse.Open;
          memLog.Lines.Add(qryParse.FieldByName('p_result_message').AsWideString);
          memLog.Lines.Add('End parsing');
          astmPacket := '';
//          Timer1.Enabled := false;
       end;
  end
  else if protocol_type = 'JUNIOR' then
  begin
// шукаємо замовлення
      qryGet.Close;
      qryGet.SQL.Text := 'CALL list_data(''{"p_mode":"LAB_JUNIOR"}'')';
      qryGet.Open;
      if qryGet.FieldByName('exec_service_id').AsInteger > 0 then
      begin
          Sendstr(qryGet.FieldByName('message').asString);
          memLog.Lines.Add('Send order');
          memLog.Lines.Add(qryGet.FieldByName('message').asString);
          qryExec.SQL.Text := 'CALL upd_exec_service_state('+qryGet.FieldByName('exec_service_id').asString+',3,0)';
          qryExec.Execute;
          memLog.Lines.Add('Update status - ' + qryGet.FieldByName('exec_service_id').asString);
      end;
// шукаємо результат
//       qryGet.Close;
//       qryGet.SQL.Text := 'CALL list_data(''{"p_mode":"LAB_JUNIOR_RESULT"}'')';
//       qryGet.Open;
//       if qryGet.FieldByName('exec_service_id').AsInteger > 0 then
//       begin
//           Sendstr(qryGet.FieldByName('message').asString);
//           memLog.Lines.Add('Send request');
//           memLog.Lines.Add(qryGet.FieldByName('message').asString);
//           qryExec.SQL.Text := 'CALL upd_exec_service_state('+qryGet.FieldByName('exec_service_id').asString+',4,0)';
//           qryExec.Execute;
//           memLog.Lines.Add('Update status - ' + qryGet.FieldByName('exec_service_id').asString);
//       end;

  end;
  MemoSizeControl(memLog);
  MemoSizeControl(memo2);

end;

procedure TfrmMain.TimerConnectTimer(Sender: TObject);
begin
    try
      AccuracyMedConnect.Ping;
    except
      AccuracyMedConnect.Close;
    end;

    try
      AccuracyMedConnect.Open;
    except
    end;
    if not AccuracyMedConnect.Connected then
    begin
      AccuracyMedConnect.Options.Direct := false;
      try
        AccuracyMedConnect.Open;
      except
        on e: Exception do memLog.Lines.Add('Not connected to database ' + e.Message);
      end;
    end;

    if chbxServer.Checked then exit;
// проверяем сервер
    if (nrSocketClient.Active = true) and (actnStart.Enabled = false) then
    begin
        if SecondsBetween(last_recive, Now) > 60 then
        try
          last_recive := now;
          nrSocketClient.SendChar(char(0));
        except
          on e: Exception do memLog.Lines.Add('Not connected to server ' + e.Message);
        end;
    end;

    if (nrComm1.Active = false) and (nrSocketClient.Active = false) and (actnStart.Enabled = false) then
    begin
        actnStartExecute(nil);
        MemoSizeControl(memLog);
    end;
end;

procedure TfrmMain.AccuracyMedConnectAfterConnect(Sender: TObject);
begin
    memLog.Lines.Add('Connected to database ' + AccuracyMedConnect.Server + ':' + IntToStr(AccuracyMedConnect.Port) + ' / ' + AccuracyMedConnect.Database);
end;

procedure TfrmMain.pmAckClick(Sender: TObject);
begin
  SendStr(#06);
end;

procedure TfrmMain.actnACKExecute(Sender: TObject);
begin
    if protocol_type <> 'HL7' then
     nrAstm1.ReplyACK
    else
     SendStr(#06);
end;

procedure TfrmMain.actnStartExecute(Sender: TObject);
var
  ini: TMemIniFile;
begin
  if PageControl1.ActivePage = tsCOM then
  begin
      if protocol_type = 'ASTM' then
      begin
        nrComm1.DataProcessor := nrAstm1;
        nrAstm1.Active := true;
      end
      else
      begin
        nrComm1.DataProcessor := nil;
        nrAstm1.Active := false;
      end;
      try
        nrComm1.Active := true;
      except
        on E: Exception do
        begin
            memLog.Lines.Add(E.Message);
            raise;
        end;
      end;
      CheckBox1.Checked := nrComm1.Active;
      if protocol_type = 'JUNIOR' then Timer1.Enabled := true;
  end
  else
  begin
      if chbxServer.Checked then
      begin
          nrSocketServer.UDP := cbProtocol.ItemIndex = 1;
          nrSocketServer.Host := ''; //eHost.Text;
          nrSocketServer.Port := ePort.Text;
          nrSocketServer.ServerMode := True;
          try
            if protocol_type <> 'ASTM' then
            begin
                nrSocketServer.DataProcessor := nil;
                nrAstm1.Active := false;
            end
            else
            begin
                nrSocketServer.DataProcessor := nrAstm1;
                nrAstm1.Active := true;
            end;
            nrSocketServer.Active := True;
          except
            on E: Exception do
            begin
               memLog.Lines.Add(E.Message);
            end;
          end;
          chActive.Checked := nrSocketServer.Active;
      end
      else
      begin
          nrSocketClient.UDP := cbProtocol.ItemIndex = 1;
          nrSocketClient.Host := eHost.Text;
          nrSocketClient.Port := ePort.Text;
          nrSocketClient.ServerMode := False;
          try
            if protocol_type <> 'ASTM' then
            begin
               nrSocketClient.DataProcessor := nil;
               nrAstm1.Active := false;
            end
            else
            begin
               nrSocketClient.DataProcessor := nrAstm1;
               nrAstm1.Active := true;
            end;
            nrSocketClient.Active := True;
          except
            on E: Exception do
            begin
               if (copy(memLog.Lines[memLog.Lines.Count-2],21,1000)=copy(memLog.Lines[memLog.Lines.Count-1],21,1000)) and
                  (copy(memLog.Lines[memLog.Lines.Count-1],21,1000) = E.Message) then
                 memLog.Lines[memLog.Lines.Count-1] := FormatDateTime('dd.mm.yyyy hh:nn:ss',now)+' '+E.Message
               else
                 memLog.Lines.Add(FormatDateTime('dd.mm.yyyy hh:nn:ss',now)+' '+E.Message);
            end;
          end;
          chActive.Checked := nrSocketServer.Active;
      end
  end;
  actnStart.Enabled := false;
  actnStop.Enabled := true;
  TimerConnect.Enabled := not chbxServer.Checked;
  PageControl1.Enabled := false;
  Ini := TMemIniFile.Create( ExtractFilePath(Application.ExeName) + 'AconnectAstm.ini', TEncoding.Utf8 );
  try
    Ini.WriteInteger( 'connect', 'port', AccuracyMedConnect.Port);
    Ini.WriteString( 'connect', 'server', AccuracyMedConnect.Server);
    Ini.WriteString( 'connect', 'dbname', AccuracyMedConnect.Database);
    Ini.WriteInteger( 'connect', 'analyzer_id', analyzer_id);
    if PageControl1.ActivePage = tsCOM then
    begin
        Ini.WriteString( 'tcp', 'host', '');
        Ini.WriteInteger('com', 'numb', nrComm1.ComPortNo);
        Ini.WriteInteger('com', 'BaudRate', nrComm1.BaudRate);
        Ini.WriteInteger('com', 'ByteSize', nrComm1.ByteSize);

        if nrComm1.Parity = pEven then
         Ini.WriteString('com', 'Parity', 'pEven')
        else if nrComm1.Parity = pMark then
         Ini.WriteString('com', 'Parity', 'pMark')
        else if nrComm1.Parity = pNone then
         Ini.WriteString('com', 'Parity', 'pNone')
        else if nrComm1.Parity = pOdd then
         Ini.WriteString('com', 'Parity', 'pOdd')
        else if nrComm1.Parity = pSpace then
         Ini.WriteString('com', 'Parity', 'pSpace');

        if nrComm1.StopBits = sbOne then
         Ini.WriteString('com', 'StopBits', 'sbOne')
        else if nrComm1.StopBits = sbOneAndHalf then
         Ini.WriteString('com', 'StopBits', 'sbOneAndHalf')
        else if nrComm1.StopBits = sbTwo then
         Ini.WriteString('com', 'StopBits', 'sbTwo');

        if nrComm1.StreamProtocol = spNone then
         Ini.WriteString('com', 'StreamProtocol', 'spNone')
        else if nrComm1.StreamProtocol = spXonXoff then
         Ini.WriteString('com', 'StreamProtocol', 'spXonXoff')
        else if nrComm1.StreamProtocol = spHardware then
         Ini.WriteString('com', 'StreamProtocol', 'spHardware');
    end
    else
    begin
        Ini.WriteString( 'tcp', 'host', eHost.Text);
        Ini.WriteString( 'tcp', 'port', ePort.Text);
        Ini.WriteBool( 'tcp', 'server', chbxServer.Checked);
        Ini.WriteInteger( 'tcp', 'udp', cbProtocol.ItemIndex);
    end;
  finally
      Ini.UpdateFile;
      Ini.Free;
  end;

end;

procedure TfrmMain.actnStopExecute(Sender: TObject);
begin
  try
    if nrComm1.Active then nrComm1.Active := false;
    if nrAstm1.Active then nrAstm1.Active := false;
    if chbxServer.Checked then
    begin
        if slConnection.Count > 0 then
        try
          TnrSocket(StrToInt(slConnection[0])).Active := false;
        except
        end;
        nrSocketServer.Active := false;
    end
    else nrSocketClient.Active := false;
//    if nrSocket1.ServerMode then DeleteConnectionAll;
  except
  end;
  actnStart.Enabled := true;
  actnStop.Enabled := false;
  PageControl1.Enabled := true;
  chActive.Checked := nrSocketServer.Active or nrSocketClient.Active;
  if protocol_type = 'JUNIOR' then Timer1.Enabled := false;
end;

procedure TfrmMain.actnTestExecute(Sender: TObject);
begin
    MemoSend.Visible := true;
    btnSend.Visible := true;
end;

procedure TfrmMain.AddConnectionTab(sockClient: TnrSocket);
var i:integer;
begin
  i := slConnection.Count;
  slConnection.Add(IntToStr(NativeInt(sockClient)));

//  memLog.Lines.Add(IntToStr(NativeInt(sockClient)));

  sockClient.Terminal := Memo2;
  sockClient.TerminalUsage := tuBoth;
  sockClient.Log := nrLogFile1;
  sockClient.OnAfterReceive := nrComm1AfterReceive;
  sockClient.TerminalEcho := true;
  sockClient.OnClose := nrSocketClientClose;
  sockClient.OnConnect := nrSocketClientConnect;
  if protocol_type = 'ASTM' then
  begin
     sockClient.DataProcessor := nrAstm1;
     nrAstm1.Active := true;
  end
  else
  begin
     sockClient.DataProcessor := nil;
     nrAstm1.Active := false;
  end;
//  sockClient.SendString(#6); // ask

{  pgNew.Visible := true;
  pgNew.Enabled := True;
  pgConnections.ActivePageIndex := 0;}
end;

procedure TfrmMain.DeleteConnectionTab(sockClient: TnrSocket);
var i:integer;
begin
 for i  := 0 to slConnection.Count - 1 do
    if TnrSocket(StrToInt(slConnection[i])) = sockClient then
    begin
        if (sockClient <> nrSocketServer) and (sockClient <> nrSocketClient) then
        begin
            sockClient.Terminal := nil;
            sockClient.Active := false;
            FreeAndNil(sockClient);
        end;
        slConnection.Delete(i);
        break;
     end;
end;

procedure TfrmMain.DeleteConnectionAll;
begin
 while slConnection.Count > 0 do
 begin
    TnrSocket(StrToInt(slConnection[0])).Active := False;
//    TnrSocket(StrToInt(slConnection[0])).Free;
//    slConnection.Delete(0);
  end;
end;


procedure TfrmMain.pmSOTClick(Sender: TObject);
begin
   SendStr(#02);
end;

procedure TfrmMain.pmSTXClick(Sender: TObject);
begin
   SendStr(#01);
end;

procedure TfrmMain.MemoSizeControl(vMemo:TMemo);
var
 i:integer;
begin
  if (vMemo.Lines.Count) > 2000 then vMemo.Lines.Clear;
{  begin
    for I := 1 to 1000 do
      vMemo.Lines.Delete(1);
  end;}
end;

// initialization

//   ReportMemoryLeaksOnShutdown := True;

end.
