using System.IO;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.Shared;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.XWPF.UserModel;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 使用 NPOI 将 <see cref="ArchiveOutboundPrintData"/> 导出为可编辑 Word 表格。
    /// </summary>
    public sealed class ArchiveOutboundWordExportService : IArchiveOutboundWordExportService
    {
        private const int CellMarginDxa = PrintPageLayoutSupport.TableCellPaddingTwips;
        private const int CellLineSpacingTwips = 240;
        private const int SingleRowHeightTwips = PrintPageLayoutSupport.TableRowContentHeightOneLineTwips;
        private const int ReasonRowHeightTwips = PrintPageLayoutSupport.TableRowContentHeightOneLineTwips;
        private const int SignatureRowHeightTwips = PrintPageLayoutSupport.TableRowContentHeightTwoLinesTwips;
        private const int TitleBlockHeightTwips = PrintPageLayoutSupport.ApprovalFormTitleBlockHeightTwips;
        private const int HeaderInfoHeightTwips = 380;
        private static readonly int[] ColumnWidthsDxa =
            WordExportTableLayoutSupport.ApprovalFormMainColumnWidthsTwips;

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
            AddDoubleRow(table, ref rowIndex, "申请人", data.ApplicantName, "申请部门", data.ApplicantDept, WordTableRowStyle.SingleLine);
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
            if (data.EnableDeptHead)
            {
                AddSingleRow(table, ref rowIndex, ApprovalWorkflowDomainValues.DisplayDeptHead, data.DeptHeadBlock, WordTableRowStyle.SingleLine);
            }

            if (data.EnableArchiveRoomHead)
            {
                AddSingleRow(table, ref rowIndex, ApprovalWorkflowDomainValues.DisplayArchiveRoomHead, data.ArchiveRoomHeadBlock, WordTableRowStyle.SingleLine);
            }

            if (data.EnableProductionHead)
            {
                AddSingleRow(table, ref rowIndex, ApprovalWorkflowDomainValues.DisplayProductionHead, data.ProductionHeadBlock, WordTableRowStyle.SingleLine);
            }

            if (data.EnableArchiveDeputyPresident)
            {
                AddSingleRow(
                    table,
                    ref rowIndex,
                    ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident,
                    data.ArchiveDeputyPresidentBlock,
                    WordTableRowStyle.SingleLine);
            }

            if (data.EnableProductionVicePresident)
            {
                AddSingleRow(
                    table,
                    ref rowIndex,
                    ApprovalWorkflowDomainValues.DisplayProductionVicePresident,
                    data.ProductionVicePresidentBlock,
                    WordTableRowStyle.SingleLine);
            }

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
            int reservedHeight =
                TitleBlockHeightTwips
                + HeaderInfoHeightTwips
                + footerHeight
                + fixedTableHeight;
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
            ApplyDocumentParagraph(paragraph);
            var run = paragraph.CreateRun();
            run.SetText("河北省第三测绘院资料室年度资料出库申请审批单");
            WordExportFontSupport.ApplyTitle(run);
        }

        private static void AddHeaderInfo(XWPFDocument document, ArchiveOutboundPrintData data)
        {
            var headerTable = document.CreateTable(1, 2);
            ConfigureHeaderTable(headerTable);

            XWPFTableRow row = headerTable.GetRow(0);
            WriteOutsideHeaderCell(row.GetCell(0), $"申请单编号：{data.OutboundNo}", ParagraphAlignment.LEFT);
            WriteOutsideHeaderCell(row.GetCell(1), $"申请日期：{data.ApplyDateText}", ParagraphAlignment.RIGHT);
        }

        private static void AddFooterNotes(XWPFDocument document, int printSequence)
        {
            var titleParagraph = document.CreateParagraph();
            ApplyDocumentParagraph(titleParagraph);
            var titleRun = titleParagraph.CreateRun();
            titleRun.SetText("备注：");
            WordExportFontSupport.ApplyFooter(titleRun, bold: true);

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
            WordExportFontSupport.ApplyFooter(run);
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
            WordExportTableLayoutSupport.ApplyRowCellWidths(row, ColumnWidthsDxa);
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
            WordExportTableLayoutSupport.ApplyRowCellWidths(row, ColumnWidthsDxa);
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
            WordExportTableLayoutSupport.ApplyFixedTableLayout(table, ColumnWidthsDxa);
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
            if (label)
            {
                WordExportFontSupport.ApplyLabel(run);
            }
            else
            {
                WordExportFontSupport.ApplyBody(run);
            }
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
            WordExportFontSupport.ApplyHalfLineParagraphSpacing(paragraph);

            var pPr = paragraph.GetCTP().pPr ?? paragraph.GetCTP().AddNewPPr();
            var spacing = pPr.spacing ?? pPr.AddNewSpacing();
            spacing.line = CellLineSpacingTwips.ToString();
            spacing.lineRule = ST_LineSpacingRule.exact;
        }

        private static void ApplyDocumentParagraph(XWPFParagraph paragraph)
        {
            WordExportFontSupport.ApplyHalfLineParagraphSpacing(paragraph);

            var pPr = paragraph.GetCTP().pPr ?? paragraph.GetCTP().AddNewPPr();
            var spacing = pPr.spacing ?? pPr.AddNewSpacing();
            spacing.line = "240";
            spacing.lineRule = ST_LineSpacingRule.auto;
        }

        private static void ConfigureHeaderTable(XWPFTable table)
        {
            int[] widths = WordExportTableLayoutSupport.DistributeEqualWidths(
                PrintPageLayoutSupport.ContentWidthTwips,
                2);
            WordExportTableLayoutSupport.ApplyFixedTableLayout(table, widths);
            ApplyNilTableOuterBorder(table);
            WordExportTableLayoutSupport.ApplyRowCellWidths(table.GetRow(0), widths);
        }

        private static void ApplyNilTableOuterBorder(XWPFTable table)
        {
            var tblPr = table.GetCTTbl().tblPr ?? table.GetCTTbl().AddNewTblPr();
            var borders = tblPr.tblBorders ?? tblPr.AddNewTblBorders();
            borders.top = CreateNilBorder();
            borders.left = CreateNilBorder();
            borders.bottom = CreateNilBorder();
            borders.right = CreateNilBorder();
            borders.insideH = CreateNilBorder();
            borders.insideV = CreateNilBorder();
        }

        private static void WriteOutsideHeaderCell(XWPFTableCell cell, string text, ParagraphAlignment alignment)
        {
            ClearCellParagraphs(cell);
            ApplyNilCellBorder(cell);

            var paragraph = cell.AddParagraph();
            paragraph.Alignment = alignment;
            ApplyDocumentParagraph(paragraph);
            var run = paragraph.CreateRun();
            run.SetText(text);
            WordExportFontSupport.ApplyBody(run);
        }

        private static void ApplyNilCellBorder(XWPFTableCell cell)
        {
            var tcPr = cell.GetCTTc().tcPr ?? cell.GetCTTc().AddNewTcPr();
            var borders = tcPr.tcBorders ?? tcPr.AddNewTcBorders();
            borders.top = CreateNilBorder();
            borders.left = CreateNilBorder();
            borders.bottom = CreateNilBorder();
            borders.right = CreateNilBorder();

            if (tcPr.tcMar == null)
            {
                tcPr.tcMar = new CT_TcMar();
            }

            tcPr.tcMar.top = CreateMargin(0);
            tcPr.tcMar.bottom = CreateMargin(0);
            tcPr.tcMar.left = CreateMargin(0);
            tcPr.tcMar.right = CreateMargin(0);
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

        private static CT_Border CreateNilBorder() =>
            new() { val = ST_Border.nil, sz = 0, color = "auto" };

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
