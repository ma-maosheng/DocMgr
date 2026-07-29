using DocMgr.Models.YearlyArchive;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Cabinets;
using DocMgr.Models.ArchiveContainers;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 立档数据访问契约：立档涉及的登记记录、容器与摆放数据读写。
/// </summary>
public interface IArchiveFilingRepository
{
    Task<IArchiveFilingRepositoryTransaction> BeginTransactionAsync();

    Task<List<YearlyArchiveRegisterRecord>> GetPendingRecordsAsync(int? year);

    Task<List<YearlyArchiveRegisterRecord>> GetPendingSimulatedRecordsAsync(int? year);

    Task<List<YearlyArchiveRegisterRecord>> GetPendingElectronicRecordsAsync(int? year);

    /// <summary>
    /// 统计指定年度内已办结且模拟介质轨已全部立档的登记单数量。
    /// </summary>
    Task<int> GetFiledSimulatedRecordCountAsync(int? year);

    /// <summary>
    /// 统计指定年度内已办结且电子介质轨已全部立档的登记单数量。
    /// </summary>
    Task<int> GetFiledElectronicRecordCountAsync(int? year);

    /// <summary>
    /// 获取已办结且仍有未立档明细的登记单（供待办中心轻量查询）。
    /// </summary>
    Task<List<YearlyArchiveRegisterRecord>> GetCompletedUnfiledRecordsForToDoAsync(int takeCount);

    Task<List<ArchiveBoxSpecification>> GetArchiveBoxSpecificationsAsync();

    Task<List<CabinetSlotSpecification>> GetCabinetSlotSpecificationsAsync();

    Task<List<CabinetSlotSpecialRule>> GetEnabledCabinetSlotSpecialRulesBySpecificationAsync(string boxSpecification);

    Task<List<Cabinet>> GetNonMagneticCabinetsAsync();

    Task<List<YearlyArchiveBox>> GetExistingYearlyArchiveBoxesWithCabinetAsync();

    Task<List<CabinetArchiveBoxPlacement>> GetArchiveBoxPlacementsAsync();

    Task<List<YearlyArchiveBox>> GetExistingBoxesForProjectAsync(string projectName, string year);

    Task<List<YearlyElectronicArchiveUnit>> GetExistingElectronicUnitsForProjectAsync(string projectName, string year);

    Task<YearlyArchiveBox?> GetLastArchiveBoxByPrefixAsync(string prefix);

    Task<YearlyElectronicArchiveUnit?> GetLastElectronicUnitByPrefixAsync(string prefix);

    Task<int> CountElectronicUnitsInSlotAsync(string slotCode, string slotPrefix);

    Task<List<int>> GetElectronicUnitSequenceIndexesInSlotAsync(string slotCode, string slotPrefix, int? excludeUnitId = null);

    Task<List<string>> GetElectronicArchiveUnitStorageLocationsInSlotAsync(string slotCode, string slotPrefix);

    Task<bool> IsArchiveSequenceExistsAsync(string sequenceNo);

    Task<bool> IsElectronicArchiveNoExistsAsync(string sequenceNo);

    Task<List<HardDiskElectronicArchiveLinkInfo>> GetElectronicArchiveLinkInfosAsync(IReadOnlyCollection<int> mediumIds);

    Task<List<YearlyArchiveRegisterRecord>> GetRegisterRecordsForArchivingAsync(IReadOnlyCollection<int> recordIds);

    Task<List<YearlyArchiveRegisterMediaItem>> GetSimulatedMediaItemsForArchivingAsync(IReadOnlyCollection<int> mediaItemIds);

    Task<List<YearlyArchiveRegisterMedia>> GetElectronicMediaEntriesForArchivingAsync(IReadOnlyCollection<int> mediaEntryIds);

    Task<List<YearlyArchiveRegisterMediaItem>> GetElectronicMediaItemsForArchivingAsync(IReadOnlyCollection<int> mediaItemIds);

    Task<List<YearlyElectronicArchiveUnitMediaItemLink>> GetElectronicArchiveUnitMediaItemLinksByUnitIdAsync(int unitId);

    Task<List<YearlyElectronicArchiveUnitMediaItemLink>> GetElectronicArchiveUnitMediaItemLinksByMediumCodeAsync(string mediumCode);

    void AddElectronicArchiveUnitMediaItemLink(YearlyElectronicArchiveUnitMediaItemLink link);

    void AddRegisterMediaItem(YearlyArchiveRegisterMediaItem item);

    Task<List<YearlyArchiveRegisterRecord>> GetRegisterRecordsForSimulatedArchivingAsync(IReadOnlyCollection<int> recordIds);

    Task<YearlyElectronicArchiveUnit?> GetElectronicArchiveUnitWithDetailsAsync(int unitId);

    Task<OpticalDiscMedium?> GetOpticalDiscMediumByCodeAsync(string discCode);

    void AddOpticalDiscMedium(OpticalDiscMedium medium);

    Task<YearlyElectronicArchiveUnitDiscLink?> GetElectronicArchiveUnitDiscLinkAsync(int unitId, int opticalDiscMediumId, string discCode);

    Task<HardDiskMedium?> GetHardDiskMediumByIdWithLedgerAsync(int mediumId);

    Task<OpticalDiscMedium?> GetOpticalDiscMediumByIdWithLedgerAsync(int mediumId);

    Task<HardDiskMedium?> GetHardDiskMediumByDiskCodeWithLedgerAsync(string diskCode);

    Task<List<HardDiskMedium>> GetHardDiskMediaByCodesWithLedgerAsync(IReadOnlyCollection<string> diskCodes);

    Task<bool> HasCompletedReturnApplicationAsync(int mediumId, int? sourceApplicationId);

    /// <summary>
    /// 指定借出硬盘编号下，是否仍存在未立档的「硬盘·介质留存·资料室借出」登记介质条目（仅统计已办结登记单）。
    /// <paramref name="excludingMediaEntryIds"/> 为本次立档拟入袋的登记介质条目，预览/提交时不应计入「未立档」。
    /// </summary>
    Task<bool> HasPendingRetainedRegisterEntriesForBorrowedDiskAsync(
        string diskCode,
        IReadOnlyCollection<int>? excludingMediaEntryIds = null);

    /// <summary>
    /// 指定登记单下，是否仍存在未立档的「硬盘·介质留存·外来硬盘」登记介质条目。
    /// </summary>
    Task<bool> HasPendingExternalRetainedRegisterEntriesOnRecordsAsync(IReadOnlyCollection<int> registerRecordIds);

    /// <summary>
    /// 根据登记介质条目主键解析所属登记单主键。
    /// </summary>
    Task<List<int>> GetRegisterRecordIdsForMediaEntriesAsync(IReadOnlyCollection<int> mediaEntryIds);

    void AddHardDiskMediaApplication(HardDiskMediaApplication application);

    void AddHardDiskMediaTransaction(HardDiskMediaTransaction transaction);

    Task<List<int>> GetArchiveBoxLinkedMediaItemIdsAsync(int boxId);

    List<int> GetArchiveBoxLinkedMediaItemIds(int boxId);

    void AddArchiveBoxMediaItemLink(YearlyArchiveBoxMediaItemLink link);

    Task<List<YearlyArchiveRegisterRecord>> GetRecordsForSimulatedStatusUpdateAsync(IReadOnlyCollection<int> recordIds);

    Task<List<YearlyArchiveRegisterRecord>> GetRecordsForElectronicStatusUpdateAsync(IReadOnlyCollection<int> recordIds);

    Task<YearlyArchiveRegisterRecord?> GetRegisterRecordForDeletionAsync(int id);

    Task<YearlyArchiveBox?> GetArchiveBoxWithRegisterRecordsAsync(int boxId);

    /// <summary>按档案盒编号批量读取档案盒（检索结果盒级摘要用）。</summary>
    Task<IReadOnlyList<YearlyArchiveBox>> GetArchiveBoxesBySequenceNosAsync(IReadOnlyCollection<string> sequenceNos);

    Task<CabinetArchiveBoxPlacement?> GetArchiveBoxPlacementByCodeAsync(string boxCode);

    CabinetArchiveBoxPlacement? GetArchiveBoxPlacementByCode(string boxCode);

    void AddArchiveBoxPlacement(CabinetArchiveBoxPlacement placement);

    void RemoveArchiveBoxPlacementByBoxCode(string boxCode);

    CabinetSlotSpecialRule? GetCabinetSlotSpecialRule(string cabinetName, string slotCode, string boxSpecification, string sideCode);

    Task<List<ArchiveContainerProjection>> GetArchiveContainerProjectionsAsync(string projectName, string year, ArchiveContainerKind containerKind);

    Task<List<string>> GetYearlyArchiveBoxLocationCodesAsync();

    Task<List<YearlyArchiveBox>> GetInUseYearlyArchiveBoxesInSlotAsync(
        string cabinetName,
        string side,
        int row,
        int column);

    Task<int> CountHistoryArchiveOccupanciesInSlotAsync(
        string cabinetName,
        string side,
        int row,
        int column);

    Task<Cabinet?> GetMagneticDiskCabinetByNameAsync(string cabinetName);

    Task<string?> GetMagneticDiskSlotCategoryNameAsync(int cabinetId, string faceCode, string slotCode);

    Task<string?> GetArchiveSlotCategoryNameAsync(int cabinetId, string faceCode, string slotCode);

    /// <summary>
    /// 批量读取标准档案柜档口用途；键为「柜体Id:面:档口编号」。
    /// </summary>
    Task<Dictionary<string, string>> GetArchiveSlotCategoryLookupForCabinetsAsync(IReadOnlyCollection<int> cabinetIds);

    Task<bool> IsMagneticDiskSlotFullyEmptyAsync(string slotCode, string slotPrefix);

    Task<List<string>> GetTopoMapBoxNumbersAsync();

    Task<List<string>> GetAerialPhotoBoxNumbersAsync();

    Task<List<string>> GetOtherMapBoxNumbersAsync();

    Task<List<SystemAttachment>> GetRegisterAttachmentsByBusinessIdAsync(int id);

    void RemoveAttachments(IEnumerable<SystemAttachment> attachments);

    void RemoveRegisterRecord(YearlyArchiveRegisterRecord record);

    void AddArchiveBox(YearlyArchiveBox box);

    void AddElectronicArchiveUnit(YearlyElectronicArchiveUnit unit);

    Task<int> SaveChangesAsync();
}
