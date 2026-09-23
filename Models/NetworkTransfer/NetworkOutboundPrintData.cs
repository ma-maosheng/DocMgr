namespace DocMgr.Models.NetworkTransfer;

/// <summary>
/// 年度资料出网申请审批单打印数据。
/// </summary>
public sealed class NetworkOutboundPrintData
{
    public string OutboundNo { get; init; } = string.Empty;

    public string ApplyDateText { get; init; } = string.Empty;

    public string ApplicantName { get; init; } = string.Empty;

    public string ApplicantDept { get; init; } = string.Empty;

    public string YearText { get; init; } = string.Empty;

    public string ProjectName { get; init; } = string.Empty;

    public string DestinationKindText { get; init; } = string.Empty;

    public string ArchivePurposeText { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string ProofMaterialNote { get; init; } = string.Empty;

    public List<string> ItemLines { get; init; } = new();

    /// <summary>是否存在尚未从离线拷贝介质补录目录/数据量的子项。</summary>
    public bool HasPendingItemDetailCapture { get; init; }

    /// <summary>申请部门负责人签字栏。</summary>
    public string DeptHeadBlock { get; init; } = string.Empty;

    public string ProductionHeadBlock { get; init; } = string.Empty;

    public string ArchiveRoomHeadBlock { get; init; } = string.Empty;

    public string ArchiveDeputyPresidentBlock { get; init; } = string.Empty;

    public string ProductionVicePresidentBlock { get; init; } = string.Empty;

    public bool EnableDeptHead { get; init; } = true;

    public bool EnableProductionHead { get; init; } = true;

    public bool EnableArchiveRoomHead { get; init; } = true;

    public bool EnableArchiveDeputyPresident { get; init; } = true;

    public bool EnableProductionVicePresident { get; init; } = true;

    /// <summary>出网交接签字栏（移交人、资料员）。</summary>
    public string HandoverSignatureBlock { get; init; } = string.Empty;

    public int PrintCount { get; init; }
}
