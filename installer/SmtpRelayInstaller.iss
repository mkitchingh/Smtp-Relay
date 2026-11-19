[Setup]
AppName=SMTP Relay
AppVersion=1.5.6
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

// Resolved absolute path to the supplied config file.
// Set in InitializeSetup (before UAC elevation changes the working directory)
// and used in CurStepChanged.
var
  ResolvedConfigPath: String;

// Returns the raw value of /config="..." from the command line, or empty string.
function GetConfigParam: String;
var
  i: Integer;
  param: String;
begin
  Result := '';
  for i := 1 to ParamCount do
  begin
    param := ParamStr(i);
    if Pos('/config=', LowerCase(param)) = 1 then
    begin
      Result := Copy(param, Length('/config=') + 1, Length(param));
      // Strip surrounding quotes if present
      if (Length(Result) >= 2) and (Result[1] = '"') and (Result[Length(Result)] = '"') then
        Result := Copy(Result, 2, Length(Result) - 2);
      Exit;
    end;
  end;
end;

// Returns true when a /config= argument was supplied.
function ConfigFileProvided: Boolean;
begin
  Result := ResolvedConfigPath <> '';
end;

// Resolve the config path, handling both absolute paths and paths relative to
// the installer's own directory.  We cannot rely on the shell working directory
// because Inno Setup self-extracts to a temp folder before any Pascal Script
// runs, changing the process CWD away from the launch location.
// Using the installer's directory (ExtractFilePath(ParamStr(0))) is predictable
// and consistent: relative paths are resolved from the same folder as the .exe.
// Also handles /? and /help.
function InitializeSetup: Boolean;
var
  i: Integer;
  p, raw, nl, msg: String;
  installerDir: String;
begin
  Result := True;

  // Handle /? and /help
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
             '      Accepts absolute or relative paths.' + nl +
             '      Relative paths are resolved from the installer''s directory.' + nl +
             '      Suppresses the post-install Config GUI launch.' + nl +
             nl +
             'Examples:' + nl +
             '  SmtpRelaySetup.exe /SILENT /config="C:\deploy\config.json"' + nl +
             '  SmtpRelaySetup.exe /SILENT /config="config.json"  (same folder as installer)';
      MsgBox(msg, mbInformation, MB_OK);
      Result := False;
      Exit;
    end;
  end;

  raw := GetConfigParam;
  if raw <> '' then
  begin
    // If the path is already absolute, use it directly.
    // Otherwise resolve relative to the installer's own directory, which is
    // reliable regardless of UAC elevation or CWD changes.
    if PathIsRooted(raw) then
      ResolvedConfigPath := raw
    else
    begin
      installerDir := ExtractFilePath(ParamStr(0));
      ResolvedConfigPath := AddBackslash(installerDir) + raw;
    end;
  end;
end;

// Copy the supplied config into place before the service starts (always overwrite).
// ssInstall fires after files are extracted but before [Run] entries (sc start), so
// the config is in place when the service starts for the first time.
procedure CurStepChanged(CurStep: TSetupStep);
var
  dest: String;
begin
  if CurStep = ssInstall then
  begin
    if ResolvedConfigPath = '' then Exit;

    // Config lives one level above the service and gui folders
    dest := ExpandConstant('{app}\config.json');

    if not FileCopy(ResolvedConfigPath, dest, False) then
      MsgBox('Warning: could not copy config file to ' + dest + '. You can copy it manually or use the Config GUI.', mbError, MB_OK);
  end;
end;
