#define MyAppName "ELITE Launcher"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "ELITE"
#define MyAppExeName "ELITE.exe"
[Setup]
AppId={{8B197B4A-9D17-4D9D-A8B0-0E1700000001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\ELITE Launcher
DefaultGroupName=ELITE Launcher
OutputDir=..\installer-output
OutputBaseFilename=ELITE-Setup-x64
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
PrivilegesRequired=lowest
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\ELITE Launcher"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\ELITE Launcher"; Filename: "{app}\{#MyAppExeName}"
[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir ELITE Launcher"; Flags: postinstall nowait skipifsilent
