#!/usr/bin/env python3
"""
Validates Inno Setup Pascal Script [Code] sections against known ISS Pascal Script identifiers.
Catches unknown function/procedure calls before pushing to CI.
"""

import re
import sys

# Known Inno Setup Pascal Script built-ins
# Source: https://jrsoftware.org/ishelp/index.php?topic=scriptfunctions
KNOWN_IDENTIFIERS = {
    # Setup steps
    'TSetupStep', 'ssInstall', 'ssPostInstall', 'ssDone',
    # Uninstall steps
    'TUninstallStep', 'usAppMutexCheck', 'usUninstall', 'usDone',
    # Message box
    'MsgBox', 'mbInformation', 'mbError', 'mbConfirmation', 'mbCriticalError',
    'MB_OK', 'MB_YESNO', 'MB_YESNOCANCEL', 'MB_OKCANCEL',
    'IDOK', 'IDYES', 'IDNO', 'IDCANCEL',
    # String functions
    'Copy', 'Pos', 'Length', 'LowerCase', 'UpperCase', 'Trim',
    'StringOfChar', 'IntToStr', 'StrToInt', 'StrToIntDef',
    'Format', 'AddBackslash', 'RemoveBackslash', 'ExtractFilePath',
    'ExtractFileName', 'ExtractFileExt', 'ChangeFileExt',
    # File functions
    'FileExists', 'DirExists', 'FileCopy', 'DeleteFile', 'RenameFile',
    'CreateDir', 'ForceDirectories', 'GetTempFileName',
    # Registry
    'RegKeyExists', 'RegValueExists', 'RegQueryStringValue',
    'RegQueryDWordValue', 'RegWriteStringValue', 'RegWriteDWordValue',
    'RegDeleteKeyIncludingSubkeys', 'RegDeleteValue',
    'HKEY_LOCAL_MACHINE', 'HKEY_CURRENT_USER', 'HKEY_CLASSES_ROOT',
    'HKLM', 'HKCU',
    # Path/expand
    'ExpandConstant', 'ExpandFileName', 'GetShortName',
    'AddQuotes', 'RemoveQuotes',
    'PathIsRooted', 'PathCombine', 'PathNormalizeSlashes',
    'PathHasInvalidCharacters', 'PathSame', 'PathStartsWith', 'PathEndsWith',
    'ExtractFileDir', 'ExtractFileDrive', 'ExtractRelativePath',
    'AddBackslash', 'RemoveBackslash', 'RemoveBackslashUnlessRoot',
    'GetCurrentDir', 'SetCurrentDir',
    # Command line
    'ParamCount', 'ParamStr',
    # Execution
    'Exec', 'ExecAsOriginalUser', 'ShellExec', 'ShellExecAsOriginalUser',
    # Ini
    'GetIniString', 'SetIniString', 'GetIniInt', 'SetIniInt',
    # Misc
    'IsWin64', 'IsAdmin', 'IsAdminInstallMode',
    'WizardDirValue', 'WizardGroupValue', 'WizardNoIcons',
    'WizardSilent', 'IsUninstaller',
    'GetWindowsVersion', 'GetWindowsVersionEx',
    'InstallOnThisVersion', 'SetupMessage',
    'ActiveLanguage', 'CustomMessage',
    'MinimizePathName', 'GetEnv',
    'BringToFrontAndRestore',
    # Pascal built-ins
    'Exit', 'Halt', 'Result',
    'True', 'False',
    'Integer', 'String', 'Boolean', 'Char',
    'begin', 'end', 'if', 'then', 'else', 'for', 'to', 'do',
    'while', 'repeat', 'until', 'var', 'function', 'procedure',
    'and', 'or', 'not', 'div', 'mod',
    # Event functions (user-defined but known signatures)
    'InitializeSetup', 'InitializeSetupEx', 'DeinitializeSetup',
    'CurStepChanged', 'NextButtonClick', 'BackButtonClick',
    'ShouldSkipPage', 'CurPageChanged',
    'InitializeUninstall', 'CurUninstallStepChanged',
    'ConfigFileProvided', 'GetConfigParam',  # project-specific
}

def extract_code_section(iss_text):
    match = re.search(r'^\[Code\](.*?)(?=^\[|\Z)', iss_text, re.MULTILINE | re.DOTALL)
    if not match:
        return None
    return match.group(1)

def find_identifiers(code):
    # Strip string literals to avoid false positives
    code_no_strings = re.sub(r"'[^']*'", "''", code)
    # Strip comments
    code_no_comments = re.sub(r'//[^\n]*', '', code_no_strings)
    # Find all word tokens that look like function/procedure calls
    # (word followed by optional whitespace and opening paren)
    calls = re.findall(r'\b([A-Za-z_][A-Za-z0-9_]*)\s*\(', code_no_comments)
    return calls

def validate(iss_path):
    with open(iss_path, 'r', encoding='utf-8-sig') as f:
        text = f.read()

    code = extract_code_section(text)
    if code is None:
        print("No [Code] section found.")
        return True

    calls = find_identifiers(code)
    unknown = []
    for call in calls:
        if call not in KNOWN_IDENTIFIERS:
            unknown.append(call)

    # Deduplicate preserving order
    seen = set()
    unknown_unique = []
    for u in unknown:
        if u not in seen:
            seen.add(u)
            unknown_unique.append(u)

    if unknown_unique:
        print(f"WARNING: Possibly unknown Inno Setup identifiers in [Code]:")
        for u in unknown_unique:
            print(f"  {u}")
        print()
        print("Verify each against https://jrsoftware.org/ishelp/index.php?topic=scriptfunctions")
        return False
    else:
        print("OK: All identifiers in [Code] are known Inno Setup Pascal Script built-ins.")
        return True

if __name__ == '__main__':
    path = sys.argv[1] if len(sys.argv) > 1 else 'installer/SmtpRelayInstaller.iss'
    ok = validate(path)
    sys.exit(0 if ok else 1)
