#ifndef MyAppVersion
  #define MyAppVersion "0.0.0.0"
#endif

#ifndef MyInstallerVersion
  #define MyInstallerVersion "0.0.0"
#endif

#ifndef MyPlatform
  #define MyPlatform "x64"
#endif

#ifndef MyPublishDir
  #define MyPublishDir "..\..\artifacts\Unpackaged\publish-x64"
#endif

#ifndef MyOutputDir
  #define MyOutputDir "..\..\artifacts\DirectSetup"
#endif

#ifndef MyOutputBaseFilename
  #define MyOutputBaseFilename "TyfloCentrumSetup"
#endif

#ifndef MySetupIconFile
  #define MySetupIconFile "..\..\src\TyfloCentrum.Windows.App\Assets\AppIcon.ico"
#endif

#define MyAppName "TyfloCentrum"
#define MyAppExeName "TyfloCentrum.Windows.App.exe"
#define MyAppPublisher "Michal Dziwisz"
#define MyAppId "{{CFB5E8F1-74C6-47A6-B2DE-2C2DA72A9A9F}"

#if MyPlatform == "ARM64"
  #define MyArchitecturesAllowed "arm64"
#else
  #define MyArchitecturesAllowed "x64compatible"
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyInstallerVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\TyfloCentrum
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
DisableProgramGroupPage=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
SetupIconFile={#MySetupIconFile}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir={#MyOutputDir}
OutputBaseFilename={#MyOutputBaseFilename}
ArchitecturesAllowed={#MyArchitecturesAllowed}
ArchitecturesInstallIn64BitMode={#MyArchitecturesAllowed}
ChangesAssociations=no
UsePreviousAppDir=yes
SetupLogging=yes

[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

[Tasks]
Name: "desktopicon"; Description: "Utworz skrot na pulpicie"; GroupDescription: "Dodatkowe skroty:"

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TyfloCentrum"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\TyfloCentrum"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Uruchom TyfloCentrum"; Flags: nowait postinstall skipifsilent
