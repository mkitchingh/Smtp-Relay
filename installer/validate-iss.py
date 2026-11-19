#!/usr/bin/env python3
"""
Validates Inno Setup Pascal Script [Code] sections against known ISS Pascal
Script identifiers, so an unknown function name is caught here instead of by
ISCC in CI (or, worse, at install time).

Follows #include directives, and auto-discovers functions/procedures declared
anywhere in the included sources so project-local helpers do not have to be
listed by hand.
"""

import os
import re
import sys

# Inno Setup Pascal Script built-ins.
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
    'Format', 'AddBackslash', 'RemoveBackslash',
    'ExtractFilePath', 'ExtractFileDir', 'ExtractFileDrive',
    'ExtractFileName', 'ExtractFileExt', 'ChangeFileExt',
    'ExtractRelativePath',
    # File functions
    'FileExists', 'DirExists', 'FileCopy', 'DeleteFile', 'RenameFile',
    'CreateDir', 'ForceDirectories', 'GetTempFileName',
    'LoadStringFromFile', 'SaveStringToFile',
    'LoadStringsFromFile', 'SaveStringsToFile',
    'FileSize', 'SetFileAttributes', 'GetFileAttributes',
    # Registry
    'RegKeyExists', 'RegValueExists', 'RegQueryStringValue',
    'RegQueryDWordValue', 'RegWriteStringValue', 'RegWriteDWordValue',
    'RegDeleteKeyIncludingSubkeys', 'RegDeleteValue',
    'HKEY_LOCAL_MACHINE', 'HKEY_CURRENT_USER', 'HKEY_CLASSES_ROOT',
    'HKLM', 'HKCU',
    # Path/expand
    'ExpandConstant', 'ExpandFileName', 'GetShortName',
    'AddQuotes', 'RemoveQuotes', 'PathIsRooted',
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
    'MinimizePathName', 'GetEnv', 'GetCurrentDir', 'SetCurrentDir',
    'BringToFrontAndRestore',
    # Pascal built-ins
    'Exit', 'Halt', 'Result',
    'True', 'False',
    'Integer', 'String', 'Boolean', 'Char',
    'begin', 'end', 'if', 'then', 'else', 'for', 'to', 'do',
    'while', 'repeat', 'until', 'var', 'function', 'procedure',
    'and', 'or', 'not', 'div', 'mod', 'const',
    # Event functions called by Setup itself
    'InitializeSetup', 'InitializeSetupEx', 'DeinitializeSetup',
    'CurStepChanged', 'NextButtonClick', 'BackButtonClick',
    'ShouldSkipPage', 'CurPageChanged',
    'InitializeUninstall', 'CurUninstallStepChanged',
}

INCLUDE_RE = re.compile(r'^\s*#include\s+"([^"]+)"', re.MULTILINE)
DECL_RE = re.compile(
    r'^\s*(?:function|procedure)\s+([A-Za-z_][A-Za-z0-9_]*)',
    re.MULTILINE | re.IGNORECASE,
)


def read(path):
    with open(path, 'r', encoding='utf-8-sig') as f:
        return f.read()


def extract_code_section(iss_text):
    m = re.search(r'^\[Code\](.*?)(?=^\[|\Z)', iss_text, re.MULTILINE | re.DOTALL)
    return m.group(1) if m else None


def gather_sources(entry_path):
    """Return [(label, text)] for the entry file's [Code] section plus every
    file it #includes (recursively)."""
    out = []
    seen = set()

    def walk(path, is_entry):
        real = os.path.realpath(path)
        if real in seen:
            return
        seen.add(real)

        text = read(path)
        body = extract_code_section(text) if is_entry else text
        if body is None:
            return

        for inc in INCLUDE_RE.findall(body):
            inc_path = os.path.normpath(
                os.path.join(os.path.dirname(path), inc.replace('\\', os.sep))
            )
            if os.path.exists(inc_path):
                walk(inc_path, False)
            else:
                print(f"ERROR: #include target not found: {inc} (from {path})")
                out.append(('__missing__', ''))

        out.append((path, body))

    walk(entry_path, True)
    return out


def strip_noise(code):
    code = re.sub(r"'(?:''|[^'])*'", "''", code)   # string literals
    code = re.sub(r'//[^\n]*', '', code)            # // comments
    code = re.sub(r'\{[^}]*\}', '', code)           # { } comments
    code = re.sub(r'^\s*;.*$', '', code, flags=re.MULTILINE)  # ; comments
    code = re.sub(r'^\s*#.*$', '', code, flags=re.MULTILINE)  # preprocessor
    return code


def validate(entry_path):
    sources = gather_sources(entry_path)
    if any(label == '__missing__' for label, _ in sources):
        return False
    if not sources:
        print("No [Code] section found.")
        return True

    declared = set()
    for _, body in sources:
        declared.update(DECL_RE.findall(body))

    known = KNOWN_IDENTIFIERS | declared

    unknown = []
    for label, body in sources:
        clean = strip_noise(body)
        for name in re.findall(r'\b([A-Za-z_][A-Za-z0-9_]*)\s*\(', clean):
            if name not in known and (label, name) not in unknown:
                unknown.append((label, name))

    seen = set()
    deduped = []
    for label, name in unknown:
        if name not in seen:
            seen.add(name)
            deduped.append((label, name))

    print(f"Checked: {', '.join(os.path.basename(l) for l, _ in sources)}")
    if declared:
        print(f"Declared in script: {', '.join(sorted(declared))}")

    if deduped:
        print("\nWARNING: Possibly unknown Inno Setup identifiers:")
        for label, name in deduped:
            print(f"  {name}  ({os.path.basename(label)})")
        print("\nVerify each against "
              "https://jrsoftware.org/ishelp/index.php?topic=scriptfunctions")
        return False

    print("OK: All identifiers resolve to built-ins or script declarations.")
    return True


if __name__ == '__main__':
    targets = sys.argv[1:] or ['installer/SmtpRelayInstaller.iss']
    ok = True
    for t in targets:
        print(f"=== {t} ===")
        if not validate(t):
            ok = False
        print()
    sys.exit(0 if ok else 1)
