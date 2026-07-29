namespace DocMgr.Services.YearlyArchive;

/// <summary>
/// 年度资料盘库登记明细草稿。
/// </summary>
public sealed class ArchiveInventoryRegisterItemDraft
{
    /// <summary>立档事实 ID（模拟轨）。</summary>
    public int FilingFactId { get; set; }

    /// <summary>盘库丢失份数（模拟轨）。</summary>
    public int LostCopyCount { get; set; }

    /// <summary>介质类别：硬盘 / 光盘（电子轨）。</summary>
    public string MediumKind { get; set; } = string.Empty;

    /// <summary>介质 ID（电子轨）。</summary>
    public int MediumId { get; set; }
}
