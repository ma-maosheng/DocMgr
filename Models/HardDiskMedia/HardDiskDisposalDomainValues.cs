using DocMgr.Models.Shared;

namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘离库处置业务域值与规则。
    /// </summary>
    public static class HardDiskDisposalDomainValues
    {
        public const string AttachmentBusinessType = "HardDiskDisposal";

        public const string AttachmentCategorySignedForm = "签批单";
        public const string AttachmentCategoryDiskPhoto = "硬盘照片";
        public const string AttachmentCategoryOther = "其他附件";

        public const string ReasonRetire = "淘汰";
        public const string ReasonDamaged = "损坏";
        public const string ReasonLost = "盘失";
        public const string ReasonScrap = "拟销";
        /// <summary>历史单据兼容：旧版整单原因「损毁」。</summary>
        public const string LegacyReasonDamaged = "损毁";
        /// <summary>历史单据兼容：旧版整单原因「其他」。</summary>
        public const string ReasonOther = "其他";

        public const string MethodDirectDestroy = "离库销毁";
        /// <summary>历史单据兼容：旧版「直接销毁」。</summary>
        public const string LegacyMethodDirectDestroy = "直接销毁";
        public const string MethodReturnOffice = "退还办公室";
        /// <summary>库内注销：专用于「在库(盘失)」「在库(拟销)」硬盘的处置方式。</summary>
        public const string MethodInventoryCancel = "库内注销";
        public const string MethodOther = "其他";

        public const string HolderOffice = "办公室";
        public const string HolderDestroyed = "已销毁";

        /// <summary>离库原因选项（按介质状态自动赋值：空盘→淘汰、损坏→损坏、盘失→盘失、拟销→拟销）。</summary>
        public static IReadOnlyList<string> ReasonOptions { get; } =
        [
            ReasonRetire,
            ReasonDamaged,
            ReasonLost,
            ReasonScrap
        ];

        /// <summary>离库后处置方式选项（按盘；盘失/拟销自动「库内注销」）。</summary>
        public static IReadOnlyList<string> DispositionMethodOptions { get; } =
        [
            MethodDirectDestroy,
            MethodReturnOffice,
            MethodInventoryCancel,
            MethodOther
        ];

        /// <summary>附件分类选项。</summary>
        public static IReadOnlyList<string> AttachmentCategoryOptions { get; } =
        [
            AttachmentCategorySignedForm,
            AttachmentCategoryDiskPhoto,
            AttachmentCategoryOther
        ];

        /// <summary>可纳入离库处置的介质状态（含盘库登记后的在库盘失/拟销）。</summary>
        public static IReadOnlyList<string> SelectableMediaStatusOptions { get; } =
        [
            HardDiskMedium.StatusInStockBlank,
            HardDiskMedium.StatusInStockDamaged,
            HardDiskMedium.StatusInStockLost,
            HardDiskMedium.StatusInStockScrap
        ];

        /// <summary>是否为有效离库原因（含历史「损毁」「其他」）。</summary>
        public static bool IsValidReason(string? reason)
        {
            string normalized = reason?.Trim() ?? string.Empty;
            return ReasonOptions.Any(item => string.Equals(item, normalized, StringComparison.Ordinal))
                || string.Equals(normalized, LegacyReasonDamaged, StringComparison.Ordinal)
                || string.Equals(normalized, ReasonOther, StringComparison.Ordinal);
        }

        /// <summary>将历史处置方式文案归一为现行文案。</summary>
        public static string NormalizeDispositionMethod(string? method)
        {
            string normalized = method?.Trim() ?? string.Empty;
            if (string.Equals(normalized, LegacyMethodDirectDestroy, StringComparison.Ordinal))
            {
                return MethodDirectDestroy;
            }

            return normalized;
        }

        /// <summary>是否为离库销毁（含历史「直接销毁」）。</summary>
        public static bool IsDirectDestroyMethod(string? method)
        {
            return string.Equals(
                NormalizeDispositionMethod(method),
                MethodDirectDestroy,
                StringComparison.Ordinal);
        }

        /// <summary>是否为有效处置方式（含历史文案）。</summary>
        public static bool IsValidDispositionMethod(string? method)
        {
            string normalized = NormalizeDispositionMethod(method);
            return DispositionMethodOptions.Any(item => string.Equals(item, normalized, StringComparison.Ordinal));
        }

        /// <summary>
        /// 按处置前介质状态自动解析离库原因：
        /// 在库(空盘)→淘汰；在库(损坏)→损坏；在库(盘失)→盘失；在库(拟销)→拟销。
        /// </summary>
        public static string ResolveReasonFromMediaStatus(string? mediaStatus)
        {
            string normalized = mediaStatus?.Trim() ?? string.Empty;
            if (string.Equals(normalized, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal))
            {
                return ReasonRetire;
            }

            if (string.Equals(normalized, HardDiskMedium.StatusInStockDamaged, StringComparison.Ordinal))
            {
                return ReasonDamaged;
            }

            if (string.Equals(normalized, HardDiskMedium.StatusInStockLost, StringComparison.Ordinal))
            {
                return ReasonLost;
            }

            if (string.Equals(normalized, HardDiskMedium.StatusInStockScrap, StringComparison.Ordinal))
            {
                return ReasonScrap;
            }

            return string.Empty;
        }

        /// <summary>
        /// 按处置前介质状态自动解析处置方式：
        /// 「在库(盘失)」「在库(拟销)」→库内注销；其余须人工指定。
        /// </summary>
        public static string ResolveDispositionMethodFromMediaStatus(string? mediaStatus)
        {
            string normalized = mediaStatus?.Trim() ?? string.Empty;
            return string.Equals(normalized, HardDiskMedium.StatusInStockLost, StringComparison.Ordinal)
                || string.Equals(normalized, HardDiskMedium.StatusInStockScrap, StringComparison.Ordinal)
                ? MethodInventoryCancel
                : string.Empty;
        }

        private static readonly string[] ReasonSummaryOrder =
        [
            ReasonRetire,
            ReasonDamaged,
            LegacyReasonDamaged,
            ReasonLost,
            ReasonScrap,
            ReasonOther
        ];

        private static readonly string[] MethodSummaryOrder =
        [
            MethodDirectDestroy,
            LegacyMethodDirectDestroy,
            MethodReturnOffice,
            MethodInventoryCancel,
            MethodOther
        ];

        /// <summary>汇总明细离库原因（去重、顿号连接），供主表列表展示。</summary>
        public static string BuildReasonSummary(IEnumerable<string?> reasons)
        {
            return BuildDistinctSummary(reasons, ReasonSummaryOrder);
        }

        /// <summary>汇总明细处置方式（去重、顿号连接；展示时归一为现行文案）。</summary>
        public static string BuildDispositionMethodSummary(IEnumerable<string?> methods)
        {
            return BuildDistinctSummary(
                methods.Select(NormalizeDispositionMethod),
                MethodSummaryOrder);
        }

        private static string BuildDistinctSummary(IEnumerable<string?> values, string[] order)
        {
            var distinct = values
                .Select(item => item?.Trim() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item =>
                {
                    int index = Array.IndexOf(order, item);
                    return index >= 0 ? index : int.MaxValue;
                })
                .ThenBy(item => item, StringComparer.Ordinal)
                .ToList();
            return distinct.Count == 0 ? string.Empty : string.Join("、", distinct);
        }

        /// <summary>
        /// 解析处置前存放位置：优先台账当前位置；
        /// 台账已清空时回退备用位置（如明细已存值，或「在库(盘失)」盘库登记流转前档口）。
        /// </summary>
        public static string ResolveBeforeStorageLocation(
            string? mediaStatus,
            string? ledgerStorageLocation,
            string? fallbackStorageLocation)
        {
            _ = mediaStatus;
            string current = ledgerStorageLocation?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(current))
            {
                return current;
            }

            return fallbackStorageLocation?.Trim() ?? string.Empty;
        }

        /// <summary>办结后统一写入「离库(处置)」终态（含原因=盘失的正式清账）。</summary>
        public static string ResolveTerminalMediaStatus(string? reason)
        {
            _ = reason;
            return HardDiskMedium.StatusDisposed;
        }

        /// <summary>按离库原因解析流转类型。</summary>
        public static string ResolveTransactionType(string? reason)
        {
            string normalized = reason?.Trim() ?? string.Empty;
            return string.Equals(normalized, ReasonLost, StringComparison.Ordinal)
                ? HardDiskMediaTransaction.TypeInventoryLost
                : HardDiskMediaTransaction.TypeDisposal;
        }

        /// <summary>是否需要填写「其他」说明（处置方式为「其他」）。</summary>
        public static bool RequiresOtherRemark(string? dispositionMethod)
        {
            return string.Equals(dispositionMethod?.Trim(), MethodOther, StringComparison.Ordinal);
        }

        /// <summary>兼容旧调用：原因参数已忽略，仅按处置方式判断。</summary>
        public static bool RequiresOtherRemark(string? reason, string? dispositionMethod)
        {
            _ = reason;
            return RequiresOtherRemark(dispositionMethod);
        }

        /// <summary>
        /// 离库原因与处置方式是否匹配：
        /// 盘失 → 必须「库内注销」；淘汰/损坏 → 不得「库内注销」。
        /// </summary>
        public static bool IsReasonAndDispositionMethodCompatible(string? reason, string? dispositionMethod)
        {
            return TryGetReasonAndDispositionMethodMismatchMessage(reason, dispositionMethod) == null;
        }

        /// <summary>
        /// 若不匹配返回提示文案；匹配返回 null。
        /// </summary>
        public static string? TryGetReasonAndDispositionMethodMismatchMessage(
            string? reason,
            string? dispositionMethod)
        {
            string normalizedReason = reason?.Trim() ?? string.Empty;
            if (string.Equals(normalizedReason, LegacyReasonDamaged, StringComparison.Ordinal))
            {
                normalizedReason = ReasonDamaged;
            }

            string normalizedMethod = NormalizeDispositionMethod(dispositionMethod);
            if (string.IsNullOrWhiteSpace(normalizedReason) || string.IsNullOrWhiteSpace(normalizedMethod))
            {
                return null;
            }

            bool isLostReason = string.Equals(normalizedReason, ReasonLost, StringComparison.Ordinal);
            bool isInventoryCancel = string.Equals(normalizedMethod, MethodInventoryCancel, StringComparison.Ordinal);

            if (isLostReason && !isInventoryCancel)
            {
                return $"离库原因为「{ReasonLost}」时，处置方式必须为「{MethodInventoryCancel}」。";
            }

            if (!isLostReason && isInventoryCancel)
            {
                return $"处置方式「{MethodInventoryCancel}」仅适用于离库原因「{ReasonLost}」。";
            }

            return null;
        }

        /// <summary>工作流状态展示。</summary>
        public static string ToStatusDisplay(int status) => ApplicationWorkflowStatus.ToDisplay(status);
    }
}
