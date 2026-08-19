; =============================================================================
;  Truck Loading Slip Management System - installer
;
;  Build with Inno Setup 6:  ISCC.exe UitvalSlips.iss
;  Or run Build-Package.ps1, which stages a clean payload first and then calls it.
; =============================================================================

#define AppName      "Uitval Slips"
; Read from the built exe rather than typed here. AssemblyInfo.cs is the single place
; a version number is set; this follows it. Two hand-maintained numbers had already
; drifted apart once - the installer said 1.0.0 while the program inside said 1.1.0.0 -
; which makes "which version are you running?" a question with two answers on a support
; call. If the payload has not been staged this fails the build outright, which is the
; right outcome: better than producing a correctly-built installer wearing a wrong label.
#define AppVersion   GetVersionNumbersString(AddBackslash(SourcePath) + "payload\SlipManagement2.exe")
#define AppPublisher "John Herbst"
#define AppExe       "SlipManagement2.exe"

[Setup]
; NEVER change this GUID. It is the only thing that tells Windows a later setup.exe
; is an UPGRADE of this program rather than a second, separate copy of it. Change it
; and the customer ends up with two installations, two Start Menu entries, and no
; idea which one their shortcut points at.
AppId={{6DFEB9DE-A4F7-4D49-9503-67609D024A0F}

AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

; The licence must be read and accepted before Next becomes available. This is the
; record that the terms were put in front of whoever installed it.
LicenseFile=..\LICENCE.txt
InfoAfterFile=AfterInstall.txt

OutputDir=Output
OutputBaseFilename=UitvalSlips-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}

; Program Files needs elevation. The data folder below is what ordinary users write to.
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

; Uses Restart Manager to close the program if it is running, so an upgrade is not
; blocked by a locked exe. Without this, upgrading while the operator has it open
; fails halfway with a file-in-use error.
CloseApplications=yes
RestartApplications=no

[Files]
; payload\ is staged by Build-Package.ps1 - stripped of .pdb, .xml documentation and
; any database. Never point this at bin\Release directly.
Source: "payload\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
; Created here, explicitly granting ordinary users write access.
;
; A folder created in ProgramData by an ADMIN installer does not give standard users
; permission to modify files inside it. Without this the program installs perfectly,
; runs perfectly for whoever installed it, and then cannot save a single slip for the
; operator - a failure that would never appear on the developer's machine.
Name: "{commonappdata}\UitvalSlips";         Permissions: users-modify
Name: "{commonappdata}\UitvalSlips\Backups"; Permissions: users-modify

[Icons]
Name: "{group}\{#AppName}";      Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a shortcut on the desktop"; GroupDescription: "Shortcuts:"

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName} now"; Flags: nowait postinstall skipifsilent

; -----------------------------------------------------------------------------
;  NOTE: there is deliberately no [UninstallDelete] section.
;
;  Uninstalling removes the program and leaves C:\ProgramData\UitvalSlips untouched -
;  the slip database, every backup, and the licence. Those records are the customer's
;  property and outlive the software; LICENCE.txt says so, and an uninstaller that
;  quietly took a quarry's weighbridge history with it would break that promise.
; -----------------------------------------------------------------------------

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    MsgBox('Uitval Slips has been removed.'#13#10#13#10 +
           'Your slips, backups and licence have NOT been deleted. They remain in:'#13#10#13#10 +
           '    C:\ProgramData\UitvalSlips'#13#10#13#10 +
           'Re-installing will pick them up again automatically. If you want them gone ' +
           'for good, delete that folder by hand - but be certain first, because printed ' +
           'slips cannot be recovered once it is removed.',
           mbInformation, MB_OK);
end;
