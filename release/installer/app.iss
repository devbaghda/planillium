; app.iss — Mentor Overseer installer.
; Normally built via release\release.ps1 (which passes /DAppVersion=X.Y.Z and
; stages release\dist first). Manual compile: iscc app.iss  (uses the
; fallback version below; dist\ must already exist — run release.ps1 -SkipPackage).

#ifndef AppVersion
  #define AppVersion "0.0.0-dev"
#endif
#define AppName "Mentor Overseer"
#define AppExe "MentorOverseer.App.exe"
#define AppPublisher "the user"

[Setup]
AppId={{F9C80A85-CB70-4F91-ABDC-9C5D9F0FE86C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\output
OutputBaseFilename=MentorOverseer-{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\..\winui\MentorOverseer.App\Assets\icon.ico
UninstallDisplayIcon={app}\{#AppExe}
; No code signing cert yet — see release\README.md. Unsigned installers get
; SmartScreen's "unrecognized app" wall; acceptable for a private, single-user
; install where the user clicks "More info" -> "Run anyway" once.

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "..\dist\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

; Guarantee these exist even though dist\ ships them empty (Inno's [Files]
; wildcard doesn't carry empty directories) — AppPaths.Root walks up from the
; exe looking for a folder that has BOTH config.json and a "plans" subfolder,
; so "plans" must be present at first launch or the app throws immediately.
[Dirs]
Name: "{app}\plans\active"
Name: "{app}\plans\archive"
Name: "{app}\data"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

; Unconditional cleanup, independent of whether THIS install session ever
; wrote the key (the app itself writes it at runtime via Settings -> "Start
; with Windows", not the installer) — otherwise uninstall can leave a Run
; entry pointing at a deleted exe. reg.exe no-ops quietly if the value is
; already absent.
[UninstallRun]
Filename: "{sys}\reg.exe"; Parameters: "delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v MentorOverseer /f"; Flags: runhidden; RunOnceId: "DelRunKey"

[UninstallDelete]
; Deliberately EMPTY — uninstall keeps data\ (progress.db, logs) and
; plans\ (active/archived plan files). Inno only removes directories it
; installed if they're still empty, so real user data left inside survives
; automatically; this section makes that "keep by default" choice explicit.
