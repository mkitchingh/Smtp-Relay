; ---------------------------------------------------------------------------
; Path-resolution probe.
;
; Builds a tiny installer that includes the SAME configpath.iss used by the
; real installer, resolves /config=, writes the outcome to the file given by
; /out=, and then aborts without installing anything.
;
; Driven by .github/workflows/test-config-path.yml, which runs it from a set
; of working directories / layouts and asserts on the resolved path.
; ---------------------------------------------------------------------------

[Setup]
AppName=Config Path Probe
AppVersion=1.0
OutputBaseFilename=probe
OutputDir=Output
Uninstallable=no
CreateAppDir=no
DisableProgramGroupPage=yes

[Code]

#include "..\configpath.iss"

// Returns the value of /out=... , or '' when not supplied.
function GetOutParam: String;
var
  i: Integer;
  param: String;
begin
  Result := '';
  for i := 1 to ParamCount do
  begin
    param := ParamStr(i);
    if Pos('/out=', LowerCase(param)) = 1 then
    begin
      Result := Copy(param, Length('/out=') + 1, Length(param));
      if (Length(Result) >= 2) and (Result[1] = '"') and (Result[Length(Result)] = '"') then
        Result := Copy(Result, 2, Length(Result) - 2);
      Exit;
    end;
  end;
end;

function InitializeSetup: Boolean;
var
  outPath, report: String;
begin
  InitConfigParam;

  report := 'RESOLVED=' + ResolvedConfigPath + #13#10 +
            'GIVEN=' + GetConfigParam + #13#10 +
            '--- search log ---' + #13#10 +
            ConfigSearchLog;

  outPath := GetOutParam;
  if outPath <> '' then
    SaveStringToFile(outPath, report, False);

  // Never actually install anything.
  Result := False;
end;
