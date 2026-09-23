namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘出库（借出）审批交接附件分类等常量。
    /// </summary>
    public static class HardDiskOutboundDomainValues
    {
        /// <summary>签批交接单（必传）。</summary>
        public const string AttachmentCategorySignedHandover = "签批交接单";

        /// <summary>实物照片（有实物流转时必传）。</summary>
        public const string AttachmentCategoryPhysicalPhoto = "实物照片";

        /// <summary>证明材料（申请声明有材料时必传）。</summary>
        public const string AttachmentCategoryProofMaterial = "证明材料";

        /// <summary>其他附件（可选，办结不强制校验）。</summary>
        public const string AttachmentCategoryOther = "其他附件";

        /// <summary>申请声明未附证明材料时的存值。</summary>
        public const string ProofMaterialNoneText = "无";

        /// <summary>出库审批交接附件分类全集。</summary>
        public static IReadOnlyList<string> AttachmentCategoryOptions { get; } =
        [
            AttachmentCategorySignedHandover,
            AttachmentCategoryPhysicalPhoto,
            AttachmentCategoryProofMaterial,
            AttachmentCategoryOther
        ];

        /// <summary>是否为已知出库交接附件分类。</summary>
        public static bool IsKnownAttachmentCategory(string? fileCategory)
        {
            string category = fileCategory?.Trim() ?? string.Empty;
            return AttachmentCategoryOptions.Contains(category, StringComparer.Ordinal);
        }

        /// <summary>是否为签批交接单分类。</summary>
        public static bool IsSignedHandoverCategory(string? fileCategory) =>
            string.Equals(fileCategory?.Trim(), AttachmentCategorySignedHandover, StringComparison.Ordinal);

        /// <summary>
        /// 硬盘出库审批是否存在实物流转（需上传实物照片）。
        /// 借出审批弹窗均经「确认实物交接」，故可选出库类型一律视为有实物流转。
        /// </summary>
        public static bool RequiresPhysicalPhotoAttachment(string? applicationType)
        {
            string type = applicationType?.Trim() ?? string.Empty;
            return string.Equals(type, HardDiskMediaApplication.TypeOutboundTemporary, StringComparison.Ordinal)
                || string.Equals(type, HardDiskMediaApplication.TypeOutboundLongTerm, StringComparison.Ordinal)
                || string.Equals(type, HardDiskMediaApplication.TypeOutboundPermanent, StringComparison.Ordinal);
        }

        /// <summary>申请人是否声明附有证明材料（<paramref name="proofMaterialNote"/> 不为「无」）。</summary>
        public static bool HasProofMaterial(string? proofMaterialNote)
        {
            string note = proofMaterialNote?.Trim() ?? string.Empty;
            return note.Length > 0
                && !string.Equals(note, ProofMaterialNoneText, StringComparison.Ordinal);
        }

        /// <summary>是否须在审批阶段上传证明材料扫描件。</summary>
        public static bool RequiresProofMaterialAttachment(string? proofMaterialNote) =>
            HasProofMaterial(proofMaterialNote);

        /// <summary>规范化证明材料备注：有名称则存名称，否则存「无」。</summary>
        public static string NormalizeProofMaterialNote(bool hasProofMaterial, string? proofMaterialName)
        {
            if (!hasProofMaterial)
            {
                return ProofMaterialNoneText;
            }

            string name = proofMaterialName?.Trim() ?? string.Empty;
            return name.Length > 0 ? name : ProofMaterialNoneText;
        }
    }
}
