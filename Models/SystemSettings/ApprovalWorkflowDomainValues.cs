using DocMgr.Models.YearlyArchive;

namespace DocMgr.Models.SystemSettings
{
    /// <summary>审核审批规则域常量。</summary>
    public static class ApprovalWorkflowDomainValues
    {
        public const string ConditionLogicAnd = "And";
        public const string ConditionLogicOr = "Or";

        public const string NodeDeptHead = "DeptHead";
        public const string NodeArchiveRoomHead = "ArchiveRoomHead";
        public const string NodeProductionHead = "ProductionHead";
        public const string NodeArchiveDeputyPresident = "ArchiveDeputyPresident";
        public const string NodeProductionVicePresident = "ProductionVicePresident";

        /// <summary>申请审批单统一提示词：部门审核。</summary>
        public const string DisplayDeptHead = "部门审核";

        /// <summary>申请审批单统一提示词：生产科签字。</summary>
        public const string DisplayProductionHead = "生产科签字";

        /// <summary>申请审批单统一提示词：资料室签字。</summary>
        public const string DisplayArchiveRoomHead = "资料室签字";

        /// <summary>申请审批单统一提示词：分管资料院长签字。</summary>
        public const string DisplayArchiveDeputyPresident = "分管资料院长签字";

        /// <summary>申请审批单统一提示词：分管生产院长签字。</summary>
        public const string DisplayProductionVicePresident = "分管生产院长签字";

        public const string FieldArchivePurpose = "ArchivePurpose";
        public const string FieldDestinationKind = "DestinationKind";
        public const string FieldHasLoss = "HasLoss";
        public const string FieldSourceKind = "SourceKind";
        public const string FieldMaterialKind = "MaterialKind";
        public const string FieldApplicationType = "ApplicationType";
        public const string FieldDisposalReason = "DisposalReason";
        public const string FieldDispositionMethod = "DispositionMethod";
        public const string FieldMediaKind = "MediaKind";
        public const string FieldMediumKind = "MediumKind";
        public const string FieldAssetKind = "AssetKind";
        public const string FieldBeforeMediaStatus = "BeforeMediaStatus";
        public const string FieldHasProofMaterial = "HasProofMaterial";
        public const string FieldHasElectronicMedia = "HasElectronicMedia";
        public const string FieldHasSimulatedMedia = "HasSimulatedMedia";
        public const string FieldHasBorrowedHardDisk = "HasBorrowedHardDisk";
        public const string FieldHasNeedReturn = "HasNeedReturn";
        public const string FieldHasExpectedReturnDate = "HasExpectedReturnDate";
        public const string FieldReturnBorrowedHardDisk = "ReturnBorrowedHardDisk";
        public const string FieldHasFormatRetain = "HasFormatRetain";
        public const string FieldIsTransferMethod = "IsTransferMethod";
        public const string FieldHasMixedPlacement = "HasMixedPlacement";
        public const string FieldHasLossDescription = "HasLossDescription";
        public const string FieldHasTargetRegister = "HasTargetRegister";
        public const string FieldHasSourceOutbound = "HasSourceOutbound";
        public const string FieldHasSourceNetworkOutbound = "HasSourceNetworkOutbound";
        public const string FieldHasOtherRemark = "HasOtherRemark";
        public const string FieldSourceType = "SourceType";
        public const string FieldConfidentialLevel = "ConfidentialLevel";

        /// <summary>库管模式：院管资料、短期存档（与字段域种子一致）。</summary>
        public const string ArchivePurposeShortTermStorage = "院管资料、短期存档";

        /// <summary>密级条件选项（与字段域种子一致）。</summary>
        public static IReadOnlyList<string> ConfidentialLevelOptions { get; } =
        [
            ArchiveRegisterDomainValues.ConfidentialLevelNone,
            "秘密",
            "机密",
            "绝密"
        ];

        /// <summary>资料来源条件选项（申请单可选值，不含存量直办）。</summary>
        public static IReadOnlyList<string> SourceTypeOptions { get; } =
        [
            ArchiveRegisterDomainValues.SourceTypeInternal,
            ArchiveRegisterDomainValues.SourceTypeExternal
        ];

        public const string Yes = "是";
        public const string No = "否";

        public static IReadOnlyList<string> ConditionLogicOptions { get; } =
        [
            ConditionLogicAnd,
            ConditionLogicOr
        ];

        public static string ToConditionLogicDisplay(string? code) =>
            string.Equals(code?.Trim(), ConditionLogicOr, StringComparison.Ordinal)
                ? "或（OR）"
                : "且（AND）";

        /// <summary>按节点键返回申请审批单统一提示词。</summary>
        public static string ToNodeDisplay(string? nodeKey) =>
            nodeKey?.Trim() switch
            {
                NodeDeptHead => DisplayDeptHead,
                NodeProductionHead => DisplayProductionHead,
                NodeArchiveRoomHead => DisplayArchiveRoomHead,
                NodeArchiveDeputyPresident => DisplayArchiveDeputyPresident,
                NodeProductionVicePresident => DisplayProductionVicePresident,
                _ => nodeKey?.Trim() ?? string.Empty
            };

        /// <summary>院级节点启用时的签字栏标签（资料优先，否则生产）。</summary>
        public static string ResolveInstituteDisplay(bool enableArchiveDeputy, bool enableProductionVp)
        {
            if (enableArchiveDeputy)
            {
                return DisplayArchiveDeputyPresident;
            }

            if (enableProductionVp)
            {
                return DisplayProductionVicePresident;
            }

            return DisplayArchiveDeputyPresident;
        }
    }
}
