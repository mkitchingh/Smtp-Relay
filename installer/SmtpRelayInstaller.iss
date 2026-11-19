[Setup]
AppName=SMTP Relay
AppVersion=1.5.7
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
Filename: "{app}\gui\SmtpRelay.GUI.exe"; Description: "Open SMTP Relay Config"; Flags: nowait postinstall skipifsilent runascurrentuser; Check: not ConfigFileProvided

[Icons]
Name: "{group}\SMTP Relay Config"; Filename: "{app}\gui\SmtpRelay.GUI.exe"; WorkingDir: "{app}\gui"; IconFilename: "{app}\smtp.ico"

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop ""SMTPRelayService""";   RunOnceId: "StopService"
Filename: "sc.exe"; Parameters: "delete ""SMTPRelayService"""; RunOnceId: "DeleteService"

[Code]

// /config= parsing and path resolution, shared with the automated
// path-resolution test in installer/test/probe.iss.
#include "configpath.iss"

// Handles /? and /help, then resolves /config=.
function InitializeSetup: Boolean;
var
  i: Integer;
  p, nl, msg: String;
begin
  Result := True;

  for i := 1 to ParamCount do
  begin
    p := LowerCase(ParamStr(i));
    if (p = '/?') or (p = '/help') then
    begin
      nl := #13#10;
      msg := 'SMTP Relay Installer' + nl +
             nl +
             'Standard Inno Setup options apply, plus:' + nl +
             nl +
             '  /config="path\to\config.json"' + nl +
             '      Pre-seeds the service configuration file.' + nl +
             '      Suppresses the post-install Config GUI launch.' + nl +
             nl +
             '      Absolute paths are used as given. A relative path is' + nl +
             '      looked for relative to the current directory, then to' + nl +
             '      the installer''s folder, then to its parent folder.' + nl +
             nl +
             'Examples:' + nl +
             '  SmtpRelaySetup.exe /SILENT /config="C:\deploy\config.json"' + nl +
             '  SmtpRelaySetup.exe /SILENT /config="config.json"' + nl +
             '  assets\SmtpRelaySetup.exe /SILENT /config="assets\config.json"';
      MsgBox(msg, mbInformation, MB_OK);
      Result := False;
      Exit;
    end;
  end;

  InitConfigParam;

  // Fail early rather than installing with no config at all. A silent install
  // is typically unattended, so report through the log and the exit code
  // instead of putting a modal dialog on a machine nobody is watching.
  if ConfigParamGiven and (ResolvedConfigPath = '') then
  begin
    msg := 'Setup could not find the config file given by /config=.' + #13#10 + #13#10 +
           ConfigSearchLog + #13#10 +
           'Check the path, or pass an absolute path.';
    Log('/config= could not be resolved.' + #13#10 + ConfigSearchLog);
    if not WizardSilent then
      MsgBox(msg, mbError, MB_OK);
    Result := False;
    Exit;
  end;
end;

// Put the supplied config in place before the service is created and started.
//
// ssInstall fires BEFORE Setup installs any files, so {app} does not exist
// yet and a plain FileCopy here fails -- the directory has to be created
// first. Copying at this point (rather than ssPostInstall) guarantees the
// config is on disk before the [Run] entries create and start the service,
// so the service reads the intended configuration on its very first start.
procedure CurStepChanged(CurStep: TSetupStep);
var
  dest, appDir: String;
begin
  if CurStep = ssInstall then
  begin
    if ResolvedConfigPath = '' then Exit;

    // Config lives one level above the service and gui folders
    appDir := ExpandConstant('{app}');
    dest   := appDir + '\config.json';

    // {app} is not created until Setup starts installing files.
    if not DirExists(appDir) then
    begin
      if not ForceDirectories(appDir) then
        RaiseException('Setup could not create ' + appDir +
                       ' in order to place the /config= file.');
    end;

    if not FileCopy(ResolvedConfigPath, dest, False) then
      RaiseException('Setup could not copy' + #13#10 +
                     '  ' + ResolvedConfigPath + #13#10 +
                     'to' + #13#10 +
                     '  ' + dest + #13#10 + #13#10 +
                     'The service would start with the wrong configuration, ' +
                     'so Setup has stopped.');

    Log('Seeded config from ' + ResolvedConfigPath + ' to ' + dest);
  end;
end;
