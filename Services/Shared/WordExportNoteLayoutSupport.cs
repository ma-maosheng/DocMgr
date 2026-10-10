using System.Windows.Media;
using DocMgr.Models.Shared;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.XWPF.UserModel;

namespace DocMgr.Services.Shared
{
    /// <summary>
    /// Word 表后说明：标签与序号分列，序号左对齐，与打印预览同一原则。
    /// </summary>
    public static class WordExportNoteLayoutSupport
    {
        /// <summary>追加无边框表后说明。序号各条从同一左缘起排。</summary>
        public static void AddAlignedNotes(XWPFDocument document, string label, IReadOnlyList<string> lines)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentException.ThrowIfNullOrWhiteSpace(label);
            ArgumentNullException.ThrowIfNull(lines);

            string labelText = label.Trim();
            double fontSizeDip = WordExportFontSupport.FooterFontPoints * 96.0 / 72.0;
            var fontFamily = new FontFamily("SimSun");
            int labelTwips = Math.Max(
                1,
                (int)Math.Round(PrintFormNoteLayoutSupport.GetLabelColumnWidthDip(labelText, fontFamily, fontSizeDip) * 1440.0 / 96.0));
            int bodyTwips = Math.Max(1, PrintPageLayoutSupport.ContentWidthTwips - labelTwips);
            int[] widths = [labelTwips, bodyTwips];

            XWPFTable table = document.CreateTable(1, 2);
            WordExportTableLayoutSupport.ApplyFixedTableLayout(table, widths);
            ApplyNilTableBorders(table);

            XWPFTableRow row = table.GetRow(0);
            WriteCell(row.GetCell(0), labelText, bold: true, labelTwips);
            WriteCell(row.GetCell(1), JoinLines(lines), bold: false, bodyTwips);
            WordExportTableLayoutSupport.ApplyRowCellWidths(row, widths);
        }

        private static string JoinLines(IReadOnlyList<string> lines)
        {
            var normalized = new List<string>(lines.Count);
            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    normalized.Add(line.Trim());
                }
            }

            return string.Join('\n', normalized);
        }

        private static void WriteCell(XWPFTableCell cell, string text, bool bold, int widthTwips)
        {
            cell.RemoveParagraph(0);
            ApplyNilCellBorder(cell);
            WordExportTableLayoutSupport.SetCellWidth(cell, widthTwips);
            cell.SetVerticalAlignment(XWPFTableCell.XWPFVertAlign.TOP);

            var paragraph = cell.AddParagraph();
            paragraph.Alignment = ParagraphAlignment.LEFT;
            paragraph.SpacingBefore = 0;
            paragraph.SpacingAfter = 0;

            string[] parts = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            if (parts.Length == 0)
            {
                parts = [string.Empty];
            }

            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    paragraph.CreateRun().AddBreak(BreakType.TEXTWRAPPING);
                }

                XWPFRun run = paragraph.CreateRun();
                run.SetText(parts[i]);
                WordExportFontSupport.ApplyFooter(run, bold);
            }
        }

        private static void ApplyNilTableBorders(XWPFTable table)
        {
            CT_TblPr tblPr = table.GetCTTbl().tblPr ?? table.GetCTTbl().AddNewTblPr();
            CT_TblBorders borders = tblPr.tblBorders ?? tblPr.AddNewTblBorders();
            SetNil(borders.top ??= new CT_Border());
            SetNil(borders.bottom ??= new CT_Border());
            SetNil(borders.left ??= new CT_Border());
            SetNil(borders.right ??= new CT_Border());
            SetNil(borders.insideH ??= new CT_Border());
            SetNil(borders.insideV ??= new CT_Border());
        }

        private static void ApplyNilCellBorder(XWPFTableCell cell)
        {
            CT_TcPr tcPr = cell.GetCTTc().tcPr ?? cell.GetCTTc().AddNewTcPr();
            CT_TcBorders borders = tcPr.tcBorders ?? tcPr.AddNewTcBorders();
            SetNil(borders.top ??= new CT_Border());
            SetNil(borders.bottom ??= new CT_Border());
            SetNil(borders.left ??= new CT_Border());
            SetNil(borders.right ??= new CT_Border());

            tcPr.tcMar ??= new CT_TcMar();
            tcPr.tcMar.top = ZeroMargin();
            tcPr.tcMar.bottom = ZeroMargin();
            tcPr.tcMar.left = ZeroMargin();
            tcPr.tcMar.right = ZeroMargin();
        }

        private static CT_TblWidth ZeroMargin() =>
            new()
            {
                type = ST_TblWidth.dxa,
                w = "0"
            };

        private static void SetNil(CT_Border border)
        {
            border.val = ST_Border.nil;
            border.sz = 0;
            border.space = 0;
            border.color = "auto";
        }
    }
}
