using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.SystemSettings;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出网申请提交、交接与办结校验。
/// </summary>
public static class NetworkOutboundApplicationValidationSupport
{
    public static IReadOnlyList<string> ValidateForSubmit(
        NetworkOutboundRecord header,
        IReadOnlyList<YearlyArchiveRegisterMedia>? mediaEntries)
    {
        var errors = new List<string>();
        ValidateHeader(header, errors);
        NetworkOutboundExternalMediaValidationSupport.ValidateForSubmit(header, mediaEntries, errors);
        return errors;
    }

    public static void EnsureValidForSubmit(
        NetworkOutboundRecord header,
        IReadOnlyList<YearlyArchiveRegisterMedia>? mediaEntries)
    {
        IReadOnlyList<string> errors = ValidateForSubmit(header, mediaEntries);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }
    }

    public static IReadOnlyList<string> ValidateForHandoverConfirm(
        NetworkOutboundRecord record,
        NetworkOutboundRecord handoverInput,
        IReadOnlyList<SystemAttachment> attachments,
        ApprovalChainResolution? approvalChain = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(handoverInput);

        var errors = new List<string>();
        CollectApprovalSignerErrors(record, handoverInput, errors, approvalChain);
        NetworkOutboundExternalMediaValidationSupport.ValidateForHandoverConfirm(
            record.MediaEntries?.ToList(),
            errors);
        return errors;
    }

    public static void EnsureValidForHandoverConfirm(
        NetworkOutboundRecord record,
        NetworkOutboundRecord handoverInput,
        IReadOnlyList<SystemAttachment> attachments,
        ApprovalChainResolution? approvalChain = null)
    {
        IReadOnlyList<string> errors = ValidateForHandoverConfirm(record, handoverInput, attachments, approvalChain);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "交接确认前校验未通过：" + Environment.NewLine + Environment.NewLine
                + string.Join(Environment.NewLine, errors));
        }
    }

    public static IReadOnlyList<string> ValidateForComplete(
        NetworkOutboundRecord record,
        IReadOnlyList<SystemAttachment> attachments,
        ApprovalChainResolution? approvalChain = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        var errors = new List<string>();
        CollectCompleteSignerErrors(record, errors, approvalChain);
        CollectCompletePathErrors(record, errors);
        CollectMandatoryAttachmentErrors(record, attachments ?? Array.Empty<SystemAttachment>(), errors);
        NetworkOutboundExternalMediaValidationSupport.ValidateForComplete(
            record.DestinationKind,
            record.MediaEntries?.ToList(),
            errors);
        return errors;
    }

    public static void EnsureValidForComplete(
        NetworkOutboundRecord record,
        IReadOnlyList<SystemAttachment> attachments,
        ApprovalChainResolution? approvalChain = null)
    {
        IReadOnlyList<string> errors = ValidateForComplete(record, attachments, approvalChain);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "办结前信息完整性校验未通过：" + Environment.NewLine + Environment.NewLine
                + string.Join(Environment.NewLine, errors));
        }
    }

    private static void CollectCompletePathErrors(NetworkOutboundRecord record, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(record.ServerPath))
        {
            errors.Add("• 生产网来源服务器路径缺失");
        }

        if (string.IsNullOrWhiteSpace(record.MaterialPath))
        {
            errors.Add("• 出网资料所在具体路径缺失");
        }
    }

    private static void CollectCompleteSignerErrors(
        NetworkOutboundRecord record,
        List<string> errors,
        ApprovalChainResolution? approvalChain)
    {
        if (approvalChain != null)
        {
            errors.AddRange(ApprovalChainApplySupport.CollectMissingSignerErrors(
                approvalChain,
                nodeKey => ApprovalChainApplySupport.ReadNetworkOutboundSigner(record, nodeKey)));
            CollectEnabledSignerDateErrors(record, approvalChain, errors);
        }
        else
        {
            CollectSignerAndDateErrors(record.DeptHead, record.DeptHeadDate, ApprovalWorkflowDomainValues.DisplayDeptHead, errors);
            CollectSignerAndDateErrors(record.ProductionHead, record.ProductionHeadDate, ApprovalWorkflowDomainValues.DisplayProductionHead, errors);
            CollectSignerAndDateErrors(record.ArchiveRoomHead, record.ArchiveRoomHeadDate, ApprovalWorkflowDomainValues.DisplayArchiveRoomHead, errors);
            CollectFallbackVicePresidentSignerAndDateErrors(record, errors);
        }

        CollectSignerAndDateErrors(record.Deliverer, record.DeliverDate, "移交人", errors);
        CollectSignerAndDateErrors(record.Administrator, record.AdminDate, "资料员", errors);
    }

    private static void CollectEnabledSignerDateErrors(
        NetworkOutboundRecord record,
        ApprovalChainResolution chain,
        List<string> errors)
    {
        if (chain.DeptHead.IsEnabled)
        {
            CollectDateOnlyError(record.DeptHeadDate, ApprovalWorkflowDomainValues.DisplayDeptHead, errors);
        }

        if (chain.ProductionHead.IsEnabled)
        {
            CollectDateOnlyError(record.ProductionHeadDate, ApprovalWorkflowDomainValues.DisplayProductionHead, errors);
        }

        if (chain.ArchiveRoomHead.IsEnabled)
        {
            CollectDateOnlyError(record.ArchiveRoomHeadDate, ApprovalWorkflowDomainValues.DisplayArchiveRoomHead, errors);
        }

        if (chain.ArchiveDeputyPresident.IsEnabled)
        {
            CollectDateOnlyError(record.ArchiveDeputyPresidentDate, ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident, errors);
        }

        if (chain.ProductionVicePresident.IsEnabled)
        {
            CollectDateOnlyError(record.ProductionVicePresidentDate, ApprovalWorkflowDomainValues.DisplayProductionVicePresident, errors);
        }
    }

    private static void CollectSignerAndDateErrors(
        string? signer,
        DateTime? signedDate,
        string roleLabel,
        List<string> errors)
    {
        string signerLabel = NormalizeSignerLabel(roleLabel);
        if (string.IsNullOrWhiteSpace(signer))
        {
            errors.Add($"• {signerLabel}缺失");
            return;
        }

        CollectDateOnlyError(signedDate, roleLabel, errors);
    }

    private static void CollectDateOnlyError(DateTime? signedDate, string roleLabel, List<string> errors)
    {
        if (!signedDate.HasValue)
        {
            errors.Add($"• {NormalizeSignerLabel(roleLabel)}日期缺失");
        }
    }

    /// <summary>
    /// Display* 常量中生产科/资料室/副院长已含「签字」，部门审核等不含；统一成可拼接「缺失/日期缺失」的标签。
    /// </summary>
    private static string NormalizeSignerLabel(string roleLabel)
    {
        string trimmed = roleLabel?.Trim() ?? string.Empty;
        return trimmed.EndsWith("签字", StringComparison.Ordinal)
            ? trimmed
            : trimmed + "签字";
    }

    private static void CollectApprovalSignerErrors(
        NetworkOutboundRecord record,
        NetworkOutboundRecord handoverInput,
        List<string> errors,
        ApprovalChainResolution? approvalChain)
    {
        if (approvalChain != null)
        {
            errors.AddRange(ApprovalChainApplySupport.CollectMissingSignerErrors(
                approvalChain,
                nodeKey => ApprovalChainApplySupport.ReadNetworkOutboundSigner(record, nodeKey)));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(record.DeptHead))
            {
                errors.Add($"• {NormalizeSignerLabel(ApprovalWorkflowDomainValues.DisplayDeptHead)}缺失");
            }

            if (string.IsNullOrWhiteSpace(record.ProductionHead))
            {
                errors.Add($"• {NormalizeSignerLabel(ApprovalWorkflowDomainValues.DisplayProductionHead)}缺失");
            }

            if (string.IsNullOrWhiteSpace(record.ArchiveRoomHead))
            {
                errors.Add($"• {NormalizeSignerLabel(ApprovalWorkflowDomainValues.DisplayArchiveRoomHead)}缺失");
            }

            CollectFallbackVicePresidentNameMissingErrors(record, errors);
        }

        if (string.IsNullOrWhiteSpace(handoverInput.Deliverer))
        {
            errors.Add("• 移交人签字缺失");
        }

        if (string.IsNullOrWhiteSpace(handoverInput.Administrator))
        {
            errors.Add("• 资料员签字缺失");
        }
    }

    /// <summary>
    /// 无审批链时按目的地默认：院内/院外读 <see cref="NetworkOutboundRecord.ProductionVicePresident"/>，
    /// 其余读 <see cref="NetworkOutboundRecord.ArchiveDeputyPresident"/>。
    /// </summary>
    private static void CollectFallbackVicePresidentNameMissingErrors(NetworkOutboundRecord record, List<string> errors)
    {
        if (NetworkTransferDomainValues.IsExternalOfflineDestination(record.DestinationKind))
        {
            if (string.IsNullOrWhiteSpace(record.ProductionVicePresident))
            {
                errors.Add($"• {NormalizeSignerLabel(ApprovalWorkflowDomainValues.DisplayProductionVicePresident)}缺失");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(record.ArchiveDeputyPresident))
        {
            errors.Add($"• {NormalizeSignerLabel(ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident)}缺失");
        }
    }

    private static void CollectFallbackVicePresidentSignerAndDateErrors(NetworkOutboundRecord record, List<string> errors)
    {
        if (NetworkTransferDomainValues.IsExternalOfflineDestination(record.DestinationKind))
        {
            CollectSignerAndDateErrors(
                record.ProductionVicePresident,
                record.ProductionVicePresidentDate,
                ApprovalWorkflowDomainValues.DisplayProductionVicePresident,
                errors);
            return;
        }

        CollectSignerAndDateErrors(
            record.ArchiveDeputyPresident,
            record.ArchiveDeputyPresidentDate,
            ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident,
            errors);
    }

    private static void CollectMandatoryAttachmentErrors(
        NetworkOutboundRecord record,
        IReadOnlyList<SystemAttachment> attachments,
        List<string> errors)
    {
        bool HasCategory(string category) =>
            attachments.Any(item =>
                string.Equals(item.FileCategory?.Trim(), category, StringComparison.Ordinal));

        if (!HasCategory(NetworkTransferDomainValues.AttachmentCategorySignedForm))
        {
            errors.Add("• 缺少「签批单」附件");
        }

        if (ArchiveRegisterDomainValues.RequiresProofMaterialAttachment(record.ProofMaterialNote)
            && !HasCategory(NetworkTransferDomainValues.AttachmentCategoryProofMaterial))
        {
            errors.Add("• 申请时已声明附有证明材料，请上传证明材料扫描件");
        }
    }

    private static void ValidateHeader(NetworkOutboundRecord header, List<string> errors)
    {
        if (!NetworkTransferDomainValues.IsAllowedOutboundDestinationKind(header.DestinationKind))
        {
            errors.Add("请选择有效的出网目的地。");
        }

        string year = header.Year?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(year) || string.Equals(year, "全部", StringComparison.Ordinal))
        {
            errors.Add("请选择具体年度。");
        }

        if (string.IsNullOrWhiteSpace(header.ProjectName))
        {
            errors.Add("请选择项目。");
        }

        if (NetworkTransferDomainValues.IsArchiveFilingDestination(header.DestinationKind)
            && string.IsNullOrWhiteSpace(header.ArchivePurpose))
        {
            errors.Add("目的地为资料室存档时，请选择库管模式。");
        }

        if (NetworkTransferDomainValues.IsExternalOfflineDestination(header.DestinationKind)
            && string.IsNullOrWhiteSpace(
                NetworkTransferDomainValues.ResolveOutboundDestinationUnit(
                    header.DestinationKind,
                    header.DestinationUnit)))
        {
            errors.Add(NetworkTransferDomainValues.IsOutboundExternalDestination(header.DestinationKind)
                ? "请填写接收部门（单位）。"
                : "请选择接收部门（单位）。");
        }

        if (string.IsNullOrWhiteSpace(header.MaterialName))
        {
            errors.Add("请填写资料名称。");
        }

        if (string.IsNullOrWhiteSpace(header.Reason))
        {
            errors.Add("请填写申请说明。");
        }
    }
}
