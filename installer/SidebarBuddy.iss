; Sidebar Buddy — Inno Setup Script
; Requires Inno Setup 6+
; Build: iscc installer\SidebarBuddy.iss

#define AppName        "Sidebar Buddy"
#define AppVersion     "1.0.1"
#define AppPublisher   "Inferno Creative Studio"
#define AppURL         "https://sidebarbuddy.com"
#define AppExeName     "SidebarBuddy.exe"
#define BuildDir       "..\bin\Publish"

[Setup]
AppId={{A3F2C1D4-7E8B-4A5C-9F0D-2B6E3A1C4D7F}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={localappdata}\SidebarBuddy
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=SidebarBuddy-Setup
SetupIconFile=..\SidebarBuddy.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "startupitem"; Description: "Launch {#AppName} when Windows starts"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
; All self-contained publish output
Source: "{#BuildDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}";   Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Windows startup entry
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; \
    Tasks: startupitem; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; \
    Description: "Launch {#AppName}"; \
    Flags: nowait postinstall skipifsilent

