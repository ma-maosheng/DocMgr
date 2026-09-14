using DocMgr.Models.HistoryArchive;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 历史存档离库处置数据访问契约。
/// </summary>
public interface IHistoryArchiveDisposalRepository
{
    Task<List<HistoryArchiveDisposalRecord>> SearchRecordsAsync(string? keyword, int? status, int? applyYear);

    Task<HistoryArchiveDisposalRecord?> GetRecordByIdAsync(int recordId, bool tracking = false);

    Task<string?> GetLastDisposalNoByPrefixAsync(string prefix);

    /// <summary>跨类混放盒号集合：同一盒号的台账关联存在多种资料类别（链接表聚合）。</summary>
    Task<List<string>> GetCrossTypeMixedBoxCodesAsync();

    /// <summary>全部在库历史档案盒（无跟踪），供处置候选构建。</summary>
    Task<List<HistoryArchiveBox>> GetInStockHistoryArchiveBoxesAsync();

    Task<List<TopoMap>> GetTopoMapsAsync();

    Task<List<AerialPhoto>> GetAerialPhotosAsync();

    Task<List<OtherMap>> GetOtherMapsAsync();

    Task<List<TopoMap>> GetTopoMapsByIdsAsync(IReadOnlyCollection<int> ids, bool tracking);

    Task<List<AerialPhoto>> GetAerialPhotosByIdsAsync(IReadOnlyCollection<int> ids, bool tracking);

    Task<List<OtherMap>> GetOtherMapsByIdsAsync(IReadOnlyCollection<int> ids, bool tracking);

    Task<HashSet<string>> GetLockedBoxCodesAsync(int? excludeRecordId);

    /// <summary>按盒号取在册历史档案盒（带跟踪），供生命周期同步与迁档改写。</summary>
    Task<List<HistoryArchiveBox>> GetHistoryArchiveBoxesByCodesAsync(IReadOnlyCollection<string> boxCodes);

    /// <summary>删除指定盒号的台账关联（盒已离库时清理残链）。</summary>
    Task RemoveHistoryArchiveBoxLinksByBoxCodesAsync(IReadOnlyCollection<string> boxCodes);

    Task<List<HistoryArchiveDisposalRecord>> GetPendingRecordsForToDoAsync(int takeCount);

    Task<List<SystemAttachment>> GetAttachmentsAsync(string disposalNo);

    Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId);

    void AddRecord(HistoryArchiveDisposalRecord record);

    void RemoveItems(IEnumerable<HistoryArchiveDisposalItem> items);

    void AddAttachment(SystemAttachment attachment);

    void RemoveAttachment(SystemAttachment attachment);

    Task SaveChangesAsync();
}
