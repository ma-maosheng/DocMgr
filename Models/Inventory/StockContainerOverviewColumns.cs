namespace DocMgr.Models.Inventory;

/// <summary>
/// 库管资料总览两张表的列。随当前资料类型切换表头与绑定字段。
/// </summary>
public static class StockContainerOverviewColumns
{
    public static IReadOnlyList<StockContainerOverviewGridColumn> ContainerColumns(string? browseKind) =>
        browseKind switch
        {
            StockContainerBrowseKind.ElectronicBag => ElectronicContainerColumns,
            StockContainerBrowseKind.HistoryTopo
                or StockContainerBrowseKind.HistoryAerial
                or StockContainerBrowseKind.HistoryOther => HistoryBoxContainerColumns,
            StockContainerBrowseKind.HistoryMisc => HistoryMiscContainerColumns,
            _ => YearlyBoxContainerColumns
        };

    public static IReadOnlyList<StockContainerOverviewGridColumn> ContentColumns(string? browseKind) =>
        browseKind switch
        {
            StockContainerBrowseKind.ElectronicBag => ElectronicContentColumns,
            StockContainerBrowseKind.HistoryTopo => TopoContentColumns,
            StockContainerBrowseKind.HistoryAerial => AerialContentColumns,
            StockContainerBrowseKind.HistoryOther => OtherContentColumns,
            StockContainerBrowseKind.HistoryMisc => MiscContentColumns,
            _ => YearlyContentColumns
        };

    private static readonly StockContainerOverviewGridColumn[] YearlyBoxContainerColumns =
    [
        Text("归档序号", nameof(StockContainerOverviewRow.ContainerCode), 120),
        Text("项目", nameof(StockContainerOverviewRow.ProjectOrCategory), 160),
        Text("年度", nameof(StockContainerOverviewRow.Year), 70),
        Location("存放位置", nameof(StockContainerOverviewRow.StorageLocation), 140),
        Text("状态", nameof(StockContainerOverviewRow.LifecycleStatusDisplay), 90),
        Number("件数", nameof(StockContainerOverviewRow.ItemCount), 60),
        Text("内容摘要", nameof(StockContainerOverviewRow.ContentSummary), 240, star: true),
        Text("规格", nameof(StockContainerOverviewRow.Specs), 90),
        Text("放置方式", nameof(StockContainerOverviewRow.PlacementModeDisplay), 90),
        Text("归档人", nameof(StockContainerOverviewRow.ArchivedBy), 80),
        Text("归档日期", nameof(StockContainerOverviewRow.ArchivedDateDisplay), 100)
    ];

    private static readonly StockContainerOverviewGridColumn[] ElectronicContainerColumns =
    [
        Text("电子编号", nameof(StockContainerOverviewRow.ContainerCode), 140),
        Text("项目", nameof(StockContainerOverviewRow.ProjectOrCategory), 160),
        Text("年度", nameof(StockContainerOverviewRow.Year), 70),
        Location("存放位置", nameof(StockContainerOverviewRow.StorageLocation), 140),
        Text("状态", nameof(StockContainerOverviewRow.LifecycleStatusDisplay), 90),
        Number("件数", nameof(StockContainerOverviewRow.ItemCount), 60),
        Text("内容摘要", nameof(StockContainerOverviewRow.ContentSummary), 240, star: true),
        Text("载体", nameof(StockContainerOverviewRow.Specs), 180),
        Text("立档人", nameof(StockContainerOverviewRow.ArchivedBy), 80),
        Text("归档日期", nameof(StockContainerOverviewRow.ArchivedDateDisplay), 100)
    ];

    private static readonly StockContainerOverviewGridColumn[] HistoryBoxContainerColumns =
    [
        Location("盒号", nameof(StockContainerOverviewRow.StorageLocation), 150),
        Text("规格", nameof(StockContainerOverviewRow.Specs), 90),
        Text("放置方式", nameof(StockContainerOverviewRow.PlacementModeDisplay), 90),
        Text("状态", nameof(StockContainerOverviewRow.LifecycleStatusDisplay), 80),
        Number("台账条数", nameof(StockContainerOverviewRow.ItemCount), 80),
        Text("盒内摘要", nameof(StockContainerOverviewRow.ContentSummary), 240, star: true),
        Text("存档批次", nameof(StockContainerOverviewRow.ProjectOrCategory), 160),
        Text("归档日期", nameof(StockContainerOverviewRow.ArchivedDateDisplay), 100),
        Text("备注", nameof(StockContainerOverviewRow.Remarks), 140)
    ];

    private static readonly StockContainerOverviewGridColumn[] HistoryMiscContainerColumns =
    [
        Text("资料大类", nameof(StockContainerOverviewRow.MajorCategory), 110),
        Text("资料子类", nameof(StockContainerOverviewRow.SubCategory), 110),
        Text("存放容器", nameof(StockContainerOverviewRow.Specs), 80),
        Text("容器编号", nameof(StockContainerOverviewRow.ContainerCode), 100),
        Location("存放位置", nameof(StockContainerOverviewRow.StorageLocation), 150),
        Text("状态", nameof(StockContainerOverviewRow.LifecycleStatusDisplay), 80),
        Number("详情条数", nameof(StockContainerOverviewRow.ItemCount), 80),
        Text("资料详情", nameof(StockContainerOverviewRow.ContentSummary), 240, star: true),
        Text("登记人", nameof(StockContainerOverviewRow.ArchivedBy), 80),
        Text("登记日期", nameof(StockContainerOverviewRow.ArchivedDateDisplay), 110),
        Text("备注", nameof(StockContainerOverviewRow.Remarks), 140)
    ];

    private static readonly StockContainerOverviewGridColumn[] YearlyContentColumns =
    [
        Text("介质类别", nameof(StockContainerContentItem.MaterialKindDisplay), 90),
        Text("资料名称", nameof(StockContainerContentItem.Title), 180),
        Text("明细", nameof(StockContainerContentItem.ItemName), 140),
        Text("密级", nameof(StockContainerContentItem.ConfidentialLevel), 70),
        Number("份数", nameof(StockContainerContentItem.ContentCount), 60),
        Text("生命周期", nameof(StockContainerContentItem.LifecycleStatusDisplay), 80),
        Text("原件/备份", nameof(StockContainerContentItem.ArchiveCopyRoleDisplay), 80),
        Text("介质编号", nameof(StockContainerContentItem.MediumCode), 110),
        Text("表单号", nameof(StockContainerContentItem.FormNo), 120),
        Text("立档编号", nameof(StockContainerContentItem.ExtraInfo), 140, star: true)
    ];

    private static readonly StockContainerOverviewGridColumn[] ElectronicContentColumns =
    [
        Text("资料名称", nameof(StockContainerContentItem.Title), 180),
        Text("明细", nameof(StockContainerContentItem.ItemName), 140),
        Text("密级", nameof(StockContainerContentItem.ConfidentialLevel), 70),
        Text("数据量(MB)", nameof(StockContainerContentItem.DataSizeDisplay), 90),
        Text("载体", nameof(StockContainerContentItem.CarrierText), 80),
        Text("介质编号", nameof(StockContainerContentItem.MediumCode), 110),
        Text("生命周期", nameof(StockContainerContentItem.LifecycleStatusDisplay), 80),
        Text("原件/备份", nameof(StockContainerContentItem.ArchiveCopyRoleDisplay), 80),
        Text("表单号", nameof(StockContainerContentItem.FormNo), 120),
        Text("立档编号", nameof(StockContainerContentItem.ExtraInfo), 140),
        Text("存储路径", nameof(StockContainerContentItem.PathText), 200, star: true)
    ];

    private static readonly StockContainerOverviewGridColumn[] TopoContentColumns =
    [
        Text("图名", nameof(StockContainerContentItem.Title), 150),
        Text("图号", nameof(StockContainerContentItem.CodeText), 110),
        Text("当前图号", nameof(StockContainerContentItem.ItemName), 120),
        Text("比例尺", nameof(StockContainerContentItem.Scale), 80),
        Number("幅数", nameof(StockContainerContentItem.ContentCount), 60),
        Text("成图日期", nameof(StockContainerContentItem.DateText), 100),
        Text("区域", nameof(StockContainerContentItem.AreaText), 140),
        Text("状态", nameof(StockContainerContentItem.LifecycleStatusDisplay), 80),
        Text("备注", nameof(StockContainerContentItem.Remark), 160, star: true)
    ];

    private static readonly StockContainerOverviewGridColumn[] AerialContentColumns =
    [
        Text("测区", nameof(StockContainerContentItem.AreaText), 150),
        Text("比例尺", nameof(StockContainerContentItem.Scale), 80),
        Text("航摄日期", nameof(StockContainerContentItem.DateText), 100),
        Text("盒内物品", nameof(StockContainerContentItem.Title), 220, star: true),
        Number("张数", nameof(StockContainerContentItem.ContentCount), 70),
        Text("状态", nameof(StockContainerContentItem.LifecycleStatusDisplay), 80),
        Text("备注", nameof(StockContainerContentItem.Remark), 160)
    ];

    private static readonly StockContainerOverviewGridColumn[] OtherContentColumns =
    [
        Text("序号", nameof(StockContainerContentItem.CodeText), 70),
        Text("资料分类", nameof(StockContainerContentItem.AreaText), 120),
        Text("年度", nameof(StockContainerContentItem.DateText), 100),
        Text("资料内容", nameof(StockContainerContentItem.Title), 220, star: true),
        Text("状态", nameof(StockContainerContentItem.LifecycleStatusDisplay), 80),
        Text("备注", nameof(StockContainerContentItem.Remark), 160)
    ];

    private static readonly StockContainerOverviewGridColumn[] MiscContentColumns =
    [
        Text("资料分类", nameof(StockContainerContentItem.Title), 180),
        Text("详情", nameof(StockContainerContentItem.ItemName), 280, star: true),
        Text("状态", nameof(StockContainerContentItem.LifecycleStatusDisplay), 80)
    ];

    private static StockContainerOverviewGridColumn Text(
        string header,
        string propertyName,
        double width,
        bool star = false) =>
        new(header, propertyName, width, star, traceLocation: false, numeric: false);

    private static StockContainerOverviewGridColumn Location(string header, string propertyName, double width) =>
        new(header, propertyName, width, star: false, traceLocation: true, numeric: false);

    private static StockContainerOverviewGridColumn Number(string header, string propertyName, double width) =>
        new(header, propertyName, width, star: false, traceLocation: false, numeric: true);
}

/// <summary>总览表格一列：表头、绑定属性与导出取值。</summary>
public sealed class StockContainerOverviewGridColumn
{
    public StockContainerOverviewGridColumn(
        string header,
        string propertyName,
        double width,
        bool star,
        bool traceLocation,
        bool numeric)
    {
        Header = header;
        PropertyName = propertyName;
        Width = width;
        Star = star;
        TraceLocation = traceLocation;
        Numeric = numeric;
    }

    public string Header { get; }

    public string PropertyName { get; }

    public double Width { get; }

    public bool Star { get; }

    /// <summary>已退库位置编码用褐色。</summary>
    public bool TraceLocation { get; }

    public bool Numeric { get; }

    public string ReadContainer(StockContainerOverviewRow row) => PropertyName switch
    {
        nameof(StockContainerOverviewRow.ContainerCode) => row.ContainerCode,
        nameof(StockContainerOverviewRow.ProjectOrCategory) => row.ProjectOrCategory,
        nameof(StockContainerOverviewRow.MajorCategory) => row.MajorCategory,
        nameof(StockContainerOverviewRow.SubCategory) => row.SubCategory,
        nameof(StockContainerOverviewRow.Year) => row.Year,
        nameof(StockContainerOverviewRow.StorageLocation) => row.StorageLocation,
        nameof(StockContainerOverviewRow.LifecycleStatusDisplay) => row.LifecycleStatusDisplay,
        nameof(StockContainerOverviewRow.ItemCount) => row.ItemCount.ToString(),
        nameof(StockContainerOverviewRow.ContentSummary) => row.ContentSummary,
        nameof(StockContainerOverviewRow.Specs) => row.Specs,
        nameof(StockContainerOverviewRow.PlacementModeDisplay) => row.PlacementModeDisplay,
        nameof(StockContainerOverviewRow.ArchivedBy) => row.ArchivedBy,
        nameof(StockContainerOverviewRow.ArchivedDateDisplay) => row.ArchivedDateDisplay,
        nameof(StockContainerOverviewRow.Remarks) => row.Remarks,
        _ => string.Empty
    };

    public string ReadContent(StockContainerContentItem item) => PropertyName switch
    {
        nameof(StockContainerContentItem.MaterialKindDisplay) => item.MaterialKindDisplay,
        nameof(StockContainerContentItem.Title) => item.Title,
        nameof(StockContainerContentItem.ItemName) => item.ItemName,
        nameof(StockContainerContentItem.ConfidentialLevel) => item.ConfidentialLevel,
        nameof(StockContainerContentItem.ContentCount) => item.ContentCount.ToString(),
        nameof(StockContainerContentItem.LifecycleStatusDisplay) => item.LifecycleStatusDisplay,
        nameof(StockContainerContentItem.ArchiveCopyRoleDisplay) => item.ArchiveCopyRoleDisplay,
        nameof(StockContainerContentItem.MediumCode) => item.MediumCode,
        nameof(StockContainerContentItem.FormNo) => item.FormNo,
        nameof(StockContainerContentItem.ExtraInfo) => item.ExtraInfo,
        nameof(StockContainerContentItem.Scale) => item.Scale,
        nameof(StockContainerContentItem.CodeText) => item.CodeText,
        nameof(StockContainerContentItem.DateText) => item.DateText,
        nameof(StockContainerContentItem.AreaText) => item.AreaText,
        nameof(StockContainerContentItem.Remark) => item.Remark,
        nameof(StockContainerContentItem.DataSizeDisplay) => item.DataSizeDisplay,
        nameof(StockContainerContentItem.CarrierText) => item.CarrierText,
        nameof(StockContainerContentItem.PathText) => item.PathText,
        _ => string.Empty
    };

    public bool TryReadContainerNumber(StockContainerOverviewRow row, out int number)
    {
        if (PropertyName == nameof(StockContainerOverviewRow.ItemCount))
        {
            number = row.ItemCount;
            return true;
        }

        number = 0;
        return false;
    }

    public bool TryReadContentNumber(StockContainerContentItem item, out int number)
    {
        if (PropertyName == nameof(StockContainerContentItem.ContentCount))
        {
            number = item.ContentCount;
            return true;
        }

        number = 0;
        return false;
    }
}
