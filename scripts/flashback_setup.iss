#define MyAppName "Flashback Printer System"
#define MyAppVersion "1.0"
#define MyAppPublisher "SComps"
#define MyAppURL "https://github.com/SComps/Flashback"
#define MyAppExeName "Flashback.Engine.exe"

#ifndef SourceDir
  #define SourceDir "..\publish\windows"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\Flashback
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=Flashback_Setup
SetupIconFile=..\Flashback.Tray\Assets\printer.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=120
PrivilegesRequired=admin
UninstallDisplayIcon={app}\Flashback.Tray.exe
CloseApplications=yes
RestartIfNeededByRun=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &Desktop shortcut for the Tray Controller"; GroupDescription: "Additional icons:"; Flags: unchecked
Name: "startupicon"; Description: "Launch Tray Controller automatically at &Windows startup"; GroupDescription: "Startup:"; Flags: checkedonce

[Files]
; Main application executables
Source: "{#SourceDir}\Flashback.Engine.exe";         DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\Flashback.Config.3270.exe";    DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\Flashback.Config.Console.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\Flashback.Tray.exe";           DestDir: "{app}"; Flags: ignoreversion
; Font assets
Source: "{#SourceDir}\*.ttf";                        DestDir: "{app}"; Flags: ignoreversion
; Tray icon assets
Source: "..\Flashback.Tray\Assets\printer.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion
Source: "..\Flashback.Tray\Assets\printer.png"; DestDir: "{app}\Assets"; Flags: ignoreversion

[Icons]
Name: "{group}\Flashback Controller";     Filename: "{app}\Flashback.Tray.exe";           WorkingDir: "{app}"
Name: "{group}\Configure Devices";       Filename: "{app}\Flashback.Config.Console.exe"; WorkingDir: "{app}"
Name: "{group}\View Log File";           Filename: "{sys}\notepad.exe";                  Parameters: """{app}\printers.log"""
Name: "{group}\Uninstall Flashback";     Filename: "{uninstallexe}"
Name: "{commondesktop}\Flashback Controller"; Filename: "{app}\Flashback.Tray.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
; Tray controller auto-start at login
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "FlashbackController"; ValueData: """{app}\Flashback.Tray.exe"""; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
; Install Engine service
Filename: "{sys}\sc.exe"; Parameters: "create FlashbackEngine binPath= ""{app}\Flashback.Engine.exe"" DisplayName= ""Flashback Printer Engine"" start= auto"; \
    Description: "Installing Flashback Engine service..."; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "description FlashbackEngine ""High-performance cross-platform printing service for legacy host systems."""; Flags: runhidden waituntilterminated

; Install 3270 Config service
Filename: "{sys}\sc.exe"; Parameters: "create FlashbackConfig3270 binPath= ""{app}\Flashback.Config.3270.exe"" DisplayName= ""Flashback 3270 Config Server"" start= auto"; \
    Description: "Installing Flashback 3270 service..."; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "description FlashbackConfig3270 ""Remote 3270 terminal-based configuration server for Flashback devices."""; Flags: runhidden waituntilterminated

; Start the services
Filename: "{sys}\sc.exe"; Parameters: "start FlashbackEngine";      Flags: runhidden waituntilterminated; Description: "Starting Flashback Engine service..."
Filename: "{sys}\sc.exe"; Parameters: "start FlashbackConfig3270";  Flags: runhidden waituntilterminated; Description: "Starting Flashback 3270 service..."

; Launch tray controller
Filename: "{app}\Flashback.Tray.exe"; Description: "Launch Flashback Tray Controller"; Flags: nowait postinstall skipifsilent; WorkingDir: "{app}"

[UninstallRun]
; Stop and remove services on uninstall
Filename: "{sys}\sc.exe"; Parameters: "stop FlashbackEngine";          Flags: runhidden waituntilterminated; RunOnceId: "StopEngine"
Filename: "{sys}\sc.exe"; Parameters: "delete FlashbackEngine";        Flags: runhidden waituntilterminated; RunOnceId: "DelEngine"
Filename: "{sys}\sc.exe"; Parameters: "stop FlashbackConfig3270";      Flags: runhidden waituntilterminated; RunOnceId: "Stop3270"
Filename: "{sys}\sc.exe"; Parameters: "delete FlashbackConfig3270";    Flags: runhidden waituntilterminated; RunOnceId: "Del3270"
; Kill tray if running
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im Flashback.Tray.exe"; Flags: runhidden waituntilterminated; RunOnceId: "KillTray"

[UninstallDelete]
; Clean up logs on uninstall but preserve config and license
Type: files; Name: "{app}\printers.log"
