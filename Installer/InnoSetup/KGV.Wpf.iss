; Inno Setup script for KGV.Wpf
; Build using: ISCC.exe KGV.Wpf.iss
; Optionally override PublishDir: ISCC.exe /DPublishDir="D:\Programmieren\KGV-Publish\AppFiles\Current" KGV.Wpf.iss

#define MyAppName "KGV Oberrothenbach"
#define MyAppExeName "KGV.Wpf.exe"
#define MyPublisher "KGV Oberrothenbach"
#define MyAppURL "https://abraeuer20-png.github.io/KGV/"

; Keep this GUID stable across all versions. Changing it breaks upgrade/uninstall detection.
#define MyAppId "{{B6D0A3B9-31B1-4DCE-9B3A-0AF1A8B2C0D7}}"

#ifndef PublishDir
  #define PublishDir "D:\Programmieren\KGV-Publish\AppFiles\Current"
#endif

#define MyAppVersion GetFileVersion(PublishDir + "\\" + MyAppExeName)

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=KGV-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupLogging=yes

; If you publish as win-x64, prefer installing in 64-bit mode.
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

; Make upgrades predictable: overwrite files from a previous installation.
UsePreviousAppDir=yes

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktop-Verknüpfung erstellen"; GroupDescription: "Zusätzliche Symbole:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName} starten"; Flags: nowait postinstall skipifsilent
