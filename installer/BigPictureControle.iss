; Compile with Inno Setup 6.7.3 or newer.
; The build wrapper may supply /DAppVersion and /DAppSourceDir.
#ifndef AppVersion
  #define AppVersion "2.2.0"
#endif
#ifndef AppSourceDir
  #define AppSourceDir SourcePath + "..\build\Release"
#endif

[Setup]
AppId={{D97DE4DD-F7BE-4B28-9460-BC79B9E79B96}
AppName=Big Picture Controle
AppVersion={#AppVersion}
AppVerName=Big Picture Controle {#AppVersion}
AppPublisher=Big Picture Controle
DefaultDirName={localappdata}\Programs\BigPictureControle
DefaultGroupName=Big Picture Controle
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
DisableProgramGroupPage=yes
DisableWelcomePage=no
UsePreviousAppDir=yes
UsePreviousTasks=yes
SetupMutex=Local\BigPictureControle.Setup
CloseApplications=no
RestartApplications=no
RestartIfNeededByRun=no
Uninstallable=yes
UninstallDisplayName=Big Picture Controle
UninstallDisplayIcon={app}\BigPictureControle.exe
SetupIconFile=..\assets\app.ico
WizardStyle=modern dark polar includetitlebar
Compression=lzma2
SolidCompression=yes
OutputDir=..\dist
OutputBaseFilename=BigPictureControle-Setup-{#AppVersion}
VersionInfoVersion={#AppVersion}.0
VersionInfoProductName=Big Picture Controle
VersionInfoProductVersion={#AppVersion}
VersionInfoCompany=Big Picture Controle

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Iniciar com o Windows, com a automação ligada"; GroupDescription: "Inicialização:"
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
Source: "{#AppSourceDir}\BigPictureControle.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\Big Picture Controle"; Filename: "{app}\BigPictureControle.exe"; WorkingDir: "{app}"
Name: "{group}\Desinstalar Big Picture Controle"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Big Picture Controle"; Filename: "{app}\BigPictureControle.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
; Uninstall removes this value only if it still points to this installation.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Big Picture Controle"; ValueData: """{app}\BigPictureControle.exe"" --startup"; Tasks: startup

[Run]
Filename: "{app}\BigPictureControle.exe"; Description: "Abrir Big Picture Controle"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
const
  AppMutexName = 'Local\ItaloBigPictureAppV1';
  ExitEventName = 'Local\ItaloBigPictureAppV1-exit';
  StartupKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  StartupValueName = 'Big Picture Controle';
  EventModifyState = $0002;

var
  PreviousStartupCommand: String;
  PreviousStartupOwned: Boolean;
  PreviousStartupCaptured: Boolean;

function IsAppStartupCommand(Command: String): Boolean;
var
  ExePath, Arguments, Tail: String;
  QuoteEnd, SuffixStart: Integer;
begin
  Result := False;
  Command := Trim(Command);
  if Command = '' then
    Exit;

  if Command[1] = '"' then
  begin
    Tail := Copy(Command, 2, Length(Command) - 1);
    QuoteEnd := Pos('"', Tail);
    if QuoteEnd = 0 then
      Exit;
    ExePath := Copy(Tail, 1, QuoteEnd - 1);
    Arguments := Trim(Copy(Tail, QuoteEnd + 1, Length(Tail)));
  end
  else
  begin
    SuffixStart := Length(Command) - Length(' --startup') + 1;
    if SuffixStart <= 1 then
      Exit;
    if CompareText(Copy(Command, SuffixStart, Length(Command)),
      ' --startup') <> 0 then
      Exit;
    ExePath := Trim(Copy(Command, 1, SuffixStart - 1));
    Arguments := '--startup';
  end;

  Result := (CompareText(ExtractFileName(ExePath),
    'BigPictureControle.exe') = 0) and
    (CompareText(Arguments, '--startup') = 0);
end;

procedure CapturePreviousStartup;
begin
  if PreviousStartupCaptured then
    Exit;
  PreviousStartupCaptured := True;
  PreviousStartupOwned :=
    RegQueryStringValue(HKCU, StartupKey, StartupValueName,
      PreviousStartupCommand);
  if PreviousStartupOwned then
    PreviousStartupOwned := IsAppStartupCommand(PreviousStartupCommand);
end;

function OpenEvent(DesiredAccess: LongWord; InheritHandle: Boolean;
  Name: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(EventHandle: THandle): Boolean;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

procedure SignalGracefulExit;
var
  EventHandle: THandle;
begin
  { This is the same command used by BigPictureControle.exe --exit.
    Sending the known event also reaches an older portable installation,
    without executing a program path obtained from the registry. }
  EventHandle := OpenEvent(EventModifyState, False, ExitEventName);
  if EventHandle <> 0 then
  begin
    try
      SetEvent(EventHandle);
    finally
      CloseHandle(EventHandle);
    end;
  end;
end;

function CloseAppAndWait: Boolean;
var
  Attempt: Integer;
  ExitCode: Integer;
  AppExe: String;
begin
  Result := True;
  if not CheckForMutexes(AppMutexName) then
    Exit;

  AppExe := ExpandConstant('{app}\BigPictureControle.exe');
  if FileExists(AppExe) then
  begin
    { Request restoration before overwriting/deleting the executable. }
    Exec(AppExe, '--exit', ExtractFileDir(AppExe), SW_HIDE,
      ewWaitUntilTerminated, ExitCode);
  end;

  { The companion recovery process may still be finishing HDMI/taskbar
    restoration after the panel exits. Never force-kill either process. }
  for Attempt := 1 to 32 do
  begin
    SignalGracefulExit;
    Sleep(250);
    if not CheckForMutexes(AppMutexName) then
      Exit;
  end;
  Result := False;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  NeedsRestart := False;
  Result := '';
  CapturePreviousStartup;
  if not CloseAppAndWait then
    Result := 'O aplicativo ainda está restaurando o setup. Encerre o Big Picture Controle usando "Encerrar app" e tente novamente.';
end;

procedure RemoveOwnedStartup(IncludePrevious: Boolean);
var
  Command: String;
begin
  if RegQueryStringValue(HKCU, StartupKey, StartupValueName, Command) then
    if (CompareText(Command,
      ExpandConstant('"{app}\BigPictureControle.exe" --startup')) = 0) or
      (IncludePrevious and PreviousStartupOwned and
       (CompareText(Command, PreviousStartupCommand) = 0)) then
      RegDeleteValue(HKCU, StartupKey, StartupValueName);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    if not WizardIsTaskSelected('startup') then
      RemoveOwnedStartup(True);
end;

function InitializeUninstall: Boolean;
begin
  Result := CloseAppAndWait;
  if not Result then
    SuppressibleMsgBox('O aplicativo ainda está restaurando o setup. Encerre o Big Picture Controle usando "Encerrar app" e tente desinstalar novamente.',
      mbError, MB_OK, IDOK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RemoveOwnedStartup(False);
  { No AppData deletion: settings and recovery information are retained. }
end;
