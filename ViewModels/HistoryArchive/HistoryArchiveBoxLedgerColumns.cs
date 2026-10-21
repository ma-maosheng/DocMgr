using DocMgr.Models.HistoryArchive;

namespace DocMgr.ViewModels.HistoryArchive;

/// <summary>
/// HA-BOX 列表列。一行是档案盒或杂项容器，列随资料类别切换。
/// </summary>
public static class HistoryArchiveBoxLedgerColumns
{
    public const string SetAll = "All";

    public const string SetBox = "Box";

    public const string SetMisc = "Misc";

    private static readonly HistoryArchiveBoxLedgerColumn[] AllColumns =
    [
        Text("资料类别", nameof(HistoryArchiveBoxLedgerRow.MaterialKindDisplay), 130),
        Text("状态", nameof(HistoryArchiveBoxLedgerRow.LifecycleStatus), 80),
        Text("容器类型", nameof(HistoryArchiveBoxLedgerRow.ContainerKind), 80),
        Text("容器编号", nameof(HistoryArchiveBoxLedgerRow.ContainerCode), 90),
        Text("资料大类", nameof(HistoryArchiveBoxLedgerRow.MajorCategory), 90),
        Text("资料子类", nameof(HistoryArchiveBoxLedgerRow.SubCategory), 90),
        Location("盒号", nameof(HistoryArchiveBoxLedgerRow.BoxCode), 150),
        Text("规格", nameof(HistoryArchiveBoxLedgerRow.BoxSpecification), 90),
        Text("柜体", nameof(HistoryArchiveBoxLedgerRow.CabinetName), 100),
        Text("盒内摘要", nameof(HistoryArchiveBoxLedgerRow.ContentSummary), 240, star: true),
        Number("台账条数", nameof(HistoryArchiveBoxLedgerRow.LedgerRecordCount), 80),
        Text("混放", nameof(HistoryArchiveBoxLedgerRow.MixedPlacementText), 50),
        Number("关联盒数", nameof(HistoryArchiveBoxLedgerRow.RelatedBoxCount), 80),
        Text("关联混放盒号", nameof(HistoryArchiveBoxLedgerRow.RelatedBoxCodesText), 160, exportOnly: true),
        Text("可处置", nameof(HistoryArchiveBoxLedgerRow.AvailabilityText), 100)
    ];

    private static readonly HistoryArchiveBoxLedgerColumn[] BoxColumns =
    [
        Text("状态", nameof(HistoryArchiveBoxLedgerRow.LifecycleStatus), 80),
        Location("盒号", nameof(HistoryArchiveBoxLedgerRow.BoxCode), 150),
        Text("规格", nameof(HistoryArchiveBoxLedgerRow.BoxSpecification), 90),
        Text("柜体", nameof(HistoryArchiveBoxLedgerRow.CabinetName), 100),
        Text("盒内摘要", nameof(HistoryArchiveBoxLedgerRow.ContentSummary), 240, star: true),
        Number("台账条数", nameof(HistoryArchiveBoxLedgerRow.LedgerRecordCount), 80),
        Text("混放", nameof(HistoryArchiveBoxLedgerRow.MixedPlacementText), 50),
        Number("关联盒数", nameof(HistoryArchiveBoxLedgerRow.RelatedBoxCount), 80),
        Text("关联混放盒号", nameof(HistoryArchiveBoxLedgerRow.RelatedBoxCodesText), 160, exportOnly: true),
        Text("可处置", nameof(HistoryArchiveBoxLedgerRow.AvailabilityText), 100)
    ];

    private static readonly HistoryArchiveBoxLedgerColumn[] MiscColumns =
    [
        Text("资料大类", nameof(HistoryArchiveBoxLedgerRow.MajorCategory), 110),
        Text("资料子类", nameof(HistoryArchiveBoxLedgerRow.SubCategory), 110),
        Text("存放容器", nameof(HistoryArchiveBoxLedgerRow.ContainerKind), 80),
        Text("容器编号", nameof(HistoryArchiveBoxLedgerRow.ContainerCode), 100),
        Location("存放位置", nameof(HistoryArchiveBoxLedgerRow.BoxCode), 150),
        Text("状态", nameof(HistoryArchiveBoxLedgerRow.LifecycleStatus), 80),
        Text("资料详情", nameof(HistoryArchiveBoxLedgerRow.ContentSummary), 240, star: true),
        Number("照片", nameof(HistoryArchiveBoxLedgerRow.PhotoCount), 60),
        Text("可处置", nameof(HistoryArchiveBoxLedgerRow.AvailabilityText), 100)
    ];

    /// <summary>按资料类别解析列集：全部 / 档案盒 / 杂项。</summary>
    public static string ResolveSet(string? materialKindDisplay)
    {
        if (HistoryArchiveDisposalDomainValues.IsMiscMaterial(materialKindDisplay))
        {
            return SetMisc;
        }

        if (string.IsNullOrWhiteSpace(materialKindDisplay)
            || string.Equals(
                materialKindDisplay.Trim(),
                HistoryArchiveBoxLedgerPageViewModel.AllOption,
                StringComparison.Ordinal))
        {
            return SetAll;
        }

        return SetBox;
    }

    /// <summary>当前类别在表格中显示的列。</summary>
    public static IReadOnlyList<HistoryArchiveBoxLedgerColumn> ForGrid(string? materialKindDisplay) =>
        ForSet(ResolveSet(materialKindDisplay)).Where(column => !column.ExportOnly).ToArray();

    /// <summary>当前类别导出的列（含仅导出的关联盒号）。</summary>
    public static IReadOnlyList<HistoryArchiveBoxLedgerColumn> ForExport(string? materialKindDisplay) =>
        ForSet(ResolveSet(materialKindDisplay));

    private static IReadOnlyList<HistoryArchiveBoxLedgerColumn> ForSet(string set) => set switch
    {
        SetMisc => MiscColumns,
        SetBox => BoxColumns,
        _ => AllColumns
    };

    private static HistoryArchiveBoxLedgerColumn Text(
        string header,
        string propertyName,
        double width,
        bool star = false,
        bool exportOnly = false) =>
        new(header, propertyName, width, star, traceLocation: false, exportOnly, numeric: false);

    private static HistoryArchiveBoxLedgerColumn Location(string header, string propertyName, double width) =>
        new(header, propertyName, width, star: false, traceLocation: true, exportOnly: false, numeric: false);

    private static HistoryArchiveBoxLedgerColumn Number(string header, string propertyName, double width) =>
        new(header, propertyName, width, star: false, traceLocation: false, exportOnly: false, numeric: true);
}

/// <summary>HA-BOX 一列：表头、绑定属性与导出取值。</summary>
public sealed class HistoryArchiveBoxLedgerColumn
{
    public HistoryArchiveBoxLedgerColumn(
        string header,
        string propertyName,
        double width,
        bool star,
        bool traceLocation,
        bool exportOnly,
        bool numeric)
    {
        Header = header;
        PropertyName = propertyName;
        Width = width;
        Star = star;
        TraceLocation = traceLocation;
        ExportOnly = exportOnly;
        Numeric = numeric;
    }

    public string Header { get; }

    public string PropertyName { get; }

    public double Width { get; }

    public bool Star { get; }

    /// <summary>已离库位置编码用褐色。</summary>
    public bool TraceLocation { get; }

    /// <summary>只写入导出，不出现在表格。</summary>
    public bool ExportOnly { get; }

    public bool Numeric { get; }

    public string ReadText(HistoryArchiveBoxLedgerRow row) => PropertyName switch
    {
        nameof(HistoryArchiveBoxLedgerRow.MaterialKindDisplay) => row.MaterialKindDisplay,
        nameof(HistoryArchiveBoxLedgerRow.LifecycleStatus) => row.LifecycleStatus,
        nameof(HistoryArchiveBoxLedgerRow.ContainerKind) => row.ContainerKind,
        nameof(HistoryArchiveBoxLedgerRow.ContainerCode) => row.ContainerCode,
        nameof(HistoryArchiveBoxLedgerRow.MajorCategory) => row.MajorCategory,
        nameof(HistoryArchiveBoxLedgerRow.SubCategory) => row.SubCategory,
        nameof(HistoryArchiveBoxLedgerRow.BoxCode) => row.BoxCode,
        nameof(HistoryArchiveBoxLedgerRow.BoxSpecification) => row.BoxSpecification,
        nameof(HistoryArchiveBoxLedgerRow.CabinetName) => row.CabinetName,
        nameof(HistoryArchiveBoxLedgerRow.ContentSummary) => row.ContentSummary,
        nameof(HistoryArchiveBoxLedgerRow.LedgerRecordCount) => row.LedgerRecordCount.ToString(),
        nameof(HistoryArchiveBoxLedgerRow.MixedPlacementText) => row.MixedPlacementText,
        nameof(HistoryArchiveBoxLedgerRow.RelatedBoxCount) => row.RelatedBoxCount.ToString(),
        nameof(HistoryArchiveBoxLedgerRow.RelatedBoxCodesText) => row.RelatedBoxCodesText,
        nameof(HistoryArchiveBoxLedgerRow.AvailabilityText) => row.AvailabilityText,
        nameof(HistoryArchiveBoxLedgerRow.PhotoCount) => row.PhotoCount.ToString(),
        _ => string.Empty
    };

    public int ReadNumber(HistoryArchiveBoxLedgerRow row) => PropertyName switch
    {
        nameof(HistoryArchiveBoxLedgerRow.LedgerRecordCount) => row.LedgerRecordCount,
        nameof(HistoryArchiveBoxLedgerRow.RelatedBoxCount) => row.RelatedBoxCount,
        nameof(HistoryArchiveBoxLedgerRow.PhotoCount) => row.PhotoCount,
        _ => 0
    };
}
