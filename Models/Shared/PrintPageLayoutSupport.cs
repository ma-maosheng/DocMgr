using System.Windows;
using System.Windows.Documents;

namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 打印表单统一采用 A4 页面与 Word「中等」页边距：上下 2.54cm、左右 1.91cm。
    /// 一单一页时，可伸缩明细行高度须为「表单 + 表后说明」共同留足空间后的剩余高度。
    /// </summary>
    public static class PrintPageLayoutSupport
    {
        /// <summary>A4 宽度（DIP，96 DPI，约 210mm）。</summary>
        public const double PageWidthDip = 793.6;

        /// <summary>A4 高度（DIP，96 DPI，约 297mm）。</summary>
        public const double PageHeightDip = 1122.5;

        /// <summary>左右页边距 1.91cm（0.75 英寸，DIP）。</summary>
        public const double MarginHorizontalDip = 72;

        /// <summary>上下页边距 2.54cm（1 英寸，DIP）。</summary>
        public const double MarginVerticalDip = 96;

        /// <summary>A4 宽度（twips，Word/Open XML）。</summary>
        public const int PageWidthTwips = 11906;

        /// <summary>A4 高度（twips，Word/Open XML）。</summary>
        public const int PageHeightTwips = 16838;

        /// <summary>左右页边距 1.91cm（twips）。</summary>
        public const int MarginHorizontalTwips = 1080;

        /// <summary>上下页边距 2.54cm（twips）。</summary>
        public const int MarginVerticalTwips = 1440;

        /// <summary>A4 中等页边距下的可打印区域宽度（twips）。</summary>
        public const int ContentWidthTwips = PageWidthTwips - MarginHorizontalTwips * 2;

        /// <summary>
        /// 版式余量（DIP）：表格外边框、段落间距等难以精确计入的开销，避免表后说明掉到第二页。
        /// </summary>
        public const double LayoutSlackDip = 24;

        /// <summary>版式余量（twips）。</summary>
        public const int LayoutSlackTwips = 360;

        /// <summary>A4 中等页边距下的可打印区域高度（DIP）。</summary>
        public static double UsablePageHeightDip => PageHeightDip - MarginVerticalDip * 2;

        /// <summary>A4 中等页边距下的可打印区域高度（twips）。</summary>
        public static int UsablePageHeightTwips => PageHeightTwips - MarginVerticalTwips * 2;

        /// <summary>FlowDocument 页边距（左、上、右、下）。</summary>
        public static Thickness PagePadding { get; } = new(
            MarginHorizontalDip,
            MarginVerticalDip,
            MarginHorizontalDip,
            MarginVerticalDip);

        /// <summary>
        /// 将 FlowDocument 设为 A4 尺寸与中等页边距。
        /// </summary>
        public static void ApplyA4MediumMargins(FlowDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            document.PageWidth = PageWidthDip;
            document.PageHeight = PageHeightDip;
            document.PagePadding = PagePadding;
        }

        /// <summary>
        /// 表格行外高（内容区高度 + 上下内边距）。预留高度时须按外高累计固定行。
        /// </summary>
        public static double GetTableRowOuterHeightDip(double contentHeightDip, double cellPaddingDip)
        {
            if (contentHeightDip < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(contentHeightDip));
            }

            if (cellPaddingDip < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellPaddingDip));
            }

            return contentHeightDip + cellPaddingDip * 2;
        }

        /// <summary>
        /// Word 表格行外高（内容区高度 + 上下单元格边距，twips）。
        /// </summary>
        public static int GetTableRowOuterHeightTwips(int contentHeightTwips, int cellMarginTwips)
        {
            if (contentHeightTwips < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(contentHeightTwips));
            }

            if (cellMarginTwips < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellMarginTwips));
            }

            return contentHeightTwips + cellMarginTwips * 2;
        }

        /// <summary>
        /// 估算表后说明段落高度（DIP）。
        /// </summary>
        public static double EstimateNoteBlockHeightDip(
            int lineCount,
            double lineHeightDip = 16,
            double topMarginDip = 8)
        {
            if (lineCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lineCount));
            }

            if (lineHeightDip < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lineHeightDip));
            }

            if (topMarginDip < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(topMarginDip));
            }

            return topMarginDip + lineCount * lineHeightDip;
        }

        /// <summary>
        /// 估算表后说明段落高度（twips）。
        /// </summary>
        public static int EstimateNoteBlockHeightTwips(
            int lineCount,
            int lineHeightTwips = 240,
            int topMarginTwips = 120)
        {
            if (lineCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lineCount));
            }

            if (lineHeightTwips < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lineHeightTwips));
            }

            if (topMarginTwips < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(topMarginTwips));
            }

            return topMarginTwips + lineCount * lineHeightTwips;
        }

        /// <summary>
        /// 计算可伸缩明细行的内容区高度（DIP）。
        /// <paramref name="reservedHeightDip"/> 必须包含标题、编号行、全部固定表格行外高、表后说明，
        /// 以及可伸缩行自身的单元格内边距（或通过 <paramref name="stretchRowCellPaddingDip"/> 传入）。
        /// 目标是「表单 + 表后说明」同处一页，不得把说明挤到第二页。
        /// </summary>
        /// <param name="reservedHeightDip">除可伸缩行内容区外已占用高度。</param>
        /// <param name="minimumRowHeightDip">明细行内容区最小高度。</param>
        /// <param name="stretchRowCellPaddingDip">可伸缩行单元格上下内边距（单侧）。</param>
        public static double CalculateStretchRowHeightDip(
            double reservedHeightDip,
            double minimumRowHeightDip,
            double stretchRowCellPaddingDip = 0)
        {
            if (reservedHeightDip < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reservedHeightDip));
            }

            if (minimumRowHeightDip < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumRowHeightDip));
            }

            if (stretchRowCellPaddingDip < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stretchRowCellPaddingDip));
            }

            double availableForStretchContent =
                UsablePageHeightDip
                - reservedHeightDip
                - LayoutSlackDip
                - stretchRowCellPaddingDip * 2;

            return Math.Max(availableForStretchContent, minimumRowHeightDip);
        }

        /// <summary>
        /// 计算可伸缩明细行的内容区高度（twips）。语义同 <see cref="CalculateStretchRowHeightDip"/>。
        /// </summary>
        public static int CalculateStretchRowHeightTwips(
            int reservedHeightTwips,
            int minimumRowHeightTwips,
            int stretchRowCellMarginTwips = 0)
        {
            if (reservedHeightTwips < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reservedHeightTwips));
            }

            if (minimumRowHeightTwips < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumRowHeightTwips));
            }

            if (stretchRowCellMarginTwips < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stretchRowCellMarginTwips));
            }

            int availableForStretchContent =
                UsablePageHeightTwips
                - reservedHeightTwips
                - LayoutSlackTwips
                - stretchRowCellMarginTwips * 2;

            return Math.Max(availableForStretchContent, minimumRowHeightTwips);
        }
    }
}
