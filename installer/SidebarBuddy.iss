; Sidebar Buddy — Inno Setup Script
; Requires Inno Setup 6+
; Build: iscc installer\SidebarBuddy.iss

#define AppName        "Sidebar Buddy"
#define AppVersion     "1.0.0"
#define AppPublisher   "Inferno Creative Studio"
#define AppURL         "https://sidebarbuddy.com"
#define AppExeName     "SidebarBuddy.exe"
#define BuildDir       "..\bin\Release\net8.0-windows"
#define DotNetVersion  "8.0"

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
; Main binaries
Source: "{#BuildDir}\{#AppExeName}";           DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\SidebarBuddy.dll";        DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\SidebarBuddy.deps.json";  DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\SidebarBuddy.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
; Skin textures
Source: "{#BuildDir}\textures\*"; DestDir: "{app}\textures"; Flags: ignoreversion recursesubdirs

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

[Code]
// ── .NET 8 Desktop Runtime check ──────────────────────────────────────────

function DotNetRuntimeInstalled: Boolean;
var
  ResultCode: Integer;
begin
  // Use dotnet --list-runtimes via cmd and look for Microsoft.WindowsDesktop.App 8.
  Result := Exec(
    ExpandConstant('{cmd}'),
    '/c dotnet --list-runtimes 2>nul | findstr /B "Microsoft.WindowsDesktop.App 8." > nul',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode
  ) and (ResultCode = 0);
end;

function InitializeSetup: Boolean;
var
  Answer: Integer;
begin
  Result := True;

  if not DotNetRuntimeInstalled then
  begin
    Answer := MsgBox(
      '.NET 8 Desktop Runtime is required to run Sidebar Buddy but was not found on this computer.' + #13#10 + #13#10 +
      'Click OK to open the Microsoft download page, then re-run this installer after installing .NET 8.' + #13#10 +
      'Click Cancel to abort installation.',
      mbConfirmation, MB_OKCANCEL
    );
    if Answer = IDOK then
      ShellExec('open', 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe', '', '', SW_SHOW, ewNoWait, Answer);
    Result := False;
  end;
end;
