[Setup]
AppName=SMTP Relay
AppVersion=1.4
DefaultDirName={pf64}\SMTP Relay
ArchitecturesInstallIn64BitMode=x64
DefaultGroupName=SMTP Relay
OutputBaseFilename=SmtpRelaySetup
OutputDir=Output
Compression=lzma
SolidCompression=yes

SetupIconFile="{#SourcePath}\smtp.ico"
UninstallDisplayIcon="{app}\smtp.ico"

DisableDirPage=yes
DisableProgramGroupPage=yes

[Files]
Source: "{#SourcePath}\smtp.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "output\service\*";        DestDir: "{app}\service"; Flags: ignoreversion recursesubdirs
Source: "output\gui\*";            DestDir: "{app}\gui";    Flags: ignoreversion recursesubdirs

[Run]
Filename: "sc.exe"; Parameters: "create ""SMTPRelayService"" binPath=""{app}\service\SmtpRelay.exe"" start=auto DisplayName=""SMTP Relay"""; Flags: runhidden
Filename: "sc.exe"; Parameters: "description ""SMTPRelayService"" ""SMTP Relay Service"""; Flags: runhidden
Filename: "sc.exe"; Parameters: "start ""SMTPRelayService"""; Flags: runhidden
Filename: "{app}\gui\SmtpRelay.GUI.exe"; Description: "Open SMTP Relay Config"; Flags: nowait postinstall skipifsilent

[Icons]
Name: "{group}\SMTP Relay Config"; Filename: "{app}\gui\SmtpRelay.GUI.exe"; WorkingDir: "{app}\gui"; IconFilename: "{app}\smtp.ico"

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop ""SMTPRelayService""";   RunOnceId: "StopService"
Filename: "sc.exe"; Parameters: "delete ""SMTPRelayService"""; RunOnceId: "DeleteService"
