using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.Interfaces;

/// <summary>
/// 硬盘离库处置业务服务。
/// </summary>
public interface IHardDiskDisposalService
{
    Task<IReadOnlyList<HardDiskDisposalRecord>> SearchRecordsAsync(string? keyword, int? status, int? applyYear);

    Task<HardDiskDisposalRecord?> GetRecordByIdAsync(int recordId);

    Task<IReadOnlyList<HardDiskMedium>> GetSelectableMediaAsync(int? currentRecordId = null);

    /// <summary>生成下一处置单号（打开新建单时预取，保存时复用）。</summary>
    Task<string> GenerateNextDisposalNoAsync();

    Task<HardDiskDisposalRecord> CreateDraftAsync(HardDiskDisposalRecord draft, IReadOnlyList<int> mediumIds, User currentUser);

    Task<HardDiskDisposalRecord> UpdateDraftAsync(HardDiskDisposalRecord draft, IReadOnlyList<int> mediumIds, User currentUser);

    Task SubmitAsync(int recordId, User currentUser);

    Task ApproveAsync(int recordId, string approvalOpinion, User currentUser);

    Task ConfirmReadyForUploadAsync(int recordId, User currentUser);

    Task CompleteAsync(int recordId, User currentUser);

    Task WithdrawAsync(int recordId, string? reason, User currentUser);

    Task RecordPrintAsync(int recordId);

    Task<HardDiskDisposalPrintData> BuildPrintDataAsync(int recordId);

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
