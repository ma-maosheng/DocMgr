; 测绘资料管理系统安装脚本（Inno Setup 6）
; 1. 先发布（任意目标目录均可，会同时写出 publish\overlay-win-x64）：
;      powershell -NoProfile -File tools\PublishOverlay.ps1 -TargetDir "D:\DocMgr"
; 2. 用 Inno Setup 编译本文件。若提示找不到 ChineseSimplified.isl，删除 [Languages] 段即可。
; 不打包、不覆盖 DocMgr.db / WAL；目标里已有 appsettings.json 时不会被替换。

#define MyAppName "测绘资料管理系统"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "河北省第三测绘院"
#define MyAppExeName "DocMgr.exe"
#define MySourceDir "..\publish\overlay-win-x64"

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
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.db,*.db-wal,*.db-shm,appsettings.json"
Source: "{#MySourceDir}\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "安装完成后启动"; Flags: nowait postinstall skipifsilent
