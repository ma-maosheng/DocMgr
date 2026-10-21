namespace DocMgr.Models.Cabinets;

/// <summary>
/// 柜体内仍有存放内容的一个档口。
/// </summary>
public sealed class CabinetStoredContentSlot
{
    public string SlotKey { get; init; } = string.Empty;

    public string FaceCode { get; init; } = string.Empty;

    public int LayerIndex { get; init; }

    public int ColumnIndex { get; init; }

    public IReadOnlyList<string> ContentKinds { get; init; } = Array.Empty<string>();

    /// <summary>例如「甲A-1-2：年度档案盒、硬盘」。</summary>
    public string DisplayText
    {
        get
        {
            string kinds = ContentKinds.Count == 0 ? "有存放内容" : string.Join("、", ContentKinds);
            return $"{SlotKey}：{kinds}";
        }
    }
}
