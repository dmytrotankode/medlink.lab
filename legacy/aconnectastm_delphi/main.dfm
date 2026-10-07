object frmMain: TfrmMain
  Left = 0
  Top = 0
  Caption = 'AconnectAstm '
  ClientHeight = 570
  ClientWidth = 827
  Color = clBtnFace
  Font.Charset = DEFAULT_CHARSET
  Font.Color = clWindowText
  Font.Height = -11
  Font.Name = 'Tahoma'
  Font.Style = []
  OldCreateOrder = False
  PopupMenu = PopupMenu1
  OnClose = FormClose
  OnCreate = FormCreate
  OnShow = FormShow
  PixelsPerInch = 96
  TextHeight = 13
  object memLog: TMemo
    Left = 0
    Top = 142
    Width = 827
    Height = 428
    Align = alBottom
    Anchors = [akLeft, akTop, akRight, akBottom]
    ScrollBars = ssBoth
    TabOrder = 0
  end
  object Memo2: TMemo
    Left = 293
    Top = 0
    Width = 516
    Height = 109
    Color = clBlack
    Font.Charset = DEFAULT_CHARSET
    Font.Color = clYellow
    Font.Height = -11
    Font.Name = 'Courier'
    Font.Style = []
    ParentFont = False
    ScrollBars = ssBoth
    TabOrder = 1
  end
  object PageControl1: TPageControl
    Left = -2
    Top = -3
    Width = 289
    Height = 109
    ActivePage = tsCOM
    TabOrder = 2
    object tsCOM: TTabSheet
      Caption = 'COM'
      object Button1: TButton
        Left = 235
        Top = 37
        Width = 33
        Height = 20
        Caption = '...'
        TabOrder = 0
        OnClick = Button1Click
      end
      object nrDeviceBox1: TnrDeviceBox
        Left = 3
        Top = 36
        Width = 217
        Height = 21
        nrComm = nrComm1
        ResetOnChanged = False
        TabOrder = 1
        Text = 'COM1 (Virtual Serial Port 8 (Eltima Software))'
      end
      object CheckBox1: TCheckBox
        Left = 3
        Top = 13
        Width = 81
        Height = 17
        Caption = 'Active'
        Enabled = False
        TabOrder = 2
        OnClick = CheckBox1Click
      end
    end
    object tsTcp: TTabSheet
      Caption = 'TCP/IP'
      ImageIndex = 1
      object Label2: TLabel
        Left = 146
        Top = 39
        Width = 20
        Height = 13
        Caption = 'Port'
      end
      object Label1: TLabel
        Left = 1
        Top = 39
        Width = 22
        Height = 13
        Caption = 'Host'
      end
      object cbProtocol: TComboBox
        Left = 200
        Top = 58
        Width = 81
        Height = 21
        Style = csDropDownList
        ItemIndex = 0
        TabOrder = 0
        Text = 'TCP/IP'
        Items.Strings = (
          'TCP/IP'
          'UDP')
      end
      object ePort: TEdit
        Left = 146
        Top = 58
        Width = 48
        Height = 21
        TabOrder = 1
        Text = '23'
      end
      object eHost: TEdit
        Left = 1
        Top = 58
        Width = 139
        Height = 21
        TabOrder = 2
        Text = 'localhost'
      end
      object chActive: TCheckBox
        Left = 3
        Top = 13
        Width = 97
        Height = 18
        Caption = 'Active'
        Enabled = False
        TabOrder = 3
        OnClick = chActiveClick
      end
      object chbxServer: TCheckBox
        Left = 146
        Top = 13
        Width = 97
        Height = 18
        Caption = 'Server'
        TabOrder = 4
      end
    end
  end
  object MemoSend: TMemo
    Left = 293
    Top = 112
    Width = 185
    Height = 24
    TabOrder = 3
    Visible = False
  end
  object btnSend: TButton
    Left = 495
    Top = 111
    Width = 75
    Height = 25
    Caption = 'Send'
    TabOrder = 4
    Visible = False
    OnClick = btnSendClick
  end
  object Button5: TButton
    Left = 638
    Top = 111
    Width = 75
    Height = 25
    Caption = 'ACK'
    TabOrder = 5
    OnClick = Button5Click
  end
  object Button2: TButton
    Left = 0
    Top = 111
    Width = 75
    Height = 25
    Action = actnStart
    TabOrder = 6
  end
  object Button3: TButton
    Left = 81
    Top = 111
    Width = 75
    Height = 25
    Action = actnStop
    TabOrder = 7
  end
  object Button4: TButton
    Left = 719
    Top = 111
    Width = 75
    Height = 25
    Caption = 'ASCII'
    TabOrder = 8
    OnClick = Button4Click
  end
  object chbxAutostart: TCheckBox
    Left = 179
    Top = 116
    Width = 81
    Height = 17
    Caption = 'Auto start'
    TabOrder = 9
    OnClick = CheckBox1Click
  end
  object Button6: TButton
    Left = 576
    Top = 111
    Width = 25
    Height = 25
    TabOrder = 10
    Visible = False
    OnClick = Button6Click
  end
  object nrComm1: TnrComm
    Log = nrLogFile1
    Active = False
    BaudRate = 19200
    Parity = pSpace
    StopBits = sbOne
    ByteSize = 8
    ComPortNo = 1
    ComPort = cpCOM1
    TraceStates = []
    EventChar = #0
    StreamProtocol = spHardware
    BufferInSize = 4096
    BufferOutSize = 4096
    TimeoutRead = 0
    TimeoutWrite = 100
    RS485Mode = False
    EnumPorts = epFullPresent
    UseMainThread = True
    KeepConnection = False
    Terminal = Memo2
    TerminalUsage = tuBoth
    TerminalEcho = False
    OnAfterReceive = nrComm1AfterReceive
    OnClose = nrComm1Close
    Left = 192
  end
  object nrAstm1: TnrAstm
    Active = False
    Log = nrLogFile1
    Timeout = 15000
    IgnoreSession = False
    RepeatDelay = 10000
    CRCCheck = False
    OnFatalError = nrAstm1FatalError
    MaxFrameSize = 64000
    OnBeginSession = nrAstm1BeginSession
    OnEndSession = nrAstm1EndSession
    OnRecord = nrAstm1Record
    OnACK = nrAstm1ACK
    OnNACK = nrAstm1NACK
    Left = 248
  end
  object nrLogFile1: TnrLogFile
    SizeLimit = 10240000
    FileName = 'AconnectAstm.log'
    DetailLevel = dlDebug
    Options = [loSafeMode, loAutoFileName, loDate, loTime, loDebugOut]
    AnsiOnly = True
    Left = 304
    Top = 8
  end
  object AccuracyMedConnect: TMyConnection
    Database = 'hospital_ge'
    Port = 3308
    Options.UseUnicode = True
    Options.Charset = 'utf8mb3'
    Options.Protocol = mpTCP
    Options.LocalFailover = True
    Username = 'test1'
    Server = 'localhost'
    AfterConnect = AccuracyMedConnectAfterConnect
    LoginPrompt = False
    Left = 437
    EncryptedPassword = '8BFF9AFF93FF8CFF96FF8BFF85FFCEFF9EFF'
  end
  object qryGet: TMyQuery
    Connection = AccuracyMedConnect
    Options.FieldOrigins = foNone
    Left = 536
    Top = 8
  end
  object qryParse: TMyQuery
    Connection = AccuracyMedConnect
    SQL.Strings = (
      'call parse_lab_message('
      ':p_analyzer_message, '
      ':p_analyzer_id, '
      ':p_mode,'
      '@p_barcode,'
      '@p_result_code,'
      '@p_result_message);'
      ''
      
        'select @p_barcode p_barcode, @p_result_code p_result_code, @p_re' +
        'sult_message p_result_message ')
    Options.FieldOrigins = foNone
    Left = 616
    Top = 8
    ParamData = <
      item
        DataType = ftUnknown
        Name = 'p_analyzer_message'
        Value = nil
      end
      item
        DataType = ftUnknown
        Name = 'p_analyzer_id'
        Value = nil
      end
      item
        DataType = ftUnknown
        Name = 'p_mode'
        Value = nil
      end>
  end
  object nrSocketClient: TnrSocket
    Terminal = Memo2
    TerminalUsage = tuBoth
    TerminalEcho = False
    UDP = False
    IPv6 = False
    ServerMode = False
    ConnectionsMax = 0
    Priority = tpNormal
    UseMainThread = True
    OnConnect = nrSocketClientConnect
    OnClose = nrSocketClientClose
    Log = nrLogFile1
    Active = False
    OnAfterReceive = nrComm1AfterReceive
    Left = 336
    Top = 240
  end
  object PopupMenu1: TPopupMenu
    Left = 704
    Top = 128
    object pmAck: TMenuItem
      Caption = 'ACK'
      OnClick = pmAckClick
    end
    object pmENQ: TMenuItem
      Caption = 'ENQ'
      OnClick = pmENQClick
    end
    object pmSTX: TMenuItem
      Caption = 'STX'
      OnClick = pmSTXClick
    end
    object pmEtx: TMenuItem
      Caption = 'ETX'
      OnClick = pmEtxClick
    end
    object pmSOT: TMenuItem
      Caption = 'SOT'
      OnClick = pmSOTClick
    end
    object pmEOT: TMenuItem
      Caption = 'EOT'
      OnClick = pmEOTClick
    end
    object pmASCII: TMenuItem
      Caption = 'ASCII'
      OnClick = pmASCIIClick
    end
  end
  object genASTM: TMyQuery
    Connection = AccuracyMedConnect
    SQL.Strings = (
      'call gen_lab_order(:p_service_barcode, :p_analyzer_id, :p_mode)')
    Options.FieldOrigins = foNone
    Left = 673
    Top = 16
    ParamData = <
      item
        DataType = ftString
        Name = 'p_service_barcode'
        Value = nil
      end
      item
        DataType = ftInteger
        Name = 'p_analyzer_id'
        Value = nil
      end
      item
        DataType = ftString
        Name = 'p_mode'
        Value = nil
      end>
  end
  object ActionList1: TActionList
    Left = 648
    Top = 232
    object actnTest: TAction
      ShortCut = 49236
      OnExecute = actnTestExecute
    end
    object actnStart: TAction
      Caption = 'Start'
      OnExecute = actnStartExecute
    end
    object actnStop: TAction
      Caption = 'Stop'
      Enabled = False
      OnExecute = actnStopExecute
    end
    object actnACK: TAction
      Caption = 'ACK'
      OnExecute = actnACKExecute
    end
  end
  object Timer1: TTimer
    Enabled = False
    OnTimer = Timer1Timer
    Left = 368
    Top = 48
  end
  object TimerConnect: TTimer
    Enabled = False
    Interval = 10000
    OnTimer = TimerConnectTimer
    Left = 744
    Top = 8
  end
  object nrSocketServer: TnrSocket
    Terminal = Memo2
    TerminalUsage = tuBoth
    TerminalEcho = False
    UDP = False
    IPv6 = False
    ServerMode = True
    ConnectionsMax = 0
    Priority = tpNormal
    UseMainThread = True
    OnConnect = nrSocketServerConnect
    OnClose = nrSocketServerClose
    Log = nrLogFile1
    Active = False
    Left = 224
    Top = 240
  end
  object qryExec: TMyCommand
    Connection = AccuracyMedConnect
    Left = 488
    Top = 48
  end
end
