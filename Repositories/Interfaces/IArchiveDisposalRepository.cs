using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 年度资料离库处置数据访问契约。
/// </summary>
public interface IArchiveDisposalRepository
{
    Task<List<YearlyArchiveDisposalRecord>> SearchRecordsAsync(
        string? keyword,
        int? status,
        int? applyYear,
        string? mediaKind);

    Task<YearlyArchiveDisposalRecord?> GetRecordByIdAsync(int recordId);

    Task<YearlyArchiveDisposalRecord?> GetRecordByIdForUpdateAsync(int recordId);

    Task<string?> GetLastDisposalNoByPrefixAsync(string prefix);

    Task<List<ArchiveDisposalSelectableItem>> GetSelectableSimulatedItemsAsync(int? excludeRecordId = null);

    Task<List<ArchiveDisposalSelectableItem>> GetSelectableElectronicItemsAsync(int? excludeRecordId = null);

    Task<List<YearlyArchiveFilingFact>> GetFilingFactsByIdsAsync(IReadOnlyCollection<int> filingFactIds);

    Task<List<YearlyArchiveBox>> GetBoxesByIdsAsync(IReadOnlyCollection<int> boxIds);

    Task<List<YearlyElectronicArchiveUnit>> GetElectronicUnitsByIdsAsync(IReadOnlyCollection<int> unitIds);

    Task<List<HardDiskMedium>> GetHardDiskMediaWithLedgerByIdsAsync(IReadOnlyCollection<int> mediumIds);

    Task<List<OpticalDiscMedium>> GetOpticalDiscMediaWithLedgerByIdsAsync(IReadOnlyCollection<int> mediumIds);

    Task<List<YearlyArchiveFilingFact>> GetFilingFactsByContainerIdAsync(int containerId, string mediaKind);

    Task<List<YearlyElectronicArchiveUnitMediumLink>> GetHardDiskLinksByUnitIdAsync(int unitId);

    Task<List<YearlyElectronicArchiveUnitDiscLink>> GetDiscLinksByUnitIdAsync(int unitId);

    Task<List<YearlyArchiveFilingFact>> GetFilingFactsByElectronicUnitIdAsync(int unitId);

    Task<bool> ExistsActiveDisposalForFilingFactAsync(int filingFactId, int? excludeRecordId = null);

    Task<bool> ExistsActiveDisposalForMediumAsync(string mediumKind, int mediumId, int? excludeRecordId = null);

    Task<List<YearlyArchiveDisposalRecord>> GetPendingRecordsForToDoAsync(int takeCount);

    Task<List<SystemAttachment>> GetAttachmentsAsync(string disposalNo);

    Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId);

    void AddRecord(YearlyArchiveDisposalRecord record);

    void RemoveItems(IEnumerable<YearlyArchiveDisposalItem> items);

    void AddMaterialTransaction(YearlyArchiveMaterialTransaction transaction);

    void AddHardDiskTransaction(HardDiskMediaTransaction transaction);

    void AddOpticalDiscTransaction(OpticalDiscMediaTransaction transaction);

    void AddRegisterLock(HardDiskRegisterLock lockItem);

    void RemoveRegisterLock(HardDiskRegisterLock lockItem);

    void RemoveArchiveBoxPlacementByBoxCode(string boxCode);

    void RemoveHardDiskMediumLink(YearlyElectronicArchiveUnitMediumLink link);

    void RemoveDiscLink(YearlyElectronicArchiveUnitDiscLink link);

    void AddAttachment(SystemAttachment attachment);

    void RemoveAttachment(SystemAttachment attachment);

    Task SaveChangesAsync();
}
