using DocMgr.Infrastructure;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.IO;

namespace DocMgr.ViewModels.SystemSettings;

/// <summary>
/// 帮助页目录项：左侧章节标题与右侧正文。
/// </summary>
public sealed class HelpManualSection
{
    public HelpManualSection(int index, string title, string body)
    {
        Index = index;
        Title = title;
        Body = body;
    }

    /// <summary>目录序号（从 1 起）。</summary>
    public int Index { get; }

    /// <summary>目录序号两位显示。</summary>
    public string IndexText => Index.ToString("00");

    /// <summary>目录显示名。</summary>
    public string Title { get; }

    /// <summary>该节 Markdown 正文。</summary>
    public string Body { get; }
}

/// <summary>
/// 帮助页：版本、当前库路径，以及按章节展示的操作手册。
/// </summary>
public sealed class HelpPageViewModel : ViewModelBase
{
    private HelpManualSection? _selectedSection;

    public HelpPageViewModel(IDatabaseBackupService databaseBackupService)
    {
        ArgumentNullException.ThrowIfNull(databaseBackupService);

        VersionDisplay = $"版本 {AppVersionInfo.DisplayVersion}";
        DatabasePathText = databaseBackupService.DatabasePath;
        DatabaseKindText = databaseBackupService.IsNetworkPath
            ? "当前为共享数据库（局域网路径）。"
            : "当前为程序目录下的本地数据库。";
        Sections = LoadSections();
        if (Sections.Count > 0)
        {
            SelectedSection = Sections[0];
        }
    }

    /// <summary>登录窗/主窗口同一套版本号。</summary>
    public string VersionDisplay { get; }

    /// <summary>当前 SQLite 文件路径。</summary>
    public string DatabasePathText { get; }

    /// <summary>本地库或共享库说明。</summary>
    public string DatabaseKindText { get; }

    /// <summary>手册章节列表。</summary>
    public IReadOnlyList<HelpManualSection> Sections { get; }

    /// <summary>当前选中的章节。</summary>
    public HelpManualSection? SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                OnPropertyChanged(nameof(SectionTitle));
                OnPropertyChanged(nameof(BodyText));
            }
        }
    }

    /// <summary>右侧章节标题。</summary>
    public string SectionTitle => SelectedSection?.Title ?? string.Empty;

    /// <summary>右侧章节正文。</summary>
    public string BodyText => SelectedSection?.Body ?? string.Empty;

    private static IReadOnlyList<HelpManualSection> LoadSections()
    {
        var sections = new List<HelpManualSection>();
        TryAddMarkdownChapters(sections, "操作手册.md");
        TryAddWholeFile(sections, "覆盖安装说明.md", "安装与升级");
        TryAddWholeFile(sections, "使用冒烟清单.md", "使用核对清单");
        if (sections.Count == 0)
        {
            sections.Add(new HelpManualSection(
                1,
                "说明",
                "未找到帮助文件。请在「系统设置 → 高级数据管理」备份当前库；覆盖安装时只替换程序文件，不要覆盖 DocMgr.db。"));
            return sections;
        }

        var numbered = new List<HelpManualSection>(sections.Count);
        for (int i = 0; i < sections.Count; i++)
        {
            numbered.Add(new HelpManualSection(i + 1, sections[i].Title, sections[i].Body));
        }

        return numbered;
    }

    private static void TryAddMarkdownChapters(List<HelpManualSection> sections, string fileName)
    {
        string? text = TryReadDoc(fileName);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        string? currentTitle = null;
        var bodyLines = new List<string>();
        foreach (string raw in lines)
        {
            if (raw.StartsWith("## ", StringComparison.Ordinal))
            {
                FlushChapter(sections, currentTitle, bodyLines);
                currentTitle = raw[3..].Trim();
                bodyLines.Clear();
                continue;
            }

            if (currentTitle == null)
            {
                continue;
            }

            bodyLines.Add(raw);
        }

        FlushChapter(sections, currentTitle, bodyLines);
    }

    private static void FlushChapter(List<HelpManualSection> sections, string? title, List<string> bodyLines)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        string body = string.Join(Environment.NewLine, bodyLines).Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        sections.Add(new HelpManualSection(0, title.Trim(), body));
    }

    private static void TryAddWholeFile(List<HelpManualSection> sections, string fileName, string title)
    {
        string? text = TryReadDoc(fileName);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        sections.Add(new HelpManualSection(0, title, text.Trim()));
    }

    private static string? TryReadDoc(string fileName)
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", fileName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}
