namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 系统附件 <c>FileCategory</c> 显示名解析（各业务英文/中文类别码统一为中文「附件分类」）。
    /// </summary>
    public static class SystemAttachmentCategoryDisplaySupport
    {
        /// <summary>
        /// 将存储的附件类别码转为界面显示名；未知时回退原文或「一般附件」。
        /// </summary>
        public static string ResolveDisplayName(string? fileCategory, string? fileName = null)
        {
            string category = fileCategory?.Trim() ?? string.Empty;
            if (category.Length == 0)
            {
                // 建档历史附件可能仅靠文件名区分
                string resolved = Models.YearlyArchive.ArchiveRegisterDomainValues.ResolveAttachmentKind(
                    fileCategory,
                    fileName);
                return Models.YearlyArchive.ArchiveRegisterDomainValues.GetAttachmentKindDisplayName(resolved);
            }

            // 已是中文类别（如归还「签批交接单」、默认「一般附件」）直接展示
            if (ContainsCjk(category))
            {
                return category;
            }

            return category switch
            {
                "SignedApprovalForm" or "SignedHandoverForm" or "SignedHandover" => "签批交接单",
                "MaterialPhoto" => "资料照片",
                "ProofMaterialScan" => "证明材料",
                "Other" => "其他附件",
                "SignedAbnormalReturnReport" => "异常归还签批件",
                "SignedOutboundForm" or "SignedReturnForm" or "SignedApproval" => "签批交接单",
                "一般附件" => "一般附件",
                _ => category
            };
        }

        private static bool ContainsCjk(string value)
        {
            foreach (char ch in value)
            {
                if (ch >= 0x4E00 && ch <= 0x9FFF)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
