using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 硬盘离库处置数据访问契约。
/// </summary>
public interface IHardDiskDisposalRepository
{
    Task<List<HardDiskDisposalRecord>> SearchRecordsAsync(string? keyword, int? status, int? applyYear);

    Task<HardDiskDisposalRecord?> GetRecordByIdAsync(int recordId);

    Task<HardDiskDisposalRecord?> GetRecordByIdForUpdateAsync(int recordId);

    Task<string?> GetLastDisposalNoByPrefixAsync(string prefix);

    Task<List<HardDiskMedium>> GetSelectableInStockMediaAsync(IReadOnlyCollection<int>? excludeMediumIds = null);

    Task<List<HardDiskMedium>> GetMediaWithLedgerByIdsAsync(IReadOnlyCollection<int> mediumIds);

    Task<bool> ExistsActiveDisposalForMediumAsync(int mediumId, int? excludeRecordId = null);

    /// <summary>
    /// 资料室待办：已提交至办结前的离库处置单（不含草稿/作废/已办结）。
    /// </summary>
    Task<List<HardDiskDisposalRecord>> GetPendingRecordsForToDoAsync(int takeCount);

    Task<List<SystemAttachment>> GetAttachmentsAsync(string disposalNo);

    Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId);

    void AddRecord(HardDiskDisposalRecord record);

    void RemoveItems(IEnumerable<HardDiskDisposalItem> items);

    void AddTransaction(HardDiskMediaTransaction transaction);

    void AddAttachment(SystemAttachment attachment);

    void RemoveAttachment(SystemAttachment attachment);

    void RemoveRegisterLock(HardDiskRegisterLock lockItem);

    Task SaveChangesAsync();
}
