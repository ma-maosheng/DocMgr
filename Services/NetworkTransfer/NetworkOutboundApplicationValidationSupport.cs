using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

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
        IReadOnlyList<SystemAttachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(handoverInput);

        var errors = new List<string>();
        CollectApprovalSignerErrors(record, handoverInput, errors);
        return errors;
    }

    public static void EnsureValidForHandoverConfirm(
        NetworkOutboundRecord record,
        NetworkOutboundRecord handoverInput,
        IReadOnlyList<SystemAttachment> attachments)
    {
        IReadOnlyList<string> errors = ValidateForHandoverConfirm(record, handoverInput, attachments);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "交接确认前校验未通过：" + Environment.NewLine + Environment.NewLine
                + string.Join(Environment.NewLine, errors));
        }
    }

    public static IReadOnlyList<string> ValidateForComplete(
        NetworkOutboundRecord record,
        IReadOnlyList<SystemAttachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(record);
        var errors = new List<string>();
        CollectCompleteSignerErrors(record, errors);
        CollectMandatoryAttachmentErrors(record, attachments ?? Array.Empty<SystemAttachment>(), errors);
        NetworkOutboundExternalMediaValidationSupport.ValidateForComplete(
            record.DestinationKind,
            record.MediaEntries?.ToList(),
            errors);
        return errors;
    }

    private static void CollectCompleteSignerErrors(NetworkOutboundRecord record, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(record.DeptLeader))
        {
            errors.Add("• 部门负责人签字缺失");
        }

        if (string.IsNullOrWhiteSpace(record.ProdLeader))
        {
            errors.Add("• 生产管理科负责人签字缺失");
        }

        if (string.IsNullOrWhiteSpace(record.RndLeader))
        {
            errors.Add("• 资料室负责人签字缺失");
        }

        if (string.IsNullOrWhiteSpace(record.DeputyLeader))
        {
            errors.Add("• 分管领导签字缺失");
        }

        if (string.IsNullOrWhiteSpace(record.Deliverer))
        {
            errors.Add("• 移交人签字缺失");
        }

        if (string.IsNullOrWhiteSpace(record.Administrator))
        {
            errors.Add("• 资料员签字缺失");
        }
    }

    private static void CollectApprovalSignerErrors(
        NetworkOutboundRecord record,
        NetworkOutboundRecord handoverInput,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(record.DeptLeader))
        {
            errors.Add("• 部门负责人签字缺失");
        }

        if (string.IsNullOrWhiteSpace(record.ProdLeader))
        {
            errors.Add("• 生产管理科负责人签字缺失");
        }

        if (string.IsNullOrWhiteSpace(record.RndLeader))
        {
            errors.Add("• 资料室负责人签字缺失");
        }

        if (string.IsNullOrWhiteSpace(record.DeputyLeader))
        {
            errors.Add("• 分管领导签字缺失");
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
