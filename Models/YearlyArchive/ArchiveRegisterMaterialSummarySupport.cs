namespace DocMgr.Models.YearlyArchive;

/// <summary>
/// 把各介质下的资料名称收成申请单摘要。列表、待办和打印表头继续读申请单上的 <c>MaterialName</c>。
/// </summary>
public static class ArchiveRegisterMaterialSummarySupport
{
    public static string Build(IEnumerable<YearlyArchiveRegisterMedia>? mediaEntries)
    {
        var names = new List<string>();
        foreach (YearlyArchiveRegisterMedia media in mediaEntries ?? [])
        {
            IEnumerable<YearlyArchiveRegisterMaterial> materials = (media.Materials ?? [])
                .OrderBy(material => material.SortOrder)
                .ThenBy(material => material.Id);
            foreach (YearlyArchiveRegisterMaterial material in materials)
            {
                string name = material.MaterialName?.Trim() ?? string.Empty;
                if (name.Length == 0 || names.Contains(name, StringComparer.Ordinal))
                {
                    continue;
                }

                names.Add(name);
            }
        }

        return string.Join("、", names);
    }

    public static void Apply(YearlyArchiveRegisterRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        record.MaterialName = Build(record.MediaEntries);
    }

    /// <summary>
    /// 存量直办，以及尚未分组的出网转入建档。已有资料名称时不重复添加。
    /// </summary>
    public static void EnsureSingleMaterial(YearlyArchiveRegisterMedia media, string? materialName)
    {
        ArgumentNullException.ThrowIfNull(media);
        if (media.Materials is { Count: > 0 })
        {
            return;
        }

        var material = new YearlyArchiveRegisterMaterial
        {
            SortOrder = 1,
            MaterialName = materialName?.Trim() ?? string.Empty
        };
        foreach (YearlyArchiveRegisterMediaItem item in media.Items ?? [])
        {
            item.RegisterMaterial = material;
            material.Items.Add(item);
        }

        media.Materials.Add(material);
    }

    /// <summary>
    /// 每条介质至少一份资料名称，名称非空，其下至少一个子项，子项必须挂在本介质的资料名称上。
    /// 建档申请与档外入网、出网共用。
    /// </summary>
    public static void AppendMaterialGroupErrors(
        YearlyArchiveRegisterMedia media,
        int mediaSeq,
        ICollection<string> errors)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(errors);

        List<YearlyArchiveRegisterMaterial> materials = (media.Materials ?? [])
            .OrderBy(material => material.SortOrder)
            .ThenBy(material => material.Id)
            .ToList();
        if (materials.Count == 0)
        {
            errors.Add($"• 第{mediaSeq}条介质至少需要一份资料名称");
        }

        for (int materialIndex = 0; materialIndex < materials.Count; materialIndex++)
        {
            YearlyArchiveRegisterMaterial material = materials[materialIndex];
            if (string.IsNullOrWhiteSpace(material.MaterialName))
            {
                errors.Add($"• 第{mediaSeq}条介质的第{materialIndex + 1}份资料名称未填写");
            }

            if (material.Items == null || material.Items.Count == 0)
            {
                errors.Add($"• 第{mediaSeq}条介质的第{materialIndex + 1}份资料至少需要一条资料子项");
            }
        }

        foreach (YearlyArchiveRegisterMediaItem item in media.Items ?? [])
        {
            if (IsItemAttachedToMaterial(item, materials))
            {
                continue;
            }

            errors.Add($"• 第{mediaSeq}条介质存在未挂到本介质资料名称上的子项");
            break;
        }
    }

    /// <summary>子项所属资料名称。导航未装载时，从本介质已加载的资料行补。</summary>
    public static string ResolveItemMaterialName(YearlyArchiveRegisterMediaItem? item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        string fromNavigation = item.RegisterMaterial?.MaterialName?.Trim() ?? string.Empty;
        if (fromNavigation.Length > 0)
        {
            return fromNavigation;
        }

        YearlyArchiveRegisterMaterial? match = item.MediaEntry?.Materials?
            .FirstOrDefault(material => material.Id > 0 && material.Id == item.YearlyArchiveRegisterMaterialId);
        if (!string.IsNullOrWhiteSpace(match?.MaterialName))
        {
            return match.MaterialName.Trim();
        }

        if (item.MediaEntry?.Materials is { Count: 1 })
        {
            return item.MediaEntry.Materials[0].MaterialName?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }

    private static bool IsItemAttachedToMaterial(
        YearlyArchiveRegisterMediaItem item,
        IReadOnlyList<YearlyArchiveRegisterMaterial> materials)
    {
        if (item.RegisterMaterial != null && materials.Contains(item.RegisterMaterial))
        {
            return true;
        }

        return item.YearlyArchiveRegisterMaterialId is int materialId
               && materialId > 0
               && materials.Any(material => material.Id == materialId);
    }
}
