using DocMgr.Models.HardDiskMedia;

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

    void AddRecord(HardDiskInventoryRegisterRecord record);

    void RemoveItems(IEnumerable<HardDiskInventoryRegisterItem> items);

    void AddTransaction(HardDiskMediaTransaction transaction);

    void RemoveRegisterLock(HardDiskRegisterLock lockItem);

    Task SaveChangesAsync();
}
