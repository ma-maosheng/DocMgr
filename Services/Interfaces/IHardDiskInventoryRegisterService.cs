using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.HardDiskMedia;

namespace DocMgr.Services.Interfaces;

/// <summary>
/// 硬盘盘库登记业务服务（B 流：确认可上传）。
/// </summary>
public interface IHardDiskInventoryRegisterService
{
    Task<IReadOnlyList<HardDiskInventoryRegisterRecord>> SearchRecordsAsync(string? keyword, int? status, int? applyYear);

    Task<HardDiskInventoryRegisterRecord?> GetRecordByIdAsync(int recordId);

    Task<IReadOnlyList<HardDiskMedium>> GetSelectableMediaAsync(int? currentRecordId = null);

    Task<string> GenerateNextRegisterNoAsync();

    Task<IReadOnlyList<HardDiskMediaReturnTargetLocationOption>> GetDamagedTargetLocationOptionsAsync();

    Task<HardDiskInventoryRegisterRecord> CreateDraftAsync(
        HardDiskInventoryRegisterRecord draft,
        IReadOnlyList<HardDiskInventoryRegisterItemDraft> items,
        User currentUser);

    Task<HardDiskInventoryRegisterRecord> UpdateDraftAsync(
        HardDiskInventoryRegisterRecord draft,
        IReadOnlyList<HardDiskInventoryRegisterItemDraft> items,
        User currentUser);

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

    Task ConfirmReadyForUploadAsync(int recordId, User currentUser, bool damagedDiskRelocationConfirmed = false);

    Task CompleteAsync(int recordId, User currentUser);

    Task WithdrawAsync(int recordId, string? reason, User currentUser);

    /// <summary>逾期强制作废（仅草稿/已提交，须达到系统逾期时限）。</summary>
    Task ForceVoidAsync(int recordId, string? reason, User currentUser);

    Task RecordPrintAsync(int recordId);

    Task<HardDiskInventoryRegisterPrintData> BuildPrintDataAsync(int recordId);

    Task<IReadOnlyList<SystemAttachment>> GetAttachmentsAsync(string registerNo);

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
