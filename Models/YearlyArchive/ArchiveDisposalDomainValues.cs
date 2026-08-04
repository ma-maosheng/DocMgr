using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.Shared;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 年度资料离库处置业务域值与规则。
    /// </summary>
    public static class ArchiveDisposalDomainValues
    {
        public const string AttachmentBusinessType = "ArchiveDisposal";

        public const string AttachmentCategorySignedForm = "签批单";
        public const string AttachmentCategoryScenePhoto = "处置现场照片";
        public const string AttachmentCategoryOther = "其他附件";

        public const string ReasonLost = "盘失";
        public const string ReasonDamaged = "损坏";
        public const string ReasonScrap = "拟销";

        public const string MethodInventoryCancel = "库内注销";
        public const string MethodMediaDestroy = "介质销毁";
        public const string MethodHardDiskFormatRetain = "硬盘低格留存";

        public const string HolderDestroyed = "已销毁";
        public const string HolderCancelled = "已注销";
        public const string HolderBlankRetained = "资料室(空盘)";

        /// <summary>离库原因选项。</summary>
        public static IReadOnlyList<string> ReasonOptions { get; } =
        [
            ReasonLost,
            ReasonDamaged,
            ReasonScrap
        ];

        /// <summary>全部处置方式选项。</summary>
        public static IReadOnlyList<string> DispositionMethodOptions { get; } =
        [
            MethodInventoryCancel,
            MethodMediaDestroy,
            MethodHardDiskFormatRetain
        ];

        /// <summary>附件分类选项。</summary>
        public static IReadOnlyList<string> AttachmentCategoryOptions { get; } =
        [
            AttachmentCategorySignedForm,
            AttachmentCategoryScenePhoto,
            AttachmentCategoryOther
        ];

        /// <summary>电子可纳入离库处置的硬盘台账状态。</summary>
        public static IReadOnlyList<string> SelectableHardDiskStatusOptions { get; } =
        [
            HardDiskMedium.StatusInStockDamaged,
            HardDiskMedium.StatusInStockLost,
            HardDiskMedium.StatusInStockScrap
        ];

        /// <summary>电子可纳入离库处置的光盘台账状态。</summary>
        public static IReadOnlyList<string> SelectableOpticalDiscStatusOptions { get; } =
        [
            OpticalDiscMedium.StatusDamaged,
            OpticalDiscMedium.StatusLost,
            OpticalDiscMedium.StatusScrap
        ];

        /// <summary>按盘库登记类型解析处置原因。</summary>
        public static string ResolveReasonFromRegisterKind(string? registerKind)
        {
            string normalized = registerKind?.Trim() ?? string.Empty;
            if (string.Equals(normalized, ArchiveInventoryRegisterDomainValues.KindLost, StringComparison.Ordinal))
            {
                return ReasonLost;
            }

            if (string.Equals(normalized, ArchiveInventoryRegisterDomainValues.KindDamage, StringComparison.Ordinal))
            {
                return ReasonDamaged;
            }

            if (string.Equals(normalized, ArchiveInventoryRegisterDomainValues.KindScrap, StringComparison.Ordinal))
            {
                return ReasonScrap;
            }

            return string.Empty;
        }

        /// <summary>按介质台账状态解析处置原因（电子轨）。</summary>
        public static string ResolveReasonFromMediaStatus(string? mediaStatus)
        {
            string normalized = mediaStatus?.Trim() ?? string.Empty;
            if (string.Equals(normalized, HardDiskMedium.StatusInStockLost, StringComparison.Ordinal)
                || string.Equals(normalized, OpticalDiscMedium.StatusLost, StringComparison.Ordinal))
            {
                return ReasonLost;
            }

            if (string.Equals(normalized, HardDiskMedium.StatusInStockDamaged, StringComparison.Ordinal)
                || string.Equals(normalized, OpticalDiscMedium.StatusDamaged, StringComparison.Ordinal))
            {
                return ReasonDamaged;
            }

            if (string.Equals(normalized, HardDiskMedium.StatusInStockScrap, StringComparison.Ordinal)
                || string.Equals(normalized, OpticalDiscMedium.StatusScrap, StringComparison.Ordinal))
            {
                return ReasonScrap;
            }

            return string.Empty;
        }

        /// <summary>
        /// 按原因/介质类别解析默认可选处置方式。
        /// </summary>
        public static IReadOnlyList<string> ResolveAllowedMethods(
            string? mediaKind,
            string? reason,
            string? mediumKind)
        {
            string normalizedReason = reason?.Trim() ?? string.Empty;
            string normalizedMediaKind = mediaKind?.Trim() ?? string.Empty;
            string normalizedMediumKind = mediumKind?.Trim() ?? string.Empty;

            if (string.Equals(normalizedReason, ReasonLost, StringComparison.Ordinal))
            {
                return [MethodInventoryCancel, MethodMediaDestroy];
            }

            if (string.Equals(normalizedReason, ReasonDamaged, StringComparison.Ordinal))
            {
                return [MethodMediaDestroy];
            }

            if (string.Equals(normalizedReason, ReasonScrap, StringComparison.Ordinal))
            {
                if (string.Equals(normalizedMediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
                    && string.Equals(normalizedMediumKind, ArchiveInventoryRegisterDomainValues.MediumKindHardDisk, StringComparison.Ordinal))
                {
                    return [MethodHardDiskFormatRetain, MethodMediaDestroy];
                }

                if (string.Equals(normalizedMediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
                    && string.Equals(normalizedMediumKind, ArchiveInventoryRegisterDomainValues.MediumKindOpticalDisc, StringComparison.Ordinal))
                {
                    return [MethodMediaDestroy, MethodInventoryCancel];
                }

                // 模拟拟销：仅介质销毁
                return [MethodMediaDestroy];
            }

            return Array.Empty<string>();
        }

        /// <summary>解析默认处置方式（取允许列表第一项）。</summary>
        public static string ResolveDefaultMethod(string? mediaKind, string? reason, string? mediumKind)
        {
            IReadOnlyList<string> allowed = ResolveAllowedMethods(mediaKind, reason, mediumKind);
            return allowed.Count > 0 ? allowed[0] : string.Empty;
        }

        /// <summary>是否为有效处置原因。</summary>
        public static bool IsValidReason(string? reason)
        {
            string normalized = reason?.Trim() ?? string.Empty;
            return ReasonOptions.Any(item => string.Equals(item, normalized, StringComparison.Ordinal));
        }

        /// <summary>是否为有效处置方式。</summary>
        public static bool IsValidDispositionMethod(string? method)
        {
            string normalized = method?.Trim() ?? string.Empty;
            return DispositionMethodOptions.Any(item => string.Equals(item, normalized, StringComparison.Ordinal));
        }

        /// <summary>原因与方式是否匹配；不匹配返回提示，匹配返回 null。</summary>
        public static string? TryGetReasonAndMethodMismatchMessage(
            string? mediaKind,
            string? reason,
            string? mediumKind,
            string? dispositionMethod)
        {
            string normalizedReason = reason?.Trim() ?? string.Empty;
            string normalizedMethod = dispositionMethod?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedReason) || string.IsNullOrWhiteSpace(normalizedMethod))
            {
                return null;
            }

            IReadOnlyList<string> allowed = ResolveAllowedMethods(mediaKind, normalizedReason, mediumKind);
            if (allowed.Count == 0)
            {
                return $"离库原因「{normalizedReason}」暂无可用处置方式。";
            }

            if (!allowed.Any(item => string.Equals(item, normalizedMethod, StringComparison.Ordinal)))
            {
                return $"离库原因「{normalizedReason}」仅允许处置方式：{string.Join("、", allowed)}。";
            }

            return null;
        }

        /// <summary>是否需要处置现场照片（介质销毁必填）。</summary>
        public static bool RequiresScenePhoto(IEnumerable<string?> methods)
        {
            return methods.Any(item =>
                string.Equals(item?.Trim(), MethodMediaDestroy, StringComparison.Ordinal));
        }

        /// <summary>是否含硬盘低格留存方式。</summary>
        public static bool HasFormatRetainMethod(IEnumerable<string?> methods)
        {
            return methods.Any(item =>
                string.Equals(item?.Trim(), MethodHardDiskFormatRetain, StringComparison.Ordinal));
        }

        /// <summary>办结后持有人/保管单位。</summary>
        public static string ResolveHolderAfterComplete(string? dispositionMethod)
        {
            string normalized = dispositionMethod?.Trim() ?? string.Empty;
            if (string.Equals(normalized, MethodMediaDestroy, StringComparison.Ordinal))
            {
                return HolderDestroyed;
            }

            if (string.Equals(normalized, MethodInventoryCancel, StringComparison.Ordinal))
            {
                return HolderCancelled;
            }

            if (string.Equals(normalized, MethodHardDiskFormatRetain, StringComparison.Ordinal))
            {
                return HolderBlankRetained;
            }

            return string.Empty;
        }

        private static readonly string[] ReasonSummaryOrder =
        [
            ReasonLost,
            ReasonDamaged,
            ReasonScrap
        ];

        private static readonly string[] MethodSummaryOrder =
        [
            MethodInventoryCancel,
            MethodMediaDestroy,
            MethodHardDiskFormatRetain
        ];

        /// <summary>汇总明细离库原因。</summary>
        public static string BuildReasonSummary(IEnumerable<string?> reasons)
        {
            return BuildDistinctSummary(reasons, ReasonSummaryOrder);
        }

        /// <summary>汇总明细处置方式。</summary>
        public static string BuildDispositionMethodSummary(IEnumerable<string?> methods)
        {
            return BuildDistinctSummary(methods, MethodSummaryOrder);
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

        /// <summary>工作流状态展示。</summary>
        public static string ToStatusDisplay(int status) => ApplicationWorkflowStatus.ToDisplay(status);
    }
}
