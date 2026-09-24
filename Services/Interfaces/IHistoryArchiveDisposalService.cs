using DocMgr.Models.HistoryArchive;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.Interfaces;

/// <summary>
/// 历史存档离库处置业务服务。
/// </summary>
public interface IHistoryArchiveDisposalService
{
    Task<IReadOnlyList<HistoryArchiveDisposalRecord>> SearchRecordsAsync(string? keyword, int? status, int? applyYear);

    Task<HistoryArchiveDisposalRecord?> GetRecordByIdAsync(int recordId);

    Task<IReadOnlyList<HistoryArchiveDisposalBoxCandidate>> GetSelectableBoxesAsync(
        string materialKind,
        int? currentRecordId = null);

    Task<string> GenerateNextDisposalNoAsync();

    Task<HistoryArchiveDisposalRecord> CreateDraftAsync(
        HistoryArchiveDisposalRecord draft,
        IReadOnlyList<HistoryArchiveDisposalItem> items,
        User currentUser);

    Task<HistoryArchiveDisposalRecord> UpdateDraftAsync(
        HistoryArchiveDisposalRecord draft,
        IReadOnlyList<HistoryArchiveDisposalItem> items,
        User currentUser);

    Task SubmitAsync(int recordId, User currentUser);

    Task ApproveAsync(int recordId, User currentUser);

    Task UpdateReviewSignersAsync(
        int recordId,
        string? deptHead,
        DateTime? deptHeadDate,
        string? archiveRoomHead,
        DateTime? archiveRoomHeadDate,
        string? productionHead,
        DateTime? productionHeadDate,
        string? archiveDeputyPresident,
        DateTime? archiveDeputyPresidentDate,
        string? productionVicePresident,
        DateTime? productionVicePresidentDate,
        User currentUser);

    Task ConfirmReadyForUploadAsync(int recordId, User currentUser);

    Task CompleteAsync(int recordId, User currentUser, bool physicalRemovalConfirmed);

    Task WithdrawAsync(int recordId, string? reason, User currentUser);

    /// <summary>逾期强制作废（仅草稿/已提交，须达到系统逾期时限）。</summary>
    Task ForceVoidAsync(int recordId, string? reason, User currentUser);

    Task RecordPrintAsync(int recordId);

    Task<HistoryArchiveDisposalPrintData> BuildPrintDataAsync(int recordId);

    Task<IReadOnlyList<SystemAttachment>> GetAttachmentsAsync(string disposalNo);

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
