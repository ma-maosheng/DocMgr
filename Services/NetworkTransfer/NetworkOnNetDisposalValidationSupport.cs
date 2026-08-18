using DocMgr.Models.NetworkTransfer;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 在网数据处置提交前完整性与业务逻辑校验。
/// </summary>
public static class NetworkOnNetDisposalValidationSupport
{
    /// <summary>
    /// 校验提交所需的申请说明、明细原因/方式与对象可处置性，返回全部错误（空列表表示通过）。
    /// </summary>
    public static IReadOnlyList<string> ValidateForSubmit(
        string? reason,
        IReadOnlyList<NetworkOnNetDisposalItem> items,
        IReadOnlySet<int>? selectableAssetIds = null)
    {
        var errors = new List<string>();
        IReadOnlyList<NetworkOnNetDisposalItem> rows = items ?? Array.Empty<NetworkOnNetDisposalItem>();

        ValidateHeader(reason, rows, errors);
        if (rows.Count == 0)
        {
            errors.Add("请至少选择一条在网对象。");
            return errors;
        }

        ValidateItems(rows, selectableAssetIds, errors);
        return errors;
    }

    /// <summary>
    /// 校验不通过时抛出包含全部错误的异常。
    /// </summary>
    public static void EnsureValidForSubmit(
        string? reason,
        IReadOnlyList<NetworkOnNetDisposalItem> items,
        IReadOnlySet<int>? selectableAssetIds = null)
    {
        IReadOnlyList<string> errors = ValidateForSubmit(reason, items, selectableAssetIds);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "提交前校验未通过：" + Environment.NewLine + Environment.NewLine
                + string.Join(Environment.NewLine, errors));
        }
    }

    private static void ValidateHeader(
        string? reason,
        IReadOnlyList<NetworkOnNetDisposalItem> items,
        List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        bool hasOther = items.Any(item =>
            string.Equals(
                item.DisposalReason?.Trim(),
                NetworkTransferDomainValues.DisposalReasonOther,
                StringComparison.Ordinal)
            || string.Equals(
                item.DispositionMethod?.Trim(),
                NetworkTransferDomainValues.DisposalMethodOther,
                StringComparison.Ordinal));

        errors.Add(hasOther
            ? "存在处置原因或方式为「其他」的明细，请填写申请说明。"
            : "请填写申请说明。");
    }

    private static void ValidateItems(
        IReadOnlyList<NetworkOnNetDisposalItem> items,
        IReadOnlySet<int>? selectableAssetIds,
        List<string> errors)
    {
        var seenAssetIds = new HashSet<int>();
        var duplicateAssetNos = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < items.Count; index++)
        {
            NetworkOnNetDisposalItem item = items[index];
            int rowNo = index + 1;
            string rowLabel = BuildRowLabel(item);

            if (item.OnNetAssetId <= 0)
            {
                errors.Add($"第 {rowNo} 行{rowLabel}：缺少在网对象关联。");
                continue;
            }

            if (!seenAssetIds.Add(item.OnNetAssetId))
            {
                duplicateAssetNos.Add(ResolveAssetNo(item));
            }

            string disposalReason = item.DisposalReason?.Trim() ?? string.Empty;
            if (!NetworkTransferDomainValues.DisposalReasonOptions.Contains(disposalReason, StringComparer.Ordinal))
            {
                errors.Add($"第 {rowNo} 行{rowLabel}：请选择有效的处置原因。");
            }

            string dispositionMethod = item.DispositionMethod?.Trim() ?? string.Empty;
            if (!NetworkTransferDomainValues.DisposalMethodOptions.Contains(dispositionMethod, StringComparer.Ordinal))
            {
                errors.Add($"第 {rowNo} 行{rowLabel}：请选择有效的处置方式。");
            }

            if (selectableAssetIds != null && !selectableAssetIds.Contains(item.OnNetAssetId))
            {
                errors.Add($"第 {rowNo} 行{rowLabel}：当前不可处置（已锁定、出网中或已处置）。");
            }
        }

        foreach (string assetNo in duplicateAssetNos)
        {
            errors.Add($"存在重复明细：资产编号「{assetNo}」。");
        }
    }

    private static string BuildRowLabel(NetworkOnNetDisposalItem item)
    {
        string assetNo = ResolveAssetNo(item);
        return string.IsNullOrWhiteSpace(assetNo) ? string.Empty : $"（{assetNo}）";
    }

    private static string ResolveAssetNo(NetworkOnNetDisposalItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.AssetNo))
        {
            return item.AssetNo.Trim();
        }

        return string.IsNullOrWhiteSpace(item.AssetName) ? string.Empty : item.AssetName.Trim();
    }
}
