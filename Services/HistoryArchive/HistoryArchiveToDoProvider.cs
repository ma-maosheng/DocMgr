using DocMgr.Models.HistoryArchive;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.HistoryArchive;

/// <summary>
/// 历史存档离库处置待办提供器。
/// </summary>
public sealed class HistoryArchiveToDoProvider : IToDoProvider
{
    private readonly IHistoryArchiveDisposalRepository _repository;

    public HistoryArchiveToDoProvider(IHistoryArchiveDisposalRepository repository)
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

        var pending = await _repository.GetPendingRecordsForToDoAsync(200);
        result.AddRange(pending.Select(record => new ToDoItem
        {
            Id = $"HA-DSP-{record.Id}-PENDING",
            Title = $"【资料离库处置】{ResolveTitle(record)}：{BuildSummary(record)}",
            BizType = "HistoryArchiveDisposal",
            BizId = record.Id,
            BizNo = record.DisposalNo,
            Stage = HistoryArchiveDisposalDomainValues.ToStatusDisplay(record.Status),
            CreatedTime = record.SubmittedAt ?? record.ApplyTime,
            Priority = "高"
        }));
        return result;
    }

    private static string ResolveTitle(HistoryArchiveDisposalRecord record) =>
        record.Status switch
        {
            HistoryArchiveDisposalRecord.StatusSubmitted => "待审批",
            HistoryArchiveDisposalRecord.StatusApproved => "待确认可上传",
            HistoryArchiveDisposalRecord.StatusSignedUploaded when !record.SignedAttachmentUploaded => "待上传签批单",
            HistoryArchiveDisposalRecord.StatusSignedUploaded => "待办结",
            _ => HistoryArchiveDisposalDomainValues.ToStatusDisplay(record.Status)
        };

    private static string BuildSummary(HistoryArchiveDisposalRecord record)
    {
        string kind = HistoryArchiveDisposalDomainValues.ToMaterialKindDisplay(record.MaterialKind);
        int boxCount = record.Items?.Count ?? record.ItemCount;
        return $"{record.DisposalNo} / {kind} / {record.DispositionMethod} / {boxCount}盒";
    }
}
