namespace DocMgr.Models.HistoryArchive;

/// <summary>
/// 杂项资料文案归一与摘要。
/// </summary>
public static class HistoryMiscMaterialTextSupport
{
    /// <summary>去掉首尾空白；与已有值完全一致时沿用已有写法。</summary>
    public static string Canonicalize(string? value, IEnumerable<string>? existingValues)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed) || existingValues == null)
        {
            return trimmed;
        }

        string? matched = existingValues.FirstOrDefault(item =>
            string.Equals(item?.Trim(), trimmed, StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(matched) ? trimmed : matched.Trim();
    }

    /// <summary>容器类型 + 编号 + 大类/子类 + 首行详情。</summary>
    public static string BuildContentSummary(
        string? containerKind,
        string? containerCode,
        string? majorCategory,
        string? subCategory,
        IEnumerable<string>? detailLines)
    {
        var parts = new List<string>();
        string kind = containerKind?.Trim() ?? string.Empty;
        string code = containerCode?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(kind) || !string.IsNullOrWhiteSpace(code))
        {
            parts.Add($"{kind} {code}".Trim());
        }

        string major = majorCategory?.Trim() ?? string.Empty;
        string sub = subCategory?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(major) && !string.IsNullOrWhiteSpace(sub))
        {
            parts.Add($"{major}/{sub}");
        }
        else if (!string.IsNullOrWhiteSpace(major))
        {
            parts.Add(major);
        }
        else if (!string.IsNullOrWhiteSpace(sub))
        {
            parts.Add(sub);
        }

        string firstLine = detailLines?
            .Select(item => item?.Trim() ?? string.Empty)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item))
            ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(firstLine))
        {
            parts.Add(firstLine);
        }

        return parts.Count == 0 ? "（无详情）" : string.Join(" · ", parts);
    }

    /// <summary>按库内详情行顺序编号并全部列出，供杂项列表展示。</summary>
    public static string FormatStoredDetailLines(IEnumerable<string>? detailLines)
    {
        if (detailLines == null)
        {
            return string.Empty;
        }

        List<string> texts = detailLines
            .Select(item => item?.Trim() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
        if (texts.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            "；",
            texts.Select((text, index) => $"{index + 1}. {text}"));
    }

    /// <summary>详情行按排序号拼接，供开柜内容展示。</summary>
    public static string JoinDetailLines(IEnumerable<HistoryMiscMaterialLine>? lines)
    {
        if (lines == null)
        {
            return string.Empty;
        }

        return string.Join(
            "；",
            lines
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .Select(item => item.LineText?.Trim() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item)));
    }
}
