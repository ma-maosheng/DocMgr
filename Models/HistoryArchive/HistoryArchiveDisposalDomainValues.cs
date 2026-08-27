using DocMgr.Models.Shared;

namespace DocMgr.Models.HistoryArchive
{
    /// <summary>
    /// 历史存档离库处置业务域值与规则。
    /// </summary>
    public static class HistoryArchiveDisposalDomainValues
    {
        public const string AttachmentBusinessType = "HistoryArchiveDisposal";

        public const string AttachmentCategorySignedForm = "签批单";
        public const string AttachmentCategoryScenePhoto = "处置资料照片";
        public const string AttachmentCategoryOther = "其他附件";

        /// <summary>历史单据兼容：旧版附件分类「处置现场照片」。</summary>
        public const string LegacyAttachmentCategoryScenePhoto = "处置现场照片";

        public const string MaterialKindTopoMap = "TopoMap";
        public const string MaterialKindAerialPhoto = "AerialPhoto";
        public const string MaterialKindOtherMap = "OtherMap";

        public const string MaterialKindDisplayTopoMap = "地形图图件";
        public const string MaterialKindDisplayAerialPhoto = "航摄胶片、像片";
        public const string MaterialKindDisplayOtherMap = "其他资料";

        public const string MethodDestroy = "离库销毁";
        public const string MethodTransfer = "离库转交";
        public const string MethodOther = "其他";

        public const string LifecycleInStock = "在库";
        public const string LifecycleLocked = "离库锁定";
        public const string LifecycleDisposed = "已离库";

        public const string PlacementSourceTopoMap = "TopoMap";
        public const string PlacementSourceAerialPhoto = "AerialPhoto";
        public const string PlacementSourceOtherMap = "OtherMap";
        public const string PlacementSourceMixed = "Mixed";

        /// <summary>资料类别选项（编码）。</summary>
        public static IReadOnlyList<string> MaterialKindOptions { get; } =
        [
            MaterialKindTopoMap,
            MaterialKindAerialPhoto,
            MaterialKindOtherMap
        ];

        /// <summary>资料类别显示名（与菜单一致）。</summary>
        public static IReadOnlyList<string> MaterialKindDisplayOptions { get; } =
        [
            MaterialKindDisplayTopoMap,
            MaterialKindDisplayAerialPhoto,
            MaterialKindDisplayOtherMap
        ];

        /// <summary>处置方式选项。</summary>
        public static IReadOnlyList<string> DispositionMethodOptions { get; } =
        [
            MethodDestroy,
            MethodTransfer,
            MethodOther
        ];

        /// <summary>附件分类选项。</summary>
        public static IReadOnlyList<string> AttachmentCategoryOptions { get; } =
        [
            AttachmentCategorySignedForm,
            AttachmentCategoryScenePhoto,
            AttachmentCategoryOther
        ];

        /// <summary>将资料类别编码转为显示名。</summary>
        public static string ToMaterialKindDisplay(string? materialKind)
        {
            string normalized = materialKind?.Trim() ?? string.Empty;
            if (string.Equals(normalized, MaterialKindTopoMap, StringComparison.Ordinal))
            {
                return MaterialKindDisplayTopoMap;
            }

            if (string.Equals(normalized, MaterialKindAerialPhoto, StringComparison.Ordinal))
            {
                return MaterialKindDisplayAerialPhoto;
            }

            if (string.Equals(normalized, MaterialKindOtherMap, StringComparison.Ordinal))
            {
                return MaterialKindDisplayOtherMap;
            }

            return normalized;
        }

        /// <summary>将显示名或编码归一为资料类别编码。</summary>
        public static string NormalizeMaterialKind(string? materialKind)
        {
            string normalized = materialKind?.Trim() ?? string.Empty;
            if (string.Equals(normalized, MaterialKindDisplayTopoMap, StringComparison.Ordinal)
                || string.Equals(normalized, "地形图", StringComparison.Ordinal)
                || string.Equals(normalized, MaterialKindTopoMap, StringComparison.Ordinal)
                || string.Equals(normalized, PlacementSourceTopoMap, StringComparison.Ordinal))
            {
                return MaterialKindTopoMap;
            }

            if (string.Equals(normalized, MaterialKindDisplayAerialPhoto, StringComparison.Ordinal)
                || string.Equals(normalized, "航摄影像", StringComparison.Ordinal)
                || string.Equals(normalized, MaterialKindAerialPhoto, StringComparison.Ordinal)
                || string.Equals(normalized, PlacementSourceAerialPhoto, StringComparison.Ordinal))
            {
                return MaterialKindAerialPhoto;
            }

            if (string.Equals(normalized, MaterialKindDisplayOtherMap, StringComparison.Ordinal)
                || string.Equals(normalized, "其他图件", StringComparison.Ordinal)
                || string.Equals(normalized, MaterialKindOtherMap, StringComparison.Ordinal)
                || string.Equals(normalized, PlacementSourceOtherMap, StringComparison.Ordinal))
            {
                return MaterialKindOtherMap;
            }

            return normalized;
        }

        /// <summary>是否为有效资料类别。</summary>
        public static bool IsValidMaterialKind(string? materialKind)
        {
            string normalized = NormalizeMaterialKind(materialKind);
            return MaterialKindOptions.Any(item => string.Equals(item, normalized, StringComparison.Ordinal));
        }

        /// <summary>解析摆放表来源类型对应的资料类别；跨类混放返回空。</summary>
        public static string? TryResolveMaterialKindFromPlacementSource(string? sourceType)
        {
            string normalized = sourceType?.Trim() ?? string.Empty;
            if (string.Equals(normalized, PlacementSourceMixed, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Mixed", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string kind = NormalizeMaterialKind(normalized);
            return IsValidMaterialKind(kind) ? kind : null;
        }

        /// <summary>是否为有效处置方式。</summary>
        public static bool IsValidDispositionMethod(string? method)
        {
            string normalized = method?.Trim() ?? string.Empty;
            return DispositionMethodOptions.Any(item => string.Equals(item, normalized, StringComparison.Ordinal));
        }

        /// <summary>是否为离库销毁。</summary>
        public static bool IsDestroyMethod(string? method) =>
            string.Equals(method?.Trim(), MethodDestroy, StringComparison.Ordinal);

        /// <summary>是否为离库转交。</summary>
        public static bool IsTransferMethod(string? method) =>
            string.Equals(method?.Trim(), MethodTransfer, StringComparison.Ordinal);

        /// <summary>是否为其他方式。</summary>
        public static bool IsOtherMethod(string? method) =>
            string.Equals(method?.Trim(), MethodOther, StringComparison.Ordinal);

        /// <summary>是否需要填写转交对象。</summary>
        public static bool RequiresTransferTarget(string? method) => IsTransferMethod(method);

        /// <summary>是否需要填写其他说明。</summary>
        public static bool RequiresOtherRemark(string? method) => IsOtherMethod(method);

        /// <summary>是否需要处置资料照片。</summary>
        public static bool RequiresScenePhoto(string? method) => IsDestroyMethod(method);

        /// <summary>是否为处置资料照片分类（含旧版「处置现场照片」）。</summary>
        public static bool IsScenePhotoCategory(string? category)
        {
            string normalized = category?.Trim() ?? string.Empty;
            return string.Equals(normalized, AttachmentCategoryScenePhoto, StringComparison.Ordinal)
                || string.Equals(normalized, LegacyAttachmentCategoryScenePhoto, StringComparison.Ordinal);
        }

        /// <summary>是否为在库（含空值，兼容存量）。</summary>
        public static bool IsInStockLifecycle(string? status)
        {
            string normalized = NormalizeLifecycleStatus(status);
            return string.Equals(normalized, LifecycleInStock, StringComparison.Ordinal);
        }

        /// <summary>是否为离库锁定。</summary>
        public static bool IsLockedLifecycle(string? status) =>
            string.Equals(NormalizeLifecycleStatus(status), LifecycleLocked, StringComparison.Ordinal);

        /// <summary>是否为已离库。</summary>
        public static bool IsDisposedLifecycle(string? status) =>
            string.Equals(NormalizeLifecycleStatus(status), LifecycleDisposed, StringComparison.Ordinal);

        /// <summary>空生命周期视为在库。</summary>
        public static string NormalizeLifecycleStatus(string? status)
        {
            string normalized = status?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(normalized) ? LifecycleInStock : normalized;
        }

        /// <summary>未办结状态（占用盒）。</summary>
        public static bool IsActiveWorkflowStatus(int status) =>
            status is ApplicationWorkflowStatus.Draft
                or ApplicationWorkflowStatus.Submitted
                or ApplicationWorkflowStatus.Approved
                or ApplicationWorkflowStatus.SignedUploaded;

        /// <summary>工作流状态展示。</summary>
        public static string ToStatusDisplay(int status) => ApplicationWorkflowStatus.ToDisplay(status);

        /// <summary>组装台账关联键。</summary>
        public static string BuildSourceRecordKey(string materialKind, int recordId) =>
            $"{NormalizeMaterialKind(materialKind)}:{recordId}";
    }
}
