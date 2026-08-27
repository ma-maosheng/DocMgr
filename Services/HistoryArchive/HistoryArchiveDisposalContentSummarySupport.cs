using DocMgr.Models.HistoryArchive;

namespace DocMgr.Services.HistoryArchive;

/// <summary>
/// 按资料类别生成档案盒内简要描述。
/// </summary>
public static class HistoryArchiveDisposalContentSummarySupport
{
    /// <summary>地形图：比例尺 + 图幅合计 + 图号/图名抽样。</summary>
    public static string BuildTopoMapSummary(IReadOnlyList<TopoMap> maps)
    {
        if (maps.Count == 0)
        {
            return "（无台账）";
        }

        string scales = JoinDistinct(maps.Select(item => item.Scale));
        int sheetTotal = maps.Sum(item => Math.Max(0, item.SheetCount));
        string sample = maps
            .Select(item => FirstNonEmpty(item.MapNumber, item.MapName))
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
            ?? string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(scales))
        {
            parts.Add(scales);
        }

        if (sheetTotal > 0)
        {
            parts.Add($"{sheetTotal}幅");
        }

        if (!string.IsNullOrWhiteSpace(sample))
        {
            parts.Add(maps.Count > 1 ? $"{sample}等{maps.Count}条" : sample);
        }
        else if (maps.Count > 1)
        {
            parts.Add($"{maps.Count}条");
        }

        return parts.Count == 0 ? $"{maps.Count}条" : string.Join(" · ", parts);
    }

    /// <summary>航摄：测区 + 盒内内容 + 照片张数。</summary>
    public static string BuildAerialPhotoSummary(IReadOnlyList<AerialPhoto> photos)
    {
        if (photos.Count == 0)
        {
            return "（无台账）";
        }

        string survey = JoinDistinct(photos.Select(item => item.SurveyArea));
        string contents = JoinDistinct(photos.Select(item => item.BoxContents));
        int photoTotal = photos.Sum(item => Math.Max(0, item.PhotoCount));

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(survey))
        {
            parts.Add(survey);
        }

        if (!string.IsNullOrWhiteSpace(contents)
            && !string.Equals(contents, survey, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(contents);
        }

        if (photoTotal > 0)
        {
            parts.Add($"{photoTotal}张");
        }

        if (photos.Count > 1)
        {
            parts.Add($"{photos.Count}条");
        }

        return parts.Count == 0 ? $"{photos.Count}条" : string.Join(" · ", parts);
    }

    /// <summary>其他资料：资料分类 + 资料内容 + 起止年度。</summary>
    public static string BuildOtherMapSummary(IReadOnlyList<OtherMap> maps)
    {
        if (maps.Count == 0)
        {
            return "（无台账）";
        }

        string category = JoinDistinct(maps.Select(item => item.MaterialCategory));
        string content = JoinDistinct(maps.Select(item => item.MapName));
        string years = JoinDistinct(maps.Select(BuildYearRange));

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(category))
        {
            parts.Add(category);
        }

        if (!string.IsNullOrWhiteSpace(content))
        {
            parts.Add(maps.Count > 1 ? $"{content.Split('、')[0]}等{maps.Count}条" : content);
        }

        if (!string.IsNullOrWhiteSpace(years))
        {
            parts.Add(years);
        }

        return parts.Count == 0 ? $"{maps.Count}条" : string.Join(" · ", parts);
    }

    private static string BuildYearRange(OtherMap map)
    {
        string start = map.StartYear?.Trim() ?? string.Empty;
        string end = map.EndYear?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(start) && string.IsNullOrWhiteSpace(end))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(start))
        {
            return end;
        }

        if (string.IsNullOrWhiteSpace(end) || string.Equals(start, end, StringComparison.Ordinal))
        {
            return start;
        }

        return $"{start}–{end}";
    }

    private static string JoinDistinct(IEnumerable<string?> values)
    {
        var distinct = values
            .Select(item => item?.Trim() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
        return distinct.Count == 0 ? string.Empty : string.Join("、", distinct);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }
}
