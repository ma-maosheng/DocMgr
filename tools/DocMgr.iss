; 测绘资料管理系统安装脚本（Inno Setup 6）
; 推荐用一键脚本（会发布程序、下载桌面运行时、再编译本文件）：
;      powershell -NoProfile -File tools\Build-InnoInstaller.ps1
; 或手动：
;   1. 框架依赖发布：
;      powershell -NoProfile -File tools\PublishOverlay.ps1 -TargetDir "D:\DocMgr"
;      （不要加 -SelfContained；运行时由本安装包 redist 提供）
;   2. 确认 tools\redist\windowsdesktop-runtime-win-x64.exe 已存在
;   3. 用 Inno Setup 编译本文件
; 不打包、不覆盖 DocMgr.db / WAL；目标里已有 appsettings.json 时不会被替换。

#define MyAppName "测绘资料管理系统"
#define MyAppVersion "1.1.1"
#define MyAppPublisher "河北省第三测绘院"
#define MyAppExeName "DocMgr.exe"
#define MySourceDir "..\publish\overlay-win-x64"
#define MyRedistDir "redist"
#define MyRuntimeInstaller "windowsdesktop-runtime-win-x64.exe"

[Setup]
AppId={{8F3C1A6B-4D2E-4B91-9C7A-2E5D8F0A1B3C}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\DocMgr
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\installer-out
OutputBaseFilename=DocMgr-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#MyAppExeName}
InfoAfterFile=installer\InfoAfter-配置说明.txt
LicenseFile=

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Files]
; 程序文件（保留已有 appsettings.json）
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.db,*.db-wal,*.db-shm,appsettings.json"
Source: "{#MySourceDir}\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist ignoreversion
; 局域网连接串示例：始终更新为包内最新模板，便于对照
Source: "{#MySourceDir}\appsettings.lan.example.json"; DestDir: "{app}"; Flags: ignoreversion
; 配置说明与 .NET 8 桌面运行时安装包
Source: "installer\配置说明.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyRedistDir}\{#MyRuntimeInstaller}"; DestDir: "{app}\redist"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autoprograms}\{#MyAppName}\打开配置说明"; Filename: "{app}\配置说明.txt"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项:"; Flags: unchecked

[Run]
Filename: "{app}\配置说明.txt"; Description: "查看数据库与配置说明（appsettings）"; Flags: postinstall shellexec skipifsilent unchecked
Filename: "{app}\{#MyAppExeName}"; Description: "安装完成后启动"; Flags: nowait postinstall skipifsilent

[Code]
function IsDotNet8DesktopRuntimeInstalled: Boolean;
var
  FindRec: TFindRec;
  BasePath: String;
begin
  Result := False;
  BasePath := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if not DirExists(BasePath) then
    Exit;
  if FindFirst(BasePath + '\8.*', FindRec) then
  try
    repeat
      if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
      begin
        Result := True;
        Break;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

procedure OfferDotNetRuntimeInstall;
var
  ResultCode: Integer;
  RuntimeInstaller: String;
begin
  if IsDotNet8DesktopRuntimeInstalled then
    Exit;

  RuntimeInstaller := ExpandConstant('{app}\redist\{#MyRuntimeInstaller}');
  if not FileExists(RuntimeInstaller) then
  begin
    MsgBox(
      '未检测到 .NET 8 桌面运行时，且安装包内缺少运行时安装程序。'#13#10 +
      '请从 Microsoft 下载并安装「.NET 8 Desktop Runtime (x64）」后再启动本程序。',
      mbError, MB_OK);
    Exit;
  end;

  if MsgBox(
    '未检测到本机已安装 .NET 8 桌面运行时（Windows Desktop）。'#13#10#13#10 +
    '本程序需要该运行时才能启动；安装包已附带官方安装程序。'#13#10 +
    '（这是运行时，不是开发用的 SDK。）'#13#10#13#10 +
    '是否现在安装？' + #13#10 +
    '将弹出系统权限确认，请允许。',
    mbConfirmation, MB_YESNO) <> IDYES then
  begin
    MsgBox(
      '已跳过运行时安装。若启动失败，请手动运行：'#13#10 +
      RuntimeInstaller,
      mbInformation, MB_OK);
    Exit;
  end;

  if not Exec(RuntimeInstaller, '/install /norestart', '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode) then
  begin
    MsgBox('无法启动运行时安装程序，请手动运行：'#13#10 + RuntimeInstaller, mbError, MB_OK);
    Exit;
  end;

  { 0=成功；3010=成功但需要重启；1638=已安装更新版本 }
  if (ResultCode <> 0) and (ResultCode <> 3010) and (ResultCode <> 1638) then
  begin
    MsgBox(
      '运行时安装程序退出码：' + IntToStr(ResultCode) + '。'#13#10 +
      '若程序无法启动，请手动再运行一次：'#13#10 + RuntimeInstaller,
      mbInformation, MB_OK);
    Exit;
  end;

  if not IsDotNet8DesktopRuntimeInstalled then
    MsgBox(
      '运行时安装已结束，但尚未检测到 Microsoft.WindowsDesktop.App 8.x。'#13#10 +
      '若刚提示需要重启，请重启后再打开本程序；否则请手动安装：'#13#10 +
      RuntimeInstaller,
      mbInformation, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    OfferDotNetRuntimeInstall;
end;
