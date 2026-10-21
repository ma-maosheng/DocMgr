namespace DocMgr.Models.HistoryArchive;

/// <summary>
/// 杂项资料的初始大类、子类、容器，以及影像胶片桶的默认详情。
/// </summary>
public static class HistoryMiscMaterialDefaults
{
    public const string ImageMajorCategory = "影像资料";

    public const string FilmSubCategory = "胶片";

    public const string BarrelContainerKind = "桶";

    /// <summary>容器编号前缀，后接登记年度与三位序号，如史-杂-2026-001。</summary>
    public const string ContainerCodePrefix = "史-杂";

    /// <summary>影像资料 / 胶片 / 桶 的默认详情。界面按行顺序编号。</summary>
    public static IReadOnlyList<string> FilmBarrelDetailLines { get; } =
    [
        "测区：",
        "年度：1998",
        "卷号：NO1",
        "航射时间：1998.8.16",
        "航摄比例尺：1:4000",
        "航摄仪：RMK",
        "焦距：153.616",
        "序号：001-200，500-600",
        "总片数：300"
    ];

    /// <summary>当年容器编号前缀，如史-杂-2026-。</summary>
    public static string BuildContainerCodePrefix(int year) => $"{ContainerCodePrefix}-{year}-";

    /// <summary>是否为影像资料、胶片、桶这一组合。</summary>
    public static bool IsFilmBarrel(string? majorCategory, string? subCategory, string? containerKind) =>
        string.Equals(majorCategory?.Trim(), ImageMajorCategory, StringComparison.Ordinal)
        && string.Equals(subCategory?.Trim(), FilmSubCategory, StringComparison.Ordinal)
        && string.Equals(containerKind?.Trim(), BarrelContainerKind, StringComparison.Ordinal);

    /// <summary>把初始大类、子类、容器并入已录入词汇，已有相同写法时不重复。</summary>
    public static HistoryMiscVocabulary MergeSeed(HistoryMiscVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        var majors = vocabulary.MajorCategories.ToList();
        AddDistinct(majors, ImageMajorCategory);
        var containers = vocabulary.ContainerKinds.ToList();
        AddDistinct(containers, BarrelContainerKind);
        var subs = vocabulary.SubCategories.ToList();
        if (!subs.Any(item =>
                string.Equals(item.MajorCategory, ImageMajorCategory, StringComparison.Ordinal)
                && string.Equals(item.SubCategory, FilmSubCategory, StringComparison.Ordinal)))
        {
            subs.Add(new HistoryMiscSubCategoryOption
            {
                MajorCategory = ImageMajorCategory,
                SubCategory = FilmSubCategory
            });
        }

        return new HistoryMiscVocabulary
        {
            MajorCategories = majors.OrderBy(item => item, StringComparer.Ordinal).ToList(),
            ContainerKinds = containers.OrderBy(item => item, StringComparer.Ordinal).ToList(),
            SubCategories = subs
                .OrderBy(item => item.MajorCategory, StringComparer.Ordinal)
                .ThenBy(item => item.SubCategory, StringComparer.Ordinal)
                .ToList()
        };
    }

    /// <summary>当前详情是否仍是影像胶片桶模板，或只有空行。</summary>
    public static bool CanReplaceWithFilmBarrelTemplate(IEnumerable<string?> lines)
    {
        List<string> texts = lines.Select(item => item?.Trim() ?? string.Empty).ToList();
        if (texts.Count == 0 || texts.All(string.IsNullOrWhiteSpace))
        {
            return true;
        }

        return texts.Count == FilmBarrelDetailLines.Count
            && texts.Select((text, index) => string.Equals(text, FilmBarrelDetailLines[index], StringComparison.Ordinal))
                .All(item => item);
    }

    private static void AddDistinct(List<string> values, string seed)
    {
        if (values.Any(item => string.Equals(item?.Trim(), seed, StringComparison.Ordinal)))
        {
            return;
        }

        values.Add(seed);
    }
}
