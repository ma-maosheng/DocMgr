using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 年度资料出入网管理仓储。
/// </summary>
public interface INetworkTransferRepository
{
    /// <summary>开启跨域出入网办结事务。</summary>
    Task<IArchiveFilingRepositoryTransaction> BeginTransactionAsync();

    Task<List<NetworkInboundRecord>> SearchInboundRecordsAsync(string? keyword, int? status, int? applyYear);

    Task<NetworkInboundRecord?> GetInboundByIdAsync(int recordId, bool tracking = false);

    void AddInbound(NetworkInboundRecord record);

    void RemoveInboundItems(IEnumerable<NetworkInboundItem> items);

    void RemoveInboundReturnHardDiskItems(IEnumerable<NetworkInboundReturnHardDiskItem> items);

    /// <summary>替换入网申请关联的登记介质树（档外资料入网）。</summary>
    Task ReplaceInboundMediaEntriesAsync(int inboundRecordId, IReadOnlyList<YearlyArchiveRegisterMedia> mediaEntries);

    /// <summary>替换出网申请关联的登记介质树。</summary>
    Task ReplaceOutboundMediaEntriesAsync(int outboundRecordId, IReadOnlyList<YearlyArchiveRegisterMedia> mediaEntries);

    Task<string?> GetLastInboundNoByPrefixAsync(string prefix);

    Task<List<NetworkOutboundRecord>> SearchOutboundRecordsAsync(string? keyword, int? status, int? applyYear);

    Task<NetworkOutboundRecord?> GetOutboundByIdAsync(int recordId, bool tracking = false);

    void AddOutbound(NetworkOutboundRecord record);

    void RemoveOutboundItems(IEnumerable<NetworkOutboundItem> items);

    Task<string?> GetLastOutboundNoByPrefixAsync(string prefix);

    Task<List<NetworkOnNetDisposalRecord>> SearchDisposalRecordsAsync(string? keyword, int? status, int? applyYear);

    Task<NetworkOnNetDisposalRecord?> GetDisposalByIdAsync(int recordId, bool tracking = false);

    void AddDisposal(NetworkOnNetDisposalRecord record);

    void RemoveDisposalItems(IEnumerable<NetworkOnNetDisposalItem> items);

    Task<string?> GetLastDisposalNoByPrefixAsync(string prefix);

    Task<List<NetworkOnNetAsset>> SearchOnNetAssetsAsync(string? keyword, string? originKind, string? lifecycleStatus);

    Task<NetworkOnNetAsset?> GetOnNetAssetByIdAsync(int assetId, bool tracking = false);

    Task<NetworkOnNetAsset?> GetOnNetAssetByOriginInboundItemIdAsync(int inboundItemId, bool tracking = false);

    Task<List<NetworkOnNetAsset>> GetOnNetAssetsByIdsAsync(IReadOnlyCollection<int> assetIds, bool tracking = false);

    Task<List<NetworkOnNetAsset>> GetSelectableOutboundAssetsAsync(int? currentOutboundRecordId = null);

    Task<List<NetworkOnNetAsset>> GetSelectableDisposalAssetsAsync(int? currentDisposalRecordId = null);

    void AddOnNetAsset(NetworkOnNetAsset asset);

    Task<string?> GetLastOnNetAssetNoByPrefixAsync(string prefix);

    Task<Dictionary<int, YearlyArchiveFilingFact>> GetFilingFactsByIdsAsync(IReadOnlyCollection<int> filingFactIds);

    Task<YearlyArchiveSearchResultSet?> GetElectronicSearchResultSetAsync(int resultSetId);

    Task<List<SystemAttachment>> GetAttachmentsAsync(string businessType, string businessNo);

    Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId);

    void AddAttachment(SystemAttachment attachment);

    void RemoveAttachment(SystemAttachment attachment);

    void AddRegisterRecord(YearlyArchiveRegisterRecord record);

    Task<YearlyArchiveRegisterRecord?> GetRegisterBySourceOutboundRecordIdAsync(
        int outboundRecordId,
        bool tracking = false);

    Task<HashSet<string>> GetExistingMaterialTransactionDedupKeysAsync(IEnumerable<string> dedupKeys);

    void AddMaterialTransactions(IEnumerable<YearlyArchiveMaterialTransaction> transactions);

    Task SaveChangesAsync();
}
