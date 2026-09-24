using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 硬盘盘库登记数据访问契约。
/// </summary>
public interface IHardDiskInventoryRegisterRepository
{
    Task<List<HardDiskInventoryRegisterRecord>> SearchRecordsAsync(string? keyword, int? status, int? applyYear);

    Task<HardDiskInventoryRegisterRecord?> GetRecordByIdAsync(int recordId);

    Task<HardDiskInventoryRegisterRecord?> GetRecordByIdForUpdateAsync(int recordId);

    Task<string?> GetLastRegisterNoByPrefixAsync(string prefix);

    Task<List<HardDiskMedium>> GetSelectableInStockMediaAsync(IReadOnlyCollection<int>? excludeMediumIds = null);

    Task<List<HardDiskMedium>> GetMediaWithLedgerByIdsAsync(IReadOnlyCollection<int> mediumIds);

    Task<bool> ExistsActiveRegisterForMediumAsync(int mediumId, int? excludeRecordId = null);

    Task<bool> ExistsActiveDisposalForMediumAsync(int mediumId);

    /// <summary>资料室待办：草稿起至办结前的盘库登记单。</summary>
    Task<List<HardDiskInventoryRegisterRecord>> GetPendingRecordsForToDoAsync(int takeCount);

    void AddRecord(HardDiskInventoryRegisterRecord record);

    void RemoveItems(IEnumerable<HardDiskInventoryRegisterItem> items);

    void AddTransaction(HardDiskMediaTransaction transaction);

    void RemoveRegisterLock(HardDiskRegisterLock lockItem);

    /// <summary>本单当前占用的全部硬盘登记锁（草稿改明细时先整单解锁再按新明细加锁）。</summary>
    Task<List<HardDiskRegisterLock>> GetOwnedRegisterLocksAsync(int recordId);

    Task<List<SystemAttachment>> GetAttachmentsAsync(string registerNo);

    Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId);

    void AddAttachment(SystemAttachment attachment);

    void RemoveAttachment(SystemAttachment attachment);

    Task SaveChangesAsync();
}
