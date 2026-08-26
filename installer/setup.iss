; Built by CI (.github/workflows/release.yml) via:
;   ISCC.exe /DMyAppVersion=1.2.3 installer\setup.iss
; expects the self-contained publish output in ..\publish (relative to this file).
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

[Setup]
AppId={{6C9F0D2E-6B0C-4C9C-9A9E-5B6B8B0E9F2A}}
AppName=Claude Account Switcher
AppVersion={#MyAppVersion}
AppPublisher=narcosteam
DefaultDirName={localappdata}\Programs\ClaudeAccountSwitcher
DefaultGroupName=Claude Account Switcher
DisableProgramGroupPage=yes
; ponytail: per-user install (no admin/UAC) — required so the app's own
; "check for updates" flow can silently re-run this installer without
; prompting the user for elevation.
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
OutputBaseFilename=ClaudeAccountSwitcherSetup
OutputDir=..\dist
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\ClaudeAccountSwitcher.exe

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Claude Account Switcher"; Filename: "{app}\ClaudeAccountSwitcher.exe"
Name: "{userdesktop}\Claude Account Switcher"; Filename: "{app}\ClaudeAccountSwitcher.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
Filename: "{app}\ClaudeAccountSwitcher.exe"; Description: "Launch Claude Account Switcher"; Flags: nowait postinstall skipifsilent
