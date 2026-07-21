using System.IO;
using DocMgr.Models.Shared;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.XWPF.UserModel;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 使用 NPOI 将 <see cref="ArchiveOutboundPrintData"/> 导出为可编辑 Word 表格。
    /// </summary>
    public sealed class ArchiveOutboundWordExportService : IArchiveOutboundWordExportService
    {
        private const int BodyFontPoints = 10;
        private const int LabelFontPoints = 10;
        private const int TitleFontPoints = 15;
        private const int FooterFontPoints = 9;
        private const int CellMarginDxa = 28;
        private const int CellLineSpacingTwips = 220;
        private const int SingleRowHeightTwips = 340;
        private const int ReasonRowHeightTwips = 400;
        private const int SignatureRowHeightTwips = 620;
        private const int TitleBlockHeightTwips = 520;
        private const int HeaderInfoHeightTwips = 380;
        private const int TableWidthDxa = PrintPageLayoutSupport.ContentWidthTwips;
        private static readonly int[] ColumnWidthsDxa = { 2044, 2829, 2044, 2829 };

        public void ExportToFile(ArchiveOutboundPrintData data, string filePath)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            string? directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("导出文件目录无效。", nameof(filePath));
            }

            Directory.CreateDirectory(directory);

            using var document = BuildDocument(data);
            using var stream = File.Create(filePath);
            document.Write(stream);
        }

        private static XWPFDocument BuildDocument(ArchiveOutboundPrintData data)
        {
            var document = new XWPFDocument();
            ConfigurePageSettings(document);
            AddTitle(document);
            AddHeaderInfo(document, data);

            var table = document.CreateTable(1, 4);
            ConfigureTableGrid(table);

            int itemDetailRowHeightTwips = CalculateItemDetailRowHeightTwips(
                !string.IsNullOrWhiteSpace(data.LongTermSimulatedStockDepletionNoticeText));
            int rowIndex = 0;
            AddDoubleRow(table, ref rowIndex, "申请部门", data.ApplicantDept, "申请人", data.ApplicantName, WordTableRowStyle.SingleLine);
            AddSingleRow(table, ref rowIndex, "原由", data.Reason, WordTableRowStyle.ReasonLine);
            AddSingleRow(table, ref rowIndex, "去向", data.DestinationText, WordTableRowStyle.SingleLine);
            AddSingleRow(table, ref rowIndex, "证明材料名称", data.ProofMaterialNote, WordTableRowStyle.SingleLine);
            AddSingleRow(table, ref rowIndex, "预计归还日期", data.ExpectedReturnDateText, WordTableRowStyle.SingleLine);
            AddSingleRow(table, ref rowIndex, "涉密资料处置", data.ConfidentialMaterialDispositionText, WordTableRowStyle.SingleLine);
            if (!string.IsNullOrWhiteSpace(data.LongTermSimulatedStockDepletionNoticeText))
            {
                AddSingleRow(
                    table,
                    ref rowIndex,
                    "重点提示",
                    data.LongTermSimulatedStockDepletionNoticeText,
                    WordTableRowStyle.ReasonLine);
            }

            AddSingleRow(table, ref rowIndex, "资料摘要", data.MaterialSummary, WordTableRowStyle.SingleLine);

            string itemText = data.ItemLines.Count > 0 ? string.Join("\n", data.ItemLines) : "(无)";
            AddSingleRow(table, ref rowIndex, "具体资料明细", itemText, WordTableRowStyle.ItemDetail, itemDetailRowHeightTwips);
            AddSingleRow(table, ref rowIndex, "申请部门审核", data.DeptAuditBlock, WordTableRowStyle.SingleLine);
            AddSingleRow(table, ref rowIndex, "资料室负责人", data.ArchiveRoomHeadBlock, WordTableRowStyle.SingleLine);
            AddSingleRow(table, ref rowIndex, "生产科负责人", data.ProductionHeadBlock, WordTableRowStyle.SingleLine);
            AddSingleRow(table, ref rowIndex, "生产副院长", data.VicePresidentBlock, WordTableRowStyle.SingleLine);
            AddSingleRow(
                table,
                ref rowIndex,
                "交接签字",
                data.HandoverSignatureBlock,
                WordTableRowStyle.Signature,
                SignatureRowHeightTwips);

            ApplyTableOuterBorder(table);
            AddFooterNotes(document, data.PrintCount + 1);

            return document;
        }

        private static int CalculateItemDetailRowHeightTwips(bool hasLongTermDepletionNotice)
        {
            // 与 FlowDocument 一致：固定行外高含单元格边距；表后 3 行说明必须同页。
            int fixedTableHeight =
                PrintPageLayoutSupport.GetTableRowOuterHeightTwips(SingleRowHeightTwips, CellMarginDxa) * 10
                + PrintPageLayoutSupport.GetTableRowOuterHeightTwips(ReasonRowHeightTwips, CellMarginDxa)
                + PrintPageLayoutSupport.GetTableRowOuterHeightTwips(SignatureRowHeightTwips, CellMarginDxa);
            if (hasLongTermDepletionNotice)
            {
                fixedTableHeight += PrintPageLayoutSupport.GetTableRowOuterHeightTwips(ReasonRowHeightTwips, CellMarginDxa);
            }

            int footerHeight = PrintPageLayoutSupport.EstimateNoteBlockHeightTwips(lineCount: 3, lineHeightTwips: 240, topMarginTwips: 120);
            int reservedHeight = TitleBlockHeightTwips + HeaderInfoHeightTwips + footerHeight + fixedTableHeight;
            return PrintPageLayoutSupport.CalculateStretchRowHeightTwips(
                reservedHeight,
                SingleRowHeightTwips * 4,
                CellMarginDxa);
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

        private static void AddTitle(XWPFDocument document)
        {
            var paragraph = document.CreateParagraph();
            paragraph.Alignment = ParagraphAlignment.CENTER;
            ApplyDocumentParagraph(paragraph, spacingAfterTwips: 24);
            var run = paragraph.CreateRun();
            run.SetText("河北省第三测绘院资料室年度资料出库申请审批单");
            run.IsBold = true;
            run.FontFamily = "黑体";
            run.FontSize = TitleFontPoints;
        }

        private static void AddHeaderInfo(XWPFDocument document, ArchiveOutboundPrintData data)
        {
            var leftParagraph = document.CreateParagraph();
            ApplyDocumentParagraph(leftParagraph);
            var leftRun = leftParagraph.CreateRun();
            leftRun.SetText($"申请单编号：{data.OutboundNo}");
            leftRun.FontFamily = "宋体";
            leftRun.FontSize = BodyFontPoints;

            var rightParagraph = document.CreateParagraph();
            rightParagraph.Alignment = ParagraphAlignment.RIGHT;
            ApplyDocumentParagraph(rightParagraph, spacingAfterTwips: 24);
            var rightRun = rightParagraph.CreateRun();
            rightRun.SetText($"申请日期：{data.ApplyDateText}");
            rightRun.FontFamily = "宋体";
            rightRun.FontSize = BodyFontPoints;
        }

        private static void AddFooterNotes(XWPFDocument document, int printSequence)
        {
            var titleParagraph = document.CreateParagraph();
            ApplyDocumentParagraph(titleParagraph, spacingBeforeTwips: 40);
            var titleRun = titleParagraph.CreateRun();
            titleRun.SetText("备注：");
            titleRun.IsBold = true;
            titleRun.FontFamily = "宋体";
            titleRun.FontSize = FooterFontPoints;

            AddFooterParagraph(document, "1、申请提交后，按“线上申请、打印表单、线下审批签字、上传签字件、资料出库交接”的流程办理。");
            AddFooterParagraph(document, "      2、签字后的审批单应回传系统，作为办理依据和归档附件。");
            AddFooterParagraph(document, $"      3、本申请单已累计打印 {printSequence} 次，最新打印请与系统记录核对。");
        }

        private static void AddFooterParagraph(XWPFDocument document, string text)
        {
            var paragraph = document.CreateParagraph();
            ApplyDocumentParagraph(paragraph);
            var run = paragraph.CreateRun();
            run.SetText(text);
            run.FontFamily = "宋体";
            run.FontSize = FooterFontPoints;
        }

        private enum WordTableRowStyle
        {
            SingleLine,
            ReasonLine,
            ItemDetail,
            Signature
        }

        private static void AddSingleRow(
            XWPFTable table,
            ref int rowIndex,
            string label,
            string content,
            WordTableRowStyle rowStyle,
            int? explicitHeightTwips = null)
        {
            var row = GetOrCreateRow(table, ref rowIndex);
            EnsureCellCount(row, 4);
            ApplyRowStyle(row, rowStyle, explicitHeightTwips);
            WriteLabelCell(row.GetCell(0), label, rowStyle);
            WriteBodyCell(row.GetCell(1), content, rowStyle);
            row.MergeCells(1, 3);
            rowIndex++;
        }

        private static void AddDoubleRow(
            XWPFTable table,
            ref int rowIndex,
            string label1,
            string content1,
            string label2,
            string content2,
            WordTableRowStyle rowStyle)
        {
            var row = GetOrCreateRow(table, ref rowIndex);
            EnsureCellCount(row, 4);
            ApplyRowStyle(row, rowStyle, null);
            WriteLabelCell(row.GetCell(0), label1, rowStyle);
            WriteBodyCell(row.GetCell(1), content1, rowStyle);
            WriteLabelCell(row.GetCell(2), label2, rowStyle);
            WriteBodyCell(row.GetCell(3), content2, rowStyle);
            rowIndex++;
        }

        private static void ApplyRowStyle(XWPFTableRow row, WordTableRowStyle rowStyle, int? explicitHeightTwips)
        {
            int heightTwips = rowStyle switch
            {
                WordTableRowStyle.ReasonLine => ReasonRowHeightTwips,
                WordTableRowStyle.ItemDetail => explicitHeightTwips ?? SingleRowHeightTwips * 4,
                WordTableRowStyle.Signature => explicitHeightTwips ?? SignatureRowHeightTwips,
                _ => SingleRowHeightTwips
            };

            SetRowHeightExact(row, heightTwips);
        }

        private static void SetRowHeightExact(XWPFTableRow row, int heightTwips)
        {
            var trPr = row.GetCTRow().trPr ?? row.GetCTRow().AddNewTrPr();
            var trHeight = trPr.AddNewTrHeight();
            trHeight.val = (ulong)heightTwips;
            trHeight.hRule = ST_HeightRule.exact;
        }

        private static XWPFTableRow GetOrCreateRow(XWPFTable table, ref int rowIndex) =>
            rowIndex == 0 ? table.GetRow(0) : table.CreateRow();

        private static void ConfigureTableGrid(XWPFTable table)
        {
            table.Width = 5000;
            var tbl = table.GetCTTbl();
            var tblPr = tbl.tblPr ?? tbl.AddNewTblPr();
            var tblW = tblPr.tblW ?? tblPr.AddNewTblW();
            tblW.type = ST_TblWidth.dxa;
            tblW.w = TableWidthDxa.ToString();

            var grid = tbl.tblGrid ?? tbl.AddNewTblGrid();
            grid.gridCol.Clear();
            foreach (int width in ColumnWidthsDxa)
            {
                var gridCol = grid.AddNewGridCol();
                gridCol.w = (ulong)width;
            }
        }

        private static void EnsureCellCount(XWPFTableRow row, int cellCount)
        {
            while (row.GetTableCells().Count < cellCount)
            {
                row.CreateCell();
            }

            for (int i = 0; i < cellCount; i++)
            {
                ApplyCellBorder(row.GetCell(i));
            }
        }

        private static void WriteLabelCell(XWPFTableCell cell, string text, WordTableRowStyle rowStyle)
        {
            ResetCell(cell, rowStyle);
            WriteCellParagraphLines(cell, text, label: true, ParagraphAlignment.CENTER);
        }

        private static void WriteBodyCell(XWPFTableCell cell, string text, WordTableRowStyle rowStyle)
        {
            ResetCell(cell, rowStyle);
            WriteCellParagraphLines(cell, text, label: false, ParagraphAlignment.LEFT);
        }

        private static void ResetCell(XWPFTableCell cell, WordTableRowStyle rowStyle)
        {
            ClearCellParagraphs(cell);
            ApplyCellVerticalAlignment(cell, rowStyle);
        }

        private static void WriteCellParagraphLines(
            XWPFTableCell cell,
            string text,
            bool label,
            ParagraphAlignment alignment)
        {
            foreach (string line in SplitLines(text))
            {
                AddCellParagraph(cell, line, label, alignment);
            }
        }

        private static void AddCellParagraph(
            XWPFTableCell cell,
            string text,
            bool label,
            ParagraphAlignment alignment)
        {
            var paragraph = cell.AddParagraph();
            paragraph.Alignment = alignment;
            ApplyCellParagraph(paragraph);

            var run = paragraph.CreateRun();
            run.SetText(text);
            run.FontFamily = label ? "黑体" : "宋体";
            run.FontSize = label ? LabelFontPoints : BodyFontPoints;
            run.IsBold = label;
        }

        private static void ApplyCellVerticalAlignment(XWPFTableCell cell, WordTableRowStyle rowStyle)
        {
            bool topAligned = rowStyle is WordTableRowStyle.ReasonLine
                or WordTableRowStyle.ItemDetail
                or WordTableRowStyle.Signature;
            cell.SetVerticalAlignment(topAligned
                ? XWPFTableCell.XWPFVertAlign.TOP
                : XWPFTableCell.XWPFVertAlign.CENTER);

            var tcPr = cell.GetCTTc().tcPr ?? cell.GetCTTc().AddNewTcPr();
            var vAlign = tcPr.vAlign ?? tcPr.AddNewVAlign();
            vAlign.val = topAligned ? ST_VerticalJc.top : ST_VerticalJc.center;
        }

        private static void ApplyCellParagraph(XWPFParagraph paragraph)
        {
            paragraph.SpacingBefore = 0;
            paragraph.SpacingAfter = 0;

            var pPr = paragraph.GetCTP().pPr ?? paragraph.GetCTP().AddNewPPr();
            var spacing = pPr.spacing ?? pPr.AddNewSpacing();
            spacing.before = 0;
            spacing.after = 0;
            spacing.line = CellLineSpacingTwips.ToString();
            spacing.lineRule = ST_LineSpacingRule.exact;
        }

        private static void ApplyDocumentParagraph(
            XWPFParagraph paragraph,
            int spacingBeforeTwips = 0,
            int spacingAfterTwips = 0)
        {
            paragraph.SpacingBefore = spacingBeforeTwips;
            paragraph.SpacingAfter = spacingAfterTwips;

            var pPr = paragraph.GetCTP().pPr ?? paragraph.GetCTP().AddNewPPr();
            var spacing = pPr.spacing ?? pPr.AddNewSpacing();
            spacing.before = (ulong)spacingBeforeTwips;
            spacing.after = (ulong)spacingAfterTwips;
            spacing.line = "240";
            spacing.lineRule = ST_LineSpacingRule.auto;
        }

        private static string[] SplitLines(string? text) =>
            string.IsNullOrEmpty(text) ? [string.Empty] : text.Split('\n');

        private static void ClearCellParagraphs(XWPFTableCell cell)
        {
            for (int i = cell.Paragraphs.Count - 1; i >= 0; i--)
            {
                cell.RemoveParagraph(i);
            }
        }

        private static void ApplyCellBorder(XWPFTableCell cell)
        {
            var tcPr = cell.GetCTTc().tcPr ?? cell.GetCTTc().AddNewTcPr();
            var borders = tcPr.tcBorders ?? tcPr.AddNewTcBorders();
            borders.top = CreateBorder();
            borders.left = CreateBorder();
            borders.bottom = CreateBorder();
            borders.right = CreateBorder();

            if (tcPr.tcMar == null)
            {
                tcPr.tcMar = new CT_TcMar();
            }

            tcPr.tcMar.top = CreateMargin(CellMarginDxa);
            tcPr.tcMar.bottom = CreateMargin(CellMarginDxa);
            tcPr.tcMar.left = CreateMargin(CellMarginDxa);
            tcPr.tcMar.right = CreateMargin(CellMarginDxa);
        }

        private static CT_TblWidth CreateMargin(int widthDxa) =>
            new() { type = ST_TblWidth.dxa, w = widthDxa.ToString() };

        private static CT_Border CreateBorder() =>
            new() { val = ST_Border.single, sz = 4, color = "000000" };

        private static void ApplyTableOuterBorder(XWPFTable table)
        {
            var tblPr = table.GetCTTbl().tblPr ?? table.GetCTTbl().AddNewTblPr();
            var borders = tblPr.tblBorders ?? tblPr.AddNewTblBorders();
            borders.top = CreateBorder();
            borders.left = CreateBorder();
            borders.bottom = CreateBorder();
            borders.right = CreateBorder();
            borders.insideH = CreateBorder();
            borders.insideV = CreateBorder();
        }
    }
}
