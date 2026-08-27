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

    Task<List<CabinetArchiveBoxPlacement>> GetHistoryPlacementsAsync();

    Task<List<TopoMap>> GetTopoMapsAsync();

    Task<List<AerialPhoto>> GetAerialPhotosAsync();

    Task<List<OtherMap>> GetOtherMapsAsync();

    Task<List<TopoMap>> GetTopoMapsForUpdateAsync();

    Task<List<AerialPhoto>> GetAerialPhotosForUpdateAsync();

    Task<List<OtherMap>> GetOtherMapsForUpdateAsync();

    Task<List<TopoMap>> GetTopoMapsByIdsAsync(IReadOnlyCollection<int> ids, bool tracking);

    Task<List<AerialPhoto>> GetAerialPhotosByIdsAsync(IReadOnlyCollection<int> ids, bool tracking);

    Task<List<OtherMap>> GetOtherMapsByIdsAsync(IReadOnlyCollection<int> ids, bool tracking);

    Task<HashSet<string>> GetLockedBoxCodesAsync(int? excludeRecordId);

    Task<List<HistoryArchiveDisposalRecord>> GetPendingRecordsForToDoAsync(int takeCount);

    Task<List<SystemAttachment>> GetAttachmentsAsync(string disposalNo);

    Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId);

    void AddRecord(HistoryArchiveDisposalRecord record);

    void RemoveItems(IEnumerable<HistoryArchiveDisposalItem> items);

    void RemoveArchiveBoxPlacementByBoxCode(string boxCode);

    void AddAttachment(SystemAttachment attachment);

    void RemoveAttachment(SystemAttachment attachment);

    Task SaveChangesAsync();
}
