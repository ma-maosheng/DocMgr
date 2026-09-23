namespace DocMgr.Models.NetworkTransfer;

/// <summary>
/// 年度资料入网申请审批单打印数据。
/// </summary>
public sealed class NetworkInboundPrintData
{
    public string InboundNo { get; init; } = string.Empty;

    public string ApplyDateText { get; init; } = string.Empty;

    public string ApplicantName { get; init; } = string.Empty;

    public string ApplicantDept { get; init; } = string.Empty;

    public string YearText { get; init; } = string.Empty;

    public string ProjectName { get; init; } = string.Empty;

    public string MaterialName { get; init; } = string.Empty;

    public string SourceKindText { get; init; } = string.Empty;

    public string ProvideUnitText { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string OtherRequests { get; init; } = string.Empty;

    public string ProofMaterialNote { get; init; } = string.Empty;

    /// <summary>借出硬盘随资料归还说明；无则留空且不打印对应行。</summary>
    public string ReturnBorrowedHardDiskText { get; init; } = string.Empty;

    public string ServerPath { get; init; } = string.Empty;

    /// <summary>服务器路径对应物理地址。</summary>
    public string ServerPhysicalPath { get; init; } = string.Empty;

    public List<string> ItemLines { get; init; } = new();

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

    /// <summary>入网交接签字栏（移交人、资料员）。</summary>
    public string HandoverSignatureBlock { get; init; } = string.Empty;

    public int PrintCount { get; init; }
}
