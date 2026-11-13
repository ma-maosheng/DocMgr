using DocMgr.Models.Projects;

namespace DocMgr.Models.YearlyArchive;

/// <summary>
/// 扫描电子介质前，核对项目库是否已有同一实施年度、同一项目名称。
/// </summary>
public static class ScannedProjectLibraryMatchSupport
{
    /// <summary>
    /// 实施年度与项目名称都对上才算库内已有项目。年度允许写成四位年份，或只加「年」「年度」。
    /// </summary>
    public static ProjectInfo? Find(IEnumerable<ProjectInfo>? projects, string? year, string? projectName)
    {
        string parsedYear = year?.Trim() ?? string.Empty;
        string parsedName = projectName?.Trim() ?? string.Empty;
        if (parsedYear.Length == 0 || parsedName.Length == 0 || projects == null)
        {
            return null;
        }

        return projects.FirstOrDefault(project =>
            NamesMatch(project.ProjectName, parsedName)
            && YearsMatch(project.ImplementYear, parsedYear));
    }

    /// <summary>
    /// 两套年度、项目名称是否是同一个。年度允许写成四位年份，或只加「年」「年度」。
    /// </summary>
    public static bool SameYearAndProject(
        string? year,
        string? projectName,
        string? otherYear,
        string? otherProjectName)
    {
        string leftYear = year?.Trim() ?? string.Empty;
        string rightYear = otherYear?.Trim() ?? string.Empty;
        string leftName = projectName?.Trim() ?? string.Empty;
        string rightName = otherProjectName?.Trim() ?? string.Empty;
        if (leftYear.Length == 0 || rightYear.Length == 0 || leftName.Length == 0 || rightName.Length == 0)
        {
            return false;
        }

        return NamesMatch(leftName, rightName)
               && (YearsMatch(leftYear, rightYear) || YearsMatch(rightYear, leftYear));
    }

    /// <summary>
    /// 介质里已有四级目录，但与页面上的年度、项目对不上。提醒先补项目库再重新扫描。
    /// </summary>
    public static string BuildUnmatchedFourLayerRescanMessage(
        string? pageYear,
        string? pageProjectName,
        IReadOnlyList<string>? diskLabels)
    {
        string yearText = string.IsNullOrWhiteSpace(pageYear) ? "（空）" : pageYear.Trim();
        string nameText = string.IsNullOrWhiteSpace(pageProjectName) ? "（空）" : pageProjectName.Trim();
        var lines = new List<string>
        {
            $"所选介质中有符合「年度\\项目\\资料名称\\子项」的文件夹，但与当前申请的实施年度「{yearText}」、项目名称「{nameText}」不一致。",
            "请先在项目信息中补全介质上的项目，然后再重新扫描这张电子介质。",
            "本次扫描未写入资料。"
        };

        List<string> pairs = DistinctYearProjects(diskLabels);
        if (pairs.Count > 0)
        {
            lines.Add("介质上的项目：");
            const int maxShown = 8;
            int shown = Math.Min(maxShown, pairs.Count);
            for (int i = 0; i < shown; i++)
            {
                lines.Add($"• {pairs[i]}");
            }

            if (pairs.Count > maxShown)
            {
                lines.Add($"其余 {pairs.Count - maxShown} 个项目未列出。");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static List<string> DistinctYearProjects(IReadOnlyList<string>? diskLabels)
    {
        var pairs = new List<string>();
        foreach (string label in diskLabels ?? [])
        {
            string[] parts = label.Split('\\');
            if (parts.Length < 2)
            {
                continue;
            }

            string pair = $"{parts[0].Trim()}\\{parts[1].Trim()}";
            if (pair.Length <= 1 || pairs.Contains(pair, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            pairs.Add(pair);
        }

        return pairs;
    }

    /// <summary>项目库没有对应项目时，提示先补全项目信息再重新扫描。</summary>
    public static string BuildMissingProjectMessage(string? year, string? projectName)
    {
        string yearText = string.IsNullOrWhiteSpace(year) ? "（未能识别）" : year.Trim();
        string nameText = string.IsNullOrWhiteSpace(projectName) ? "（未能识别）" : projectName.Trim();
        return string.Join(
            Environment.NewLine,
            $"项目库中没有实施年度为「{yearText}」、项目名称为「{nameText}」的项目。",
            "请先在项目信息中补全该项目，然后再重新扫描这张电子介质。");
    }

    private static bool NamesMatch(string? libraryName, string scannedName) =>
        string.Equals(libraryName?.Trim(), scannedName, StringComparison.OrdinalIgnoreCase);

    private static bool YearsMatch(string? implementYear, string scannedYear)
    {
        string value = implementYear?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return false;
        }

        return string.Equals(value, scannedYear, StringComparison.Ordinal)
               || string.Equals(value, scannedYear + "年", StringComparison.Ordinal)
               || string.Equals(value, scannedYear + "年度", StringComparison.Ordinal);
    }
}
