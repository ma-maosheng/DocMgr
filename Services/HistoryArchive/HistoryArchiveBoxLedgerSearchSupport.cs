using DocMgr.Models.HistoryArchive;

namespace DocMgr.Services.HistoryArchive;

/// <summary>
/// 按资料类别拼装盒内台账的可检索文本。列表仍按盒展示，关键词命中其中任意一条资料即可。
/// </summary>
public static class HistoryArchiveBoxLedgerSearchSupport
{
    /// <summary>地形图：图上图号、当前图号及该条其余业务字段。</summary>
    public static string BuildTopoMap(TopoMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return Join(
            map.Category,
            map.Scale,
            map.MapNumber,
            map.CurrentMapNumber,
            map.MapName,
            map.SheetCount.ToString(),
            map.CreationDate,
            map.SurveyDate,
            map.CoordinateSystem,
            map.ElevationDatum,
            map.Region,
            map.Registrant,
            map.RegistrationDate,
            map.Modifier,
            map.ModificationDate,
            map.Remark,
            map.BoxNumber,
            map.BoxSpecification,
            map.LifecycleStatus,
            map.LastStorageLocation);
    }

    /// <summary>航摄：测区、盒内内容及该条其余业务字段。</summary>
    public static string BuildAerialPhoto(AerialPhoto photo)
    {
        ArgumentNullException.ThrowIfNull(photo);
        return Join(
            photo.Category,
            photo.SurveyArea,
            photo.Scale,
            photo.PhotographyDate,
            photo.BoxContents,
            photo.PhotoCount.ToString(),
            photo.Registrant,
            photo.RegistrationDate,
            photo.Modifier,
            photo.ModificationDate,
            photo.Remark,
            photo.BoxNumber,
            photo.BoxSpecification,
            photo.LifecycleStatus,
            photo.LastStorageLocation);
    }

    /// <summary>其他资料：序号、资料分类、资料内容、起止年度及该条其余业务字段。</summary>
    public static string BuildOtherMap(OtherMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return Join(
            map.Category,
            map.SequenceNumber,
            map.MaterialCategory,
            map.StartYear,
            map.EndYear,
            map.Scale,
            map.MapName,
            map.Registrant,
            map.RegistrationDate,
            map.Modifier,
            map.ModificationDate,
            map.Remark,
            map.BoxNumber,
            map.BoxSpecification,
            map.LifecycleStatus,
            map.LastStorageLocation);
    }

    /// <summary>杂项：容器、分类、详情行及登记信息。</summary>
    public static string BuildMisc(HistoryMiscMaterial material, int photoCount)
    {
        ArgumentNullException.ThrowIfNull(material);
        var values = new List<string?>
        {
            material.MajorCategory,
            material.SubCategory,
            material.ContainerKind,
            material.ContainerCode,
            material.StorageLocation,
            material.LastStorageLocation,
            material.Remark,
            material.Registrant,
            FormatDate(material.RegisteredAt),
            material.Modifier,
            FormatDate(material.ModifiedAt),
            material.LifecycleStatus,
            photoCount.ToString()
        };
        values.AddRange(material.Lines
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .Select(item => item.LineText));
        return Join(values);
    }

    /// <summary>合并多条资料的检索文本。</summary>
    public static string Join(IEnumerable<string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return string.Join(
            '\n',
            values
                .Select(item => item?.Trim() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private static string Join(params string?[] values) => Join((IEnumerable<string?>)values);

    private static string FormatDate(DateTime? value) =>
        value.HasValue ? value.Value.ToString("yyyy-MM-dd") : string.Empty;
}
