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
        private const int CellMarginDxa = PrintPageLayoutSupport.TableCellPaddingTwips;

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
                        AddPlainParagraph(
                            document,
                            ExtractUiElementText(uiContainer.Child),
                            centered: false,
                            bold: false,
                            isTitle: false,
                            isFooter: false);
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
            // 空段落一律跳过，避免编号行与主表之间出现多余空行。
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            bool isTitle = source.FontSize >= 18
                || (source.FontWeight == FontWeights.Bold
                    && source.TextAlignment == System.Windows.TextAlignment.Center
                    && source.FontSize >= 16);
            bool isFooter = source.FontSize > 0 && source.FontSize <= 11;
            bool bold = source.FontWeight == FontWeights.Bold || isTitle;

            AddPlainParagraph(
                document,
                text,
                centered: source.TextAlignment == System.Windows.TextAlignment.Center,
                bold: bold,
                isTitle: isTitle,
                isFooter: isFooter && !isTitle);
        }

        private static void AddPlainParagraph(
            XWPFDocument document,
            string text,
            bool centered,
            bool bold,
            bool isTitle,
            bool isFooter)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var paragraph = document.CreateParagraph();
            paragraph.Alignment = centered ? ParagraphAlignment.CENTER : ParagraphAlignment.LEFT;
            WordExportFontSupport.ApplyHalfLineParagraphSpacing(paragraph);
            var run = paragraph.CreateRun();
            run.SetText(text ?? string.Empty);
            if (isTitle)
            {
                WordExportFontSupport.ApplyTitle(run);
            }
            else if (isFooter)
            {
                WordExportFontSupport.ApplyFooter(run, bold);
            }
            else
            {
                WordExportFontSupport.ApplyBody(run, bold);
            }
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

            // 打印预览里「申请单编号/申请日期」等为无边框表头表，导出时不得加边框，避免看起来像主表内行。
            bool isOutsideMetaHeader = !HasVisibleBorder(source);

            var table = document.CreateTable(rows.Count, columnCount);
            int[] columnWidths = ResolveColumnWidthsTwips(source, columnCount);
            WordExportTableLayoutSupport.ApplyFixedTableLayout(table, columnWidths);

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
                    WriteCell(targetCell, sourceCell, applyBorder: !isOutsideMetaHeader);

                    if (span > 1)
                    {
                        targetRow.MergeCells(columnIndex, columnIndex + span - 1);
                    }

                    columnIndex += span;
                }

                while (columnIndex < columnCount)
                {
                    XWPFTableCell emptyCell = targetRow.GetCell(columnIndex);
                    if (isOutsideMetaHeader)
                    {
                        ApplyNilCellBorder(emptyCell);
                    }
                    else
                    {
                        ApplyCellBorder(emptyCell);
                    }

                    columnIndex++;
                }

                WordExportTableLayoutSupport.ApplyRowCellWidths(targetRow, columnWidths);
            }

            if (isOutsideMetaHeader)
            {
                ApplyNilTableOuterBorder(table);
            }
            else
            {
                ApplyTableOuterBorder(table);
            }
        }

        private static bool HasVisibleBorder(Table source)
        {
            if (source.BorderBrush == null)
            {
                return false;
            }

            Thickness thickness = source.BorderThickness;
            return thickness.Left > 0
                || thickness.Top > 0
                || thickness.Right > 0
                || thickness.Bottom > 0;
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

        private static int[] ResolveColumnWidthsTwips(Table source, int columnCount)
        {
            if (source.Columns.Count != columnCount)
            {
                return WordExportTableLayoutSupport.DistributeEqualWidths(
                    PrintPageLayoutSupport.ContentWidthTwips,
                    columnCount);
            }

            int contentWidth = PrintPageLayoutSupport.ContentWidthTwips;
            var fixedTwips = new int?[columnCount];
            var stars = new double[columnCount];
            int fixedSum = 0;
            double starSum = 0;
            bool anyStar = false;
            bool anyFixed = false;

            for (int i = 0; i < columnCount; i++)
            {
                GridLength width = source.Columns[i].Width;
                if (width.GridUnitType == GridUnitType.Pixel && width.Value > 0)
                {
                    // DIP → twips：96 DPI 下 1 DIP = 15 twips。
                    int twips = (int)Math.Round(width.Value * 1440.0 / 96.0);
                    fixedTwips[i] = twips;
                    fixedSum += twips;
                    anyFixed = true;
                }
                else if (width.GridUnitType == GridUnitType.Star && width.Value > 0)
                {
                    stars[i] = width.Value;
                    starSum += width.Value;
                    anyStar = true;
                }
                else
                {
                    stars[i] = 1;
                    starSum += 1;
                    anyStar = true;
                }
            }

            if (!anyStar && !anyFixed)
            {
                return WordExportTableLayoutSupport.DistributeEqualWidths(contentWidth, columnCount);
            }

            if (!anyFixed)
            {
                return WordExportTableLayoutSupport.DistributeWidths(contentWidth, stars);
            }

            if (!anyStar)
            {
                var absolute = new int[columnCount];
                for (int i = 0; i < columnCount; i++)
                {
                    absolute[i] = fixedTwips[i] ?? 0;
                }

                return absolute;
            }

            int remaining = Math.Max(0, contentWidth - fixedSum);
            var result = new int[columnCount];
            int allocatedStars = 0;
            int lastStarIndex = -1;
            for (int i = 0; i < columnCount; i++)
            {
                if (!fixedTwips[i].HasValue)
                {
                    lastStarIndex = i;
                }
            }

            for (int i = 0; i < columnCount; i++)
            {
                if (fixedTwips[i].HasValue)
                {
                    result[i] = fixedTwips[i]!.Value;
                    continue;
                }

                if (i == lastStarIndex)
                {
                    result[i] = Math.Max(0, remaining - allocatedStars);
                }
                else
                {
                    int share = starSum > 0
                        ? (int)Math.Round(remaining * stars[i] / starSum)
                        : 0;
                    result[i] = share;
                    allocatedStars += share;
                }
            }

            return result;
        }

        private static void EnsureCellCount(XWPFTableRow row, int columnCount)
        {
            while (row.GetTableCells().Count < columnCount)
            {
                row.CreateCell();
            }
        }

        private static void WriteCell(XWPFTableCell targetCell, TableCell sourceCell, bool applyBorder = true)
        {
            if (applyBorder)
            {
                ApplyCellBorder(targetCell);
            }
            else
            {
                ApplyNilCellBorder(targetCell);
            }

            targetCell.RemoveParagraph(0);

            string text = ExtractCellText(sourceCell);
            bool isLabel = applyBorder && LooksLikeLabel(sourceCell, text);

            var paragraph = targetCell.AddParagraph();
            paragraph.Alignment = ResolveCellAlignment(sourceCell, isLabel);
            WordExportFontSupport.ApplyHalfLineParagraphSpacing(paragraph);
            var run = paragraph.CreateRun();
            run.SetText(text);
            if (isLabel)
            {
                WordExportFontSupport.ApplyLabel(run);
            }
            else
            {
                WordExportFontSupport.ApplyBody(run);
            }

            // 多行内容顶对齐，单行标签/正文居中；表外编号/日期顶对齐更接近预览。
            bool alignTop = !applyBorder || text.Contains('\n', StringComparison.Ordinal);
            targetCell.SetVerticalAlignment(alignTop
                ? XWPFTableCell.XWPFVertAlign.TOP
                : XWPFTableCell.XWPFVertAlign.CENTER);
            var tcPr = targetCell.GetCTTc().tcPr ?? targetCell.GetCTTc().AddNewTcPr();
            var vAlign = tcPr.vAlign ?? tcPr.AddNewVAlign();
            vAlign.val = alignTop ? ST_VerticalJc.top : ST_VerticalJc.center;

            if (tcPr.tcMar == null)
            {
                tcPr.tcMar = new CT_TcMar();
            }

            int margin = applyBorder ? CellMarginDxa : 0;
            tcPr.tcMar.top = CreateMargin(margin);
            tcPr.tcMar.bottom = CreateMargin(margin);
            tcPr.tcMar.left = CreateMargin(margin);
            tcPr.tcMar.right = CreateMargin(margin);
        }

        private static ParagraphAlignment ResolveCellAlignment(TableCell sourceCell, bool isLabel)
        {
            if (!isLabel)
            {
                return sourceCell.TextAlignment switch
                {
                    System.Windows.TextAlignment.Right => ParagraphAlignment.RIGHT,
                    System.Windows.TextAlignment.Center => ParagraphAlignment.CENTER,
                    _ => ParagraphAlignment.LEFT
                };
            }

            return ParagraphAlignment.CENTER;
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

        private static void ApplyNilCellBorder(XWPFTableCell cell)
        {
            var tcPr = cell.GetCTTc().tcPr ?? cell.GetCTTc().AddNewTcPr();
            var borders = tcPr.tcBorders ?? tcPr.AddNewTcBorders();
            SetNilBorder(borders.top ??= new CT_Border());
            SetNilBorder(borders.bottom ??= new CT_Border());
            SetNilBorder(borders.left ??= new CT_Border());
            SetNilBorder(borders.right ??= new CT_Border());
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

        private static void ApplyNilTableOuterBorder(XWPFTable table)
        {
            var tbl = table.GetCTTbl();
            var tblPr = tbl.tblPr ?? tbl.AddNewTblPr();
            var borders = tblPr.tblBorders ?? tblPr.AddNewTblBorders();
            SetNilBorder(borders.top ??= new CT_Border());
            SetNilBorder(borders.bottom ??= new CT_Border());
            SetNilBorder(borders.left ??= new CT_Border());
            SetNilBorder(borders.right ??= new CT_Border());
            SetNilBorder(borders.insideH ??= new CT_Border());
            SetNilBorder(borders.insideV ??= new CT_Border());
        }

        private static void SetBorder(CT_Border border, ulong size)
        {
            border.val = ST_Border.single;
            border.sz = size;
            border.space = 0;
            border.color = "000000";
        }

        private static void SetNilBorder(CT_Border border)
        {
            border.val = ST_Border.nil;
            border.sz = 0;
            border.space = 0;
            border.color = "auto";
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
