using DocMgr.Infrastructure;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.IO;

namespace DocMgr.ViewModels.SystemSettings;

/// <summary>
/// 帮助页：展示版本、当前库路径、使用期冒烟清单与覆盖安装说明。
/// </summary>
public sealed class HelpPageViewModel : ViewModelBase
{
    public HelpPageViewModel(IDatabaseBackupService databaseBackupService)
    {
        ArgumentNullException.ThrowIfNull(databaseBackupService);

        VersionDisplay = $"版本 {AppVersionInfo.DisplayVersion}";
        DatabasePathText = databaseBackupService.DatabasePath;
        DatabaseKindText = databaseBackupService.IsNetworkPath
            ? "当前为共享数据库（局域网路径）。"
            : "当前为程序目录下的本地数据库。";
        GuideText = LoadCombinedGuideText();
    }

    /// <summary>登录窗/主窗口同一套版本号。</summary>
    public string VersionDisplay { get; }

    /// <summary>当前 SQLite 文件路径。</summary>
    public string DatabasePathText { get; }

    /// <summary>本地库或共享库说明。</summary>
    public string DatabaseKindText { get; }

    /// <summary>使用期冒烟清单与覆盖安装说明全文。</summary>
    public string GuideText { get; }

    private static string LoadCombinedGuideText()
    {
        var parts = new List<string>();
        TryAddDoc(parts, "使用冒烟清单.md");
        TryAddDoc(parts, "覆盖安装说明.md");
        if (parts.Count > 0)
        {
            return string.Join(
                Environment.NewLine + Environment.NewLine + "----------" + Environment.NewLine + Environment.NewLine,
                parts);
        }

        return "未找到帮助文件。请在「高级数据管理」备份当前库；覆盖安装时只替换程序文件，不要覆盖 DocMgr.db。";
    }

    private static void TryAddDoc(List<string> parts, string fileName)
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", fileName);
        if (File.Exists(path))
        {
            parts.Add(File.ReadAllText(path));
        }
    }
}
