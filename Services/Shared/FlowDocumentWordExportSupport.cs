using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.Shared;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.XWPF.UserModel;

namespace DocMgr.Services.Shared
{
    /// <summary>
    /// 将打印用 <see cref="FlowDocument"/> 导出为 A4 中等页边距的 Word 文档（通用回退实现）。
    /// </summary>
    public static class FlowDocumentWordExportSupport
    {
        private const int BodyFontPoints = 10;
        private const int TitleFontPoints = 15;
        private const int CellMarginDxa = 28;

        /// <summary>
        /// 导出 FlowDocument 到 .docx 文件。
        /// </summary>
        public static void ExportToFile(FlowDocument flowDocument, string filePath)
        {
            ArgumentNullException.ThrowIfNull(flowDocument);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            string? directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("导出文件目录无效。", nameof(filePath));
            }

            Directory.CreateDirectory(directory);

            using var document = BuildDocument(flowDocument);
            using var stream = File.Create(filePath);
            document.Write(stream);
        }

        /// <summary>
        /// 从 FlowDocument 标题段落推断默认文件名。
        /// </summary>
        public static string SuggestDefaultFileName(FlowDocument flowDocument)
        {
            ArgumentNullException.ThrowIfNull(flowDocument);

            foreach (Block block in flowDocument.Blocks)
            {
                if (block is not Paragraph paragraph)
                {
                    continue;
                }

                string text = ExtractPlainText(paragraph).Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                string sanitized = SanitizeFileName(text);
                if (sanitized.Length == 0)
                {
                    break;
                }

                if (sanitized.Length > 40)
                {
                    sanitized = sanitized[..40].TrimEnd();
                }

                return sanitized + ".docx";
            }

            return "打印表单.docx";
        }

        private static XWPFDocument BuildDocument(FlowDocument flowDocument)
        {
            var document = new XWPFDocument();
            ConfigurePageSettings(document);

            foreach (Block block in flowDocument.Blocks)
            {
                switch (block)
                {
                    case Paragraph paragraph:
                        AddParagraph(document, paragraph);
                        break;
                    case Table table:
                        AddTable(document, table);
                        break;
                    case BlockUIContainer uiContainer:
                        AddPlainParagraph(document, ExtractUiElementText(uiContainer.Child), centered: false, bold: false, fontPoints: BodyFontPoints);
                        break;
                }
            }

            return document;
        }

        private static void ConfigurePageSettings(XWPFDocument document)
        {
            var body = document.Document.body;
            var sectPr = body.sectPr ?? body.AddNewSectPr();
            var pgSz = sectPr.pgSz ?? sectPr.AddNewPgSz();
            pgSz.w = (ulong)PrintPageLayoutSupport.PageWidthTwips;
            pgSz.h = (ulong)PrintPageLayoutSupport.PageHeightTwips;

            if (sectPr.pgMar == null)
            {
                sectPr.pgMar = new CT_PageMar();
            }

            sectPr.pgMar.top = (ulong)PrintPageLayoutSupport.MarginVerticalTwips;
            sectPr.pgMar.bottom = (ulong)PrintPageLayoutSupport.MarginVerticalTwips;
            sectPr.pgMar.left = (ulong)PrintPageLayoutSupport.MarginHorizontalTwips;
            sectPr.pgMar.right = (ulong)PrintPageLayoutSupport.MarginHorizontalTwips;
        }

        private static void AddParagraph(XWPFDocument document, Paragraph source)
        {
            string text = ExtractPlainText(source).Trim('\r', '\n');
            if (string.IsNullOrWhiteSpace(text) && source.Margin.Bottom <= 0 && source.Margin.Top <= 0)
            {
                return;
            }

            bool isTitle = source.FontSize >= 18 || source.FontWeight == FontWeights.Bold && source.TextAlignment == System.Windows.TextAlignment.Center;
            bool bold = source.FontWeight == FontWeights.Bold || isTitle;
            int fontPoints = isTitle ? TitleFontPoints : BodyFontPoints;
            if (source.FontSize > 0 && source.FontSize < 18)
            {
                fontPoints = (int)Math.Round(source.FontSize * 0.75);
                fontPoints = Math.Clamp(fontPoints, 9, BodyFontPoints);
            }

            AddPlainParagraph(
                document,
                text,
                centered: source.TextAlignment == System.Windows.TextAlignment.Center,
                bold: bold,
                fontPoints: isTitle ? TitleFontPoints : fontPoints);
        }

        private static void AddPlainParagraph(
            XWPFDocument document,
            string text,
            bool centered,
            bool bold,
            int fontPoints)
        {
            var paragraph = document.CreateParagraph();
            paragraph.Alignment = centered ? ParagraphAlignment.CENTER : ParagraphAlignment.LEFT;
            var run = paragraph.CreateRun();
            run.SetText(text ?? string.Empty);
            run.IsBold = bold;
            run.FontSize = fontPoints;
            run.FontFamily = bold ? "黑体" : "宋体";
        }

        private static void AddTable(XWPFDocument document, Table source)
        {
            int columnCount = ResolveColumnCount(source);
            if (columnCount <= 0)
            {
                return;
            }

            var rows = source.RowGroups.SelectMany(group => group.Rows).ToList();
            if (rows.Count == 0)
            {
                return;
            }

            var table = document.CreateTable(rows.Count, columnCount);
            ConfigureTableWidth(table, columnCount);

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                TableRow sourceRow = rows[rowIndex];
                XWPFTableRow targetRow = table.GetRow(rowIndex);
                EnsureCellCount(targetRow, columnCount);

                int columnIndex = 0;
                foreach (TableCell sourceCell in sourceRow.Cells)
                {
                    if (columnIndex >= columnCount)
                    {
                        break;
                    }

                    int span = Math.Max(1, sourceCell.ColumnSpan);
                    span = Math.Min(span, columnCount - columnIndex);

                    XWPFTableCell targetCell = targetRow.GetCell(columnIndex);
                    WriteCell(targetCell, sourceCell);

                    if (span > 1)
                    {
                        targetRow.MergeCells(columnIndex, columnIndex + span - 1);
                    }

                    columnIndex += span;
                }

                // 未填满的右侧单元格保持空边框。
                while (columnIndex < columnCount)
                {
                    ApplyCellBorder(targetRow.GetCell(columnIndex));
                    columnIndex++;
                }
            }

            ApplyTableOuterBorder(table);
        }

        private static int ResolveColumnCount(Table source)
        {
            if (source.Columns.Count > 0)
            {
                return source.Columns.Count;
            }

            int max = 0;
            foreach (TableRow row in source.RowGroups.SelectMany(group => group.Rows))
            {
                int count = 0;
                foreach (TableCell cell in row.Cells)
                {
                    count += Math.Max(1, cell.ColumnSpan);
                }

                max = Math.Max(max, count);
            }

            return max;
        }

        private static void ConfigureTableWidth(XWPFTable table, int columnCount)
        {
            table.Width = 5000;
            var tbl = table.GetCTTbl();
            var tblPr = tbl.tblPr ?? tbl.AddNewTblPr();
            var tblW = tblPr.tblW ?? tblPr.AddNewTblW();
            tblW.type = ST_TblWidth.dxa;
            tblW.w = PrintPageLayoutSupport.ContentWidthTwips.ToString();

            int columnWidth = PrintPageLayoutSupport.ContentWidthTwips / Math.Max(1, columnCount);
            var grid = tbl.tblGrid ?? tbl.AddNewTblGrid();
            grid.gridCol.Clear();
            for (int i = 0; i < columnCount; i++)
            {
                var gridCol = grid.AddNewGridCol();
                gridCol.w = (ulong)columnWidth;
            }
        }

        private static void EnsureCellCount(XWPFTableRow row, int columnCount)
        {
            while (row.GetTableCells().Count < columnCount)
            {
                row.CreateCell();
            }
        }

        private static void WriteCell(XWPFTableCell targetCell, TableCell sourceCell)
        {
            ApplyCellBorder(targetCell);
            targetCell.RemoveParagraph(0);

            string text = ExtractCellText(sourceCell);
            bool isLabel = LooksLikeLabel(sourceCell, text);

            var paragraph = targetCell.AddParagraph();
            paragraph.Alignment = isLabel ? ParagraphAlignment.CENTER : ParagraphAlignment.LEFT;
            var run = paragraph.CreateRun();
            run.SetText(text);
            run.IsBold = isLabel;
            run.FontFamily = isLabel ? "黑体" : "宋体";
            run.FontSize = BodyFontPoints;

            targetCell.SetVerticalAlignment(XWPFTableCell.XWPFVertAlign.CENTER);
            var tcPr = targetCell.GetCTTc().tcPr ?? targetCell.GetCTTc().AddNewTcPr();
            var vAlign = tcPr.vAlign ?? tcPr.AddNewVAlign();
            vAlign.val = ST_VerticalJc.center;

            if (tcPr.tcMar == null)
            {
                tcPr.tcMar = new CT_TcMar();
            }

            tcPr.tcMar.top = CreateMargin(CellMarginDxa);
            tcPr.tcMar.bottom = CreateMargin(CellMarginDxa);
            tcPr.tcMar.left = CreateMargin(CellMarginDxa);
            tcPr.tcMar.right = CreateMargin(CellMarginDxa);
        }

        private static CT_TblWidth CreateMargin(int dxa) =>
            new()
            {
                type = ST_TblWidth.dxa,
                w = dxa.ToString()
            };

        private static bool LooksLikeLabel(TableCell sourceCell, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (text.Length <= 12 && !text.Contains('\n', StringComparison.Ordinal))
            {
                foreach (Block block in sourceCell.Blocks)
                {
                    if (block is BlockUIContainer { Child: Panel panel })
                    {
                        foreach (UIElement child in panel.Children)
                        {
                            if (child is TextBlock textBlock && textBlock.FontWeight == FontWeights.Bold)
                            {
                                return true;
                            }
                        }
                    }

                    if (block is Paragraph paragraph && paragraph.FontWeight == FontWeights.Bold)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string ExtractCellText(TableCell cell)
        {
            var builder = new StringBuilder();
            foreach (Block block in cell.Blocks)
            {
                string part = block switch
                {
                    Paragraph paragraph => ExtractPlainText(paragraph),
                    BlockUIContainer ui => ExtractUiElementText(ui.Child),
                    _ => string.Empty
                };

                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(part.Trim());
            }

            return builder.ToString();
        }

        private static string ExtractPlainText(Paragraph paragraph)
        {
            try
            {
                return new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text?
                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace('\r', '\n')
                    ?? string.Empty;
            }
            catch
            {
                var builder = new StringBuilder();
                foreach (Inline inline in paragraph.Inlines)
                {
                    if (inline is Run run)
                    {
                        builder.Append(run.Text);
                    }
                }

                return builder.ToString();
            }
        }

        private static string ExtractUiElementText(UIElement? element)
        {
            switch (element)
            {
                case null:
                    return string.Empty;
                case TextBlock textBlock:
                    return textBlock.Text ?? string.Empty;
                case TextBox textBox:
                    return textBox.Text ?? string.Empty;
                case Panel panel:
                {
                    var builder = new StringBuilder();
                    foreach (UIElement child in LogicalTreeHelper.GetChildren(panel).OfType<UIElement>())
                    {
                        string part = ExtractUiElementText(child);
                        if (string.IsNullOrWhiteSpace(part))
                        {
                            continue;
                        }

                        if (builder.Length > 0)
                        {
                            builder.Append('\n');
                        }

                        builder.Append(part);
                    }

                    return builder.ToString();
                }
                case Decorator decorator:
                    return ExtractUiElementText(decorator.Child);
                case ContentControl contentControl when contentControl.Content is UIElement contentElement:
                    return ExtractUiElementText(contentElement);
                case ContentControl contentControl:
                    return contentControl.Content?.ToString() ?? string.Empty;
                default:
                    return element.ToString() ?? string.Empty;
            }
        }

        private static void ApplyCellBorder(XWPFTableCell cell)
        {
            var tcPr = cell.GetCTTc().tcPr ?? cell.GetCTTc().AddNewTcPr();
            var borders = tcPr.tcBorders ?? tcPr.AddNewTcBorders();
            SetBorder(borders.top ??= new CT_Border(), 4);
            SetBorder(borders.bottom ??= new CT_Border(), 4);
            SetBorder(borders.left ??= new CT_Border(), 4);
            SetBorder(borders.right ??= new CT_Border(), 4);
        }

        private static void ApplyTableOuterBorder(XWPFTable table)
        {
            var tbl = table.GetCTTbl();
            var tblPr = tbl.tblPr ?? tbl.AddNewTblPr();
            var borders = tblPr.tblBorders ?? tblPr.AddNewTblBorders();
            SetBorder(borders.top ??= new CT_Border(), 12);
            SetBorder(borders.bottom ??= new CT_Border(), 12);
            SetBorder(borders.left ??= new CT_Border(), 12);
            SetBorder(borders.right ??= new CT_Border(), 12);
            SetBorder(borders.insideH ??= new CT_Border(), 4);
            SetBorder(borders.insideV ??= new CT_Border(), 4);
        }

        private static void SetBorder(CT_Border border, ulong size)
        {
            border.val = ST_Border.single;
            border.sz = size;
            border.space = 0;
            border.color = "000000";
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (char ch in value)
            {
                if (Array.IndexOf(invalid, ch) >= 0 || ch < 32)
                {
                    continue;
                }

                builder.Append(ch);
            }

            return builder.ToString().Trim();
        }
    }
}
