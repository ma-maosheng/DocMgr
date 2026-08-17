using DocMgr.Models.Shared;
using DocMgr.Models.YearlyArchive;

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
        public const string AttachmentCategoryProofMaterial = "证明材料";
        public const string AttachmentCategoryOther = "其他附件";

        public const string AssetKindJobData = "作业数据";
        public const string AssetKindJobSoftware = "作业软件";
        public const string AssetKindSecuritySoftware = "安全软件";
        public const string AssetKindDocument = "文档资料";

        public const string SourceKindExternalOfflineInternal = "档外资料（院内）";
        public const string SourceKindExternalOfflineExternal = "档外资料（院外）";
        public const string SourceKindArchivedElectronicSearch = "存档资料";

        /// <summary>入网存档来源对应的提供部门固定值。</summary>
        public const string InboundProvideUnitArchiveRoom = "资料室";

        /// <summary>历史数据源类别（兼容旧数据）。</summary>
        public const string LegacySourceKindExternalOffline = "外部离线";

        /// <summary>历史数据源类别（兼容旧数据，归一化为档外资料（院内））。</summary>
        public const string LegacySourceKindExternalOfflineLabel = "档外资料";

        /// <summary>历史数据源类别（兼容旧数据，归一化为档外资料（院内））。</summary>
        public const string LegacySourceKindExternalOfflineInternal = "档外资料（内部）";

        /// <summary>历史数据源类别（兼容旧数据，归一化为档外资料（院外））。</summary>
        public const string LegacySourceKindExternalOfflineExternal = "档外资料（外部）";

        /// <summary>历史数据源类别（兼容旧数据，归一化为存档资料）。</summary>
        public const string LegacySourceKindArchivedElectronicSearchLabel = "立档资料";

        /// <summary>历史数据源类别（兼容旧数据，归一化为存档资料）。</summary>
        public const string LegacySourceKindArchivedElectronicSearch = "已立档资料";

        /// <summary>历史数据源类别（兼容旧数据）。</summary>
        public const string LegacySourceKindOther = "其他";

        public const string OriginKindInbound = "入网产生";
        public const string OriginKindProcessedOutput = "加工产出";

        public const string LifecycleOnNet = "在网";
        public const string LifecycleOutboundLocked = "出网中";
        public const string LifecycleOutbounded = "已出网";
        public const string LifecycleDisposalLocked = "处置中";
        public const string LifecycleDisposed = "已处置";

        public const string DestinationKindOutboundInternal = "出网（院内）";
        public const string DestinationKindOutboundExternal = "出网（院外）";
        public const string DestinationKindArchiveFiling = "资料室存档";
        public const string DestinationKindOther = "其他";

        /// <summary>出网外部离线场景下的电子介质处置方式（拷贝后介质由申请人带走）。</summary>
        public const string OutboundElectronicDispositionTakeAway = "介质带走";

        /// <summary>历史出网外部离线处置方式（兼容旧数据，语义同「介质带走」）。</summary>
        public const string LegacyOutboundElectronicDispositionReturn = "介质带回";

        /// <summary>历史出网目的地（兼容旧数据，归一化为出网（院内））。</summary>
        public const string LegacyDestinationKindExternalOffline = "外部离线";

        /// <summary>历史出网目的地（兼容旧数据）。</summary>
        public const string LegacyDestinationKindArchiveFiling = "资料室立档";

        public const string RegisterSourceTypeNetworkOutbound = "出网转入";

        public const string BusinessTypeInbound = "NetworkInbound";
        public const string BusinessTypeOutbound = "NetworkOutbound";
        public const string BusinessTypeArchiveRegister = "YearlyArchiveRegister";
        public const string BusinessTypeArchiveMaterialTransaction = "YearlyArchiveMaterialTransaction";
        public const string BusinessTypeHardDiskReturn = "HardDiskReturn";

        public const string ScenarioArchivedElectronicInbound = "ArchivedElectronicInbound";
        public const string ScenarioExternalOfflineInbound = "ExternalOfflineInbound";
        public const string ScenarioOutboundToArchive = "OutboundToArchive";
        public const string ScenarioOutboundToExternal = "OutboundToExternal";

        public const string TaskKindPrimaryApplication = "PrimaryApplication";
        public const string TaskKindArchiveCopy = "ArchiveCopy";
        public const string TaskKindOnNetRegistration = "OnNetRegistration";
        public const string TaskKindArchiveRegister = "ArchiveRegister";
        public const string TaskKindHardDiskReturn = "HardDiskReturn";

        public const string BusinessTaskStatusPending = "Pending";
        public const string BusinessTaskStatusInProgress = "InProgress";
        public const string BusinessTaskStatusCompleted = "Completed";
        public const string BusinessTaskStatusCancelled = "Cancelled";

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
            SourceKindArchivedElectronicSearch,
            SourceKindExternalOfflineInternal,
            SourceKindExternalOfflineExternal
        ];

        public static IReadOnlyList<string> DestinationKindOptions { get; } =
        [
            DestinationKindOutboundInternal,
            DestinationKindOutboundExternal,
            DestinationKindArchiveFiling,
            DestinationKindOther
        ];

        /// <summary>出网编辑页目的地下拉（不含「其他」）。</summary>
        public static IReadOnlyList<string> OutboundDestinationKindOptions { get; } =
        [
            DestinationKindOutboundInternal,
            DestinationKindOutboundExternal,
            DestinationKindArchiveFiling
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
            AttachmentCategoryOther
        ];

        public static string ResolveInboundScenarioKind(string? sourceKind) =>
            IsArchivedElectronicSearchSource(sourceKind)
                ? ScenarioArchivedElectronicInbound
                : ScenarioExternalOfflineInbound;

        public static string ResolveOutboundScenarioKind(string? destinationKind) =>
            IsArchiveFilingDestination(destinationKind)
                ? ScenarioOutboundToArchive
                : ScenarioOutboundToExternal;

        public static string ToBusinessTaskStatusDisplay(string? status) =>
            status?.Trim() switch
            {
                BusinessTaskStatusPending => "待处理",
                BusinessTaskStatusInProgress => "进行中",
                BusinessTaskStatusCompleted => "已完成",
                BusinessTaskStatusCancelled => "已取消",
                _ => "未建立"
            };

        /// <summary>
        /// 入网附件分类下拉：签批单 →（有声明时）证明材料 → 其他附件。
        /// </summary>
        public static IReadOnlyList<string> BuildInboundAttachmentCategoryOptions(bool hasProofMaterial) =>
            BuildAttachmentCategoryOptions(hasProofMaterial);

        /// <summary>
        /// 出网附件分类下拉：签批单 →（有声明时）证明材料 → 其他附件。
        /// </summary>
        public static IReadOnlyList<string> BuildOutboundAttachmentCategoryOptions(bool hasProofMaterial) =>
            BuildAttachmentCategoryOptions(hasProofMaterial);

        private static IReadOnlyList<string> BuildAttachmentCategoryOptions(bool hasProofMaterial)
        {
            if (!hasProofMaterial)
            {
                return AttachmentCategoryOptions;
            }

            return
            [
                AttachmentCategorySignedForm,
                AttachmentCategoryProofMaterial,
                AttachmentCategoryOther
            ];
        }

        public static bool IsArchivedElectronicSearchSource(string? sourceKind)
        {
            string trimmed = sourceKind?.Trim() ?? string.Empty;
            return string.Equals(trimmed, SourceKindArchivedElectronicSearch, StringComparison.Ordinal)
                   || string.Equals(trimmed, LegacySourceKindArchivedElectronicSearchLabel, StringComparison.Ordinal)
                   || string.Equals(trimmed, LegacySourceKindArchivedElectronicSearch, StringComparison.Ordinal);
        }

        /// <summary>是否为档外资料（院内）入网来源。</summary>
        public static bool IsExternalOfflineInternalSource(string? sourceKind)
        {
            string trimmed = sourceKind?.Trim() ?? string.Empty;
            return string.Equals(trimmed, SourceKindExternalOfflineInternal, StringComparison.Ordinal)
                   || string.Equals(trimmed, LegacySourceKindExternalOfflineInternal, StringComparison.Ordinal)
                   || string.Equals(trimmed, LegacySourceKindExternalOfflineLabel, StringComparison.Ordinal)
                   || string.Equals(trimmed, LegacySourceKindExternalOffline, StringComparison.Ordinal);
        }

        /// <summary>是否为档外资料（院外）入网来源。</summary>
        public static bool IsExternalOfflineExternalSource(string? sourceKind)
        {
            string trimmed = sourceKind?.Trim() ?? string.Empty;
            return string.Equals(trimmed, SourceKindExternalOfflineExternal, StringComparison.Ordinal)
                   || string.Equals(trimmed, LegacySourceKindExternalOfflineExternal, StringComparison.Ordinal);
        }

        /// <summary>是否为档外资料入网来源（院内或院外）。</summary>
        public static bool IsExternalOfflineSource(string? sourceKind) =>
            IsExternalOfflineInternalSource(sourceKind)
            || IsExternalOfflineExternalSource(sourceKind);

        /// <summary>是否为有效的入网数据来源（含历史值）。</summary>
        public static bool IsValidSourceKind(string? sourceKind)
        {
            string trimmed = sourceKind?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            return SourceKindOptions.Contains(trimmed, StringComparer.Ordinal)
                   || string.Equals(trimmed, LegacySourceKindExternalOfflineInternal, StringComparison.Ordinal)
                   || string.Equals(trimmed, LegacySourceKindExternalOfflineExternal, StringComparison.Ordinal)
                   || string.Equals(trimmed, LegacySourceKindExternalOfflineLabel, StringComparison.Ordinal)
                   || string.Equals(trimmed, LegacySourceKindExternalOffline, StringComparison.Ordinal)
                   || string.Equals(trimmed, LegacySourceKindArchivedElectronicSearchLabel, StringComparison.Ordinal)
                   || string.Equals(trimmed, LegacySourceKindArchivedElectronicSearch, StringComparison.Ordinal)
                   || string.Equals(trimmed, LegacySourceKindOther, StringComparison.Ordinal);
        }

        /// <summary>将历史数据来源归一化为当前选项值。</summary>
        public static string NormalizeSourceKind(string? sourceKind)
        {
            string trimmed = sourceKind?.Trim() ?? string.Empty;
            if (string.Equals(trimmed, LegacySourceKindExternalOfflineLabel, StringComparison.Ordinal)
                || string.Equals(trimmed, LegacySourceKindExternalOffline, StringComparison.Ordinal)
                || string.Equals(trimmed, LegacySourceKindExternalOfflineInternal, StringComparison.Ordinal)
                || string.Equals(trimmed, LegacySourceKindOther, StringComparison.Ordinal))
            {
                return SourceKindExternalOfflineInternal;
            }

            if (string.Equals(trimmed, LegacySourceKindExternalOfflineExternal, StringComparison.Ordinal))
            {
                return SourceKindExternalOfflineExternal;
            }

            if (string.Equals(trimmed, LegacySourceKindArchivedElectronicSearchLabel, StringComparison.Ordinal)
                || string.Equals(trimmed, LegacySourceKindArchivedElectronicSearch, StringComparison.Ordinal))
            {
                return SourceKindArchivedElectronicSearch;
            }

            return trimmed;
        }

        /// <summary>按数据来源解析入网提供部门（单位）。</summary>
        public static string ResolveInboundProvideUnit(string? sourceKind, string? provideUnit)
        {
            if (IsArchivedElectronicSearchSource(sourceKind))
            {
                return InboundProvideUnitArchiveRoom;
            }

            return provideUnit?.Trim() ?? string.Empty;
        }

        public static bool IsArchiveFilingDestination(string? destinationKind)
        {
            string trimmed = destinationKind?.Trim() ?? string.Empty;
            return string.Equals(trimmed, DestinationKindArchiveFiling, StringComparison.Ordinal)
                   || string.Equals(trimmed, LegacyDestinationKindArchiveFiling, StringComparison.Ordinal);
        }

        /// <summary>是否为出网（院内/院外）目的地（含历史「外部离线」）。</summary>
        public static bool IsExternalOfflineDestination(string? destinationKind)
        {
            string trimmed = destinationKind?.Trim() ?? string.Empty;
            return string.Equals(trimmed, DestinationKindOutboundInternal, StringComparison.Ordinal)
                   || string.Equals(trimmed, DestinationKindOutboundExternal, StringComparison.Ordinal)
                   || string.Equals(trimmed, LegacyDestinationKindExternalOffline, StringComparison.Ordinal);
        }

        /// <summary>是否为出网（院外）目的地。</summary>
        public static bool IsOutboundExternalDestination(string? destinationKind) =>
            string.Equals(destinationKind?.Trim(), DestinationKindOutboundExternal, StringComparison.Ordinal);

        /// <summary>出网单分管领导默认审批角色：出网（院内/院外）→分管生产副院长，否则→分管资料副院长。</summary>
        public static string ResolveOutboundDeputyLeaderRole(string? destinationKind) =>
            IsExternalOfflineDestination(destinationKind)
                ? "分管生产副院长"
                : "分管资料副院长";

        /// <summary>将历史出网目的地归一化为当前选项值。</summary>
        public static string NormalizeOutboundDestinationKind(string? destinationKind)
        {
            string trimmed = destinationKind?.Trim() ?? string.Empty;
            if (string.Equals(trimmed, LegacyDestinationKindExternalOffline, StringComparison.Ordinal))
            {
                return DestinationKindOutboundInternal;
            }

            if (string.Equals(trimmed, LegacyDestinationKindArchiveFiling, StringComparison.Ordinal))
            {
                return DestinationKindArchiveFiling;
            }

            return trimmed;
        }

        /// <summary>是否为有效的出网目的地（含历史值只读识别）。</summary>
        public static bool IsValidOutboundDestinationKind(string? destinationKind)
        {
            string trimmed = destinationKind?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            return OutboundDestinationKindOptions.Contains(trimmed, StringComparer.Ordinal)
                   || string.Equals(trimmed, DestinationKindOther, StringComparison.Ordinal)
                   || string.Equals(trimmed, LegacyDestinationKindExternalOffline, StringComparison.Ordinal)
                   || string.Equals(trimmed, LegacyDestinationKindArchiveFiling, StringComparison.Ordinal);
        }

        /// <summary>新出网单是否允许保存/提交的目的地值。</summary>
        public static bool IsAllowedOutboundDestinationKind(string? destinationKind) =>
            OutboundDestinationKindOptions.Contains(
                destinationKind?.Trim() ?? string.Empty,
                StringComparer.Ordinal);

        /// <summary>是否为出网「介质带走」处置（含历史「介质带回」）。</summary>
        public static bool IsOutboundTakeAwayDisposition(string? disposition)
        {
            string trimmed = disposition?.Trim() ?? string.Empty;
            return string.Equals(trimmed, OutboundElectronicDispositionTakeAway, StringComparison.Ordinal)
                   || string.Equals(trimmed, LegacyOutboundElectronicDispositionReturn, StringComparison.Ordinal);
        }

        /// <summary>将出网外部离线处置方式归一化为当前选项值。</summary>
        public static string NormalizeOutboundTakeAwayDisposition(string? disposition)
        {
            return IsOutboundTakeAwayDisposition(disposition)
                ? OutboundElectronicDispositionTakeAway
                : disposition?.Trim() ?? string.Empty;
        }

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
