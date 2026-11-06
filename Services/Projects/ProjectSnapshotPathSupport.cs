namespace DocMgr.Services.Projects;

/// <summary>
/// 项目名称、实施年度变更时，改写库内路径文本中的对应段。不移动磁盘上的真实目录。
/// </summary>
internal static class ProjectSnapshotPathSupport
{
    /// <summary>
    /// 替换路径中第一处「年度段 \ 旧项目名称」。年度段可以是四位数字，也可以是当时的实施年度。
    /// 年度段本身保持不变，避免把立档编号年度改成项目实施年度。
    /// </summary>
    public static string ReplaceProjectSegment(string? path, string oldProjectName, string newProjectName, string oldYear)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path ?? string.Empty;
        }

        string oldProjectRaw = oldProjectName.Trim();
        string newProjectRaw = newProjectName.Trim();
        if (string.IsNullOrEmpty(oldProjectRaw))
        {
            return path;
        }

        string oldProjectSegment = SanitizeProject(oldProjectRaw);
        string newProjectSegment = SanitizeProject(newProjectRaw);
        if (string.Equals(oldProjectSegment, newProjectSegment, StringComparison.Ordinal)
            && string.Equals(oldProjectRaw, newProjectRaw, StringComparison.Ordinal))
        {
            return path;
        }

        string[] segments = path.Split('\\');
        string oldYearRaw = oldYear.Trim();
        string oldYearSegment = SanitizeYear(oldYearRaw);
        for (int index = 0; index < segments.Length - 1; index++)
        {
            if (!IsYearSegment(segments[index], oldYearRaw, oldYearSegment))
            {
                continue;
            }

            if (!Matches(segments[index + 1], oldProjectRaw, oldProjectSegment))
            {
                continue;
            }

            if (string.Equals(segments[index + 1], newProjectSegment, StringComparison.Ordinal))
            {
                return path;
            }

            segments[index + 1] = newProjectSegment;
            return string.Join('\\', segments);
        }

        return path;
    }

    /// <summary>
    /// 替换出入网相对路径中第一处「旧年度 \ 旧项目」成对段。
    /// </summary>
    public static string ReplaceYearAndProjectPair(
        string? path,
        string oldYear,
        string oldProjectName,
        string newYear,
        string newProjectName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path ?? string.Empty;
        }

        string oldYearRaw = oldYear.Trim();
        string newYearRaw = newYear.Trim();
        string oldProjectRaw = oldProjectName.Trim();
        string newProjectRaw = newProjectName.Trim();
        if (string.IsNullOrEmpty(oldYearRaw) || string.IsNullOrEmpty(oldProjectRaw))
        {
            return path;
        }

        string oldYearSegment = SanitizeYear(oldYearRaw);
        string newYearSegment = SanitizeYear(newYearRaw);
        string oldProjectSegment = SanitizeProject(oldProjectRaw);
        string newProjectSegment = SanitizeProject(newProjectRaw);
        string[] segments = path.Split('\\');
        for (int index = 0; index < segments.Length - 1; index++)
        {
            if (!Matches(segments[index], oldYearRaw, oldYearSegment)
                || !Matches(segments[index + 1], oldProjectRaw, oldProjectSegment))
            {
                continue;
            }

            if (string.Equals(segments[index], newYearSegment, StringComparison.Ordinal)
                && string.Equals(segments[index + 1], newProjectSegment, StringComparison.Ordinal))
            {
                return path;
            }

            segments[index] = newYearSegment;
            segments[index + 1] = newProjectSegment;
            return string.Join('\\', segments);
        }

        return ReplaceProjectSegment(path, oldProjectRaw, newProjectRaw, oldYearRaw);
    }

    private static bool IsYearSegment(string segment, string oldYearRaw, string oldYearSegment)
    {
        if (Matches(segment, oldYearRaw, oldYearSegment))
        {
            return true;
        }

        return segment.Length == 4 && segment.All(char.IsDigit);
    }

    private static bool Matches(string segment, string raw, string sanitized)
    {
        return string.Equals(segment, raw, StringComparison.Ordinal)
            || string.Equals(segment, sanitized, StringComparison.Ordinal);
    }

    private static string SanitizeProject(string value)
        => ElectronicFilingStoragePathSupport.SanitizePathSegment(value, "未知项目");

    private static string SanitizeYear(string value)
        => ElectronicFilingStoragePathSupport.SanitizePathSegment(value, "未知年度");
}
