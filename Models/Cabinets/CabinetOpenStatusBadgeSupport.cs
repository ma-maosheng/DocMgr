using System;
using System.Collections.Generic;
using System.Windows;

namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 开柜实体卡片四角状态标识解析：一类语义一角；右上盘库角允许失/销（及电子 X）并排。
    /// </summary>
    public static class CabinetOpenStatusBadgeSupport
    {
        public const string ReservationBadgeText = "预订";
        public const string MixedBadgeText = "混";
        public const string NonStandardBadgeText = "非";
        public const string InventoryLostMarkText = "失";
        public const string InventoryScrapMarkText = "销";
        public const string InventoryDamagedMarkText = "X";

        /// <summary>单角徽章展示数据。</summary>
        public sealed record CornerBadge(
            string Text,
            string Background,
            string BorderBrush,
            string Foreground,
            string ToolTip = "")
        {
            public Visibility Visibility => string.IsNullOrWhiteSpace(Text)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        /// <summary>档案盒四角 + 底色提示（是否盘库洗底）。</summary>
        public sealed record ArchiveBoxCornerLayout(
            CornerBadge Nw,
            CornerBadge Ne,
            CornerBadge NeSecondary,
            CornerBadge Se,
            CornerBadge Sw,
            bool HasInventoryMarkWash,
            string NonStandardToolTipSuffix);

        /// <summary>介质卡四角；中部类型 pill 仅在无业务角标时显示。</summary>
        public sealed record MediaCornerLayout(
            CornerBadge Nw,
            CornerBadge Ne,
            CornerBadge NeSecondary,
            CornerBadge NeTertiary,
            CornerBadge Se,
            CornerBadge Sw,
            bool HideCenterTypePill);

        /// <summary>
        /// 解析档案盒四角：左上混放/非标、右上失/销（可并排）、右下待还、左下预订。
        /// </summary>
        public static ArchiveBoxCornerLayout ResolveArchiveBox(
            bool isMixedPlacement,
            string? inventoryMarkBadgeText,
            int pendingReturnCopyCount,
            bool hasOccupationLock,
            string? occupationLockToolTipText,
            bool isNonStandardSpecification)
        {
            string nonStandardTip = string.Empty;
            CornerBadge nw = ResolveArchiveBoxNw(
                isMixedPlacement,
                isNonStandardSpecification,
                out nonStandardTip);

            var (ne, neSecondary, _) = ResolveInventoryNeBadges(inventoryMarkBadgeText, includeDamaged: false);
            CornerBadge se = pendingReturnCopyCount <= 0
                ? Hidden()
                : Create(
                    pendingReturnCopyCount > 1 ? $"待还{pendingReturnCopyCount}份" : "待还",
                    "#FFEDD5",
                    "#FDBA74",
                    "#C2410C");

            string lockTip = occupationLockToolTipText?.Trim() ?? string.Empty;
            CornerBadge sw = !isMixedPlacement && hasOccupationLock
                ? Create(ReservationBadgeText, "#FEF3C7", "#D97706", "#B45309", lockTip)
                : Hidden();

            bool inventoryWash = ne.Visibility == Visibility.Visible
                || neSecondary.Visibility == Visibility.Visible;
            return new ArchiveBoxCornerLayout(nw, ne, neSecondary, se, sw, inventoryWash, nonStandardTip);
        }

        /// <summary>
        /// 解析介质卡四角：左上序号、右上失/销/X（可并排）、右下待还、左下预订。
        /// </summary>
        public static MediaCornerLayout ResolveMedia(
            string? archiveSequenceText,
            int archiveSequenceNumber,
            string? inventoryMarkBadgeText,
            bool isPendingReturn,
            bool hasOccupationLock,
            string? occupationLockToolTipText)
        {
            string sequence = !string.IsNullOrWhiteSpace(archiveSequenceText)
                ? archiveSequenceText.Trim()
                : (archiveSequenceNumber > 0 ? archiveSequenceNumber.ToString("D2") : string.Empty);
            CornerBadge nw = string.IsNullOrWhiteSpace(sequence)
                ? Hidden()
                : Create(sequence, "#FEF3C7", "#F59E0B", "#92400E");

            var (ne, neSecondary, neTertiary) = ResolveInventoryNeBadges(inventoryMarkBadgeText, includeDamaged: true);
            CornerBadge se = isPendingReturn
                ? Create("待还", "#FFEDD5", "#FDBA74", "#C2410C")
                : Hidden();
            CornerBadge sw = hasOccupationLock
                ? Create(
                    ReservationBadgeText,
                    "#FEF3C7",
                    "#D97706",
                    "#B45309",
                    occupationLockToolTipText?.Trim() ?? string.Empty)
                : Hidden();

            bool hideCenter = ne.Visibility == Visibility.Visible
                || neSecondary.Visibility == Visibility.Visible
                || neTertiary.Visibility == Visibility.Visible
                || se.Visibility == Visibility.Visible
                || sw.Visibility == Visibility.Visible;
            return new MediaCornerLayout(nw, ne, neSecondary, neTertiary, se, sw, hideCenter);
        }

        /// <summary>
        /// 组装模拟档案盒盘库角文案：有丢失标「失」、有拟销标「销」，可并存（逗号分隔）；不再使用「空」。
        /// </summary>
        public static string BuildSimulatedInventoryMarkBadgeText(int inventoryLostCopyCount, int inventoryScrapCopyCount)
        {
            bool hasLost = inventoryLostCopyCount > 0;
            bool hasScrap = inventoryScrapCopyCount > 0;
            if (hasLost && hasScrap)
            {
                return $"{InventoryLostMarkText},{InventoryScrapMarkText}";
            }

            if (hasLost)
            {
                return InventoryLostMarkText;
            }

            if (hasScrap)
            {
                return InventoryScrapMarkText;
            }

            return string.Empty;
        }

        /// <summary>盘库标识 tooltip / 页脚展示用顿号连接（含电子 X）。</summary>
        public static string FormatInventoryMarkDisplayText(string? inventoryMarkBadgeText)
        {
            ParseInventoryMarks(inventoryMarkBadgeText, includeDamaged: true, out bool hasLost, out bool hasScrap, out bool hasDamaged);
            var parts = new List<string>(3);
            if (hasLost)
            {
                parts.Add(InventoryLostMarkText);
            }

            if (hasScrap)
            {
                parts.Add(InventoryScrapMarkText);
            }

            if (hasDamaged)
            {
                parts.Add(InventoryDamagedMarkText);
            }

            return parts.Count == 0 ? string.Empty : string.Join("、", parts);
        }

        /// <summary>占用角标统一显示「预订」（数据层仍可用锁符号）。</summary>
        public static string NormalizeReservationDisplayText(bool hasOccupationLock, string? rawBadgeText)
        {
            if (!hasOccupationLock)
            {
                return string.Empty;
            }

            string raw = rawBadgeText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw)
                || string.Equals(raw, CabinetOccupationLockSupport.LockBadgeMark, StringComparison.Ordinal)
                || string.Equals(raw, "占用", StringComparison.Ordinal))
            {
                return ReservationBadgeText;
            }

            return raw;
        }

        private static CornerBadge ResolveArchiveBoxNw(
            bool isMixedPlacement,
            bool isNonStandardSpecification,
            out string nonStandardTip)
        {
            nonStandardTip = string.Empty;
            if (isMixedPlacement)
            {
                if (isNonStandardSpecification)
                {
                    nonStandardTip = "规格：非标（见图例）";
                }

                string tip = string.IsNullOrWhiteSpace(nonStandardTip)
                    ? "混放"
                    : $"混放；{nonStandardTip}";
                return Create(MixedBadgeText, "#FEE2E2", "#FCA5A5", "#B91C1C", tip);
            }

            if (isNonStandardSpecification)
            {
                return Create(NonStandardBadgeText, "#EDE9FE", "#C4B5FD", "#6D28D9", "非标");
            }

            return Hidden();
        }

        /// <summary>右上盘库：顺序 失 → 销 → X（电子），有则并排。</summary>
        private static (CornerBadge Primary, CornerBadge Secondary, CornerBadge Tertiary) ResolveInventoryNeBadges(
            string? inventoryMarkBadgeText,
            bool includeDamaged)
        {
            ParseInventoryMarks(inventoryMarkBadgeText, includeDamaged, out bool hasLost, out bool hasScrap, out bool hasDamaged);
            var badges = new List<CornerBadge>(3);
            if (hasLost)
            {
                badges.Add(CreateInventoryMark(InventoryLostMarkText));
            }

            if (hasScrap)
            {
                badges.Add(CreateInventoryMark(InventoryScrapMarkText));
            }

            if (hasDamaged)
            {
                badges.Add(CreateInventoryMark(InventoryDamagedMarkText));
            }

            CornerBadge primary = badges.Count > 0 ? badges[0] : Hidden();
            CornerBadge secondary = badges.Count > 1 ? badges[1] : Hidden();
            CornerBadge tertiary = badges.Count > 2 ? badges[2] : Hidden();
            return (primary, secondary, tertiary);
        }

        private static void ParseInventoryMarks(
            string? inventoryMarkBadgeText,
            bool includeDamaged,
            out bool hasLost,
            out bool hasScrap,
            out bool hasDamaged)
        {
            hasLost = false;
            hasScrap = false;
            hasDamaged = false;
            string raw = inventoryMarkBadgeText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            // 兼容「失,销」「失·销」「失销」「X」以及历史单字；忽略遗留「空」。
            hasLost = raw.Contains(InventoryLostMarkText, StringComparison.Ordinal);
            hasScrap = raw.Contains(InventoryScrapMarkText, StringComparison.Ordinal);
            if (includeDamaged)
            {
                hasDamaged = raw.Contains(InventoryDamagedMarkText, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static CornerBadge CreateInventoryMark(string text)
            => Create(text, "#FEE2E2", "#FCA5A5", "#B91C1C");

        private static CornerBadge Create(
            string text,
            string background,
            string borderBrush,
            string foreground,
            string toolTip = "")
            => new(text, background, borderBrush, foreground, toolTip);

        private static CornerBadge Hidden()
            => new(string.Empty, "#00000000", "#00000000", "#00000000");
    }
}
