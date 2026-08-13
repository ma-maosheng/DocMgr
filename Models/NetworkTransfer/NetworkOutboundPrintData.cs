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

    public string Reason { get; init; } = string.Empty;

    public string ProofMaterialNote { get; init; } = string.Empty;

    public List<string> ItemLines { get; init; } = new();

    /// <summary>申请部门负责人签字栏。</summary>
    public string DeptLeaderBlock { get; init; } = string.Empty;

    public string ProdLeaderBlock { get; init; } = string.Empty;

    public string RndLeaderBlock { get; init; } = string.Empty;

    public string DeputyLeaderBlock { get; init; } = string.Empty;

    /// <summary>出网交接签字栏（移交人、资料员）。</summary>
    public string HandoverSignatureBlock { get; init; } = string.Empty;

    public int PrintCount { get; init; }
}
