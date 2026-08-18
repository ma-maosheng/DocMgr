namespace DocMgr.Models.NetworkTransfer;

/// <summary>
/// 在网对象关联的电子介质组织形式快照（列表补全用，不落库）。
/// </summary>
public sealed class NetworkOnNetElectronicMediaSnapshot
{
    public int MediaItemId { get; init; }

    public int? InboundRecordId { get; init; }

    public int? OutboundRecordId { get; init; }

    public string ContentDesc { get; init; } = string.Empty;

    public string DataOrganizationForm { get; init; } = string.Empty;

    public int EntryCount { get; init; }
}
