using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出入网管理待办提供器。
/// </summary>
public sealed class NetworkTransferToDoProvider : IToDoProvider
{
    private readonly INetworkTransferRepository _repository;

    public NetworkTransferToDoProvider(INetworkTransferRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ToDoItem>> GetToDosAsync(User currentUser)
    {
        var result = new List<ToDoItem>();
        if (!ArchiveRegisterBusinessRules.IsArchiveAdminUser(currentUser))
        {
            return result;
        }

        var inbounds = await _repository.SearchInboundRecordsAsync(null, null, null);
        foreach (var record in inbounds.Where(IsPendingApprovalOrHandover))
        {
            bool isSubmit = record.Status == NetworkInboundRecord.StatusSubmitted;
            result.Add(new ToDoItem
            {
                Id = $"NET-IN-{record.Id}-{(isSubmit ? "APPROVAL" : "HANDOVER")}",
                Title = $"【入网{(isSubmit ? "审批" : "交接")}】{record.InboundNo}",
                BizType = isSubmit ? "NetworkInboundApproval" : "NetworkInboundHandover",
                BizId = record.Id,
                BizNo = record.InboundNo,
                Stage = ApplicationWorkflowStatus.ToDisplay(record.Status),
                CreatedTime = record.SubmittedAt ?? record.ApplyTime,
                Priority = "高"
            });
        }

        var outbounds = await _repository.SearchOutboundRecordsAsync(null, null, null);
        foreach (var record in outbounds.Where(IsPendingApprovalOrHandoverOutbound))
        {
            bool isSubmit = record.Status == NetworkOutboundRecord.StatusSubmitted;
            result.Add(new ToDoItem
            {
                Id = $"NET-OUT-{record.Id}-{(isSubmit ? "APPROVAL" : "HANDOVER")}",
                Title = $"【出网{(isSubmit ? "审批" : "交接")}】{record.OutboundNo}",
                BizType = isSubmit ? "NetworkOutboundApproval" : "NetworkOutboundHandover",
                BizId = record.Id,
                BizNo = record.OutboundNo,
                Stage = ApplicationWorkflowStatus.ToDisplay(record.Status),
                CreatedTime = record.SubmittedAt ?? record.ApplyTime,
                Priority = "高"
            });
        }

        return result;
    }

    private static bool IsPendingApprovalOrHandover(NetworkInboundRecord record) =>
        record.Status is NetworkInboundRecord.StatusSubmitted
            or NetworkInboundRecord.StatusApproved
            or NetworkInboundRecord.StatusSignedUploaded;

    private static bool IsPendingApprovalOrHandoverOutbound(NetworkOutboundRecord record) =>
        record.Status is NetworkOutboundRecord.StatusSubmitted
            or NetworkOutboundRecord.StatusApproved
            or NetworkOutboundRecord.StatusSignedUploaded;
}
