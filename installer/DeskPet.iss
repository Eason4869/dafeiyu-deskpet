#ifndef RuntimeDir
  #error Build with BuildInstaller.ps1
#endif
#ifndef ProductVersion
  #error Missing ProductVersion
#endif
#ifndef RepositoryUrl
  #error Missing RepositoryUrl
#endif

[Setup]
AppId={{3B63C162-076E-4B2F-BDCC-2572664875A9}
AppName=大肥鱼桌面伙伴
AppVersion={#ProductVersion}
AppPublisher=彧晟Eason
AppPublisherURL={#RepositoryUrl}
AppSupportURL={#RepositoryUrl}/issues
AppUpdatesURL={#RepositoryUrl}/releases/latest
DefaultDirName={localappdata}\Programs\DafeiyuDeskPet
DefaultGroupName=大肥鱼桌面伙伴
DisableProgramGroupPage=auto
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible and not arm64
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
AppMutex=Local\Dafeiyu.DeskPet.Installation
CloseApplications=no
RestartApplications=no
OutputDir={#OutputDir}
OutputBaseFilename=DafeiyuDeskPet-Setup-{#ProductVersion}
SetupIconFile=..\assets\pet.ico
UninstallDisplayIcon={app}\大肥鱼桌宠.exe
UninstallDisplayName=大肥鱼桌面伙伴
VersionInfoVersion={#AssemblyVersion}
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
DisableWelcomePage=no
Uninstallable=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: unchecked

[Files]
Source: "{#RuntimeDir}\大肥鱼桌宠.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#RuntimeDir}\大肥鱼桌宠.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#RuntimeDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#RuntimeDir}\CHANGELOG.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#RuntimeDir}\docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RuntimeDir}\templates\*"; DestDir: "{app}\templates"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RuntimeDir}\previews\*"; DestDir: "{app}\previews"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\大肥鱼桌面伙伴"; Filename: "{app}\大肥鱼桌宠.exe"; WorkingDir: "{app}"
Name: "{group}\卸载大肥鱼桌面伙伴"; Filename: "{uninstallexe}"
Name: "{autodesktop}\大肥鱼桌面伙伴"; Filename: "{app}\大肥鱼桌宠.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\大肥鱼桌宠.exe"; Description: "启动大肥鱼桌面伙伴"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := IsDotNetInstalled(net48, 0);
  if not Result then
    SuppressibleMsgBox('请先安装 Microsoft .NET Framework 4.8 或更新版本，再运行此安装包。', mbError, MB_OK, IDOK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  RegisteredCommand, ExpectedCommand: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    ExpectedCommand := '"' + ExpandConstant('{app}\大肥鱼桌宠.exe') + '" --startup';
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'DafeiyuDeskPet', RegisteredCommand) then
      if CompareText(RegisteredCommand, ExpectedCommand) = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'DafeiyuDeskPet');
  end;
end;
