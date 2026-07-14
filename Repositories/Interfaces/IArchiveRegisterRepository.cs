using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Models.HardDiskMedia;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 资料登记数据访问契约：年度登记记录及明细数据读写。
/// </summary>
public interface IArchiveRegisterRepository
{
    Task<YearlyArchiveRegisterRecord?> GetByFormNoWithDetailsAsync(string formNo);

    Task<YearlyArchiveRegisterRecord?> GetByIdWithDetailsAsync(int id);

    Task<List<YearlyArchiveRegisterRecord>> SearchRecordsAsync(string keyword, int? year, int? status, int? projectId);

    /// <summary>列出资料室尚未办结（已提交/已审批/已上传签字件）的登记申请，供待办提醒使用。</summary>
    Task<List<YearlyArchiveRegisterRecord>> GetSubmittedRecordsForToDoAsync(int takeCount);

    Task<int> SaveOrUpdateRecordGraphAsync(YearlyArchiveRegisterRecord record);

    Task<int> LinkOrphanAttachmentsToRecordAsync(string formNo, int recordId);

    Task<List<User>> GetUsersAsync();

    Task<List<SystemAttachment>> GetAttachmentSummariesByFormNoAsync(string formNo);

    Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId);

    void AddAttachment(SystemAttachment attachment);

    void RemoveAttachment(SystemAttachment attachment);

    Task<List<YearlyArchiveRegisterRecord>> GetRecordsByApplicantAsync(string applicantName);

    Task<List<YearlyArchiveRegisterRecord>> GetRecordsByYearAsync(int year);

    Task<List<string>> GetFormNosByPrefixAsync(string prefix);

    Task<List<int>> GetDistinctCreatedYearsAsync();

    Task<YearlyArchiveRegisterRecord?> GetRecordForRemovalAsync(int id);

    Task<List<SystemAttachment>> GetAttachmentsByBusinessIdAsync(int businessId);

    Task<List<SystemAttachment>> GetOrphanAttachmentsByFormNoAsync(string formNo);

    void RemoveAttachments(IEnumerable<SystemAttachment> attachments);

    void RemoveRegisterRecord(YearlyArchiveRegisterRecord record);

    Task<List<FieldDomainDefinition>> GetPageDomainDefinitionsAsync(
        string registerRecordEntityName,
        IReadOnlyCollection<string> registerRecordFields,
        string registerMediaEntityName,
        IReadOnlyCollection<string> registerMediaFields,
        string registerMediaItemEntityName,
        IReadOnlyCollection<string> registerMediaItemFields);

    List<FieldDomainDefinition> GetPageDomainDefinitions(
        string registerRecordEntityName,
        IReadOnlyCollection<string> registerRecordFields,
        string registerMediaEntityName,
        IReadOnlyCollection<string> registerMediaFields,
        string registerMediaItemEntityName,
        IReadOnlyCollection<string> registerMediaItemFields);

    void SeedFieldDomainDefaults();

    Task<List<int>> GetElectronicArchiveUnitIdsByRegisterRecordIdAsync(int registerRecordId);

    Task<List<(string DiscCode, string Location, string BusinessNo, DateTime OperateTime)>> GetOpticalDiscLedgerRowsAsync(IReadOnlyCollection<int> unitIds);

    Task<List<HardDiskMedium>> GetHardDiskMediaByRegisterLockAsync(int recordId, string formNo, bool onlyNotDeleted);

    Task<List<HardDiskMedium>> GetHardDiskMediaByDiskCodesAsync(IReadOnlyCollection<string> diskCodes);

    void RemoveHardDiskRegisterLock(HardDiskRegisterLock registerLock);

    Task<int> SaveChangesAsync();
}
