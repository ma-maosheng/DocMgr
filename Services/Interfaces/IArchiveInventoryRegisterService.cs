using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.Interfaces;

/// <summary>
/// 年度资料盘库登记业务服务。
/// </summary>
public interface IArchiveInventoryRegisterService
{
    Task<IReadOnlyList<YearlyArchiveInventoryRegisterRecord>> SearchRecordsAsync(
        string? mediaKind,
        string? keyword,
        int? status,
        int? applyYear);

    Task<YearlyArchiveInventoryRegisterRecord?> GetRecordByIdAsync(int recordId);

    Task<IReadOnlyList<ArchiveInventorySelectableSimulatedFact>> GetSelectableSimulatedFilingFactsAsync(int? currentRecordId = null);

    Task<IReadOnlyList<ArchiveInventorySelectableElectronicMedia>> GetSelectableElectronicMediaAsync(int? currentRecordId = null);

    Task<string> GenerateNextRegisterNoAsync();

    Task<YearlyArchiveInventoryRegisterRecord> CreateDraftAsync(
        YearlyArchiveInventoryRegisterRecord draft,
        IReadOnlyList<ArchiveInventoryRegisterItemDraft> items,
        User currentUser);

    Task<YearlyArchiveInventoryRegisterRecord> UpdateDraftAsync(
        YearlyArchiveInventoryRegisterRecord draft,
        IReadOnlyList<ArchiveInventoryRegisterItemDraft> items,
        User currentUser);

    Task CompleteAsync(int recordId, User currentUser);

    Task WithdrawAsync(int recordId, string? reason, User currentUser);
}
