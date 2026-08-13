using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces;

/// <summary>
/// 年度资料出入网管理业务服务。
/// </summary>
public interface INetworkTransferService
{
    Task<string> GenerateNextInboundNoAsync();

    Task<string> GenerateNextOutboundNoAsync();

    Task<string> GenerateNextDisposalNoAsync();

    Task<IReadOnlyList<NetworkInboundRecord>> SearchInboundRecordsAsync(string? keyword, int? status, int? applyYear);

    Task<NetworkInboundRecord?> GetInboundByIdAsync(int recordId);

    Task<NetworkInboundRecord> CreateInboundDraftAsync(
        NetworkInboundRecord draft,
        IReadOnlyList<NetworkInboundItem> items,
        User currentUser);

    Task<NetworkInboundRecord> UpdateInboundDraftAsync(
        NetworkInboundRecord draft,
        IReadOnlyList<NetworkInboundItem> items,
        User currentUser);

    Task<IReadOnlyList<NetworkInboundItem>> BuildInboundItemsFromElectronicSearchAsync(
        int resultSetId,
        IReadOnlyCollection<int>? selectedItemIds);

    Task<IReadOnlyDictionary<int, YearlyArchiveFilingFact>> GetFilingFactsByIdsAsync(IReadOnlyCollection<int> filingFactIds);

    Task SubmitInboundAsync(int recordId, User currentUser);

    Task ApproveInboundAsync(NetworkInboundRecord approval, User currentUser);

    /// <summary>审批环节维护借出硬盘空白归位档口。</summary>
    Task UpdateInboundReturnHardDiskSlotsAsync(
        int recordId,
        IReadOnlyList<NetworkInboundReturnHardDiskItem> slotInputs,
        User currentUser);

    Task ConfirmInboundHandoverAsync(NetworkInboundRecord handover, User currentUser);

    /// <summary>审批/交接阶段补录各明细目标服务器路径。</summary>
    Task UpdateInboundItemPathsAsync(
        int recordId,
        IReadOnlyList<NetworkInboundItem> items,
        User currentUser,
        string? targetServerPath = null,
        IReadOnlyList<YearlyArchiveRegisterMedia>? externalMediaEntries = null);

    Task CompleteInboundAsync(int recordId, User currentUser);

    Task<NetworkInboundPrintData> BuildInboundPrintDataAsync(int recordId, bool blankApprovalSignatures);

    Task RecordInboundPrintAsync(int recordId);

    Task WithdrawInboundAsync(int recordId, string? reason, User currentUser);

    Task<IReadOnlyList<NetworkOutboundRecord>> SearchOutboundRecordsAsync(string? keyword, int? status, int? applyYear);

    Task<NetworkOutboundRecord?> GetOutboundByIdAsync(int recordId);

    Task<IReadOnlyList<NetworkOnNetAsset>> GetSelectableOutboundAssetsAsync(int? currentOutboundRecordId = null);

    Task<NetworkOutboundRecord> CreateOutboundDraftAsync(
        NetworkOutboundRecord draft,
        IReadOnlyList<NetworkOutboundItem> items,
        User currentUser);

    Task<NetworkOutboundRecord> UpdateOutboundDraftAsync(
        NetworkOutboundRecord draft,
        IReadOnlyList<NetworkOutboundItem> items,
        User currentUser);

    Task SubmitOutboundAsync(int recordId, User currentUser);

    Task ApproveOutboundAsync(NetworkOutboundRecord approval, User currentUser);

    Task ConfirmOutboundHandoverAsync(NetworkOutboundRecord handover, User currentUser);

    Task CompleteOutboundAsync(int recordId, User currentUser);

    Task<NetworkOutboundPrintData> BuildOutboundPrintDataAsync(int recordId, bool blankApprovalSignatures);

    Task RecordOutboundPrintAsync(int recordId);

    Task WithdrawOutboundAsync(int recordId, string? reason, User currentUser);

    Task<IReadOnlyList<NetworkOnNetAsset>> SearchOnNetAssetsAsync(
        string? keyword,
        string? originKind,
        string? lifecycleStatus);

    Task<NetworkOnNetAsset> RegisterProcessedOutputAsync(NetworkOnNetAsset draft, User currentUser);

    Task<IReadOnlyList<NetworkOnNetDisposalRecord>> SearchDisposalRecordsAsync(
        string? keyword,
        int? status,
        int? applyYear);

    Task<NetworkOnNetDisposalRecord?> GetDisposalByIdAsync(int recordId);

    Task<IReadOnlyList<NetworkOnNetAsset>> GetSelectableDisposalAssetsAsync(int? currentDisposalRecordId = null);

    Task<NetworkOnNetDisposalRecord> CreateDisposalDraftAsync(
        NetworkOnNetDisposalRecord draft,
        IReadOnlyList<NetworkOnNetDisposalItem> items,
        User currentUser);

    Task<NetworkOnNetDisposalRecord> UpdateDisposalDraftAsync(
        NetworkOnNetDisposalRecord draft,
        IReadOnlyList<NetworkOnNetDisposalItem> items,
        User currentUser);

    Task SubmitDisposalAsync(int recordId, User currentUser);

    Task ApproveDisposalAsync(int recordId, string approvalOpinion, User currentUser);

    Task ConfirmDisposalReadyForUploadAsync(int recordId, User currentUser);

    Task CompleteDisposalAsync(int recordId, User currentUser);

    Task WithdrawDisposalAsync(int recordId, string? reason, User currentUser);

    Task<IReadOnlyList<SystemAttachment>> GetAttachmentsAsync(string businessType, string businessNo);

    Task<(bool Ok, string Message, SystemAttachment? Attachment)> UploadAttachmentAsync(
        string businessType,
        int recordId,
        string businessNo,
        string fileCategory,
        string fileName,
        string extension,
        long fileSize,
        byte[] fileContent,
        User currentUser);

    Task<(bool Ok, string Message)> DeleteAttachmentAsync(int attachmentId, User currentUser);
}
