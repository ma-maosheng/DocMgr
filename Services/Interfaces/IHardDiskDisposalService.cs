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

    /// <summary>
    /// 解析介质处置前存放位置（盘失清档口时回退盘库登记流转前位置）。
    /// </summary>
    Task<IReadOnlyDictionary<int, string>> ResolveBeforeStorageLocationsAsync(IReadOnlyList<HardDiskMedium> media);

    /// <summary>
    /// 按介质 ID 解析盘失前存放位置（已办结明细回填用）。
    /// </summary>
    Task<IReadOnlyDictionary<int, string>> GetInventoryLostBeforeLocationsAsync(IReadOnlyCollection<int> mediumIds);

    /// <summary>生成下一处置单号（打开新建单时预取，保存时复用）。</summary>
    Task<string> GenerateNextDisposalNoAsync();

    Task<HardDiskDisposalRecord> CreateDraftAsync(HardDiskDisposalRecord draft, IReadOnlyList<int> mediumIds, User currentUser);

    Task<HardDiskDisposalRecord> UpdateDraftAsync(HardDiskDisposalRecord draft, IReadOnlyList<int> mediumIds, User currentUser);

    Task SubmitAsync(int recordId, User currentUser);

    Task ApproveAsync(int recordId, string approvalOpinion, User currentUser);

    /// <summary>已审批后更新审核审批人姓名与签字日期。</summary>
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

    Task CompleteAsync(int recordId, User currentUser);

    Task WithdrawAsync(int recordId, string? reason, User currentUser);

    /// <summary>逾期强制作废（仅草稿/已提交，须达到系统逾期时限）。</summary>
    Task ForceVoidAsync(int recordId, string? reason, User currentUser);

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
