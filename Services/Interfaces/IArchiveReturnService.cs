using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 资料归还服务契约：对已办结出库的提档(借出原件)项进行收回入库。轻量流程：登记 → 办结。
    /// </summary>
    public interface IArchiveReturnService
    {
        bool IsArchiveAdminUser(User? user);

        /// <summary>列出可发起归还的出库单（已办结出库、存在未归还提档项、且无有效归还单）。</summary>
        Task<List<YearlyArchiveOutboundRecord>> GetReturnableOutboundsAsync(int year);

        /// <summary>列出归还单（管理员看全部，否则看本人登记的）。</summary>
        Task<List<YearlyArchiveReturnRecord>> ListReturnsAsync(int year, User user);

        Task<YearlyArchiveReturnRecord?> GetReturnAsync(int id);

        Task<string> GenerateNextReturnNoAsync();

        /// <summary>由出库单生成归还单草稿（含待归还提档明细，未落库）。</summary>
        Task<YearlyArchiveReturnRecord> CreateDraftFromOutboundAsync(int outboundRecordId, User registrar);

        /// <summary>保存草稿或提交登记。</summary>
        Task<ArchiveReturnFlowResult> SaveReturnFlowAsync(SaveReturnRequest request, User user);

        /// <summary>办结归还：在单一事务内反向冲销出库提档对资料台账的影响。</summary>
        Task<ArchiveReturnFlowResult> CompleteReturnFlowAsync(int recordId, User admin);

        /// <summary>作废归还单。</summary>
        Task<ArchiveReturnFlowResult> VoidReturnFlowAsync(int recordId, string? reason, User user);

        /// <summary>装配归还回执打印数据。</summary>
        Task<ArchiveReturnReceiptPrintData> BuildReceiptPrintDataAsync(int recordId, bool blankHandoverSignatures);

        /// <summary>记录归还回执打印次数。</summary>
        Task RecordPrintAsync(int recordId);

        /// <summary>列出归还单附件。</summary>
        Task<IReadOnlyList<SystemAttachment>> GetAttachmentsAsync(int recordId);

        /// <summary>上传非正常归还报备签字件。</summary>
        Task<ArchiveReturnAttachmentFlowResult> UploadAbnormalReportAttachmentFlowAsync(int recordId, SystemAttachment attachment, User user);

        /// <summary>删除非正常归还报备签字件。</summary>
        Task<ArchiveReturnAttachmentFlowResult> DeleteAbnormalReportAttachmentFlowAsync(int recordId, SystemAttachment attachment, User user);

        /// <summary>准备附件查看（加载完整内容）。</summary>
        Task<ArchiveReturnAttachmentFlowResult> PrepareAttachmentViewFlowAsync(SystemAttachment attachment);

        /// <summary>装配非正常归还报备单打印数据。</summary>
        Task<ArchiveReturnAbnormalReportPrintData> BuildAbnormalReportPrintDataAsync(int recordId, bool blankApprovalSignatures);

        /// <summary>列出可用于异常归还的在用目标盒。</summary>
        Task<IReadOnlyList<ArchiveReturnRehomeTargetOption>> GetRehomeTargetOptionsAsync(int filingFactId);

        /// <summary>草稿态：为失效盒明细指定已有目标盒。</summary>
        Task<ArchiveReturnFlowResult> AssignRehomeTargetBoxAsync(
            int returnRecordId,
            int returnItemId,
            int targetBoxId,
            User user);

        /// <summary>草稿态：新建空盒并指定为归还目标。</summary>
        Task<ArchiveReturnFlowResult> CreateEmptyRehomeBoxAndAssignAsync(
            int returnRecordId,
            int returnItemId,
            ArchiveReturnCreateEmptyBoxRequest request,
            User user);
    }
}
