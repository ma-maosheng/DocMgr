using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.Cabinets;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 硬盘介质台账数据访问契约：硬盘登记、借还与位置数据读写。
/// </summary>
public interface IHardDiskMediaRepository
{
    Task<List<HardDiskMedium>> GetOverviewMediaAsync();

    Task<List<HardDiskMediaApplication>> GetOverviewApplicationsAsync();

    Task<List<HardDiskMediaTransaction>> GetOverviewTransactionsAsync();

    /// <summary>概览：离库处置单清单（轻量，不含明细）。</summary>
    Task<List<HardDiskDisposalRecord>> GetOverviewDisposalRecordsAsync();

    /// <summary>概览：盘库登记单清单（轻量，不含明细）。</summary>
    Task<List<HardDiskInventoryRegisterRecord>> GetOverviewInventoryRegisterRecordsAsync();

    Task<List<HardDiskMedium>> GetSelectableMediaAsync();

    Task<List<HardDiskMediaApplication>> GetCompletedOutboundApplicationsForReturnCandidatesAsync();

    /// <summary>获取介质上尚未办结的归还登记单（草稿/已登记/待办结）。</summary>
    Task<HardDiskMediaApplication?> GetActiveReturnRegistrationByMediumIdAsync(int mediumId);

    /// <summary>获取介质上尚未办结的归还登记单（可更新，供档口搬迁同步目标位置）。</summary>
    Task<HardDiskMediaApplication?> GetActiveReturnRegistrationByMediumIdForUpdateAsync(int mediumId);

    /// <summary>获取存在未办结归还登记单的介质 ID 集合。</summary>
    Task<List<int>> GetMediumIdsWithActiveReturnRegistrationAsync();

    /// <summary>获取存在硬盘占用锁的介质 ID 集合。</summary>
    Task<HashSet<int>> GetMediumIdsWithRegisterLockAsync(IReadOnlyCollection<int> mediumIds);

    /// <summary>查询资料出库办结后库内空盘征用、仍处于借出且需归还的硬盘来源。</summary>
    Task<List<HardDiskMediaArchiveOutboundRequisitionReturnSource>> GetArchiveOutboundRequisitionReturnSourcesAsync();

    Task<HardDiskMediaApplication?> GetLatestCompletedOutboundApplicationByDiskCodeAsync(string diskCode);

    Task<List<HardDiskMedium>> SearchMediaAsync(string? keyword, string? status, string? nature);

    /// <summary>
    /// 查询资料立档可选取的在库空白硬盘（未被 HardDiskRegisterLocks 占用）。
    /// </summary>
    Task<List<HardDiskMedium>> GetArchiveFilingCandidateBlankHardDisksAsync(string? keyword);

    Task<List<OpticalDiscMedium>> SearchOpticalDiscMediaAsync(string? keyword, string? status);

    Task<List<OpticalDiscMedium>> GetOpticalDiscMediaForExportAsync();

    /// <summary>获取光盘概览用的介质清单（含台账）。</summary>
    Task<List<OpticalDiscMedium>> GetOpticalDiscOverviewMediaAsync();

    /// <summary>获取光盘概览用的流转流水。</summary>
    Task<List<OpticalDiscMediaTransaction>> GetOpticalDiscOverviewTransactionsAsync();

    Task<List<OpticalDiscMediumTransactionRecord>> SearchOpticalDiscTransactionsAsync(
        string? discCodeKeyword,
        string? businessNoKeyword,
        int? mediumId = null,
        string? transactionType = null);

    Task<List<HardDiskMediaTransaction>> SearchTransactionsAsync(string? keyword, string? transactionType);

    Task<List<HardDiskMediaApplication>> SearchApplicationsAsync(string? keyword, int? status, string? applicationType);

    /// <summary>列出资料室尚未办结（已提交/已审批/已上传签字件）的硬盘出库类申请，供待办提醒使用。</summary>
    Task<List<HardDiskMediaApplication>> GetSubmittedApplicationsForToDoAsync(int takeCount);

    /// <summary>列出资料室尚未办结（已提交/已审批/已上传签字件）的硬盘归还/挂失登记，供待办提醒使用。</summary>
    Task<List<HardDiskMediaApplication>> GetPendingReturnRegistrationsForToDoAsync(int takeCount);

    /// <summary>列出已超过预计归还期限、介质仍处于借出状态且无有效归还登记的出库申请（供超期待办使用）。</summary>
    Task<List<HardDiskMediaApplication>> GetOverdueOutboundApplicationsForToDoAsync(DateTime asOf, int takeCount);

    Task<List<SystemAttachment>> GetApplicationAttachmentsAsync(string businessType, string applicationNo);

    Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId);

    Task<HardDiskMedium?> GetActiveMediumByIdAsync(int mediumId);

    Task<HardDiskMedium?> GetActiveMediumWithLedgerByIdAsync(int mediumId);

    Task<HardDiskMedium?> GetActiveMediumWithLedgerByIdForUpdateAsync(int mediumId);

    Task<HardDiskMediaApplication?> GetApplicationByIdAsync(int applicationId);

    Task<HardDiskMediaApplication?> GetApplicationWithMediumLedgerByIdAsync(int applicationId);

    Task<HardDiskMediaApplication?> GetApplicationWithMediumLedgerByIdAsNoTrackingAsync(int applicationId);

    Task<bool> ExistsOtherActiveOutboundApplicationAsync(int mediumId, int? excludedApplicationId);

    Task<string?> GetApplicationNoByIdAsync(int applicationId);

    Task<string?> GetOutboundNoByRecordIdAsync(int outboundRecordId);

    Task<HardDiskMediaBorrowApprovalSnapshot?> GetOutboundApprovalSnapshotAsync(int outboundRecordId);

    Task<bool> HasDuplicateApplicationNoAsync(int currentId, string applicationNo);

    Task<bool> HasDuplicateDiskCodeAsync(int currentId, string diskCode);

    Task<bool> HasDuplicateSerialNumberAsync(int currentId, string serialNumber);

    void AddApplication(HardDiskMediaApplication application);

    void AddMedium(HardDiskMedium medium);

    void AddTransaction(HardDiskMediaTransaction transaction);

    Task<SystemAttachment?> GetSystemAttachmentByIdAsync(int attachmentId);

    void AddSystemAttachment(SystemAttachment attachment);

    void RemoveSystemAttachment(SystemAttachment attachment);

    Task<bool> HasOtherSignedAttachmentsAsync(string businessType, int businessId, int excludedAttachmentId, string fileCategory);

    Task<List<string>> GetDomainOptionLabelsAsync(string entityName, string fieldName);

    Task<List<CabinetHardDiskSlotCategoryAssignment>> GetDedicatedMagneticSlotsByCategoryAsync(string categoryName);

    Task<List<HardDiskMedium>> GetBlankInStockMediaNeedingLocationAssignmentAsync();

    Task<CabinetHardDiskSlotCategoryAssignment?> GetFirstDedicatedMagneticSlotByCategoryAsync(string categoryName);

    Task<Dictionary<string, int>> GetInStockLedgerCountsByLocationsAsync(IReadOnlyCollection<string> locations);

    Task<Dictionary<string, int>> GetInStockBlankLedgerCountsBySlotCodesAsync(IReadOnlyCollection<string> slotCodes);

    Task<List<int>> GetInStockHardDiskSequenceIndexesInSlotAsync(string slotCode);

    Task<List<string>> GetInStockHardDiskStorageLocationsInSlotAsync(string slotCode);

    /// <summary>
    /// 查询指定档口键下在库空白硬盘。
    /// </summary>
    /// <param name="slotKey">档口键。</param>
    /// <param name="unlockedOnly">
    /// true：仅返回无征用锁的可用空白盘（选源/可征用）；
    /// false：含征用锁占用盘（按物理占用校验目标档口容量与混放）。
    /// </param>
    Task<List<HardDiskMedium>> GetInStockBlankHardDisksInSlotAsync(string slotKey, bool unlockedOnly = true);

    /// <summary>
    /// 查询指定档口键下在库损坏硬盘。
    /// </summary>
    /// <param name="slotKey">档口键。</param>
    /// <param name="unlockedOnly">
    /// true：仅返回无征用锁的可用损坏盘（选源）；
    /// false：含征用锁占用盘（按物理占用校验目标档口）。
    /// </param>
    Task<List<HardDiskMedium>> GetInStockDamagedHardDisksInSlotAsync(string slotKey, bool unlockedOnly = true);

    /// <summary>
    /// 查询指定档口键下在库损坏光盘。
    /// </summary>
    Task<List<OpticalDiscMedium>> GetInStockDamagedOpticalDiscsInSlotAsync(string slotKey);

    /// <summary>
    /// 统计指定档口内借出未还、原归属该档口的空白硬盘数量。
    /// </summary>
    Task<int> CountPendingReturnBlankHardDisksInSlotAsync(string slotKey);

    /// <summary>
    /// 加载指定档口内借出未还、原归属该档口的空白硬盘（含台账与流转，供档口搬迁更新引用）。
    /// </summary>
    Task<List<HardDiskMedium>> LoadPendingReturnBlankHardDisksInSlotForRelocationAsync(string slotKey);

    /// <summary>
    /// 按介质 ID 加载已办结的硬盘借出申请单。
    /// </summary>
    Task<List<HardDiskMediaApplication>> GetCompletedOutboundApplicationsByMediumIdsAsync(IReadOnlyCollection<int> mediumIds);

    Task<List<string>> GetInStockOpticalDiscStorageLocationsInSlotAsync(string slotCode);

    Task<int> GetCurrentInStockMediumCountAsync(string location);

    Task<List<string>> GetActiveDiskCodesAsync();

    Task<string?> FindFirstDuplicateDiskCodeAsync(IReadOnlyCollection<string> diskCodes);

    Task<string?> FindFirstDuplicateSerialNumberAsync(IReadOnlyCollection<string> serialNumbers);

    void RemoveApplication(HardDiskMediaApplication application);

    Task<string?> GetLastApplicationNoByPrefixAsync(string prefix);

    Task<int> SaveChangesAsync();

    Task<bool> HasMediaRecordsAsync();

    Task<bool> HasAnyApplicationsAsync();

    Task<bool> HasAnyTransactionsAsync();

    Task<int> GetMediaCountAsync();

    Task DeleteAllMediaAsync();

    Task AddMediaRangeAsync(IReadOnlyCollection<HardDiskMedium> media);

    Task<IHardDiskMediaRepositoryTransaction> BeginTransactionAsync();
}
