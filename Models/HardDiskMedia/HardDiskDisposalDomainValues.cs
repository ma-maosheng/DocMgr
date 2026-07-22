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
        public const string ReasonDamaged = "损毁";
        public const string ReasonLost = "盘失";
        public const string ReasonOther = "其他";

        public const string MethodDirectDestroy = "直接销毁";
        public const string MethodReturnOffice = "退还办公室";
        public const string MethodOther = "其他";

        public const string HolderOffice = "办公室";
        public const string HolderDestroyed = "已销毁";

        /// <summary>离库原因选项（固定顺序）。</summary>
        public static IReadOnlyList<string> ReasonOptions { get; } =
        [
            ReasonRetire,
            ReasonDamaged,
            ReasonLost,
            ReasonOther
        ];

        /// <summary>离库后处置方式选项（整单唯一）。</summary>
        public static IReadOnlyList<string> DispositionMethodOptions { get; } =
        [
            MethodDirectDestroy,
            MethodReturnOffice,
            MethodOther
        ];

        /// <summary>附件分类选项。</summary>
        public static IReadOnlyList<string> AttachmentCategoryOptions { get; } =
        [
            AttachmentCategorySignedForm,
            AttachmentCategoryDiskPhoto,
            AttachmentCategoryOther
        ];

        /// <summary>可纳入离库处置的介质状态（不含在库资料盘）。</summary>
        public static IReadOnlyList<string> SelectableMediaStatusOptions { get; } =
        [
            HardDiskMedium.StatusInStockBlank,
            HardDiskMedium.StatusInStockDamaged
        ];

        /// <summary>是否为有效离库原因。</summary>
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

        /// <summary>按离库原因解析办结后介质状态。</summary>
        public static string ResolveTerminalMediaStatus(string? reason)
        {
            string normalized = reason?.Trim() ?? string.Empty;
            return string.Equals(normalized, ReasonLost, StringComparison.Ordinal)
                ? HardDiskMedium.StatusOutLost
                : HardDiskMedium.StatusOutDestroyed;
        }

        /// <summary>按离库原因解析流转类型。</summary>
        public static string ResolveTransactionType(string? reason)
        {
            string normalized = reason?.Trim() ?? string.Empty;
            return string.Equals(normalized, ReasonLost, StringComparison.Ordinal)
                ? HardDiskMediaTransaction.TypeLossRegistration
                : HardDiskMediaTransaction.TypeOutboundDestroy;
        }

        /// <summary>是否需要填写「其他」说明（原因或其他处置方式为「其他」）。</summary>
        public static bool RequiresOtherRemark(string? reason, string? dispositionMethod)
        {
            return string.Equals(reason?.Trim(), ReasonOther, StringComparison.Ordinal)
                || string.Equals(dispositionMethod?.Trim(), MethodOther, StringComparison.Ordinal);
        }

        /// <summary>工作流状态展示。</summary>
        public static string ToStatusDisplay(int status) => ApplicationWorkflowStatus.ToDisplay(status);
    }
}
