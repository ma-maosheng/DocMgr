using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 模拟介质检索筛选池份数：默认 1 份，保存时不得超过当前库内可提份数。
    /// </summary>
    public static class ArchiveSearchPoolCopyCountSupport
    {
        public const int DefaultRequestedCopyCount = 1;

        /// <summary>资料子项允许申请的最大份数（当前库内；可为 0）。</summary>
        public static int ResolveMaxCopyCount(int availableCopyCount) => Math.Max(0, availableCopyCount);

        /// <summary>校验模拟介质筛选份数；通过返回 null，否则返回错误文案。</summary>
        public static string? ValidateSimulatedRequestedCopyCount(
            int requestedCopyCount,
            int availableCopyCount,
            string itemLabel)
        {
            string label = string.IsNullOrWhiteSpace(itemLabel) ? "资料子项" : itemLabel.Trim();
            if (requestedCopyCount < DefaultRequestedCopyCount)
            {
                return $"• [{label}] 份数至少为 {DefaultRequestedCopyCount}。";
            }

            int maxCopyCount = ResolveMaxCopyCount(availableCopyCount);
            if (maxCopyCount <= 0)
            {
                return $"• [{label}] 当前库内无可提份数（可能已全部借出、不还或灭失）。";
            }

            if (requestedCopyCount > maxCopyCount)
            {
                return $"• [{label}] 筛选份数（{requestedCopyCount}）不能超过当前库内份数（{maxCopyCount}）。";
            }

            return null;
        }

        /// <summary>模拟介质整子项筛选是否应展示可编辑份数。</summary>
        public static bool IsEditableSimulatedWholeItem(string mediaKind, ArchiveSearchPoolSelection selection)
        {
            return string.Equals(
                       mediaKind,
                       ArchiveRegisterDomainValues.MediaKindSimulated,
                       StringComparison.Ordinal)
                   && selection.IsWholeMediaItem;
        }
    }
}
