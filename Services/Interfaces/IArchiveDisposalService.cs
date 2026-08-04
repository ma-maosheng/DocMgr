using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces;

/// <summary>
/// 年度资料离库处置业务服务。
/// </summary>
public interface IArchiveDisposalService
{
    /// <summary>按条件检索处置单。</summary>
    Task<IReadOnlyList<YearlyArchiveDisposalRecord>> SearchRecordsAsync(
        string? keyword,
        int? status,
        int? applyYear,
        string mediaKind);

    /// <summary>按主键加载处置单。</summary>
    Task<YearlyArchiveDisposalRecord?> GetRecordByIdAsync(int recordId);

    /// <summary>加载可纳入处置的候选明细。</summary>
    Task<IReadOnlyList<ArchiveDisposalSelectableItem>> GetSelectableItemsAsync(
        string mediaKind,
        int? currentRecordId = null);

    /// <summary>生成下一处置单编号。</summary>
    Task<string> GenerateNextDisposalNoAsync();

    /// <summary>新建草稿。</summary>
    Task<YearlyArchiveDisposalRecord> CreateDraftAsync(
        YearlyArchiveDisposalRecord draft,
        IReadOnlyList<YearlyArchiveDisposalItem> items,
        User currentUser);

    /// <summary>更新草稿。</summary>
    Task<YearlyArchiveDisposalRecord> UpdateDraftAsync(
        YearlyArchiveDisposalRecord draft,
        IReadOnlyList<YearlyArchiveDisposalItem> items,
        User currentUser);

    /// <summary>提交审批。</summary>
    Task SubmitAsync(int recordId, User currentUser);

    /// <summary>审批通过。</summary>
    Task ApproveAsync(int recordId, string approvalOpinion, User currentUser);

    /// <summary>确认可上传签批单。</summary>
    Task ConfirmReadyForUploadAsync(int recordId, User currentUser);

    /// <summary>
    /// 办结清账。
    /// </summary>
    /// <param name="physicalRemovalConfirmed">本单将释档空盒/空袋时须为 true。</param>
    /// <param name="formatRetainedConfirmed">含硬盘低格留存时须为 true。</param>
    Task CompleteAsync(
        int recordId,
        User currentUser,
        bool physicalRemovalConfirmed,
        bool formatRetainedConfirmed);

    /// <summary>撤回作废。</summary>
    Task WithdrawAsync(int recordId, string? reason, User currentUser);

    /// <summary>记录打印次数。</summary>
    Task RecordPrintAsync(int recordId);

    /// <summary>构建签批单打印数据。</summary>
    Task<YearlyArchiveDisposalPrintData> BuildPrintDataAsync(int recordId);

    /// <summary>判断办结是否需要物理移除确认。</summary>
    Task<bool> RequiresPhysicalRemovalConfirmationAsync(int recordId);

    /// <summary>判断办结是否需要低格确认。</summary>
    Task<bool> RequiresFormatRetainConfirmationAsync(int recordId);

    Task<IReadOnlyList<SystemAttachment>> GetAttachmentsAsync(string disposalNo);

    Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId);

    Task<(bool Ok, string Message, SystemAttachment? Attachment)> UploadAttachmentAsync(
        int recordId,
        string fileCategory,
        string fileName,
        string extension,
        long fileSize,
        byte[] fileContent,
        User currentUser);

    Task<(bool Ok, string Message)> DeleteAttachmentAsync(int attachmentId, User currentUser);
}
