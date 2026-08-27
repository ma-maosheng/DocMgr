using DocMgr.Models.HistoryArchive;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.HistoryArchive;

/// <summary>
/// 历史存档离库处置提交/办结前完整性校验。
/// </summary>
public static class HistoryArchiveDisposalValidationSupport
{
    /// <summary>校验提交所需字段与混放整组完整性。</summary>
    public static IReadOnlyList<string> ValidateForSubmit(
        string? materialKind,
        string? dispositionMethod,
        string? transferTarget,
        string? otherRemark,
        string? reason,
        IReadOnlyList<HistoryArchiveDisposalItem> items,
        IReadOnlyDictionary<string, HistoryArchiveDisposalBoxCandidate>? selectableByBoxCode = null)
    {
        var errors = new List<string>();
        IReadOnlyList<HistoryArchiveDisposalItem> rows = items ?? Array.Empty<HistoryArchiveDisposalItem>();

        if (!HistoryArchiveDisposalDomainValues.IsValidMaterialKind(materialKind))
        {
            errors.Add("请选择资料类别（地形图图件 / 航摄胶片、像片 / 其他资料）。");
        }

        if (!HistoryArchiveDisposalDomainValues.IsValidDispositionMethod(dispositionMethod))
        {
            errors.Add("请选择处置方式（离库销毁 / 离库转交 / 其他）。");
        }

        if (HistoryArchiveDisposalDomainValues.RequiresTransferTarget(dispositionMethod)
            && string.IsNullOrWhiteSpace(transferTarget))
        {
            errors.Add("处置方式为「离库转交」时，请填写转交对象。");
        }

        if (HistoryArchiveDisposalDomainValues.RequiresOtherRemark(dispositionMethod)
            && string.IsNullOrWhiteSpace(otherRemark))
        {
            errors.Add("处置方式为「其他」时，请填写其他说明。");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            errors.Add("请填写申请说明。");
        }

        if (rows.Count == 0)
        {
            errors.Add("请至少选择一个档案盒。");
            return errors;
        }

        CollectItemErrors(rows, selectableByBoxCode, errors);
        CollectMixedGroupErrors(rows, errors);
        return errors;
    }

    /// <summary>提交校验不通过时抛出。</summary>
    public static void EnsureValidForSubmit(
        string? materialKind,
        string? dispositionMethod,
        string? transferTarget,
        string? otherRemark,
        string? reason,
        IReadOnlyList<HistoryArchiveDisposalItem> items,
        IReadOnlyDictionary<string, HistoryArchiveDisposalBoxCandidate>? selectableByBoxCode = null)
    {
        IReadOnlyList<string> errors = ValidateForSubmit(
            materialKind,
            dispositionMethod,
            transferTarget,
            otherRemark,
            reason,
            items,
            selectableByBoxCode);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "提交前校验未通过：" + Environment.NewLine + Environment.NewLine
                + string.Join(Environment.NewLine, errors));
        }
    }

    /// <summary>办结校验：提交项 + 审核审批人 + 签批单 + 销毁资料照片。</summary>
    public static IReadOnlyList<string> ValidateForComplete(
        string? materialKind,
        string? dispositionMethod,
        string? transferTarget,
        string? otherRemark,
        string? reason,
        IReadOnlyList<HistoryArchiveDisposalItem> items,
        string? archiveRoomHead,
        DateTime? archiveRoomHeadDate,
        string? archiveDeputyPresident,
        DateTime? archiveDeputyPresidentDate,
        IReadOnlyList<SystemAttachment>? attachments,
        bool physicalRemovalConfirmed)
    {
        var errors = new List<string>();
        foreach (string error in ValidateForSubmit(
                     materialKind,
                     dispositionMethod,
                     transferTarget,
                     otherRemark,
                     reason,
                     items))
        {
            errors.Add(error.StartsWith("• ", StringComparison.Ordinal) ? error : "• " + error);
        }

        CollectSignerAndDateErrors(archiveRoomHead, archiveRoomHeadDate, "资料室负责人", errors);
        CollectSignerAndDateErrors(archiveDeputyPresident, archiveDeputyPresidentDate, "分管资料副院长", errors);

        IReadOnlyList<SystemAttachment> files = attachments ?? Array.Empty<SystemAttachment>();
        if (!files.Any(item => string.Equals(
                item.FileCategory?.Trim(),
                HistoryArchiveDisposalDomainValues.AttachmentCategorySignedForm,
                StringComparison.Ordinal)))
        {
            errors.Add("• 缺少「签批单」附件");
        }

        if (HistoryArchiveDisposalDomainValues.RequiresScenePhoto(dispositionMethod)
            && !files.Any(item => HistoryArchiveDisposalDomainValues.IsScenePhotoCategory(item.FileCategory)))
        {
            errors.Add("• 处置方式为「离库销毁」时，须上传「处置资料照片」");
        }

        if (!physicalRemovalConfirmed)
        {
            errors.Add("• 办结前须确认已从档案柜撤出对应档案盒");
        }

        return errors;
    }

    public static void EnsureValidForComplete(
        string? materialKind,
        string? dispositionMethod,
        string? transferTarget,
        string? otherRemark,
        string? reason,
        IReadOnlyList<HistoryArchiveDisposalItem> items,
        string? archiveRoomHead,
        DateTime? archiveRoomHeadDate,
        string? archiveDeputyPresident,
        DateTime? archiveDeputyPresidentDate,
        IReadOnlyList<SystemAttachment>? attachments,
        bool physicalRemovalConfirmed)
    {
        IReadOnlyList<string> errors = ValidateForComplete(
            materialKind,
            dispositionMethod,
            transferTarget,
            otherRemark,
            reason,
            items,
            archiveRoomHead,
            archiveRoomHeadDate,
            archiveDeputyPresident,
            archiveDeputyPresidentDate,
            attachments,
            physicalRemovalConfirmed);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "办结前信息完整性校验未通过：" + Environment.NewLine + Environment.NewLine
                + string.Join(Environment.NewLine, errors));
        }
    }

    private static void CollectItemErrors(
        IReadOnlyList<HistoryArchiveDisposalItem> rows,
        IReadOnlyDictionary<string, HistoryArchiveDisposalBoxCandidate>? selectableByBoxCode,
        List<string> errors)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < rows.Count; index++)
        {
            HistoryArchiveDisposalItem item = rows[index];
            string boxCode = item.BoxCode?.Trim() ?? string.Empty;
            int rowNo = index + 1;
            if (string.IsNullOrWhiteSpace(boxCode))
            {
                errors.Add($"第 {rowNo} 行：缺少档案盒编号。");
                continue;
            }

            if (!seen.Add(boxCode))
            {
                errors.Add($"存在重复档案盒：「{boxCode}」。");
            }

            if (selectableByBoxCode == null)
            {
                continue;
            }

            if (!selectableByBoxCode.TryGetValue(boxCode, out HistoryArchiveDisposalBoxCandidate? candidate)
                || !candidate.IsSelectable)
            {
                errors.Add($"档案盒「{boxCode}」当前不可处置（跨类同盒、已占用或已离库）。");
            }
        }
    }

    private static void CollectMixedGroupErrors(
        IReadOnlyList<HistoryArchiveDisposalItem> rows,
        List<string> errors)
    {
        var selected = rows
            .Select(item => item.BoxCode?.Trim() ?? string.Empty)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (HistoryArchiveDisposalItem item in rows)
        {
            foreach (string related in HistoryArchiveBoxCodeSupport.SplitBoxCodes(item.RelatedBoxCodes))
            {
                if (!selected.Contains(related))
                {
                    missing.Add(related);
                }
            }
        }

        if (missing.Count > 0)
        {
            errors.Add("混放盒须整组纳入，缺少关联盒：" + string.Join("、", missing.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)));
        }
    }

    private static void CollectSignerAndDateErrors(
        string? signer,
        DateTime? signedDate,
        string roleLabel,
        List<string> errors)
    {
        string name = signer?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add($"• {roleLabel}姓名缺失");
            return;
        }

        if (!signedDate.HasValue)
        {
            errors.Add($"• {roleLabel}日期缺失");
        }
    }
}
