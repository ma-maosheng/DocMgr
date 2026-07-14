using DocMgr.Models.YearlyArchive;

namespace DocMgr.Repositories.Interfaces
{
    /// <summary>
    /// 资料归还数据访问契约：归还单读写、待归还出库查询与原子事务。
    /// </summary>
    public interface IArchiveReturnRepository
    {
        /// <summary>开启资料归还业务的数据库事务，保证多表写入原子提交。</summary>
        Task<IArchiveFilingRepositoryTransaction> BeginTransactionAsync();

        Task<List<string>> GetReturnNosByPrefixAsync(string prefix);

        Task<List<YearlyArchiveReturnRecord>> ListByYearAsync(int year);

        Task<YearlyArchiveReturnRecord?> GetByIdWithDetailsAsync(int id);

        Task<int> SaveOrUpdateRecordGraphAsync(YearlyArchiveReturnRecord record);

        Task SaveChangesAsync();

        /// <summary>该出库单是否已存在未作废的归还单（防重复归还）。</summary>
        Task<bool> HasActiveReturnForOutboundAsync(int outboundRecordId, int excludeReturnId = 0);

        /// <summary>列出指定年度可发起归还的出库单（已办结出库、存在未归还的提档项、且无有效归还单）。</summary>
        Task<List<YearlyArchiveOutboundRecord>> GetReturnableOutboundsAsync(int year);

        /// <summary>列出已超过预计归还期限、仍有未归还提档项且无有效归还单的出库单（跨年度，供超期待办使用）。</summary>
        Task<List<YearlyArchiveOutboundRecord>> GetOverdueReturnOutboundsAsync(DateTime asOf, int take);

        /// <summary>列出已登记、尚未办结的资料归还单，供待办提醒使用。</summary>
        Task<List<YearlyArchiveReturnRecord>> GetPendingReturnRecordsForToDoAsync(int take);

        Task<List<SystemAttachment>> GetAttachmentsByBusinessIdAsync(int businessId);

        Task<List<SystemAttachment>> GetAttachmentsByBusinessNoAsync(string businessNo, string businessType);

        void AddAttachment(SystemAttachment attachment);

        void RemoveAttachment(SystemAttachment attachment);

        Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId);

        Task LinkOrphanAttachmentsToRecordAsync(string businessNo, string businessType, int recordId);
    }
}
