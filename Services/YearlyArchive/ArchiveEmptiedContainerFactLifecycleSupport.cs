using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 空盒/空袋释放占位时，按资料子项份数将立档事实生命周期对齐为已转移或已销毁。
    /// </summary>
    internal static class ArchiveEmptiedContainerFactLifecycleSupport
    {
        /// <summary>
        /// 库内与待还均为 0 时：纯灭失 → 已销毁；含不还（或与灭失混合）→ 已转移。
        /// 仍有库内或待还时返回 null（不改生命周期）。
        /// </summary>
        public static string? ResolveTerminalLifecycleStatus(MediaItemCopyCountBreakdown breakdown)
        {
            if (breakdown.CurrentInArchiveCopyCount > 0 || breakdown.PendingReturnCopyCount > 0)
            {
                return null;
            }

            if (breakdown.LostCopyCount > 0 && breakdown.NoReturnCopyCount == 0)
            {
                return FilingFactLifecycleStatus.Destroyed;
            }

            if (breakdown.NoReturnCopyCount > 0 || breakdown.LostCopyCount > 0)
            {
                return FilingFactLifecycleStatus.Transferred;
            }

            return null;
        }

        /// <summary>
        /// 清空当前位置，并在可判定终态时写入生命周期与借出提示。
        /// </summary>
        public static void ApplyOnContainerEmptied(
            YearlyArchiveFilingFact fact,
            MediaItemCopyCountBreakdown breakdown,
            DateTime operatedAt)
        {
            ArgumentNullException.ThrowIfNull(fact);

            fact.CurrentStorageLocation = string.Empty;

            string? status = ResolveTerminalLifecycleStatus(breakdown);
            if (status == null)
            {
                return;
            }

            // 已是终态则保留（避免覆盖更精确的办结写入），仅清位置。
            if (string.Equals(fact.LifecycleStatus, FilingFactLifecycleStatus.Destroyed, StringComparison.Ordinal)
                || string.Equals(fact.LifecycleStatus, FilingFactLifecycleStatus.Transferred, StringComparison.Ordinal)
                || string.Equals(fact.LifecycleStatus, FilingFactLifecycleStatus.Disposed, StringComparison.Ordinal))
            {
                ClearBorrowHint(fact, operatedAt);
                return;
            }

            fact.LifecycleStatus = status;
            ClearBorrowHint(fact, operatedAt);
            fact.LifecycleUpdatedAt = operatedAt;
        }

        private static void ClearBorrowHint(YearlyArchiveFilingFact fact, DateTime operatedAt)
        {
            fact.BorrowHintLevel = FilingFactBorrowHintLevel.None;
            fact.BorrowHintText = string.Empty;
            fact.BorrowHintUpdatedAt = operatedAt;
        }
    }
}
