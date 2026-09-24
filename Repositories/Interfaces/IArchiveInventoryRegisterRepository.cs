using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 年度资料盘库登记数据访问契约。
/// </summary>
public interface IArchiveInventoryRegisterRepository
{
    Task<List<YearlyArchiveInventoryRegisterRecord>> SearchRecordsAsync(
        string? mediaKind,
        string? keyword,
        int? status,
        int? applyYear);

    Task<YearlyArchiveInventoryRegisterRecord?> GetRecordByIdAsync(int recordId);

    Task<YearlyArchiveInventoryRegisterRecord?> GetRecordByIdForUpdateAsync(int recordId);

    Task<string?> GetLastRegisterNoByPrefixAsync(string prefix);

    Task<List<ArchiveInventorySelectableSimulatedFact>> GetSelectableSimulatedFilingFactsAsync(
        IReadOnlyCollection<int>? excludeFactIds = null);

    Task<List<ArchiveInventorySelectableElectronicMedia>> GetSelectableElectronicMediaAsync(
        IReadOnlyCollection<ArchiveInventoryElectronicMediumKey>? excludeMedia = null,
        int? excludeRecordId = null);

    Task<bool> ExistsActiveArchiveInventoryForMediumAsync(
        string mediumKind,
        int mediumId,
        int? excludeRecordId = null);

    Task<bool> ExistsActiveArchiveInventoryForFilingFactAsync(
        int filingFactId,
        int? excludeRecordId = null);

    Task<bool> ExistsActiveHardDiskInventoryOrDisposalForMediumAsync(int mediumId);

    /// <summary>资料室待办：草稿起至办结前的盘库登记单。</summary>
    Task<List<YearlyArchiveInventoryRegisterRecord>> GetPendingRecordsForToDoAsync(int takeCount);

    Task<List<YearlyArchiveFilingFact>> GetFactsWithDetailsAsync(IReadOnlyCollection<int> filingFactIds);

    /// <summary>按项目 ID 批量取实施年度（ImplementYear）。</summary>
    Task<Dictionary<int, string>> GetProjectImplementYearsByIdsAsync(IReadOnlyCollection<int> projectIds);

    Task<List<YearlyArchiveFilingFact>> GetElectronicFilingFactsByMediumAsync(
        string mediumKind,
        int mediumId,
        int electronicArchiveUnitId);

    Task<HardDiskMedium?> GetHardDiskWithLedgerAsync(int mediumId);

    Task<OpticalDiscMedium?> GetOpticalDiscWithLedgerAsync(int mediumId);

    Task<List<HardDiskMedium>> GetHardDisksWithLedgerByIdsAsync(IReadOnlyCollection<int> mediumIds);

    void AddRecord(YearlyArchiveInventoryRegisterRecord record);

    void RemoveItems(IEnumerable<YearlyArchiveInventoryRegisterItem> items);

    void AddMaterialTransaction(YearlyArchiveMaterialTransaction transaction);

    void AddHardDiskTransaction(HardDiskMediaTransaction transaction);

    void AddOpticalDiscTransaction(OpticalDiscMediaTransaction transaction);

    void RemoveRegisterLock(HardDiskRegisterLock lockItem);

    /// <summary>本单当前占用的全部硬盘登记锁（电子轨草稿改明细时先整单解锁再按新明细加锁）。</summary>
    Task<List<HardDiskRegisterLock>> GetOwnedRegisterLocksAsync(int recordId);

    Task<List<SystemAttachment>> GetAttachmentsAsync(string registerNo);

    Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId);

    void AddAttachment(SystemAttachment attachment);

    void RemoveAttachment(SystemAttachment attachment);

    Task SaveChangesAsync();
}

/// <summary>
/// 模拟轨盘库登记可选立档事实行。
/// </summary>
public sealed class ArchiveInventorySelectableSimulatedFact
{
    public int FilingFactId { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public string Year { get; init; } = string.Empty;

    public string MaterialName { get; init; } = string.Empty;

    public string ItemName { get; init; } = string.Empty;

    public string ContainerCode { get; init; } = string.Empty;

    public string StorageLocation { get; init; } = string.Empty;

    public int AvailableCopyCount { get; init; }
}

/// <summary>
/// 电子轨盘库登记可选介质行。
/// </summary>
public sealed class ArchiveInventorySelectableElectronicMedia
{
    public string MediumKind { get; init; } = string.Empty;

    public int MediumId { get; init; }

    public string MediumCode { get; init; } = string.Empty;

    public int ElectronicArchiveUnitId { get; init; }

    public string ElectronicArchiveNo { get; init; } = string.Empty;

    public string ProjectName { get; init; } = string.Empty;

    public string Year { get; init; } = string.Empty;

    public string BeforeMediaStatus { get; init; } = string.Empty;

    public string BeforeStorageLocation { get; init; } = string.Empty;

    public string MaterialName { get; init; } = string.Empty;

    public string ItemName { get; init; } = string.Empty;
}

/// <summary>
/// 电子轨可选介质排除键。
/// </summary>
public readonly record struct ArchiveInventoryElectronicMediumKey(string MediumKind, int MediumId);
