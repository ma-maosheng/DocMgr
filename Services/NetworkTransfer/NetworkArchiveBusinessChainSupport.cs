using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 跨域业务链创建、任务幂等同步与进度汇总。
/// </summary>
internal static class NetworkArchiveBusinessChainSupport
{
    public static NetworkArchiveBusinessChain CreateForInbound(NetworkInboundRecord record, DateTime now)
    {
        string chainNo = BuildChainNo(record.InboundNo);
        var chain = new NetworkArchiveBusinessChain
        {
            ChainNo = chainNo,
            ScenarioKind = NetworkTransferDomainValues.ResolveInboundScenarioKind(record.SourceKind),
            PrimaryBusinessType = NetworkTransferDomainValues.BusinessTypeInbound,
            PrimaryBusinessId = record.Id,
            CreatedAt = now,
            UpdatedAt = now
        };
        EnsureTask(
            chain,
            NetworkTransferDomainValues.TaskKindPrimaryApplication,
            NetworkTransferDomainValues.BusinessTypeInbound,
            record.Id > 0 ? record.Id : null,
            record.InboundNo,
            NetworkTransferDomainValues.BusinessTaskStatusPending,
            "入网主申请",
            now);
        SynchronizeInboundTasks(chain, record, now);
        RefreshSummary(chain, now);
        return chain;
    }

    public static NetworkArchiveBusinessChain CreateForOutbound(NetworkOutboundRecord record, DateTime now)
    {
        string chainNo = BuildChainNo(record.OutboundNo);
        var chain = new NetworkArchiveBusinessChain
        {
            ChainNo = chainNo,
            ScenarioKind = NetworkTransferDomainValues.ResolveOutboundScenarioKind(record.DestinationKind),
            PrimaryBusinessType = NetworkTransferDomainValues.BusinessTypeOutbound,
            PrimaryBusinessId = record.Id,
            CreatedAt = now,
            UpdatedAt = now
        };
        EnsureTask(
            chain,
            NetworkTransferDomainValues.TaskKindPrimaryApplication,
            NetworkTransferDomainValues.BusinessTypeOutbound,
            record.Id > 0 ? record.Id : null,
            record.OutboundNo,
            NetworkTransferDomainValues.BusinessTaskStatusPending,
            "出网主申请",
            now);
        SynchronizeOutboundTasks(chain, record, now);
        RefreshSummary(chain, now);
        return chain;
    }

    public static void SynchronizeInboundTasks(
        NetworkArchiveBusinessChain chain,
        NetworkInboundRecord record,
        DateTime now)
    {
        chain.ScenarioKind = NetworkTransferDomainValues.ResolveInboundScenarioKind(record.SourceKind);
        SetPrimaryBusiness(chain, record.Id, record.InboundNo, now);

        SetOptionalTask(
            chain,
            NetworkTransferDomainValues.TaskKindArchiveCopy,
            NetworkTransferDomainValues.BusinessTypeArchiveMaterialTransaction,
            NetworkTransferDomainValues.IsArchivedElectronicSearchSource(record.SourceKind),
            "立档资料复制入网（原件保持在库）",
            now);
        SetOptionalTask(
            chain,
            NetworkTransferDomainValues.TaskKindOnNetRegistration,
            "NetworkOnNetAsset",
            true,
            "入网办结后登记在网台账",
            now);
        SetOptionalTask(
            chain,
            NetworkTransferDomainValues.TaskKindHardDiskReturn,
            NetworkTransferDomainValues.BusinessTypeHardDiskReturn,
            record.ReturnBorrowedHardDiskWithInbound,
            "借出硬盘随入网资料归还",
            now);
        RefreshSummary(chain, now);
    }

    public static void SynchronizeOutboundTasks(
        NetworkArchiveBusinessChain chain,
        NetworkOutboundRecord record,
        DateTime now)
    {
        chain.ScenarioKind = NetworkTransferDomainValues.ResolveOutboundScenarioKind(record.DestinationKind);
        SetPrimaryBusiness(chain, record.Id, record.OutboundNo, now);
        SetOptionalTask(
            chain,
            NetworkTransferDomainValues.TaskKindArchiveRegister,
            NetworkTransferDomainValues.BusinessTypeArchiveRegister,
            NetworkTransferDomainValues.IsArchiveFilingDestination(record.DestinationKind),
            "出网办结后生成完整建档草稿",
            now);
        RefreshSummary(chain, now);
    }

    public static void MarkPrimaryInProgress(NetworkArchiveBusinessChain? chain, DateTime now) =>
        MarkTask(chain, NetworkTransferDomainValues.TaskKindPrimaryApplication,
            NetworkTransferDomainValues.BusinessTaskStatusInProgress, "申请已提交，进入审批办理", now);

    public static void MarkInboundCompleted(NetworkArchiveBusinessChain? chain, DateTime now)
    {
        MarkTask(chain, NetworkTransferDomainValues.TaskKindArchiveCopy,
            NetworkTransferDomainValues.BusinessTaskStatusCompleted, "已记录档案复制入网履历", now);
        MarkTask(chain, NetworkTransferDomainValues.TaskKindOnNetRegistration,
            NetworkTransferDomainValues.BusinessTaskStatusCompleted, "在网台账已登记", now);
        MarkTask(chain, NetworkTransferDomainValues.TaskKindHardDiskReturn,
            NetworkTransferDomainValues.BusinessTaskStatusCompleted, "关联硬盘归还已办结", now);
        MarkTask(chain, NetworkTransferDomainValues.TaskKindPrimaryApplication,
            NetworkTransferDomainValues.BusinessTaskStatusCompleted, "入网申请已办结", now);
    }

    public static void MarkOutboundCompleted(
        NetworkArchiveBusinessChain? chain,
        YearlyArchiveRegisterRecord? register,
        DateTime now)
    {
        if (chain == null)
        {
            return;
        }

        NetworkArchiveBusinessTask? task =
            chain.Tasks.FirstOrDefault(item =>
                string.Equals(item.TaskKind, NetworkTransferDomainValues.TaskKindArchiveRegister, StringComparison.Ordinal));
        if (task != null && register != null)
        {
            task.BusinessId = register.Id;
            task.BusinessNo = register.FormNo;
            task.Status = NetworkTransferDomainValues.BusinessTaskStatusCompleted;
            task.ResultMessage = "完整建档草稿已生成，待确认立档专属信息";
            task.UpdatedAt = now;
        }

        MarkTask(chain, NetworkTransferDomainValues.TaskKindPrimaryApplication,
            NetworkTransferDomainValues.BusinessTaskStatusCompleted, "出网申请已办结", now);
    }

    public static void MarkCancelled(NetworkArchiveBusinessChain? chain, DateTime now)
    {
        if (chain == null)
        {
            return;
        }

        foreach (NetworkArchiveBusinessTask task in chain.Tasks.Where(item =>
                     !string.Equals(item.Status, NetworkTransferDomainValues.BusinessTaskStatusCompleted, StringComparison.Ordinal)))
        {
            task.Status = NetworkTransferDomainValues.BusinessTaskStatusCancelled;
            task.ResultMessage = "主申请已撤回";
            task.UpdatedAt = now;
        }
        RefreshSummary(chain, now);
    }

    private static void SetPrimaryBusiness(
        NetworkArchiveBusinessChain chain,
        int businessId,
        string businessNo,
        DateTime now)
    {
        chain.PrimaryBusinessId = businessId;
        NetworkArchiveBusinessTask? task = chain.Tasks.FirstOrDefault(item =>
            string.Equals(item.TaskKind, NetworkTransferDomainValues.TaskKindPrimaryApplication, StringComparison.Ordinal));
        if (task == null)
        {
            return;
        }

        task.BusinessId = businessId > 0 ? businessId : null;
        task.BusinessNo = businessNo?.Trim() ?? string.Empty;
        task.UpdatedAt = now;
    }

    private static void SetOptionalTask(
        NetworkArchiveBusinessChain chain,
        string taskKind,
        string businessType,
        bool required,
        string resultMessage,
        DateTime now)
    {
        NetworkArchiveBusinessTask? existing = chain.Tasks.FirstOrDefault(item =>
            string.Equals(item.TaskKind, taskKind, StringComparison.Ordinal));
        if (!required)
        {
            if (existing != null && existing.Id == 0)
            {
                chain.Tasks.Remove(existing);
            }
            else if (existing != null
                     && !string.Equals(existing.Status, NetworkTransferDomainValues.BusinessTaskStatusCompleted, StringComparison.Ordinal))
            {
                existing.Status = NetworkTransferDomainValues.BusinessTaskStatusCancelled;
                existing.ResultMessage = "当前业务场景不需要该任务";
                existing.UpdatedAt = now;
            }
            return;
        }

        EnsureTask(
            chain,
            taskKind,
            businessType,
            null,
            string.Empty,
            NetworkTransferDomainValues.BusinessTaskStatusPending,
            resultMessage,
            now);
    }

    private static void EnsureTask(
        NetworkArchiveBusinessChain chain,
        string taskKind,
        string businessType,
        int? businessId,
        string businessNo,
        string status,
        string resultMessage,
        DateTime now)
    {
        NetworkArchiveBusinessTask? existing = chain.Tasks.FirstOrDefault(item =>
            string.Equals(item.TaskKind, taskKind, StringComparison.Ordinal));
        if (existing != null)
        {
            if (string.Equals(existing.Status, NetworkTransferDomainValues.BusinessTaskStatusCancelled, StringComparison.Ordinal))
            {
                existing.Status = status;
            }
            existing.BusinessId = businessId ?? existing.BusinessId;
            existing.BusinessNo = string.IsNullOrWhiteSpace(businessNo) ? existing.BusinessNo : businessNo.Trim();
            existing.ResultMessage = resultMessage;
            existing.UpdatedAt = now;
            return;
        }

        chain.Tasks.Add(new NetworkArchiveBusinessTask
        {
            TaskKind = taskKind,
            BusinessType = businessType,
            BusinessId = businessId,
            BusinessNo = businessNo?.Trim() ?? string.Empty,
            Status = status,
            DedupKey = $"{chain.ChainNo}:{taskKind}",
            ResultMessage = resultMessage,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private static void MarkTask(
        NetworkArchiveBusinessChain? chain,
        string taskKind,
        string status,
        string resultMessage,
        DateTime now)
    {
        if (chain == null)
        {
            return;
        }

        NetworkArchiveBusinessTask? task = chain.Tasks.FirstOrDefault(item =>
            string.Equals(item.TaskKind, taskKind, StringComparison.Ordinal));
        if (task == null || string.Equals(task.Status, NetworkTransferDomainValues.BusinessTaskStatusCancelled, StringComparison.Ordinal))
        {
            return;
        }

        task.Status = status;
        task.ResultMessage = resultMessage;
        task.UpdatedAt = now;
        RefreshSummary(chain, now);
    }

    private static void RefreshSummary(NetworkArchiveBusinessChain chain, DateTime now)
    {
        NetworkArchiveBusinessTask[] active = chain.Tasks
            .Where(item => !string.Equals(
                item.Status,
                NetworkTransferDomainValues.BusinessTaskStatusCancelled,
                StringComparison.Ordinal))
            .ToArray();
        int completed = active.Count(item => string.Equals(
            item.Status,
            NetworkTransferDomainValues.BusinessTaskStatusCompleted,
            StringComparison.Ordinal));
        chain.StatusSummary = active.Length == 0
            ? "无待执行任务"
            : $"已完成 {completed}/{active.Length} 项";
        chain.UpdatedAt = now;
    }

    private static string BuildChainNo(string businessNo) =>
        $"CHAIN-{businessNo?.Trim()}";
}
