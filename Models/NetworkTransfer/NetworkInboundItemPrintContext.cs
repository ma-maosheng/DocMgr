using DocMgr.Models.YearlyArchive;

namespace DocMgr.Models.NetworkTransfer;

/// <summary>
/// 入网明细打印 enrichment 上下文（立档检索集来源）。
/// </summary>
public sealed class NetworkInboundItemPrintContext
{
    public static NetworkInboundItemPrintContext Empty { get; } = new()
    {
        HitsByFactId = new Dictionary<int, FiledArchiveSearchHit>(),
        ResultSetItemsById = new Dictionary<int, YearlyArchiveSearchResultSetItem>()
    };

    public IReadOnlyDictionary<int, FiledArchiveSearchHit> HitsByFactId { get; init; } =
        new Dictionary<int, FiledArchiveSearchHit>();

    public IReadOnlyDictionary<int, YearlyArchiveSearchResultSetItem> ResultSetItemsById { get; init; } =
        new Dictionary<int, YearlyArchiveSearchResultSetItem>();
}
