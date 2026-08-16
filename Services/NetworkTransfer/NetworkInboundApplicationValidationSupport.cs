using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 入网申请提交前完整性与业务逻辑校验。
/// </summary>
public static class NetworkInboundApplicationValidationSupport
{
    /// <summary>
    /// 校验提交申请所需的头信息与明细，返回全部错误（空列表表示通过）。
    /// </summary>
    public static IReadOnlyList<string> ValidateForSubmit(
        NetworkInboundRecord header,
        IReadOnlyList<NetworkInboundItem> items,
        IReadOnlyList<YearlyArchiveRegisterMedia>? mediaEntries = null)
    {
        var errors = new List<string>();
        ValidateHeader(header, errors);

        if (NetworkTransferDomainValues.IsExternalOfflineSource(header.SourceKind))
        {
            NetworkInboundExternalMediaValidationSupport.ValidateForSubmit(header, mediaEntries, errors);
            NetworkInboundReturnHardDiskSupport.ValidateForSubmit(header, header.ReturnHardDiskItems?.ToList() ?? [], errors);
            return errors;
        }

        if (items == null || items.Count == 0)
        {
            errors.Add("请至少录入一条入网明细。");
            return errors;
        }

        string sharedPath = !string.IsNullOrWhiteSpace(header.TargetServerPath)
            ? header.TargetServerPath.Trim()
            : items
                .Select(item => item.TargetServerPath?.Trim() ?? string.Empty)
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path))
                ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sharedPath))
        {
            errors.Add("请选择服务器路径。");
        }

        if (string.IsNullOrWhiteSpace(header.MaterialPath))
        {
            errors.Add("请填写资料路径。");
        }

        if (NetworkTransferDomainValues.IsArchivedElectronicSearchSource(header.SourceKind))
        {
            ValidateArchivedItems(items, errors);
        }
        else
        {
            ValidateExternalItems(items, errors);
        }

        NetworkInboundReturnHardDiskSupport.ValidateForSubmit(header, header.ReturnHardDiskItems?.ToList() ?? [], errors);

        return errors;
    }

    /// <summary>
    /// 校验不通过时抛出包含全部错误的异常。
    /// </summary>
    public static void EnsureValidForSubmit(
        NetworkInboundRecord header,
        IReadOnlyList<NetworkInboundItem> items,
        IReadOnlyList<YearlyArchiveRegisterMedia>? mediaEntries = null)
    {
        IReadOnlyList<string> errors = ValidateForSubmit(header, items, mediaEntries);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }
    }

    /// <summary>
    /// 校验确认入网交接所需的审批信息、服务器路径与必备附件，返回全部错误（空列表表示通过）。
    /// </summary>
    public static IReadOnlyList<string> ValidateForHandoverConfirm(
        NetworkInboundRecord record,
        NetworkInboundRecord handoverInput,
        IReadOnlyList<SystemAttachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(handoverInput);

        var errors = new List<string>();
        CollectApprovalSignerErrors(record, handoverInput, errors);
        CollectInboundPathErrors(record, errors);
        return errors;
    }

    /// <summary>
    /// 校验确认办结所需的审批信息与必备附件。
    /// </summary>
    public static IReadOnlyList<string> ValidateForComplete(
        NetworkInboundRecord record,
        IReadOnlyList<SystemAttachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(record);

        var errors = new List<string>();
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

        CollectInboundPathErrors(record, errors);
        CollectMandatoryAttachmentErrors(record, attachments ?? Array.Empty<SystemAttachment>(), errors);
        return errors;
    }

    /// <summary>
    /// 校验不通过时抛出包含全部错误的异常。
    /// </summary>
    public static void EnsureValidForHandoverConfirm(
        NetworkInboundRecord record,
        NetworkInboundRecord handoverInput,
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

    private static void CollectApprovalSignerErrors(
        NetworkInboundRecord record,
        NetworkInboundRecord handoverInput,
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

    private static void CollectInboundPathErrors(NetworkInboundRecord record, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(record.MaterialPath))
        {
            errors.Add("• 入网单缺少资料路径");
        }

        if (NetworkTransferDomainValues.IsExternalOfflineSource(record.SourceKind))
        {
            if (string.IsNullOrWhiteSpace(record.TargetServerPath))
            {
                errors.Add("• 入网单缺少目标服务器路径");
            }

            return;
        }

        foreach (var item in record.Items)
        {
            if (string.IsNullOrWhiteSpace(item.TargetServerPath))
            {
                string label = string.IsNullOrWhiteSpace(item.AssetName)
                    ? "入网明细"
                    : $"明细「{item.AssetName.Trim()}」";
                errors.Add($"• {label}缺少目标服务器路径");
            }
        }
    }

    private static void CollectMandatoryAttachmentErrors(
        NetworkInboundRecord record,
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

    private static void ValidateHeader(NetworkInboundRecord header, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(header.SourceKind)
            || !NetworkTransferDomainValues.IsValidSourceKind(header.SourceKind))
        {
            errors.Add("请选择有效的数据来源。");
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

        if (NetworkTransferDomainValues.IsArchivedElectronicSearchSource(header.SourceKind)
            && (!header.SourceResultSetId.HasValue || header.SourceResultSetId.Value <= 0))
        {
            errors.Add("存档资料入网须选择电子检索集。");
        }

        ValidateProvideUnit(header, errors);
    }

    private static void ValidateProvideUnit(NetworkInboundRecord header, List<string> errors)
    {
        string provideUnit = NetworkTransferDomainValues.ResolveInboundProvideUnit(
            header.SourceKind,
            header.ProvideUnit);

        if (NetworkTransferDomainValues.IsArchivedElectronicSearchSource(header.SourceKind))
        {
            if (!string.Equals(provideUnit, NetworkTransferDomainValues.InboundProvideUnitArchiveRoom, StringComparison.Ordinal))
            {
                errors.Add("存档资料入网的提供部门须为资料室。");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(provideUnit))
        {
            errors.Add(NetworkTransferDomainValues.IsExternalOfflineExternalSource(header.SourceKind)
                ? "请填写提供部门（单位）。"
                : "请选择提供部门（单位）。");
        }
    }

    private static void ValidateArchivedItems(IReadOnlyList<NetworkInboundItem> items, List<string> errors)
    {
        for (int index = 0; index < items.Count; index++)
        {
            NetworkInboundItem item = items[index];
            int rowNo = index + 1;
            string rowLabel = BuildRowLabel(item, rowNo);

            if (!item.SourceFilingFactId.HasValue || item.SourceFilingFactId.Value <= 0)
            {
                errors.Add($"第 {rowNo} 行{rowLabel}：立档明细缺少立档事实关联。");
            }

            if (string.IsNullOrWhiteSpace(item.ItemName))
            {
                errors.Add($"第 {rowNo} 行{rowLabel}：资料明细不能为空。");
            }
        }
    }

    private static void ValidateExternalItems(IReadOnlyList<NetworkInboundItem> items, List<string> errors)
    {
        var duplicateKeys = new HashSet<string>(StringComparer.Ordinal);
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < items.Count; index++)
        {
            NetworkInboundItem item = items[index];
            int rowNo = index + 1;
            string rowLabel = BuildRowLabel(item, rowNo);

            string assetKind = item.AssetKind?.Trim() ?? string.Empty;
            if (!NetworkTransferDomainValues.AssetKindOptions.Contains(assetKind, StringComparer.Ordinal))
            {
                errors.Add($"第 {rowNo} 行{rowLabel}：请选择资料类别。");
            }

            if (string.IsNullOrWhiteSpace(item.AssetName))
            {
                errors.Add($"第 {rowNo} 行{rowLabel}：资料名称不能为空。");
            }

            if (string.IsNullOrWhiteSpace(item.ItemName))
            {
                errors.Add($"第 {rowNo} 行{rowLabel}：资料明细不能为空。");
            }

            if (string.IsNullOrWhiteSpace(item.ConfidentialLevel))
            {
                errors.Add($"第 {rowNo} 行{rowLabel}：请选择密级。");
            }

            if (!NetworkInboundItemDisplaySupport.TryParseDataSizeText(item.DataSizeText, out decimal dataSize, out string unit))
            {
                errors.Add($"第 {rowNo} 行{rowLabel}：请填写有效的数据量（数值 + MB/GB/TB）。");
            }
            else if (!NetworkInboundItemDisplaySupport.DataSizeUnitOptions.Contains(unit, StringComparer.Ordinal))
            {
                errors.Add($"第 {rowNo} 行{rowLabel}：数据量单位须为 MB、GB 或 TB。");
            }
            else if (dataSize <= 0)
            {
                errors.Add($"第 {rowNo} 行{rowLabel}：数据量须大于 0。");
            }

            string dedupKey = $"{item.AssetName.Trim()}|{item.ItemName.Trim()}|{assetKind}";
            if (!seenKeys.Add(dedupKey))
            {
                duplicateKeys.Add(dedupKey);
            }
        }

        foreach (string duplicateKey in duplicateKeys)
        {
            string[] parts = duplicateKey.Split('|');
            errors.Add($"存在重复明细：资料名称「{parts[0]}」、资料明细「{parts[1]}」、资料类别「{parts[2]}」。");
        }
    }

    private static string BuildRowLabel(NetworkInboundItem item, int rowNo)
    {
        if (!string.IsNullOrWhiteSpace(item.AssetName))
        {
            return $"（{item.AssetName.Trim()}）";
        }

        if (!string.IsNullOrWhiteSpace(item.MaterialName))
        {
            return $"（{item.MaterialName.Trim()}）";
        }

        return string.Empty;
    }
}
