using DocMgr.Models.Shared;

namespace DocMgr.Models.NetworkTransfer
{
    /// <summary>
    /// 年度资料出入网管理域值与规则。
    /// </summary>
    public static class NetworkTransferDomainValues
    {
        public const string InboundAttachmentBusinessType = "NetworkInbound";
        public const string OutboundAttachmentBusinessType = "NetworkOutbound";
        public const string DisposalAttachmentBusinessType = "NetworkOnNetDisposal";

        public const string AttachmentCategorySignedForm = "签批单";
        public const string AttachmentCategoryUploadProof = "上传完成证明";
        public const string AttachmentCategoryOther = "其他附件";

        public const string AssetKindJobData = "作业数据";
        public const string AssetKindJobSoftware = "作业软件";
        public const string AssetKindSecuritySoftware = "安全软件";
        public const string AssetKindDocument = "文档资料";

        public const string SourceKindExternalOffline = "外部离线";
        public const string SourceKindArchivedElectronicSearch = "已立档资料";
        public const string SourceKindOther = "其他";

        public const string OriginKindInbound = "入网产生";
        public const string OriginKindProcessedOutput = "加工产出";

        public const string LifecycleOnNet = "在网";
        public const string LifecycleOutboundLocked = "出网中";
        public const string LifecycleOutbounded = "已出网";
        public const string LifecycleDisposalLocked = "处置中";
        public const string LifecycleDisposed = "已处置";

        public const string DestinationKindExternalOffline = "外部离线";
        public const string DestinationKindArchiveFiling = "资料室立档";
        public const string DestinationKindOther = "其他";

        public const string RegisterSourceTypeNetworkOutbound = "出网转入";

        public const string DisposalReasonExpired = "到期清理";
        public const string DisposalReasonIntermediate = "中间成果删除";
        public const string DisposalReasonUninstall = "软件卸载";
        public const string DisposalReasonSecureDestroy = "安全销毁";
        public const string DisposalReasonOther = "其他";

        public const string DisposalMethodDelete = "服务器删除";
        public const string DisposalMethodUnregister = "台账注销";
        public const string DisposalMethodOther = "其他";

        public static IReadOnlyList<string> AssetKindOptions { get; } =
        [
            AssetKindJobData,
            AssetKindJobSoftware,
            AssetKindSecuritySoftware,
            AssetKindDocument
        ];

        public static IReadOnlyList<string> SourceKindOptions { get; } =
        [
            SourceKindExternalOffline,
            SourceKindArchivedElectronicSearch,
            SourceKindOther
        ];

        public static IReadOnlyList<string> DestinationKindOptions { get; } =
        [
            DestinationKindExternalOffline,
            DestinationKindArchiveFiling,
            DestinationKindOther
        ];

        public static IReadOnlyList<string> DisposalReasonOptions { get; } =
        [
            DisposalReasonExpired,
            DisposalReasonIntermediate,
            DisposalReasonUninstall,
            DisposalReasonSecureDestroy,
            DisposalReasonOther
        ];

        public static IReadOnlyList<string> DisposalMethodOptions { get; } =
        [
            DisposalMethodDelete,
            DisposalMethodUnregister,
            DisposalMethodOther
        ];

        public static IReadOnlyList<string> AttachmentCategoryOptions { get; } =
        [
            AttachmentCategorySignedForm,
            AttachmentCategoryUploadProof,
            AttachmentCategoryOther
        ];

        public static bool IsArchivedElectronicSearchSource(string? sourceKind) =>
            string.Equals(sourceKind?.Trim(), SourceKindArchivedElectronicSearch, StringComparison.Ordinal);

        public static bool IsArchiveFilingDestination(string? destinationKind) =>
            string.Equals(destinationKind?.Trim(), DestinationKindArchiveFiling, StringComparison.Ordinal);

        public static bool IsProcessedOutputOrigin(string? originKind) =>
            string.Equals(originKind?.Trim(), OriginKindProcessedOutput, StringComparison.Ordinal);

        public static bool CanOutbound(string? originKind, string? lifecycleStatus) =>
            IsProcessedOutputOrigin(originKind)
            && string.Equals(lifecycleStatus?.Trim(), LifecycleOnNet, StringComparison.Ordinal);

        public static bool CanDispose(string? lifecycleStatus) =>
            string.Equals(lifecycleStatus?.Trim(), LifecycleOnNet, StringComparison.Ordinal);

        public static string ToStatusDisplay(int status) => ApplicationWorkflowStatus.ToDisplay(status);
    }

    /// <summary>
    /// 入网/出网工作台模式。
    /// </summary>
    public enum NetworkTransferWorkspaceMode
    {
        Application = 1,
        Approval = 2
    }
}
