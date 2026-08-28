using DocMgr.Infrastructure;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using System;
using System.IO;

namespace DocMgr.ViewModels.SystemSettings;

/// <summary>
/// 帮助页：展示版本、当前库路径与覆盖安装说明。
/// </summary>
public sealed class HelpPageViewModel : ViewModelBase
{
    private const string GuideFileName = "覆盖安装说明.md";

    public HelpPageViewModel(IDatabaseBackupService databaseBackupService)
    {
        ArgumentNullException.ThrowIfNull(databaseBackupService);

        VersionDisplay = $"版本 {AppVersionInfo.DisplayVersion}";
        DatabasePathText = databaseBackupService.DatabasePath;
        DatabaseKindText = databaseBackupService.IsNetworkPath
            ? "当前为共享数据库（局域网路径）。"
            : "当前为程序目录下的本地数据库。";
        GuideText = LoadGuideText();
    }

    /// <summary>登录窗/主窗口同一套版本号。</summary>
    public string VersionDisplay { get; }

    /// <summary>当前 SQLite 文件路径。</summary>
    public string DatabasePathText { get; }

    /// <summary>本地库或共享库说明。</summary>
    public string DatabaseKindText { get; }

    /// <summary>覆盖安装与备份全文。</summary>
    public string GuideText { get; }

    private static string LoadGuideText()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", GuideFileName);
        if (File.Exists(path))
        {
            return File.ReadAllText(path);
        }

        return "未找到覆盖安装说明文件。请在「高级数据管理」备份当前库，覆盖安装时只替换程序文件，不要覆盖 DocMgr.db。";
    }
}
