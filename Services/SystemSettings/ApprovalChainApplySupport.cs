using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.SystemSettings
{
    /// <summary>将签批链解析结果写入各业务单据默认签字字段（仅填充空值）。</summary>
    public static class ApprovalChainApplySupport
    {
        public static void ApplyToRegister(YearlyArchiveRegisterRecord record, ApprovalChainResolution chain, DateTime now)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(chain);
            ApplyFiveNodeSigners(
                chain,
                now,
                (name, date) =>
                {
                    record.DeptHead = Coalesce(record.DeptHead, name);
                    record.DeptHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ArchiveRoomHead = Coalesce(record.ArchiveRoomHead, name);
                    record.ArchiveRoomHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ProductionHead = Coalesce(record.ProductionHead, name);
                    record.ProductionHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ArchiveDeputyPresident = Coalesce(record.ArchiveDeputyPresident, name);
                    record.ArchiveDeputyPresidentDate ??= date;
                },
                (name, date) =>
                {
                    record.ProductionVicePresident = Coalesce(record.ProductionVicePresident, name);
                    record.ProductionVicePresidentDate ??= date;
                });
        }

        public static void ApplyToInbound(NetworkInboundRecord record, ApprovalChainResolution chain, DateTime now)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(chain);
            ApplyFiveNodeSigners(
                chain,
                now,
                (name, date) =>
                {
                    record.DeptHead = Coalesce(record.DeptHead, name);
                    record.DeptHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ArchiveRoomHead = Coalesce(record.ArchiveRoomHead, name);
                    record.ArchiveRoomHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ProductionHead = Coalesce(record.ProductionHead, name);
                    record.ProductionHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ArchiveDeputyPresident = Coalesce(record.ArchiveDeputyPresident, name);
                    record.ArchiveDeputyPresidentDate ??= date;
                },
                (name, date) =>
                {
                    record.ProductionVicePresident = Coalesce(record.ProductionVicePresident, name);
                    record.ProductionVicePresidentDate ??= date;
                });
        }

        public static void ApplyToNetworkOutbound(NetworkOutboundRecord record, ApprovalChainResolution chain, DateTime now)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(chain);
            ApplyFiveNodeSigners(
                chain,
                now,
                (name, date) =>
                {
                    record.DeptHead = Coalesce(record.DeptHead, name);
                    record.DeptHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ArchiveRoomHead = Coalesce(record.ArchiveRoomHead, name);
                    record.ArchiveRoomHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ProductionHead = Coalesce(record.ProductionHead, name);
                    record.ProductionHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ArchiveDeputyPresident = Coalesce(record.ArchiveDeputyPresident, name);
                    record.ArchiveDeputyPresidentDate ??= date;
                },
                (name, date) =>
                {
                    record.ProductionVicePresident = Coalesce(record.ProductionVicePresident, name);
                    record.ProductionVicePresidentDate ??= date;
                });
        }

        public static void ApplyToOutbound(YearlyArchiveOutboundRecord record, ApprovalChainResolution chain, DateTime now)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(chain);

            record.DeptHeadOpinion = string.Empty;
            record.ArchiveRoomHeadOpinion = string.Empty;
            record.ProductionHeadOpinion = string.Empty;
            record.VicePresidentOpinion = string.Empty;

            ApplyFiveNodeSigners(
                chain,
                now,
                (name, date) =>
                {
                    record.DeptHead = Coalesce(record.DeptHead, name);
                    record.DeptHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ArchiveRoomHead = Coalesce(record.ArchiveRoomHead, name);
                    record.ArchiveRoomHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ProductionHead = Coalesce(record.ProductionHead, name);
                    record.ProductionHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ArchiveDeputyPresident = Coalesce(record.ArchiveDeputyPresident, name);
                    record.ArchiveDeputyPresidentDate ??= date;
                },
                (name, date) =>
                {
                    record.ProductionVicePresident = Coalesce(record.ProductionVicePresident, name);
                    record.ProductionVicePresidentDate ??= date;
                });
        }

        public static void ApplyToHardDiskApplication(HardDiskMediaApplication application, ApprovalChainResolution chain, DateTime now)
        {
            ArgumentNullException.ThrowIfNull(application);
            ArgumentNullException.ThrowIfNull(chain);
            ApplyFiveNodeSigners(
                chain,
                now,
                (name, date) =>
                {
                    application.DeptHead = Coalesce(application.DeptHead, name);
                    application.DeptHeadDate ??= date;
                },
                (name, date) =>
                {
                    application.ArchiveRoomHead = Coalesce(application.ArchiveRoomHead, name);
                    application.ArchiveRoomHeadDate ??= date;
                },
                (name, date) =>
                {
                    application.ProductionHead = Coalesce(application.ProductionHead, name);
                    application.ProductionHeadDate ??= date;
                },
                (name, date) =>
                {
                    application.ArchiveDeputyPresident = Coalesce(application.ArchiveDeputyPresident, name);
                    application.ArchiveDeputyPresidentDate ??= date;
                },
                (name, date) =>
                {
                    application.ProductionVicePresident = Coalesce(application.ProductionVicePresident, name);
                    application.ProductionVicePresidentDate ??= date;
                });
        }

        public static void ApplyToHistoryArchiveDisposal(HistoryArchiveDisposalRecord record, ApprovalChainResolution chain, DateTime now)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(chain);
            ApplyFiveNodeSigners(
                chain,
                now,
                (name, date) =>
                {
                    record.DeptHead = Coalesce(record.DeptHead, name);
                    record.DeptHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ArchiveRoomHead = Coalesce(record.ArchiveRoomHead, name);
                    record.ArchiveRoomHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ProductionHead = Coalesce(record.ProductionHead, name);
                    record.ProductionHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ArchiveDeputyPresident = Coalesce(record.ArchiveDeputyPresident, name);
                    record.ArchiveDeputyPresidentDate ??= date;
                },
                (name, date) =>
                {
                    record.ProductionVicePresident = Coalesce(record.ProductionVicePresident, name);
                    record.ProductionVicePresidentDate ??= date;
                });
        }

        public static void ApplyToNetworkOnNetDisposal(NetworkOnNetDisposalRecord record, ApprovalChainResolution chain, DateTime now)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(chain);
            ApplyFiveNodeSigners(
                chain,
                now,
                (name, date) =>
                {
                    record.DeptHead = Coalesce(record.DeptHead, name);
                    record.DeptHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ArchiveRoomHead = Coalesce(record.ArchiveRoomHead, name);
                    record.ArchiveRoomHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ProductionHead = Coalesce(record.ProductionHead, name);
                    record.ProductionHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ArchiveDeputyPresident = Coalesce(record.ArchiveDeputyPresident, name);
                    record.ArchiveDeputyPresidentDate ??= date;
                },
                (name, date) =>
                {
                    record.ProductionVicePresident = Coalesce(record.ProductionVicePresident, name);
                    record.ProductionVicePresidentDate ??= date;
                });
        }

        public static void ApplyToYearlyDisposal(YearlyArchiveDisposalRecord record, ApprovalChainResolution chain, DateTime now)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(chain);
            ApplyFiveNodeSigners(
                chain,
                now,
                (name, date) =>
                {
                    record.DeptHead = Coalesce(record.DeptHead, name);
                    record.DeptHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ArchiveRoomHead = Coalesce(record.ArchiveRoomHead, name);
                    record.ArchiveRoomHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ProductionHead = Coalesce(record.ProductionHead, name);
                    record.ProductionHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ArchiveDeputyPresident = Coalesce(record.ArchiveDeputyPresident, name);
                    record.ArchiveDeputyPresidentDate ??= date;
                },
                (name, date) =>
                {
                    record.ProductionVicePresident = Coalesce(record.ProductionVicePresident, name);
                    record.ProductionVicePresidentDate ??= date;
                });
        }

        public static void ApplyToHardDiskDisposal(HardDiskDisposalRecord record, ApprovalChainResolution chain, DateTime now)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(chain);
            ApplyFiveNodeSigners(
                chain,
                now,
                (name, date) =>
                {
                    record.DeptHead = Coalesce(record.DeptHead, name);
                    record.DeptHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ArchiveRoomHead = Coalesce(record.ArchiveRoomHead, name);
                    record.ArchiveRoomHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ProductionHead = Coalesce(record.ProductionHead, name);
                    record.ProductionHeadDate ??= date;
                },
                (name, date) =>
                {
                    record.ArchiveDeputyPresident = Coalesce(record.ArchiveDeputyPresident, name);
                    record.ArchiveDeputyPresidentDate ??= date;
                },
                (name, date) =>
                {
                    record.ProductionVicePresident = Coalesce(record.ProductionVicePresident, name);
                    record.ProductionVicePresidentDate ??= date;
                });
        }

        public static IReadOnlyList<string> CollectMissingSignerErrors(
            ApprovalChainResolution chain,
            Func<string, string?> readCurrentName)
        {
            ArgumentNullException.ThrowIfNull(chain);
            ArgumentNullException.ThrowIfNull(readCurrentName);

            var errors = new List<string>();
            foreach (var signer in chain.EnabledSigners())
            {
                if (string.IsNullOrWhiteSpace(readCurrentName(signer.NodeKey)))
                {
                    errors.Add($"• 请填写{signer.DisplayName}");
                }
            }

            return errors;
        }

        public static void ApplyToReturn(
            YearlyArchiveReturnRecord record,
            ApprovalChainResolution chain,
            YearlyArchiveOutboundRecord? outbound,
            DateTime now)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(chain);

            if (chain.DeptHead.IsEnabled)
            {
                record.DeptHead = Coalesce(
                    record.DeptHead,
                    FirstNonEmpty(outbound?.DeptHead, chain.DeptHead.DefaultRealName));
                record.DeptHeadDate ??= outbound?.DeptHeadDate ?? now;
            }
            else
            {
                record.DeptHead = string.Empty;
                record.DeptHeadDate = null;
            }

            if (chain.ArchiveRoomHead.IsEnabled)
            {
                record.ArchiveRoomHead = Coalesce(
                    record.ArchiveRoomHead,
                    FirstNonEmpty(outbound?.ArchiveRoomHead, chain.ArchiveRoomHead.DefaultRealName));
                record.ArchiveRoomHeadDate ??= outbound?.ArchiveRoomHeadDate ?? now;
            }
            else
            {
                record.ArchiveRoomHead = string.Empty;
                record.ArchiveRoomHeadDate = null;
            }

            if (chain.ProductionHead.IsEnabled)
            {
                record.ProductionHead = Coalesce(
                    record.ProductionHead,
                    FirstNonEmpty(outbound?.ProductionHead, chain.ProductionHead.DefaultRealName));
                record.ProductionHeadDate ??= outbound?.ProductionHeadDate ?? now;
            }
            else
            {
                record.ProductionHead = string.Empty;
                record.ProductionHeadDate = null;
            }

            if (chain.ArchiveDeputyPresident.IsEnabled)
            {
                record.ArchiveDeputyPresident = Coalesce(
                    record.ArchiveDeputyPresident,
                    FirstNonEmpty(outbound?.ArchiveDeputyPresident, chain.ArchiveDeputyPresident.DefaultRealName));
                record.ArchiveDeputyPresidentDate ??= outbound?.ArchiveDeputyPresidentDate ?? now;
            }
            else
            {
                record.ArchiveDeputyPresident = string.Empty;
                record.ArchiveDeputyPresidentDate = null;
            }

            if (chain.ProductionVicePresident.IsEnabled)
            {
                record.ProductionVicePresident = Coalesce(
                    record.ProductionVicePresident,
                    FirstNonEmpty(outbound?.ProductionVicePresident, chain.ProductionVicePresident.DefaultRealName));
                record.ProductionVicePresidentDate ??= outbound?.ProductionVicePresidentDate ?? now;
            }
            else
            {
                record.ProductionVicePresident = string.Empty;
                record.ProductionVicePresidentDate = null;
            }
        }

        public static string ToYesNo(bool value) =>
            value ? ApprovalWorkflowDomainValues.Yes : ApprovalWorkflowDomainValues.No;

        public static string ToHasLossValue(bool hasLoss) => ToYesNo(hasLoss);

        /// <summary>建档登记签批匹配字段。</summary>
        public static Dictionary<string, string> BuildRegisterFieldValues(YearlyArchiveRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            bool hasBorrowed = record.MediaEntries?.Any(item => item.IsBorrowedHardDisk) == true;
            var mediaItems = record.MediaEntries?
                .SelectMany(media => media.Items ?? Enumerable.Empty<YearlyArchiveRegisterMediaItem>())
                .ToList()
                ?? [];
            return BuildFieldValues(
                (ApprovalWorkflowDomainValues.FieldArchivePurpose, record.ArchivePurpose),
                (ApprovalWorkflowDomainValues.FieldSourceType,
                    ResolveUniformValue(mediaItems.Select(item => item.SourceType))),
                (ApprovalWorkflowDomainValues.FieldConfidentialLevel,
                    ResolveUniformConfidentialLevel(mediaItems.Select(item => item.ConfidentialLevel))),
                (ApprovalWorkflowDomainValues.FieldHasElectronicMedia, ToYesNo(record.HasElectronicMedia)),
                (ApprovalWorkflowDomainValues.FieldHasSimulatedMedia, ToYesNo(record.HasSimulatedMedia)),
                (ApprovalWorkflowDomainValues.FieldHasProofMaterial,
                    ToYesNo(ArchiveRegisterDomainValues.HasProofMaterial(record.ProofMaterialNote))),
                (ApprovalWorkflowDomainValues.FieldHasBorrowedHardDisk, ToYesNo(hasBorrowed)));
        }

        /// <summary>资料出库签批匹配字段。</summary>
        public static Dictionary<string, string> BuildOutboundFieldValues(YearlyArchiveOutboundRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            var items = record.Items?.ToList() ?? [];
            bool hasExpectedReturn = record.ExpectedReturnDate.HasValue
                || items.Any(item => item.ExpectedReturnDate.HasValue);
            return BuildFieldValues(
                (ApprovalWorkflowDomainValues.FieldDestinationKind, record.DestinationKind),
                (ApprovalWorkflowDomainValues.FieldMediaKind,
                    ResolveUniformValue(items.Select(item => item.MediaKind))),
                (ApprovalWorkflowDomainValues.FieldConfidentialLevel,
                    ResolveUniformConfidentialLevel(items.Select(item => item.ConfidentialLevel))),
                (ApprovalWorkflowDomainValues.FieldHasProofMaterial,
                    ToYesNo(ArchiveOutboundDomainValues.HasProofMaterial(record.ProofMaterialNote))),
                (ApprovalWorkflowDomainValues.FieldHasNeedReturn, ToYesNo(items.Any(item => item.NeedReturn))),
                (ApprovalWorkflowDomainValues.FieldHasExpectedReturnDate, ToYesNo(hasExpectedReturn)));
        }

        /// <summary>资料归还签批匹配字段。</summary>
        public static Dictionary<string, string> BuildReturnFieldValues(
            YearlyArchiveReturnRecord record,
            string? sourceDestinationKind = null)
        {
            ArgumentNullException.ThrowIfNull(record);
            var items = record.Items?.ToList() ?? [];
            bool hasElectronic = items.Any(item =>
                string.Equals(
                    item.MediaKind?.Trim(),
                    ArchiveRegisterDomainValues.MediaKindElectronic,
                    StringComparison.Ordinal));
            return BuildFieldValues(
                (ApprovalWorkflowDomainValues.FieldHasLoss,
                    ToYesNo(ArchiveReturnDomainValues.HasAbnormalReturnItems(items))),
                (ApprovalWorkflowDomainValues.FieldHasLossDescription,
                    ToYesNo(!string.IsNullOrWhiteSpace(record.LossDescription))),
                (ApprovalWorkflowDomainValues.FieldDestinationKind, sourceDestinationKind),
                (ApprovalWorkflowDomainValues.FieldMediaKind,
                    ResolveUniformValue(items.Select(item => item.MediaKind))),
                (ApprovalWorkflowDomainValues.FieldConfidentialLevel,
                    ResolveUniformConfidentialLevel(items.Select(item => item.ConfidentialLevel))),
                (ApprovalWorkflowDomainValues.FieldHasElectronicMedia, ToYesNo(hasElectronic)));
        }

        /// <summary>入网申请签批匹配字段。</summary>
        public static Dictionary<string, string> BuildNetworkInboundFieldValues(NetworkInboundRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            var items = record.Items?.ToList() ?? [];
            return BuildFieldValues(
                (ApprovalWorkflowDomainValues.FieldSourceKind, record.SourceKind),
                (ApprovalWorkflowDomainValues.FieldHasProofMaterial,
                    ToYesNo(ArchiveRegisterDomainValues.HasProofMaterial(record.ProofMaterialNote))),
                (ApprovalWorkflowDomainValues.FieldReturnBorrowedHardDisk,
                    ToYesNo(record.ReturnBorrowedHardDiskWithInbound)),
                (ApprovalWorkflowDomainValues.FieldAssetKind,
                    ResolveUniformValue(items.Select(item => item.AssetKind))),
                (ApprovalWorkflowDomainValues.FieldConfidentialLevel,
                    ResolveUniformConfidentialLevel(items.Select(item => item.ConfidentialLevel))));
        }

        /// <summary>出网申请签批匹配字段。</summary>
        public static Dictionary<string, string> BuildNetworkOutboundFieldValues(NetworkOutboundRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            var items = record.Items?.ToList() ?? [];
            return BuildFieldValues(
                (ApprovalWorkflowDomainValues.FieldDestinationKind, record.DestinationKind),
                (ApprovalWorkflowDomainValues.FieldArchivePurpose, record.ArchivePurpose),
                (ApprovalWorkflowDomainValues.FieldHasProofMaterial,
                    ToYesNo(ArchiveRegisterDomainValues.HasProofMaterial(record.ProofMaterialNote))),
                (ApprovalWorkflowDomainValues.FieldHasTargetRegister,
                    ToYesNo(record.TargetRegisterRecordId.HasValue && record.TargetRegisterRecordId.Value > 0)),
                (ApprovalWorkflowDomainValues.FieldAssetKind,
                    ResolveUniformValue(items.Select(item => item.AssetKind))),
                (ApprovalWorkflowDomainValues.FieldConfidentialLevel,
                    ResolveUniformConfidentialLevel(items.Select(item => item.ConfidentialLevel))));
        }

        /// <summary>年度资料离库签批匹配字段。</summary>
        public static Dictionary<string, string> BuildYearlyDisposalFieldValues(YearlyArchiveDisposalRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            return BuildYearlyDisposalFieldValues(record.MediaKind, record.Items);
        }

        /// <summary>年度资料离库签批匹配字段（编辑中明细尚未回写主单时使用）。</summary>
        public static Dictionary<string, string> BuildYearlyDisposalFieldValues(
            string? mediaKind,
            IEnumerable<YearlyArchiveDisposalItem>? items)
        {
            var list = items?.ToList() ?? [];
            return BuildFieldValues(
                (ApprovalWorkflowDomainValues.FieldMediaKind, mediaKind),
                (ApprovalWorkflowDomainValues.FieldDisposalReason,
                    ResolveUniformValue(list.Select(item => item.DisposalReason))),
                (ApprovalWorkflowDomainValues.FieldDispositionMethod,
                    ResolveUniformValue(list.Select(item => item.DispositionMethod))),
                (ApprovalWorkflowDomainValues.FieldHasFormatRetain,
                    ToYesNo(ArchiveDisposalDomainValues.HasFormatRetainMethod(
                        list.Select(item => item.DispositionMethod)))),
                (ApprovalWorkflowDomainValues.FieldMediumKind,
                    ResolveUniformValue(list.Select(item => item.MediumKind))));
        }

        /// <summary>历史存档离库签批匹配字段。</summary>
        public static Dictionary<string, string> BuildHistoryDisposalFieldValues(HistoryArchiveDisposalRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            var items = record.Items?.ToList() ?? [];
            return BuildFieldValues(
                (ApprovalWorkflowDomainValues.FieldMaterialKind, record.MaterialKind),
                (ApprovalWorkflowDomainValues.FieldDispositionMethod, record.DispositionMethod),
                (ApprovalWorkflowDomainValues.FieldIsTransferMethod,
                    ToYesNo(HistoryArchiveDisposalDomainValues.IsTransferMethod(record.DispositionMethod))),
                (ApprovalWorkflowDomainValues.FieldHasMixedPlacement,
                    ToYesNo(items.Any(item => item.IsMixedPlacement))),
                (ApprovalWorkflowDomainValues.FieldHasOtherRemark,
                    ToYesNo(!string.IsNullOrWhiteSpace(record.OtherRemark))));
        }

        /// <summary>在网数据处置签批匹配字段。</summary>
        public static Dictionary<string, string> BuildNetworkOnNetDisposalFieldValues(
            NetworkOnNetDisposalRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            var items = record.Items?.ToList() ?? [];
            return BuildFieldValues(
                (ApprovalWorkflowDomainValues.FieldDisposalReason,
                    ResolveUniformValue(items.Select(item => item.DisposalReason))),
                (ApprovalWorkflowDomainValues.FieldDispositionMethod,
                    ResolveUniformValue(items.Select(item => item.DispositionMethod))),
                (ApprovalWorkflowDomainValues.FieldAssetKind,
                    ResolveUniformValue(items.Select(item => item.AssetKind))),
                (ApprovalWorkflowDomainValues.FieldHasOtherRemark,
                    ToYesNo(!string.IsNullOrWhiteSpace(record.Remark))));
        }

        /// <summary>硬盘出库签批匹配字段。</summary>
        public static Dictionary<string, string> BuildHardDiskOutboundFieldValues(HardDiskMediaApplication application)
        {
            ArgumentNullException.ThrowIfNull(application);
            return BuildFieldValues(
                (ApprovalWorkflowDomainValues.FieldApplicationType, application.ApplicationType),
                (ApprovalWorkflowDomainValues.FieldDestinationKind, application.DestinationKind),
                (ApprovalWorkflowDomainValues.FieldHasExpectedReturnDate,
                    ToYesNo(application.ExpectedReturnDate.HasValue)),
                (ApprovalWorkflowDomainValues.FieldHasSourceOutbound,
                    ToYesNo(application.SourceOutboundRecordId.HasValue && application.SourceOutboundRecordId.Value > 0)),
                (ApprovalWorkflowDomainValues.FieldHasSourceNetworkOutbound,
                    ToYesNo(application.SourceNetworkOutboundRecordId.HasValue
                        && application.SourceNetworkOutboundRecordId.Value > 0)));
        }

        /// <summary>硬盘归还签批匹配字段。</summary>
        public static Dictionary<string, string> BuildHardDiskReturnFieldValues(HardDiskMediaApplication application)
        {
            ArgumentNullException.ThrowIfNull(application);
            return BuildFieldValues(
                (ApprovalWorkflowDomainValues.FieldApplicationType, application.ApplicationType));
        }

        /// <summary>
        /// 按申请类型解析硬盘申请单对应的审核审批业务类型。
        /// </summary>
        public static string ResolveHardDiskApplicationBusinessType(HardDiskMediaApplication application)
        {
            ArgumentNullException.ThrowIfNull(application);
            return HardDiskMediaReturnDomainValues.IsReturnRegistrationApplicationType(application.ApplicationType)
                ? ApprovalWorkflowBusinessTypes.HardDiskReturn
                : ApprovalWorkflowBusinessTypes.HardDiskOutbound;
        }

        /// <summary>按申请类型构建硬盘申请单签批匹配字段。</summary>
        public static Dictionary<string, string> BuildHardDiskApplicationFieldValues(HardDiskMediaApplication application)
        {
            ArgumentNullException.ThrowIfNull(application);
            return HardDiskMediaReturnDomainValues.IsReturnRegistrationApplicationType(application.ApplicationType)
                ? BuildHardDiskReturnFieldValues(application)
                : BuildHardDiskOutboundFieldValues(application);
        }

        /// <summary>硬盘离库签批匹配字段。</summary>
        public static Dictionary<string, string> BuildHardDiskDisposalFieldValues(
            HardDiskDisposalRecord record,
            Func<HardDiskDisposalItem, string>? resolveDisposalReason = null,
            Func<HardDiskDisposalItem, string>? resolveDispositionMethod = null)
        {
            ArgumentNullException.ThrowIfNull(record);
            var items = record.Items?.ToList() ?? [];
            resolveDisposalReason ??= static item => item.DisposalReason;
            resolveDispositionMethod ??= static item => item.DispositionMethod;
            return BuildFieldValues(
                (ApprovalWorkflowDomainValues.FieldDisposalReason,
                    ResolveUniformValue(items.Select(item => resolveDisposalReason(item)))),
                (ApprovalWorkflowDomainValues.FieldDispositionMethod,
                    ResolveUniformValue(items.Select(item => resolveDispositionMethod(item)))),
                (ApprovalWorkflowDomainValues.FieldBeforeMediaStatus,
                    ResolveUniformValue(items.Select(item => item.BeforeMediaStatus))),
                (ApprovalWorkflowDomainValues.FieldHasOtherRemark,
                    ToYesNo(!string.IsNullOrWhiteSpace(record.OtherRemark))));
        }

        public static string? ReadRegisterSigner(YearlyArchiveRegisterRecord record, string nodeKey) =>
            ReadFiveNodeSigner(
                nodeKey,
                record.DeptHead,
                record.ArchiveRoomHead,
                record.ProductionHead,
                record.ArchiveDeputyPresident,
                record.ProductionVicePresident);

        public static string? ReadOutboundSigner(YearlyArchiveOutboundRecord record, string nodeKey) =>
            ReadFiveNodeSigner(
                nodeKey,
                record.DeptHead,
                record.ArchiveRoomHead,
                record.ProductionHead,
                record.ArchiveDeputyPresident,
                record.ProductionVicePresident);

        public static string? ReadNetworkInboundSigner(NetworkInboundRecord record, string nodeKey) =>
            ReadFiveNodeSigner(
                nodeKey,
                record.DeptHead,
                record.ArchiveRoomHead,
                record.ProductionHead,
                record.ArchiveDeputyPresident,
                record.ProductionVicePresident);

        public static string? ReadNetworkOutboundSigner(NetworkOutboundRecord record, string nodeKey) =>
            ReadFiveNodeSigner(
                nodeKey,
                record.DeptHead,
                record.ArchiveRoomHead,
                record.ProductionHead,
                record.ArchiveDeputyPresident,
                record.ProductionVicePresident);

        public static string? ReadReturnSigner(YearlyArchiveReturnRecord record, string nodeKey) =>
            ReadFiveNodeSigner(
                nodeKey,
                record.DeptHead,
                record.ArchiveRoomHead,
                record.ProductionHead,
                record.ArchiveDeputyPresident,
                record.ProductionVicePresident);

        public static string? ReadHardDiskSigner(HardDiskMediaApplication application, string nodeKey) =>
            ReadFiveNodeSigner(
                nodeKey,
                application.DeptHead,
                application.ArchiveRoomHead,
                application.ProductionHead,
                application.ArchiveDeputyPresident,
                application.ProductionVicePresident);

        public static string? ReadYearlyDisposalSigner(YearlyArchiveDisposalRecord record, string nodeKey) =>
            ReadFiveNodeSigner(
                nodeKey,
                record.DeptHead,
                record.ArchiveRoomHead,
                record.ProductionHead,
                record.ArchiveDeputyPresident,
                record.ProductionVicePresident);

        public static string? ReadHardDiskDisposalSigner(HardDiskDisposalRecord record, string nodeKey) =>
            ReadFiveNodeSigner(
                nodeKey,
                record.DeptHead,
                record.ArchiveRoomHead,
                record.ProductionHead,
                record.ArchiveDeputyPresident,
                record.ProductionVicePresident);

        public static string? ReadHistoryDisposalSigner(HistoryArchiveDisposalRecord record, string nodeKey) =>
            ReadFiveNodeSigner(
                nodeKey,
                record.DeptHead,
                record.ArchiveRoomHead,
                record.ProductionHead,
                record.ArchiveDeputyPresident,
                record.ProductionVicePresident);

        public static string? ReadNetworkOnNetDisposalSigner(NetworkOnNetDisposalRecord record, string nodeKey) =>
            ReadFiveNodeSigner(
                nodeKey,
                record.DeptHead,
                record.ArchiveRoomHead,
                record.ProductionHead,
                record.ArchiveDeputyPresident,
                record.ProductionVicePresident);

        private static string? ReadFiveNodeSigner(
            string nodeKey,
            string deptHead,
            string archiveRoomHead,
            string productionHead,
            string archiveDeputyPresident,
            string productionVicePresident) =>
            nodeKey switch
            {
                ApprovalWorkflowDomainValues.NodeDeptHead => deptHead,
                ApprovalWorkflowDomainValues.NodeArchiveRoomHead => archiveRoomHead,
                ApprovalWorkflowDomainValues.NodeProductionHead => productionHead,
                ApprovalWorkflowDomainValues.NodeArchiveDeputyPresident => archiveDeputyPresident,
                ApprovalWorkflowDomainValues.NodeProductionVicePresident => productionVicePresident,
                _ => null
            };

        private static void ApplyFiveNodeSigners(
            ApprovalChainResolution chain,
            DateTime now,
            Action<string, DateTime?> assignDeptHead,
            Action<string, DateTime?> assignArchiveRoomHead,
            Action<string, DateTime?> assignProductionHead,
            Action<string, DateTime?> assignArchiveDeputyPresident,
            Action<string, DateTime?> assignProductionVicePresident)
        {
            if (chain.DeptHead.IsEnabled)
            {
                assignDeptHead(chain.DeptHead.DefaultRealName, now);
            }

            if (chain.ArchiveRoomHead.IsEnabled)
            {
                assignArchiveRoomHead(chain.ArchiveRoomHead.DefaultRealName, now);
            }

            if (chain.ProductionHead.IsEnabled)
            {
                assignProductionHead(chain.ProductionHead.DefaultRealName, now);
            }

            if (chain.ArchiveDeputyPresident.IsEnabled)
            {
                assignArchiveDeputyPresident(chain.ArchiveDeputyPresident.DefaultRealName, now);
            }

            if (chain.ProductionVicePresident.IsEnabled)
            {
                assignProductionVicePresident(chain.ProductionVicePresident.DefaultRealName, now);
            }
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        public static Dictionary<string, string> BuildFieldValues(params (string Key, string? Value)[] pairs)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, value) in pairs)
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                map[key.Trim()] = value.Trim();
            }

            return map;
        }

        /// <summary>
        /// 明细级字段取唯一非空值；多值并存时返回空，避免汇总串无法精确匹配条件。
        /// </summary>
        public static string ResolveUniformValue(IEnumerable<string?> values)
        {
            ArgumentNullException.ThrowIfNull(values);

            var distinct = values
                .Select(value => value?.Trim() ?? string.Empty)
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            return distinct.Count == 1 ? distinct[0] : string.Empty;
        }

        /// <summary>明细密级取唯一归一化值；多值并存时返回空。</summary>
        public static string ResolveUniformConfidentialLevel(IEnumerable<string?> values)
        {
            ArgumentNullException.ThrowIfNull(values);
            return ResolveUniformValue(values.Select(ArchiveRegisterDomainValues.NormalizeConfidentialLevel));
        }

        private static string Coalesce(string? current, string fallback) =>
            string.IsNullOrWhiteSpace(current) && !string.IsNullOrWhiteSpace(fallback)
                ? fallback.Trim()
                : (current ?? string.Empty);
    }
}
