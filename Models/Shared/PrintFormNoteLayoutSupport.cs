using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 打印表后说明：标签独占左列，序号正文在右列左对齐。
    /// 禁止用空格模拟缩进，否则加粗「备注：」「说明：」与半角空格字宽不一致，1、2、3 会错位。
    /// </summary>
    public static class PrintFormNoteLayoutSupport
    {
        /// <summary>
        /// 生成无边框的表后说明。各条序号共用右列左缘。
        /// </summary>
        public static Table Create(
            string label,
            IReadOnlyList<string> lines,
            FontFamily fontFamily,
            double fontSize,
            double lineHeight,
            double topMargin)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(label);
            ArgumentNullException.ThrowIfNull(lines);
            ArgumentNullException.ThrowIfNull(fontFamily);
            ValidateMetrics(fontSize, lineHeight, topMargin);

            string labelText = label.Trim();
            IReadOnlyList<string> normalized = NormalizeLines(lines);
            double bodyStar = GetBodyStarWeight(labelText, fontFamily, fontSize);

            var table = new Table
            {
                CellSpacing = 0,
                Margin = new Thickness(0, topMargin, 0, 0),
                FontFamily = fontFamily,
                FontSize = fontSize,
                BorderBrush = null,
                BorderThickness = new Thickness(0)
            };
            // 权重用比值，不用 DIP 原值，避免 FlowDocument 把左列拉满整页。
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(bodyStar, GridUnitType.Star) });

            var labelParagraph = new Paragraph(new Run(labelText) { FontWeight = FontWeights.Bold })
            {
                Margin = new Thickness(0),
                LineHeight = lineHeight,
                FontFamily = fontFamily,
                FontSize = fontSize
            };

            var bodyParagraph = new Paragraph
            {
                Margin = new Thickness(0),
                LineHeight = lineHeight,
                FontFamily = fontFamily,
                FontSize = fontSize
            };
            for (int i = 0; i < normalized.Count; i++)
            {
                if (i > 0)
                {
                    bodyParagraph.Inlines.Add(new LineBreak());
                }

                bodyParagraph.Inlines.Add(new Run(normalized[i]));
            }

            var row = new TableRow();
            row.Cells.Add(CreatePlainCell(labelParagraph));
            row.Cells.Add(CreatePlainCell(bodyParagraph));

            var group = new TableRowGroup();
            group.Rows.Add(row);
            table.RowGroups.Add(group);
            return table;
        }

        /// <summary>
        /// 按右列实际宽度估算表后说明高度（含 1 行安全余量）。
        /// </summary>
        public static double EstimateHeightDip(
            string label,
            IReadOnlyList<string> lines,
            FontFamily fontFamily,
            double fontSize,
            double lineHeight,
            double topMargin)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(label);
            ArgumentNullException.ThrowIfNull(lines);
            ArgumentNullException.ThrowIfNull(fontFamily);
            ValidateMetrics(fontSize, lineHeight, topMargin);

            string text = string.Join("\n", NormalizeLines(lines));
            double width = GetBodyWidthDip(label.Trim(), fontFamily, fontSize);
            return PrintPageLayoutSupport.EstimateNoteBlockHeightFromTextWithSafetyDip(
                text,
                width,
                fontSize,
                lineHeight,
                topMargin);
        }

        /// <summary>标签列宽（DIP）：加粗实测宽度再留 2 DIP，避免「备注：」折行。</summary>
        public static double GetLabelColumnWidthDip(string label, FontFamily fontFamily, double fontSize)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(label);
            ArgumentNullException.ThrowIfNull(fontFamily);
            if (fontSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fontSize));
            }

            return MeasureLabelWidthDip(label.Trim(), fontFamily, fontSize) + 2;
        }

        /// <summary>右列可用宽度（DIP）：可打印宽减去标签列。</summary>
        public static double GetBodyWidthDip(string label, FontFamily fontFamily, double fontSize)
        {
            double labelWidth = GetLabelColumnWidthDip(label, fontFamily, fontSize);
            return Math.Max(fontSize, PrintPageLayoutSupport.ContentWidthDip - labelWidth);
        }

        /// <summary>加粗标签的实际宽度（DIP），供列宽与 Word 列宽换算。</summary>
        public static double MeasureLabelWidthDip(string label, FontFamily fontFamily, double fontSize)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(label);
            ArgumentNullException.ThrowIfNull(fontFamily);
            if (fontSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fontSize));
            }

            var typeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var formatted = new FormattedText(
                label.Trim(),
                CultureInfo.GetCultureInfo("zh-CN"),
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Black,
                1.0);
            return Math.Ceiling(formatted.WidthIncludingTrailingWhitespace);
        }

        private static double GetBodyStarWeight(string label, FontFamily fontFamily, double fontSize)
        {
            double labelWidth = Math.Max(1, GetLabelColumnWidthDip(label, fontFamily, fontSize));
            double bodyWidth = GetBodyWidthDip(label, fontFamily, fontSize);
            return Math.Max(1, bodyWidth / labelWidth);
        }

        private static TableCell CreatePlainCell(Paragraph paragraph) =>
            new(paragraph)
            {
                BorderBrush = null,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                TextAlignment = TextAlignment.Left
            };

        private static List<string> NormalizeLines(IReadOnlyList<string> lines)
        {
            var result = new List<string>(lines.Count);
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                result.Add(line.Trim());
            }

            return result;
        }

        private static void ValidateMetrics(double fontSize, double lineHeight, double topMargin)
        {
            if (fontSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fontSize));
            }

            if (lineHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lineHeight));
            }

            if (topMargin < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(topMargin));
            }
        }
    }
}
