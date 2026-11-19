; ---------------------------------------------------------------------------
; Shared /config= command-line handling.
;
; Included by both SmtpRelayInstaller.iss (the real installer) and
; test/probe.iss (the automated path-resolution test) so the logic under
; test is literally the logic that ships.
;
; Relative path handling
; ----------------------
; A relative /config= path is ambiguous: it may be written relative to the
; shell's working directory, or relative to the installer itself. Real-world
; usage (see issues #19 / #29) looks like:
;
;     cd C:\deploy
;     assets\SmtpRelaySetup.exe /SILENT /config="assets\config.json"
;
; Here BOTH paths are relative to C:\deploy, so the config is at
; C:\deploy\assets\config.json -- which is neither "relative to CWD as the
; installer sees it" (Setup relaunches itself from a temp copy, so the process
; CWD is unreliable) nor "relative to the installer's own folder" (that would
; give C:\deploy\assets\assets\config.json).
;
; Rather than guess which base is correct, we try each plausible base in turn
; and take the first one that actually exists on disk. Every candidate is
; gated on FileExists, so we can never silently resolve to a wrong path.
; ---------------------------------------------------------------------------

var
  ConfigParamGiven: Boolean;    // /config= was supplied (regardless of success)
  ResolvedConfigPath: String;   // absolute path to an existing file, or ''
  ConfigSearchLog: String;      // every candidate tried, for diagnostics

// Returns the raw value of /config="..." from the command line, or ''.
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
      // Strip surrounding quotes if the shell left them in place
      if (Length(Result) >= 2) and (Result[1] = '"') and (Result[Length(Result)] = '"') then
        Result := Copy(Result, 2, Length(Result) - 2);
      Exit;
    end;
  end;
end;

// True for "C:\..." and "\\server\share\..." style paths.
// Hand-rolled rather than relying on PathIsRooted so there is one less
// Inno Setup built-in to be wrong about.
function IsAbsolutePath(const P: String): Boolean;
begin
  Result := False;
  if Length(P) >= 2 then
  begin
    if P[2] = ':' then
      Result := True
    else if (P[1] = '\') and (P[2] = '\') then
      Result := True;
  end;
end;

// Records a candidate in the search log and returns True if the file is there.
function TryConfigCandidate(const Candidate: String; var Found: String): Boolean;
begin
  Result := False;
  if Candidate = '' then Exit;

  if FileExists(Candidate) then
  begin
    ConfigSearchLog := ConfigSearchLog + '[HIT ] ' + Candidate + #13#10;
    Found := Candidate;
    Result := True;
  end
  else
    ConfigSearchLog := ConfigSearchLog + '[miss] ' + Candidate + #13#10;
end;

// Resolves the raw /config= value to an existing absolute path, or '' when
// the file cannot be found under any candidate base.
function ResolveConfigPath(const Raw: String): String;
var
  setupExe, setupDir, parentDir: String;
begin
  Result := '';
  if Raw = '' then Exit;

  // {srcexe} is the ORIGINAL Setup.exe path. ParamStr(0) is not safe here:
  // Setup copies itself into %TEMP% and relaunches, so ParamStr(0) can point
  // at the temp copy rather than where the user actually launched from.
  setupExe  := ExpandConstant('{srcexe}');
  setupDir  := ExtractFileDir(setupExe);
  parentDir := ExtractFileDir(setupDir);

  ConfigSearchLog := 'raw      = ' + Raw + #13#10 +
                     'srcexe   = ' + setupExe + #13#10 +
                     'setupDir = ' + setupDir + #13#10 +
                     'parent   = ' + parentDir + #13#10 +
                     'cwd(exp) = ' + ExpandFileName('.') + #13#10;

  // Absolute paths are used as given -- no searching.
  if IsAbsolutePath(Raw) then
  begin
    if TryConfigCandidate(Raw, Result) then Exit;
    Result := '';
    Exit;
  end;

  // 1. Relative to the process working directory. Correct when the CWD
  //    survived Setup's self-relaunch.
  if TryConfigCandidate(ExpandFileName(Raw), Result) then Exit;

  // 2. Relative to the folder holding Setup.exe.
  //    Covers:  setup.exe /config="config.json"  (both side by side)
  if (setupDir <> '') and TryConfigCandidate(AddBackslash(setupDir) + Raw, Result) then Exit;

  // 3. Relative to the PARENT of the folder holding Setup.exe.
  //    Covers:  cd C:\deploy
  //             assets\setup.exe /config="assets\config.json"
  //    which is the exact shape reported in issues #19 / #29.
  if (parentDir <> '') and (parentDir <> setupDir) and
     TryConfigCandidate(AddBackslash(parentDir) + Raw, Result) then Exit;

  Result := '';
end;

// True when /config= was supplied. Used by [Run] to suppress the GUI launch.
// Deliberately keyed off the parameter being present, not off resolution
// succeeding, so a bad path does not silently pop the GUI in a silent install.
function ConfigFileProvided: Boolean;
begin
  Result := ConfigParamGiven;
end;

// Parses and resolves /config=. Call from InitializeSetup.
procedure InitConfigParam;
var
  raw: String;
begin
  raw := GetConfigParam;
  ConfigParamGiven := raw <> '';
  if ConfigParamGiven then
    ResolvedConfigPath := ResolveConfigPath(raw);
end;
