测绘资料管理系统 测试安装包（1.0.0，自包含 win-x64）
====================================================

本包已包含 .NET 8 运行时，目标电脑不必单独安装桌面运行时。仅支持 64 位 Windows。

方式一：解压即用（最快）
- 把整个文件夹拷到测试机（建议放到 D:\DocMgr 或文档目录，不要放在会自动清理的临时目录）。
- 双击 DocMgr.exe。
- 首次启动会在该目录生成 DocMgr.db。

方式二：安装到当前用户目录
- 解压后在文件夹中右键 Install-DocMgr.ps1 → 使用 PowerShell 运行。
  或：powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-DocMgr.ps1
- 需要桌面快捷方式时加上：-DesktopShortcut
- 默认安装到：%LOCALAPPDATA%\DocMgr
- 不会覆盖已有 DocMgr.db，也不会替换已有 appsettings.json。

局域网共享库
- 把 appsettings.json 里 Database:Path 改成共享盘上的库路径（可参考包内 appsettings.lan.example.json）。
- 多台机器同时打开前，先确认路径与忙等待超时配置一致。

注意
- 不要用本包覆盖正在使用的 DocMgr.db / .db-wal / .db-shm。
- 升级请先在「系统设置 → 高级数据管理」备份当前库。
