using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Models.SystemSettings
{
    /// <summary>条件字段定义（业务白名单）。</summary>
    public sealed class ApprovalConditionFieldDefinition
    {
        public required string FieldKey { get; init; }

        public required string DisplayName { get; init; }

        public required IReadOnlyList<(string Value, string Label)> Options { get; init; }
    }

    /// <summary>业务类型元数据：显示名 + 可选条件字段（规则最多选用其中 2 个）。</summary>
    public sealed class ApprovalBusinessTypeDefinition
    {
        public required string BusinessType { get; init; }

        public required string DisplayName { get; init; }

        public required IReadOnlyList<ApprovalConditionFieldDefinition> ConditionFields { get; init; }
    }

    /// <summary>审核审批配置目录：业务类型与整单级条件字段白名单。</summary>
    public static class ApprovalWorkflowCatalog
    {
        private static ApprovalConditionFieldDefinition YesNoField(string fieldKey, string displayName) => new()
        {
            FieldKey = fieldKey,
            DisplayName = displayName,
            Options =
            [
                (ApprovalWorkflowDomainValues.Yes, "是"),
                (ApprovalWorkflowDomainValues.No, "否")
            ]
        };

        private static readonly ApprovalConditionFieldDefinition HasLossField =
            YesNoField(ApprovalWorkflowDomainValues.FieldHasLoss, "是否灭失");

        private static readonly ApprovalConditionFieldDefinition HasProofMaterialField =
            YesNoField(ApprovalWorkflowDomainValues.FieldHasProofMaterial, "是否有证明材料");

        private static readonly ApprovalConditionFieldDefinition HasElectronicMediaField =
            YesNoField(ApprovalWorkflowDomainValues.FieldHasElectronicMedia, "是否含电子介质");

        private static readonly ApprovalConditionFieldDefinition HasSimulatedMediaField =
            YesNoField(ApprovalWorkflowDomainValues.FieldHasSimulatedMedia, "是否含模拟介质");

        private static readonly ApprovalConditionFieldDefinition HasBorrowedHardDiskField =
            YesNoField(ApprovalWorkflowDomainValues.FieldHasBorrowedHardDisk, "是否借用硬盘");

        private static readonly ApprovalConditionFieldDefinition HasNeedReturnField =
            YesNoField(ApprovalWorkflowDomainValues.FieldHasNeedReturn, "是否需归还");

        private static readonly ApprovalConditionFieldDefinition HasExpectedReturnDateField =
            YesNoField(ApprovalWorkflowDomainValues.FieldHasExpectedReturnDate, "是否填写预计归还日期");

        private static readonly ApprovalConditionFieldDefinition ReturnBorrowedHardDiskField =
            YesNoField(ApprovalWorkflowDomainValues.FieldReturnBorrowedHardDisk, "入网时归还借用硬盘");

        private static readonly ApprovalConditionFieldDefinition HasFormatRetainField =
            YesNoField(ApprovalWorkflowDomainValues.FieldHasFormatRetain, "是否含低格留盘");

        private static readonly ApprovalConditionFieldDefinition IsTransferMethodField =
            YesNoField(ApprovalWorkflowDomainValues.FieldIsTransferMethod, "是否离库转交");

        private static readonly ApprovalConditionFieldDefinition HasMixedPlacementField =
            YesNoField(ApprovalWorkflowDomainValues.FieldHasMixedPlacement, "是否含混放盒");

        private static readonly ApprovalConditionFieldDefinition HasLossDescriptionField =
            YesNoField(ApprovalWorkflowDomainValues.FieldHasLossDescription, "是否填写灭失说明");

        private static readonly ApprovalConditionFieldDefinition HasTargetRegisterField =
            YesNoField(ApprovalWorkflowDomainValues.FieldHasTargetRegister, "是否关联建档申请");

        private static readonly ApprovalConditionFieldDefinition HasSourceOutboundField =
            YesNoField(ApprovalWorkflowDomainValues.FieldHasSourceOutbound, "是否关联资料出库单");

        private static readonly ApprovalConditionFieldDefinition HasSourceNetworkOutboundField =
            YesNoField(ApprovalWorkflowDomainValues.FieldHasSourceNetworkOutbound, "是否关联出网申请单");

        private static readonly ApprovalConditionFieldDefinition HasOtherRemarkField =
            YesNoField(ApprovalWorkflowDomainValues.FieldHasOtherRemark, "是否填写其他说明");

        private static readonly ApprovalConditionFieldDefinition OutboundDestinationField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldDestinationKind,
            DisplayName = "资料去向",
            Options =
            [
                (ArchiveOutboundDomainValues.DestinationInternal, "院内"),
                (ArchiveOutboundDomainValues.DestinationExternal, "院外")
            ]
        };

        private static readonly ApprovalConditionFieldDefinition NetworkDestinationField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldDestinationKind,
            DisplayName = "目的地类别",
            Options = NetworkTransferDomainValues.OutboundDestinationKindOptions
                .Select(value => (value, value))
                .ToList()
        };

        private static readonly ApprovalConditionFieldDefinition ArchivePurposeField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldArchivePurpose,
            DisplayName = "库管模式",
            Options =
            [
                (ApprovalWorkflowDomainValues.ArchivePurposeShortTermStorage,
                    ApprovalWorkflowDomainValues.ArchivePurposeShortTermStorage),
                (ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage,
                    ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage),
                (ArchiveRegisterDomainValues.ArchivePurposeExternalEntrusted,
                    ArchiveRegisterDomainValues.ArchivePurposeExternalEntrusted)
            ]
        };

        private static readonly ApprovalConditionFieldDefinition NetworkSourceKindField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldSourceKind,
            DisplayName = "来源类别",
            Options = NetworkTransferDomainValues.SourceKindOptions
                .Select(value => (value, value))
                .ToList()
        };

        private static readonly ApprovalConditionFieldDefinition HardDiskOutboundApplicationTypeField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldApplicationType,
            DisplayName = "申请类型",
            Options =
            [
                (HardDiskMediaApplication.TypeOutboundTemporary, HardDiskMediaApplication.TypeOutboundTemporary),
                (HardDiskMediaApplication.TypeOutboundLongTerm, HardDiskMediaApplication.TypeOutboundLongTerm),
                (HardDiskMediaApplication.TypeOutboundPermanent, HardDiskMediaApplication.TypeOutboundPermanent)
            ]
        };

        private static readonly ApprovalConditionFieldDefinition HardDiskReturnApplicationTypeField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldApplicationType,
            DisplayName = "申请类型",
            Options =
            [
                (HardDiskMediaApplication.TypeReturnBlankRegistration, HardDiskMediaApplication.TypeReturnBlankRegistration),
                (HardDiskMediaApplication.TypeReturnDataRegistration, HardDiskMediaApplication.TypeReturnDataRegistration),
                (HardDiskMediaApplication.TypeReturnDamagedRegistration, HardDiskMediaApplication.TypeReturnDamagedRegistration),
                (HardDiskMediaApplication.TypeLossRegistration, HardDiskMediaApplication.TypeLossRegistration)
            ]
        };

        private static readonly ApprovalConditionFieldDefinition HardDiskDestinationKindField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldDestinationKind,
            DisplayName = "目标去向",
            Options =
            [
                (HardDiskMediaApplication.DestinationKindInternal,
                    HardDiskMediaApplication.DestinationKindInternal),
                (HardDiskMediaApplication.DestinationKindExternal,
                    HardDiskMediaApplication.DestinationKindExternal)
            ]
        };

        private static readonly ApprovalConditionFieldDefinition MediaKindField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldMediaKind,
            DisplayName = "介质类别",
            Options =
            [
                (ArchiveRegisterDomainValues.MediaKindElectronic, "电子"),
                (ArchiveRegisterDomainValues.MediaKindSimulated, "模拟")
            ]
        };

        private static readonly ApprovalConditionFieldDefinition MediumKindField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldMediumKind,
            DisplayName = "载体类型",
            Options =
            [
                (ArchiveInventoryRegisterDomainValues.MediumKindHardDisk, "硬盘"),
                (ArchiveInventoryRegisterDomainValues.MediumKindOpticalDisc, "光盘")
            ]
        };

        private static readonly ApprovalConditionFieldDefinition MaterialKindField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldMaterialKind,
            DisplayName = "资料类别",
            Options =
            [
                (HistoryArchiveDisposalDomainValues.MaterialKindTopoMap,
                    HistoryArchiveDisposalDomainValues.MaterialKindDisplayTopoMap),
                (HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto,
                    HistoryArchiveDisposalDomainValues.MaterialKindDisplayAerialPhoto),
                (HistoryArchiveDisposalDomainValues.MaterialKindOtherMap,
                    HistoryArchiveDisposalDomainValues.MaterialKindDisplayOtherMap)
            ]
        };

        private static readonly ApprovalConditionFieldDefinition AssetKindField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldAssetKind,
            DisplayName = "资料类别",
            Options = NetworkTransferDomainValues.AssetKindOptions
                .Select(value => (value, value))
                .ToList()
        };

        private static readonly ApprovalConditionFieldDefinition BeforeMediaStatusField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldBeforeMediaStatus,
            DisplayName = "处置前介质状态",
            Options = HardDiskDisposalDomainValues.SelectableMediaStatusOptions
                .Select(value => (value, value))
                .ToList()
        };

        private static readonly ApprovalConditionFieldDefinition HardDiskInventoryRegisterKindField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldRegisterKind,
            DisplayName = "登记类型",
            Options = HardDiskInventoryRegisterDomainValues.RegisterKindOptions
                .Select(value => (value, value))
                .ToList()
        };

        private static readonly ApprovalConditionFieldDefinition YearlyInventoryRegisterKindField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldRegisterKind,
            DisplayName = "登记类型",
            Options = ArchiveInventoryRegisterDomainValues.RegisterKindOptions
                .Select(value => (value, value))
                .ToList()
        };

        private static readonly ApprovalConditionFieldDefinition YearlyDisposalReasonField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldDisposalReason,
            DisplayName = "离库原因",
            Options = ArchiveDisposalDomainValues.ReasonOptions
                .Select(value => (value, value))
                .ToList()
        };

        private static readonly ApprovalConditionFieldDefinition YearlyDispositionMethodField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldDispositionMethod,
            DisplayName = "处置方式",
            Options = ArchiveDisposalDomainValues.DispositionMethodOptions
                .Select(value => (value, value))
                .ToList()
        };

        private static readonly ApprovalConditionFieldDefinition HistoryDispositionMethodField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldDispositionMethod,
            DisplayName = "处置方式",
            Options = HistoryArchiveDisposalDomainValues.DispositionMethodOptions
                .Select(value => (value, value))
                .ToList()
        };

        private static readonly ApprovalConditionFieldDefinition NetworkDisposalReasonField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldDisposalReason,
            DisplayName = "处置原因",
            Options = NetworkTransferDomainValues.DisposalReasonOptions
                .Select(value => (value, value))
                .ToList()
        };

        private static readonly ApprovalConditionFieldDefinition NetworkDispositionMethodField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldDispositionMethod,
            DisplayName = "处置方式",
            Options = NetworkTransferDomainValues.DisposalMethodOptions
                .Select(value => (value, value))
                .ToList()
        };

        private static readonly ApprovalConditionFieldDefinition HardDiskDisposalReasonField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldDisposalReason,
            DisplayName = "离库原因",
            Options = HardDiskDisposalDomainValues.ReasonOptions
                .Select(value => (value, value))
                .ToList()
        };

        private static readonly ApprovalConditionFieldDefinition HardDiskDispositionMethodField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldDispositionMethod,
            DisplayName = "处置方式",
            Options = HardDiskDisposalDomainValues.DispositionMethodOptions
                .Select(value => (value, value))
                .ToList()
        };

        private static readonly ApprovalConditionFieldDefinition SourceTypeField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldSourceType,
            DisplayName = "资料来源",
            Options = ApprovalWorkflowDomainValues.SourceTypeOptions
                .Select(value => (value, value))
                .ToList()
        };

        private static readonly ApprovalConditionFieldDefinition ConfidentialLevelField = new()
        {
            FieldKey = ApprovalWorkflowDomainValues.FieldConfidentialLevel,
            DisplayName = "密级",
            Options = ApprovalWorkflowDomainValues.ConfidentialLevelOptions
                .Select(value => (value, value))
                .ToList()
        };

        public static IReadOnlyList<ApprovalBusinessTypeDefinition> BusinessTypes { get; } =
        [
            new()
            {
                BusinessType = ApprovalWorkflowBusinessTypes.YearlyArchiveRegister,
                DisplayName = ApprovalWorkflowBusinessTypes.ToDisplay(ApprovalWorkflowBusinessTypes.YearlyArchiveRegister),
                ConditionFields =
                [
                    ArchivePurposeField,
                    SourceTypeField,
                    ConfidentialLevelField,
                    HasElectronicMediaField,
                    HasSimulatedMediaField,
                    HasProofMaterialField,
                    HasBorrowedHardDiskField
                ]
            },
            new()
            {
                BusinessType = ApprovalWorkflowBusinessTypes.YearlyArchiveOutbound,
                DisplayName = ApprovalWorkflowBusinessTypes.ToDisplay(ApprovalWorkflowBusinessTypes.YearlyArchiveOutbound),
                ConditionFields =
                [
                    OutboundDestinationField,
                    MediaKindField,
                    ConfidentialLevelField,
                    HasProofMaterialField,
                    HasNeedReturnField,
                    HasExpectedReturnDateField
                ]
            },
            new()
            {
                BusinessType = ApprovalWorkflowBusinessTypes.YearlyArchiveReturn,
                DisplayName = ApprovalWorkflowBusinessTypes.ToDisplay(ApprovalWorkflowBusinessTypes.YearlyArchiveReturn),
                ConditionFields =
                [
                    HasLossField,
                    HasLossDescriptionField,
                    OutboundDestinationField,
                    MediaKindField,
                    ConfidentialLevelField,
                    HasElectronicMediaField
                ]
            },
            new()
            {
                BusinessType = ApprovalWorkflowBusinessTypes.NetworkInbound,
                DisplayName = ApprovalWorkflowBusinessTypes.ToDisplay(ApprovalWorkflowBusinessTypes.NetworkInbound),
                ConditionFields =
                [
                    NetworkSourceKindField,
                    HasProofMaterialField,
                    ReturnBorrowedHardDiskField,
                    AssetKindField,
                    ConfidentialLevelField
                ]
            },
            new()
            {
                BusinessType = ApprovalWorkflowBusinessTypes.NetworkOutbound,
                DisplayName = ApprovalWorkflowBusinessTypes.ToDisplay(ApprovalWorkflowBusinessTypes.NetworkOutbound),
                ConditionFields =
                [
                    NetworkDestinationField,
                    ArchivePurposeField,
                    HasProofMaterialField,
                    HasTargetRegisterField,
                    AssetKindField,
                    ConfidentialLevelField
                ]
            },
            new()
            {
                BusinessType = ApprovalWorkflowBusinessTypes.YearlyArchiveDisposal,
                DisplayName = ApprovalWorkflowBusinessTypes.ToDisplay(ApprovalWorkflowBusinessTypes.YearlyArchiveDisposal),
                ConditionFields =
                [
                    MediaKindField,
                    YearlyDisposalReasonField,
                    YearlyDispositionMethodField,
                    HasFormatRetainField,
                    MediumKindField
                ]
            },
            new()
            {
                BusinessType = ApprovalWorkflowBusinessTypes.HistoryArchiveDisposal,
                DisplayName = ApprovalWorkflowBusinessTypes.ToDisplay(ApprovalWorkflowBusinessTypes.HistoryArchiveDisposal),
                ConditionFields =
                [
                    MaterialKindField,
                    HistoryDispositionMethodField,
                    IsTransferMethodField,
                    HasMixedPlacementField,
                    HasOtherRemarkField
                ]
            },
            new()
            {
                BusinessType = ApprovalWorkflowBusinessTypes.NetworkOnNetDisposal,
                DisplayName = ApprovalWorkflowBusinessTypes.ToDisplay(ApprovalWorkflowBusinessTypes.NetworkOnNetDisposal),
                ConditionFields =
                [
                    NetworkDisposalReasonField,
                    NetworkDispositionMethodField,
                    AssetKindField,
                    HasOtherRemarkField
                ]
            },
            new()
            {
                BusinessType = ApprovalWorkflowBusinessTypes.HardDiskOutbound,
                DisplayName = ApprovalWorkflowBusinessTypes.ToDisplay(ApprovalWorkflowBusinessTypes.HardDiskOutbound),
                ConditionFields =
                [
                    HardDiskOutboundApplicationTypeField,
                    HardDiskDestinationKindField,
                    HasExpectedReturnDateField,
                    HasSourceOutboundField,
                    HasSourceNetworkOutboundField
                ]
            },
            new()
            {
                BusinessType = ApprovalWorkflowBusinessTypes.HardDiskReturn,
                DisplayName = ApprovalWorkflowBusinessTypes.ToDisplay(ApprovalWorkflowBusinessTypes.HardDiskReturn),
                ConditionFields =
                [
                    HardDiskReturnApplicationTypeField
                ]
            },
            new()
            {
                BusinessType = ApprovalWorkflowBusinessTypes.HardDiskDisposal,
                DisplayName = ApprovalWorkflowBusinessTypes.ToDisplay(ApprovalWorkflowBusinessTypes.HardDiskDisposal),
                ConditionFields =
                [
                    HardDiskDisposalReasonField,
                    HardDiskDispositionMethodField,
                    BeforeMediaStatusField,
                    HasOtherRemarkField
                ]
            },
            new()
            {
                BusinessType = ApprovalWorkflowBusinessTypes.HardDiskInventoryRegister,
                DisplayName = ApprovalWorkflowBusinessTypes.ToDisplay(ApprovalWorkflowBusinessTypes.HardDiskInventoryRegister),
                ConditionFields =
                [
                    HardDiskInventoryRegisterKindField
                ]
            },
            new()
            {
                BusinessType = ApprovalWorkflowBusinessTypes.YearlyArchiveInventoryRegister,
                DisplayName = ApprovalWorkflowBusinessTypes.ToDisplay(ApprovalWorkflowBusinessTypes.YearlyArchiveInventoryRegister),
                ConditionFields =
                [
                    MediaKindField,
                    YearlyInventoryRegisterKindField
                ]
            }
        ];

        public static ApprovalBusinessTypeDefinition? FindBusinessType(string? businessType)
        {
            string key = businessType?.Trim() ?? string.Empty;
            return BusinessTypes.FirstOrDefault(item =>
                string.Equals(item.BusinessType, key, StringComparison.Ordinal));
        }

        public static ApprovalConditionFieldDefinition? FindConditionField(string? businessType, string? fieldKey)
        {
            var definition = FindBusinessType(businessType);
            if (definition == null)
            {
                return null;
            }

            string key = fieldKey?.Trim() ?? string.Empty;
            return definition.ConditionFields.FirstOrDefault(item =>
                string.Equals(item.FieldKey, key, StringComparison.Ordinal));
        }
    }
}
