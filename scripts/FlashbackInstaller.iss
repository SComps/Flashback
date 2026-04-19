; ============================================================================
; Flashback LPT - Inno Setup Installer Script
; ============================================================================
; Build with: ISCC.exe FlashbackInstaller.iss
; Requires: Inno Setup 6.x+
; ============================================================================

#define MyAppName        "Flashback Printer System"
#define MyAppVersion     "2.1.Alpha"
#define MyAppPublisher   "@ScottJ"
#define MyAppURL         ""
#define SourceDir        "E:\Flashback-Publish"

; Service names (must match the ServiceName values in Program.vb)
#define EngineServiceName      "FlashbackEngine"
#define EngineServiceDisplay   "Flashback Engine"
#define Config3270ServiceName  "FlashbackConfig3270"
#define Config3270ServiceDisplay "Flashback Config 3270"

[Setup]
AppId={{A7F3B2C1-5D4E-4F6A-8B9C-1D2E3F4A5B6C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppSupportURL={#MyAppURL}
DefaultDirName=C:\FLASHBACK-LPT
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=no
OutputDir=E:\Flashback-Inno
OutputBaseFilename=FlashbackLPT_Setup
SetupIconFile={#SourceDir}\Assets\printer.ico
UninstallDisplayIcon={app}\Assets\printer.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Don't append version to install dir
UsePreviousAppDir=yes
; Allow user to change the install directory
DisableDirPage=no
; Minimum Windows 10
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; ============================================================================
; Tasks - Optional components the user can select
; ============================================================================
[Tasks]
Name: "installservices"; Description: "Install Flashback Engine and Config 3270 as Windows Services"; GroupDescription: "System Services:"; Flags: unchecked
Name: "installservices\installtray"; Description: "Launch Flashback Tray at Windows startup (monitoring)"; Flags: unchecked

; ============================================================================
; Files - Everything from the publish directory
; ============================================================================
[Files]
; All files from root of publish directory
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,printers.log,devices.dat,flashback.lic"

; ============================================================================
; Icons (Start Menu Shortcuts)
; ============================================================================
[Icons]
; Configuration tools - always installed
Name: "{group}\Flashback Config Console"; Filename: "{app}\Flashback.Config.Console.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\printer.ico"; Comment: "Flashback Console Configuration"
Name: "{group}\Flashback Config WPF"; Filename: "{app}\Flashback.Config.WPF.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\printer.ico"; Comment: "Flashback WPF Configuration"
;Name: "{group}\Flashback Config WinUI"; Filename: "{app}\Flashback.Config.WinUI.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\printer.ico"; Comment: "Flashback WinUI Configuration"
Name: "{group}\Flashback Engine"; Filename: "{app}\Flashback.Engine.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\printer.ico"; Comment: "Flashback Print Engine"
Name: "{group}\Flashback Config 3270"; Filename: "{app}\Flashback.Config.3270.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\printer.ico"; Comment: "Flashback 3270 Terminal Configuration"
Name: "{group}\Uninstall Flashback LPT"; Filename: "{uninstallexe}"; Comment: "Uninstall Flashback LPT"

; Tray auto-startup is now handled by a Scheduled Task in the [Run] section for silent elevation

; ============================================================================
; Run - Post-install actions
; ============================================================================
[Run]
; Install and start services if the user selected the services task
Filename: "sc.exe"; Parameters: "create {#EngineServiceName} binPath= ""{app}\Flashback.Engine.exe"" DisplayName= ""{#EngineServiceDisplay}"" start= auto"; Flags: runhidden waituntilterminated; Tasks: installservices; StatusMsg: "Installing Flashback Engine service..."
Filename: "sc.exe"; Parameters: "description {#EngineServiceName} ""Flashback LPT Print Engine Service"""; Flags: runhidden waituntilterminated; Tasks: installservices
Filename: "sc.exe"; Parameters: "start {#EngineServiceName}"; Flags: runhidden waituntilterminated; Tasks: installservices; StatusMsg: "Starting Flashback Engine service..."

Filename: "sc.exe"; Parameters: "create {#Config3270ServiceName} binPath= ""\""{app}\Flashback.Config.3270.exe\"" -p {code:Get3270Port}"" DisplayName= ""{#Config3270ServiceDisplay}"" start= auto"; Flags: runhidden waituntilterminated; Tasks: installservices; StatusMsg: "Installing Flashback Config 3270 service..."
Filename: "sc.exe"; Parameters: "description {#Config3270ServiceName} ""Flashback 3270 Terminal Configuration Service"""; Flags: runhidden waituntilterminated; Tasks: installservices
Filename: "sc.exe"; Parameters: "start {#Config3270ServiceName}"; Flags: runhidden waituntilterminated; Tasks: installservices; StatusMsg: "Starting Flashback Config 3270 service..."

; Launch the tray app after install if services were installed
Filename: "{app}\Flashback.Tray.exe"; Flags: nowait postinstall skipifsilent shellexec; Tasks: installservices\installtray; Description: "Launch Flashback Tray Monitor"

; Optionally launch configuration after install
;Filename: "{app}\Flashback.Config.WinUI.exe"; Flags: nowait postinstall skipifsilent unchecked shellexec; Description: "Launch Flashback Configuration (WinUI)"

; Create silent elevated startup task for the tray app instead of standard shortcut
Filename: "schtasks.exe"; Parameters: "/Create /F /TN ""FlashbackTray"" /TR ""\""{app}\Flashback.Tray.exe\"""" /SC ONLOGON /RL HIGHEST"; Flags: runhidden waituntilterminated; Tasks: installservices\installtray; StatusMsg: "Configuring silent elevated Tray start..."

; ============================================================================
; UninstallRun - Pre-uninstall actions (stop and remove services)
; ============================================================================
[UninstallRun]
; Stop and remove the Engine service
Filename: "sc.exe"; Parameters: "stop {#EngineServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "StopEngine"
Filename: "sc.exe"; Parameters: "delete {#EngineServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteEngine"

; Stop and remove the Config 3270 service
Filename: "sc.exe"; Parameters: "stop {#Config3270ServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "StopConfig3270"
Filename: "sc.exe"; Parameters: "delete {#Config3270ServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteConfig3270"

; Kill the tray app if it's running
Filename: "taskkill.exe"; Parameters: "/F /IM Flashback.Tray.exe"; Flags: runhidden waituntilterminated; RunOnceId: "KillTray"

; Remove the silent elevation startup task
Filename: "schtasks.exe"; Parameters: "/Delete /TN ""FlashbackTray"" /F"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteTrayTask"

; ============================================================================
; UninstallDelete - Clean up files that may have been created at runtime
; ============================================================================
[UninstallDelete]
Type: files; Name: "{app}\printers.log"
Type: dirifempty; Name: "{app}"

; ============================================================================
; Code - Pascal Script for advanced logic
; ============================================================================
[Code]
var
  PortPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  PortPage := CreateInputQueryPage(wpSelectTasks,
    'Service Configuration', 'Configure Flashback Config 3270 Service',
    'Please specify the port number for the Flashback Config 3270 service. This parameter will be appended to the service start command.');
  
  PortPage.Add('Port Number:', False);
  PortPage.Values[0] := '3270'; // Default port
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  // Only show the port page if the user has selected the services task
  if (PageID = PortPage.ID) and (not WizardIsTaskSelected('installservices')) then
    Result := True;
end;

function Get3270Port(Param: String): String;
begin
  Result := PortPage.Values[0];
end;

// Before installation, stop any existing services that might be running
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  NeedsRestart := False;

  // Attempt to stop existing services (ignore errors if they don't exist)
  Exec('sc.exe', 'stop {#EngineServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('sc.exe', 'stop {#Config3270ServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  // Kill tray process if running
  Exec('taskkill.exe', '/F /IM Flashback.Tray.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  // Brief pause to allow file handles to release
  Sleep(1500);
end;

// On uninstall, make sure everything is cleaned up
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Stop services before file removal
    Exec('sc.exe', 'stop {#EngineServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('sc.exe', 'stop {#Config3270ServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill.exe', '/F /IM Flashback.Tray.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(2000);

    // Remove services
    Exec('sc.exe', 'delete {#EngineServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('sc.exe', 'delete {#Config3270ServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
