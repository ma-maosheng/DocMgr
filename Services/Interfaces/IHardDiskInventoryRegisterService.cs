using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.HardDiskMedia;

namespace DocMgr.Services.Interfaces;

/// <summary>
/// 硬盘盘库登记业务服务。
/// </summary>
public interface IHardDiskInventoryRegisterService
{
    Task<IReadOnlyList<HardDiskInventoryRegisterRecord>> SearchRecordsAsync(string? keyword, int? status, int? applyYear);

    Task<HardDiskInventoryRegisterRecord?> GetRecordByIdAsync(int recordId);

    Task<IReadOnlyList<HardDiskMedium>> GetSelectableMediaAsync(int? currentRecordId = null);

    Task<string> GenerateNextRegisterNoAsync();

    Task<IReadOnlyList<HardDiskMediaReturnTargetLocationOption>> GetDamagedTargetLocationOptionsAsync();

    Task<HardDiskInventoryRegisterRecord> CreateDraftAsync(
        HardDiskInventoryRegisterRecord draft,
        IReadOnlyList<HardDiskInventoryRegisterItemDraft> items,
        User currentUser);

    Task<HardDiskInventoryRegisterRecord> UpdateDraftAsync(
        HardDiskInventoryRegisterRecord draft,
        IReadOnlyList<HardDiskInventoryRegisterItemDraft> items,
        User currentUser);

    Task CompleteAsync(int recordId, User currentUser);

    Task WithdrawAsync(int recordId, string? reason, User currentUser);
}
