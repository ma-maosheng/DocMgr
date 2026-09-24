using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 单业务附件分区策略：签批 / 照片 / 证明 / 其他（分类常量仍来自各 *DomainValues）。
    /// </summary>
    public sealed class ApprovalAttachmentPolicy
    {
        public required string BusinessTypeKey { get; init; }

        public OfflineApprovalLifecycleSupport.FlowKind FlowKind { get; init; }

        /// <summary>B 流：已审批态是否允许先传「其他附件」。</summary>
        public bool AllowOtherWhileApproved { get; init; }

        /// <summary>主签批分类（上传时默认写入）。</summary>
        public required string PrimarySignedCategory { get; init; }

        /// <summary>签批分类判定（可含多种签批子类）。</summary>
        public required Func<string, bool> IsSignedCategory { get; init; }

        /// <summary>照片分类判定；null 表示无照片区。</summary>
        public Func<string, bool>? IsPhotoCategory { get; init; }

        /// <summary>证明材料分类；null 表示无证明区。</summary>
        public string? ProofCategory { get; init; }

        /// <summary>其他附件分类。</summary>
        public required string OtherCategory { get; init; }

        public bool HasPhotoZone => IsPhotoCategory != null;

        public bool HasProofZone => !string.IsNullOrWhiteSpace(ProofCategory);

        public bool IsOtherCategory(string? category)
            => string.Equals(category?.Trim(), OtherCategory, StringComparison.Ordinal);

        public bool IsProofCategory(string? category)
            => ProofCategory != null
               && string.Equals(category?.Trim(), ProofCategory, StringComparison.Ordinal);
    }

    /// <summary>
    /// 按业务类型解析附件策略，并提供分区装载 / 上传门禁封装。
    /// </summary>
    public static class ApprovalAttachmentPolicySupport
    {
        private static readonly Dictionary<string, ApprovalAttachmentPolicy> Policies =
            new(StringComparer.Ordinal)
            {
                [ApprovalWorkflowBusinessTypes.HardDiskOutbound] = Create(
                    ApprovalWorkflowBusinessTypes.HardDiskOutbound,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    allowOtherWhileApproved: false,
                    HardDiskOutboundDomainValues.AttachmentCategorySignedHandover,
                    c => string.Equals(c, HardDiskOutboundDomainValues.AttachmentCategorySignedHandover, StringComparison.Ordinal),
                    c => string.Equals(c, HardDiskOutboundDomainValues.AttachmentCategoryPhysicalPhoto, StringComparison.Ordinal),
                    HardDiskOutboundDomainValues.AttachmentCategoryProofMaterial,
                    HardDiskOutboundDomainValues.AttachmentCategoryOther),

                [ApprovalWorkflowBusinessTypes.HardDiskReturn] = Create(
                    ApprovalWorkflowBusinessTypes.HardDiskReturn,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    allowOtherWhileApproved: false,
                    HardDiskOutboundDomainValues.AttachmentCategorySignedHandover,
                    c => string.Equals(c, HardDiskOutboundDomainValues.AttachmentCategorySignedHandover, StringComparison.Ordinal)
                         || string.IsNullOrWhiteSpace(c),
                    isPhoto: null,
                    proofCategory: null,
                    HardDiskOutboundDomainValues.AttachmentCategoryOther),

                [ApprovalWorkflowBusinessTypes.HardDiskDisposal] = Create(
                    ApprovalWorkflowBusinessTypes.HardDiskDisposal,
                    OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                    allowOtherWhileApproved: true,
                    HardDiskDisposalDomainValues.AttachmentCategorySignedForm,
                    c => string.Equals(c, HardDiskDisposalDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal),
                    c => string.Equals(c, HardDiskDisposalDomainValues.AttachmentCategoryDiskPhoto, StringComparison.Ordinal),
                    proofCategory: null,
                    HardDiskDisposalDomainValues.AttachmentCategoryOther),

                [ApprovalWorkflowBusinessTypes.YearlyArchiveDisposal] = Create(
                    ApprovalWorkflowBusinessTypes.YearlyArchiveDisposal,
                    OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                    allowOtherWhileApproved: true,
                    ArchiveDisposalDomainValues.AttachmentCategorySignedForm,
                    c => string.Equals(c, ArchiveDisposalDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal),
                    ArchiveDisposalDomainValues.IsScenePhotoCategory,
                    proofCategory: null,
                    ArchiveDisposalDomainValues.AttachmentCategoryOther),

                [ApprovalWorkflowBusinessTypes.HistoryArchiveDisposal] = Create(
                    ApprovalWorkflowBusinessTypes.HistoryArchiveDisposal,
                    OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                    allowOtherWhileApproved: true,
                    HistoryArchiveDisposalDomainValues.AttachmentCategorySignedForm,
                    c => string.Equals(c, HistoryArchiveDisposalDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal),
                    HistoryArchiveDisposalDomainValues.IsScenePhotoCategory,
                    proofCategory: null,
                    HistoryArchiveDisposalDomainValues.AttachmentCategoryOther),

                [ApprovalWorkflowBusinessTypes.NetworkOnNetDisposal] = Create(
                    ApprovalWorkflowBusinessTypes.NetworkOnNetDisposal,
                    OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                    allowOtherWhileApproved: true,
                    NetworkTransferDomainValues.AttachmentCategorySignedForm,
                    c => string.Equals(c, NetworkTransferDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal),
                    isPhoto: null,
                    proofCategory: null,
                    NetworkTransferDomainValues.AttachmentCategoryOther),

                [ApprovalWorkflowBusinessTypes.HardDiskInventoryRegister] = Create(
                    ApprovalWorkflowBusinessTypes.HardDiskInventoryRegister,
                    OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                    allowOtherWhileApproved: true,
                    HardDiskInventoryRegisterDomainValues.AttachmentCategorySignedForm,
                    c => string.Equals(c, HardDiskInventoryRegisterDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal),
                    isPhoto: null,
                    proofCategory: null,
                    HardDiskInventoryRegisterDomainValues.AttachmentCategoryOther),

                [ApprovalWorkflowBusinessTypes.YearlyArchiveInventoryRegister] = Create(
                    ApprovalWorkflowBusinessTypes.YearlyArchiveInventoryRegister,
                    OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                    allowOtherWhileApproved: true,
                    ArchiveInventoryRegisterDomainValues.AttachmentCategorySignedForm,
                    c => string.Equals(c, ArchiveInventoryRegisterDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal),
                    isPhoto: null,
                    proofCategory: null,
                    ArchiveInventoryRegisterDomainValues.AttachmentCategoryOther),

                [ApprovalWorkflowBusinessTypes.NetworkInbound] = Create(
                    ApprovalWorkflowBusinessTypes.NetworkInbound,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    allowOtherWhileApproved: false,
                    NetworkTransferDomainValues.AttachmentCategorySignedForm,
                    c => string.Equals(c, NetworkTransferDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal),
                    isPhoto: null,
                    NetworkTransferDomainValues.AttachmentCategoryProofMaterial,
                    NetworkTransferDomainValues.AttachmentCategoryOther),

                [ApprovalWorkflowBusinessTypes.NetworkOutbound] = Create(
                    ApprovalWorkflowBusinessTypes.NetworkOutbound,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    allowOtherWhileApproved: false,
                    NetworkTransferDomainValues.AttachmentCategorySignedForm,
                    c => string.Equals(c, NetworkTransferDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal),
                    isPhoto: null,
                    NetworkTransferDomainValues.AttachmentCategoryProofMaterial,
                    NetworkTransferDomainValues.AttachmentCategoryOther),

                [ApprovalWorkflowBusinessTypes.YearlyArchiveOutbound] = Create(
                    ApprovalWorkflowBusinessTypes.YearlyArchiveOutbound,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    allowOtherWhileApproved: false,
                    ArchiveOutboundDomainValues.AttachmentKindSignedHandoverForm,
                    ArchiveOutboundDomainValues.IsSignedFormAttachmentKind,
                    c => string.Equals(c, ArchiveOutboundDomainValues.AttachmentKindMaterialPhoto, StringComparison.Ordinal),
                    ArchiveOutboundDomainValues.AttachmentKindProofMaterialScan,
                    ArchiveOutboundDomainValues.AttachmentKindOther),

                [ApprovalWorkflowBusinessTypes.YearlyArchiveRegister] = Create(
                    ApprovalWorkflowBusinessTypes.YearlyArchiveRegister,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    allowOtherWhileApproved: false,
                    ArchiveRegisterDomainValues.AttachmentKindSignedHandoverForm,
                    c => string.Equals(c, ArchiveRegisterDomainValues.AttachmentKindSignedHandoverForm, StringComparison.Ordinal),
                    c => string.Equals(c, ArchiveRegisterDomainValues.AttachmentKindMaterialPhoto, StringComparison.Ordinal),
                    ArchiveRegisterDomainValues.AttachmentKindProofMaterialScan,
                    ArchiveRegisterDomainValues.AttachmentKindOther),

                [ApprovalWorkflowBusinessTypes.YearlyArchiveReturn] = Create(
                    ApprovalWorkflowBusinessTypes.YearlyArchiveReturn,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    allowOtherWhileApproved: false,
                    ArchiveReturnDomainValues.AttachmentKindSignedHandover,
                    c => string.Equals(c, ArchiveReturnDomainValues.AttachmentKindSignedHandover, StringComparison.Ordinal),
                    isPhoto: null,
                    proofCategory: null,
                    ArchiveReturnDomainValues.AttachmentKindOther)
            };

        /// <summary>别名：附件 BusinessType 字符串与审批业务类型不完全一致时的映射。</summary>
        private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
        {
            [HardDiskDisposalDomainValues.AttachmentBusinessType] = ApprovalWorkflowBusinessTypes.HardDiskDisposal,
            [ArchiveDisposalDomainValues.AttachmentBusinessType] = ApprovalWorkflowBusinessTypes.YearlyArchiveDisposal,
            [HistoryArchiveDisposalDomainValues.AttachmentBusinessType] = ApprovalWorkflowBusinessTypes.HistoryArchiveDisposal,
            [NetworkTransferDomainValues.InboundAttachmentBusinessType] = ApprovalWorkflowBusinessTypes.NetworkInbound,
            [NetworkTransferDomainValues.OutboundAttachmentBusinessType] = ApprovalWorkflowBusinessTypes.NetworkOutbound,
            [NetworkTransferDomainValues.DisposalAttachmentBusinessType] = ApprovalWorkflowBusinessTypes.NetworkOnNetDisposal,
            [HardDiskInventoryRegisterDomainValues.AttachmentBusinessType] = ApprovalWorkflowBusinessTypes.HardDiskInventoryRegister,
            [ArchiveInventoryRegisterDomainValues.AttachmentBusinessType] = ApprovalWorkflowBusinessTypes.YearlyArchiveInventoryRegister,
            ["YearlyArchiveRegister"] = ApprovalWorkflowBusinessTypes.YearlyArchiveRegister
        };

        public static bool TryGet(string? businessTypeOrAlias, out ApprovalAttachmentPolicy policy)
        {
            policy = null!;
            string key = ResolveKey(businessTypeOrAlias);
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return Policies.TryGetValue(key, out policy!);
        }

        public static ApprovalAttachmentPolicy Get(string businessTypeOrAlias)
        {
            if (!TryGet(businessTypeOrAlias, out var policy))
            {
                throw new ArgumentException($"未配置附件策略：{businessTypeOrAlias}", nameof(businessTypeOrAlias));
            }

            return policy;
        }

        public static void Partition(
            ApprovalAttachmentPolicy policy,
            IEnumerable<SystemAttachment> source,
            ICollection<SystemAttachment>? all,
            ICollection<SystemAttachment> signed,
            ICollection<SystemAttachment>? photos,
            ICollection<SystemAttachment>? proof,
            ICollection<SystemAttachment> other)
        {
            ArgumentNullException.ThrowIfNull(policy);
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(signed);
            ArgumentNullException.ThrowIfNull(other);

            all?.Clear();
            signed.Clear();
            photos?.Clear();
            proof?.Clear();
            other.Clear();

            foreach (SystemAttachment item in source)
            {
                all?.Add(item);
                string category = item.FileCategory?.Trim() ?? string.Empty;
                if (policy.IsSignedCategory(category))
                {
                    signed.Add(item);
                }
                else if (policy.IsPhotoCategory != null && policy.IsPhotoCategory(category))
                {
                    photos?.Add(item);
                }
                else if (policy.IsProofCategory(category))
                {
                    proof?.Add(item);
                }
                else
                {
                    other.Add(item);
                }
            }
        }

        /// <summary>上传门禁：结合策略 FlowKind / 其他分类判定。</summary>
        public static OfflineApprovalLifecycleSupport.GateResult EvaluateUpload(
            ApprovalAttachmentPolicy policy,
            int status,
            bool signedAttachmentUploaded,
            string? fileCategory,
            bool isArchiveAdmin)
        {
            ArgumentNullException.ThrowIfNull(policy);
            bool isOther = policy.IsOtherCategory(fileCategory);
            return OfflineApprovalLifecycleSupport.EvaluateAttachmentUpload(
                new OfflineApprovalLifecycleSupport.GateContext(
                    status,
                    signedAttachmentUploaded,
                    policy.FlowKind,
                    isArchiveAdmin
                        ? OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin
                        : OfflineApprovalLifecycleSupport.ActorRole.Applicant),
                isOtherCategory: isOther,
                isArchiveAdmin: isArchiveAdmin,
                allowOtherWhileApproved: policy.AllowOtherWhileApproved);
        }

        private static string ResolveKey(string? businessTypeOrAlias)
        {
            string raw = businessTypeOrAlias?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            if (Aliases.TryGetValue(raw, out string? mapped))
            {
                return mapped;
            }

            return raw;
        }

        private static ApprovalAttachmentPolicy Create(
            string businessTypeKey,
            OfflineApprovalLifecycleSupport.FlowKind flowKind,
            bool allowOtherWhileApproved,
            string primarySigned,
            Func<string, bool> isSigned,
            Func<string, bool>? isPhoto,
            string? proofCategory,
            string otherCategory)
            => new()
            {
                BusinessTypeKey = businessTypeKey,
                FlowKind = flowKind,
                AllowOtherWhileApproved = allowOtherWhileApproved,
                PrimarySignedCategory = primarySigned,
                IsSignedCategory = isSigned,
                IsPhotoCategory = isPhoto,
                ProofCategory = proofCategory,
                OtherCategory = otherCategory
            };
    }
}
