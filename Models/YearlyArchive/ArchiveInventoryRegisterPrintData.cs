namespace DocMgr.Models.YearlyArchive;

/// <summary>
/// 年度资料盘库登记签批单打印数据。
/// </summary>
public sealed class ArchiveInventoryRegisterPrintData
{
    public string RegisterNo { get; init; } = string.Empty;

    public string MediaKind { get; init; } = string.Empty;

    public string RegisterKind { get; init; } = string.Empty;

    public string ApplyDateText { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string Remark { get; init; } = string.Empty;

    public string ApplicantName { get; init; } = string.Empty;

    public string ApplicantDept { get; init; } = string.Empty;

    public string ApprovedBy { get; init; } = string.Empty;

    public string ApprovedDateText { get; init; } = string.Empty;

    public string ApprovalOpinion { get; init; } = string.Empty;

    public string CompletedBy { get; init; } = string.Empty;

    public string CompletedDateText { get; init; } = string.Empty;

    public bool IsCompleted { get; init; }

    public string DeptHead { get; init; } = string.Empty;

    public string DeptHeadDateText { get; init; } = string.Empty;

    public string ArchiveRoomHead { get; init; } = string.Empty;

    public string ArchiveRoomHeadDateText { get; init; } = string.Empty;

    public string ProductionHead { get; init; } = string.Empty;

    public string ProductionHeadDateText { get; init; } = string.Empty;

    public string ArchiveDeputyPresident { get; init; } = string.Empty;

    public string ArchiveDeputyPresidentDateText { get; init; } = string.Empty;

    public string ProductionVicePresident { get; init; } = string.Empty;

    public string ProductionVicePresidentDateText { get; init; } = string.Empty;

    public bool EnableDeptHead { get; init; }

    public bool EnableArchiveRoomHead { get; init; } = true;

    public bool EnableProductionHead { get; init; }

    public bool EnableArchiveDeputyPresident { get; init; } = true;

    public bool EnableProductionVicePresident { get; init; }

    /// <summary>已累计打印次数（不含本次）。</summary>
    public int PrintCount { get; init; }

    public IReadOnlyList<ArchiveInventoryRegisterPrintItemData> Items { get; init; } =
        Array.Empty<ArchiveInventoryRegisterPrintItemData>();
}

/// <summary>
/// 年度资料盘库登记签批单明细行。
/// </summary>
public sealed class ArchiveInventoryRegisterPrintItemData
{
    public int SortOrder { get; init; }

    public string MaterialName { get; init; } = string.Empty;

    public string ItemName { get; init; } = string.Empty;

    public string ContainerCode { get; init; } = string.Empty;

    public string MediumKind { get; init; } = string.Empty;

    public string MediumCode { get; init; } = string.Empty;

    public int LostCopyCount { get; init; }

    public string BeforeStorageLocation { get; init; } = string.Empty;
}
