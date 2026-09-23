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

        /// <summary>A4 中等页边距下的可打印区域宽度（DIP）。</summary>
        public static double ContentWidthDip => PageWidthDip - MarginHorizontalDip * 2;

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

        /// <summary>申请审批单正文/标签字号（DIP，与各工厂 BodyFontSize=12 一致）。</summary>
        public const double ApprovalFormBodyFontSizeDip = 12;

        /// <summary>
        /// 申请审批单标签列可容纳汉字数。
        /// 须覆盖最长节点名「分管生产院长签字」（8 字）。
        /// </summary>
        public const int ApprovalFormLabelColumnCharCount = 8;

        /// <summary>
        /// 申请审批单标签列目标宽度（DIP）= 8 字 × 字号 + 左右单元格内边距。
        /// 仅作语义说明；实际列宽见 <see cref="ApprovalFormMainColumnStars"/>。
        /// </summary>
        public static double ApprovalFormLabelColumnWidthDip =>
            ApprovalFormLabelColumnCharCount * ApprovalFormBodyFontSizeDip
            + TableCellPaddingDip * 2;

        /// <summary>
        /// 申请审批单主表列宽 Star 权重（标签|内容|标签|内容）。
        /// <para>
        /// 1.6:3.4 使标签列约占可打印宽度的 8 汉字份额（约 <see cref="ApprovalFormLabelColumnWidthDip"/>）。
        /// </para>
        /// <para>
        /// 权重总和必须保持较小（约 10）。FlowDocument 在 Star 权重过大（如用 DIP 原值 104/220）时会错乱，
        /// 表现为最左列占满整页、其余列不可见。禁止再用 Absolute 列宽（会被整页拉伸）。
        /// </para>
        /// </summary>
        public static readonly double[] ApprovalFormMainColumnStars = [1.6, 3.4, 1.6, 3.4];

        /// <summary>
        /// 为四列「标签|内容|标签|内容」主表应用统一列宽（仅小数值 Star，见 <see cref="ApprovalFormMainColumnStars"/>）。
        /// </summary>
        public static void ApplyApprovalFormMainTableColumns(Table table)
        {
            ArgumentNullException.ThrowIfNull(table);

            for (int i = 0; i < ApprovalFormMainColumnStars.Length; i++)
            {
                table.Columns.Add(new TableColumn
                {
                    Width = new GridLength(ApprovalFormMainColumnStars[i], GridUnitType.Star)
                });
            }
        }

        /// <summary>
        /// 版式余量（DIP）：表格外边框、段落间距、行底边框等难以精确计入的开销，
        /// 避免表后说明最后一行掉到第二页。
        /// </summary>
        public const double LayoutSlackDip = 12;

        /// <summary>版式余量（twips）。</summary>
        public const int LayoutSlackTwips = 180;

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
        /// 申请审批单：标题相对可打印区顶端的额外下移（DIP）。
        /// 与上页边距叠加后，各申请审批单标题距纸张顶端距离恒为
        /// <see cref="MarginVerticalDip"/> + 本值。
        /// </summary>
        public const double ApprovalFormTitleTopInsetDip = 0;

        /// <summary>申请审批单标题段落后边距（DIP）。</summary>
        public const double ApprovalFormTitleBottomMarginDip = 12;

        /// <summary>申请审批单标题字号（DIP/磅，FlowDocument）。</summary>
        public const double ApprovalFormTitleFontSize = 22;

        /// <summary>申请审批单用于比例间距的基准行高（DIP）。</summary>
        public const double ApprovalFormLineHeightDip = 20;

        /// <summary>申请单编号/日期行与主表间距（0.3 行高，DIP）。</summary>
        public static double ApprovalFormHeaderToTableGapDip => ApprovalFormLineHeightDip * 0.3;

        /// <summary>
        /// 申请审批单标题块预估占用高度（DIP）：顶插 + 一行标题行高 + 底边距。
        /// 撑满一页计算时用本值替代各工厂散落的 TitleBlockHeight。
        /// </summary>
        public static double ApprovalFormTitleBlockHeightDip =>
            ApprovalFormTitleTopInsetDip
            + ApprovalFormLineHeightDip * 1.5
            + ApprovalFormTitleBottomMarginDip;

        /// <summary>申请审批单标题相对可打印区顶端的额外下移（twips）。</summary>
        public const int ApprovalFormTitleTopInsetTwips = 0;

        /// <summary>申请审批单标题段落后边距（twips）。</summary>
        public const int ApprovalFormTitleBottomMarginTwips = 180;

        /// <summary>申请审批单基准行高（twips，对应 <see cref="ApprovalFormLineHeightDip"/>）。</summary>
        public const int ApprovalFormLineHeightTwips = 300;

        /// <summary>申请单编号/日期行与主表间距（0.3 行高，twips）。</summary>
        public static int ApprovalFormHeaderToTableGapTwips =>
            (int)(ApprovalFormLineHeightTwips * 0.3);

        /// <summary>申请审批单标题块预估占用高度（twips）。</summary>
        public const int ApprovalFormTitleBlockHeightTwips =
            ApprovalFormTitleTopInsetTwips
            + ApprovalFormLineHeightTwips * 3 / 2
            + ApprovalFormTitleBottomMarginTwips;

        /// <summary>申请审批单标题段落边距（FlowDocument）。</summary>
        public static Thickness ApprovalFormTitleMargin { get; } = new(
            0,
            ApprovalFormTitleTopInsetDip,
            0,
            ApprovalFormTitleBottomMarginDip);

        /// <summary>申请单编号/日期行表格边距（FlowDocument，底边 = 0.3 行高）。</summary>
        public static Thickness ApprovalFormHeaderTableMargin { get; } = new(
            0,
            0,
            0,
            ApprovalFormHeaderToTableGapDip);

        /// <summary>打印表格单元格标准上下内边距（单侧，DIP）。</summary>
        public const double TableCellPaddingDip = 4;

        /// <summary>
        /// 表格内容区高度：约 1 行正文（12pt + 行距余量）。
        /// 多行内容行高 = 本值 × 行数（见 <see cref="GetTableRowContentHeightDip"/>）。
        /// </summary>
        public const double TableRowContentHeightOneLineDip = 32;

        /// <summary>2 行正文内容区高度（DIP）。</summary>
        public const double TableRowContentHeightTwoLinesDip = TableRowContentHeightOneLineDip * 2;

        /// <summary>3 行正文内容区高度（DIP）。</summary>
        public const double TableRowContentHeightThreeLinesDip = TableRowContentHeightOneLineDip * 3;

        /// <summary>打印表格单元格标准上下边距（单侧，twips）。</summary>
        public const int TableCellPaddingTwips = 60;

        /// <summary>1 行正文内容区高度（twips）。</summary>
        public const int TableRowContentHeightOneLineTwips = 480;

        /// <summary>2 行正文内容区高度（twips）。</summary>
        public const int TableRowContentHeightTwoLinesTwips = TableRowContentHeightOneLineTwips * 2;

        /// <summary>3 行正文内容区高度（twips）。</summary>
        public const int TableRowContentHeightThreeLinesTwips = TableRowContentHeightOneLineTwips * 3;

        /// <summary>
        /// 按正文占用行数计算表格内容区高度（DIP）。可伸缩明细行仍用 <see cref="CalculateStretchRowHeightDip"/>。
        /// </summary>
        public static double GetTableRowContentHeightDip(int contentLineCount)
        {
            if (contentLineCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(contentLineCount));
            }

            return TableRowContentHeightOneLineDip * contentLineCount;
        }

        /// <summary>按正文占用行数计算表格内容区高度（twips）。</summary>
        public static int GetTableRowContentHeightTwips(int contentLineCount)
        {
            if (contentLineCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(contentLineCount));
            }

            return TableRowContentHeightOneLineTwips * contentLineCount;
        }

        /// <summary>
        /// 申请审批单「标签列 + 右侧合并三列」时，内容区可用宽度（DIP，已扣左右内边距）。
        /// </summary>
        public static double GetApprovalFormSpannedContentWidthDip()
        {
            double starSum = 0;
            for (int i = 0; i < ApprovalFormMainColumnStars.Length; i++)
            {
                starSum += ApprovalFormMainColumnStars[i];
            }

            if (starSum <= 0)
            {
                return Math.Max(ApprovalFormBodyFontSizeDip, ContentWidthDip - TableCellPaddingDip * 2);
            }

            double labelShare = ApprovalFormMainColumnStars[0] / starSum;
            double contentWidth = ContentWidthDip * (1 - labelShare) - TableCellPaddingDip * 2;
            return Math.Max(ApprovalFormBodyFontSizeDip, contentWidth);
        }

        /// <summary>
        /// 按显式换行与按字宽折行估算正文占用行数（默认至少 1 行）。
        /// 汉字宽度按 <paramref name="fontSizeDip"/> 计；不依赖 UI Measure，供打印行高预估。
        /// </summary>
        public static int EstimateTextLineCount(
            string? text,
            double availableContentWidthDip,
            double fontSizeDip = ApprovalFormBodyFontSizeDip,
            int minimumLineCount = 1,
            int maximumLineCount = 20)
        {
            if (minimumLineCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumLineCount));
            }

            if (maximumLineCount < minimumLineCount)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumLineCount));
            }

            if (fontSizeDip <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fontSizeDip));
            }

            string normalized = string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace('\r', '\n')
                    .TrimEnd();

            if (normalized.Length == 0)
            {
                return minimumLineCount;
            }

            double width = Math.Max(fontSizeDip, availableContentWidthDip);
            int charsPerLine = Math.Max(1, (int)Math.Floor(width / fontSizeDip));

            int totalLines = 0;
            string[] paragraphs = normalized.Split('\n');
            for (int i = 0; i < paragraphs.Length; i++)
            {
                int length = paragraphs[i].Length;
                totalLines += length == 0
                    ? 1
                    : (length + charsPerLine - 1) / charsPerLine;
            }

            if (totalLines < minimumLineCount)
            {
                return minimumLineCount;
            }

            return totalLines > maximumLineCount ? maximumLineCount : totalLines;
        }

        /// <summary>
        /// 按正文实际行数解析表格内容区高度：默认 1 行，随内容增高，受 <paramref name="maximumLineCount"/> 上限约束。
        /// </summary>
        public static double ResolveRowContentHeightDip(
            string? text,
            double availableContentWidthDip,
            double fontSizeDip = ApprovalFormBodyFontSizeDip,
            int minimumLineCount = 1,
            int maximumLineCount = 20)
        {
            int lineCount = EstimateTextLineCount(
                text,
                availableContentWidthDip,
                fontSizeDip,
                minimumLineCount,
                maximumLineCount);
            return GetTableRowContentHeightDip(lineCount);
        }

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
        /// 按表后说明实际文案估算占用高度（含折行），供撑满一页时预留，避免备注掉到第二页。
        /// </summary>
        public static double EstimateNoteBlockHeightFromTextDip(
            string? text,
            double availableWidthDip,
            double fontSizeDip = 10.5,
            double lineHeightDip = 16,
            double topMarginDip = 8,
            int maximumLineCount = 30)
        {
            int lineCount = EstimateTextLineCount(
                text,
                availableWidthDip,
                fontSizeDip,
                minimumLineCount: 1,
                maximumLineCount: maximumLineCount);
            return EstimateNoteBlockHeightDip(lineCount, lineHeightDip, topMarginDip);
        }

        /// <summary>
        /// 同 <see cref="EstimateNoteBlockHeightFromTextDip"/>，并多留 1 行余量
        /// （FlowDocument 段落行距/分页边界常比纯行数估算略高）。
        /// </summary>
        public static double EstimateNoteBlockHeightFromTextWithSafetyDip(
            string? text,
            double availableWidthDip,
            double fontSizeDip = 10.5,
            double lineHeightDip = 16,
            double topMarginDip = 8,
            int maximumLineCount = 30)
        {
            return EstimateNoteBlockHeightFromTextDip(
                    text,
                    availableWidthDip,
                    fontSizeDip,
                    lineHeightDip,
                    topMarginDip,
                    maximumLineCount)
                + lineHeightDip;
        }

        /// <summary>
        /// 申请审批单合并内容列上的可变文本行高：默认 1 行，按正文增高。
        /// </summary>
        public static double ResolveSpannedContentRowHeightDip(
            string? text,
            int maximumLineCount = 5,
            int minimumLineCount = 1) =>
            ResolveRowContentHeightDip(
                text,
                GetApprovalFormSpannedContentWidthDip(),
                minimumLineCount: minimumLineCount,
                maximumLineCount: maximumLineCount);

        /// <summary>
        /// 主表行底边框预留高度（DIP）。每行约 1 DIP；可伸缩行另计 1。
        /// </summary>
        public static double EstimateTableBottomBorderHeightDip(int fixedRowCount, bool includeStretchRow = true)
        {
            if (fixedRowCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fixedRowCount));
            }

            return fixedRowCount + (includeStretchRow ? 1 : 0);
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
        /// <param name="minimumRowHeightDip">明细行内容区最小高度（正文完整显示所需）。</param>
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

            // 有剩余空间时：用剩余空间撑满一页，但不超过剩余空间，保证表后说明同页。
            // 正文下限大于剩余空间时：仍不超过剩余空间（单元格内折行/裁切优先于备注掉页）。
            if (availableForStretchContent > 0)
            {
                return availableForStretchContent;
            }

            // 页面已被其它区块占满：退回正文下限（可能挤到第二页，属极端情况）。
            return minimumRowHeightDip;
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

            if (availableForStretchContent > 0)
            {
                return availableForStretchContent;
            }

            return minimumRowHeightTwips;
        }
    }
}
