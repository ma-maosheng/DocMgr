using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 年度资料出库服务契约：借阅/移交申请、审批、交接与打印。
    /// </summary>
    public interface IArchiveOutboundService
    {
        bool IsArchiveAdminUser(User? user);

        Task<List<YearlyArchiveOutboundRecord>> ListRecordsAsync(OutboundListCriteria criteria, User user);

        /// <summary>返回已有出库申请记录涉及的申请年度（降序）。</summary>
        Task<List<int>> GetExistingApplyYearsAsync();

        Task<YearlyArchiveOutboundRecord?> GetRecordAsync(int id);

        Task<YearlyArchiveOutboundRecord> CreateDraftRecordAsync(User applicant);

        Task<YearlyArchiveOutboundRecord> CreateDraftFromSearchPoolAsync(CreateOutboundFromPoolRequest request, User applicant);

        Task<string> GenerateNextOutboundNoAsync();

        Task<ArchiveOutboundFlowResult> SaveDraftFlowAsync(SaveOutboundDraftRequest request, User user);

        Task<ArchiveOutboundFlowResult> SubmitApplicationFlowAsync(int recordId, User user);

        Task<ArchiveOutboundSubmitPreviewResult> PreviewSubmitApplicationAsync(int recordId, User user);

        Task<ArchiveOutboundFlowResult> WithdrawApplicationFlowAsync(int recordId, string? reason, User user);

        Task<ArchiveOutboundFlowResult> ForceVoidByAdminFlowAsync(int recordId, string reason, User admin);

        Task<int> ProcessOverdueAutoForceVoidAsync(DateTime asOf);

        Task<ArchiveOutboundFlowResult> SaveApprovalFlowAsync(YearlyArchiveOutboundRecord record, User operatorUser);

        Task ApplyDefaultApprovalInfoAsync(YearlyArchiveOutboundRecord record, User operatorUser);

        Task<ArchiveOutboundFlowResult> UploadAttachmentFlowAsync(
            int recordId,
            string attachmentKind,
            SystemAttachment attachment,
            User user);

        Task<ArchiveOutboundAttachmentFlowResult> DeleteAttachmentFlowAsync(
            int recordId,
            SystemAttachment attachment,
            User user);

        Task<ArchiveOutboundAttachmentFlowResult> PrepareAttachmentViewFlowAsync(SystemAttachment attachment);

        Task<ArchiveOutboundApprovalValidationResult> ValidateApprovalPhaseAsync(YearlyArchiveOutboundRecord record);

        Task<ArchiveOutboundFlowResult> CompleteApprovalPhaseFlowAsync(YearlyArchiveOutboundRecord record, User user);

        Task<ArchiveOutboundPrintData> BuildPrintDataAsync(int recordId, bool blankApprovalSignatures);

        Task<ArchiveOutboundPrintData> BuildPrintDataFromRecordAsync(
            YearlyArchiveOutboundRecord record,
            bool blankApprovalSignatures);

        Task<ArchiveOutboundHandoverPrintData> BuildHandoverPrintDataAsync(int recordId, string? handoverRemark, bool blankHandoverSignatures);

        Task RecordPrintAsync(int recordId);

        Task<ArchiveOutboundFlowResult> AttachSearchResultSetAsync(int recordId, int resultSetId, User user);

        Task<ArchiveOutboundFlowResult> RemoveApplicationItemAsync(int recordId, int itemId, User user);

        Task<ArchiveOutboundFlowResult> RemoveApplicationItemsAsync(int recordId, IReadOnlyCollection<int> itemIds, User user);

        Task<ArchiveOutboundFlowResult> CompletePhysicalOutboundFlowAsync(int recordId, string handoverRemark, User admin);

        Task<IReadOnlyList<SystemAttachment>> GetAttachmentsAsync(int recordId);
    }
}
