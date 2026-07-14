using System.IO;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.XWPF.UserModel;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 使用 NPOI 将 <see cref="ArchiveRegisterPrintData"/> 导出为可编辑 Word 表格。
    /// </summary>
    public class ArchiveRegisterWordExportService : IArchiveRegisterWordExportService
    {
        // NPOI XWPFRun.FontSize 单位为磅（pt）。
        private const int BodyFontPoints = 10;
        private const int LabelFontPoints = 10;
        private const int TitleFontPoints = 15;
        private const int FooterFontPoints = 9;
        private const int CellMarginDxa = 28;
        private const int CellLineSpacingTwips = 220;

        /// <summary>单行表格行高 0.6cm（twips）。</summary>
        private const int SingleRowHeightTwips = 340;

        /// <summary>双列签字区两行高度 1.2cm（twips）。</summary>
        private const int SignatureBlockRowHeightTwips = 680;

        /// <summary>A4 可打印区域宽度（twips），与页边距匹配。</summary>
        private const int TableWidthDxa = 9360;

        // 标签列略宽，避免「资料名称」「开发室分管领导」等被截断。
        private static readonly int[] ColumnWidthsDxa = { 1950, 2730, 1950, 2730 };

        public void ExportToFile(ArchiveRegisterPrintData data, string filePath)
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

        private static XWPFDocument BuildDocument(ArchiveRegisterPrintData data)
        {
            var document = new XWPFDocument();
            ConfigurePageSettings(document);

            AddTitle(document);
            AddHeaderInfo(document, data);

            var table = document.CreateTable(1, 4);
            ConfigureTableGrid(table);

            int rowIndex = 0;
            AddSingleRow(table, ref rowIndex, "资料名称", data.MaterialName, WordTableRowStyle.SingleLine);
            AddDoubleRow(table, ref rowIndex, "所属项目", data.ProjectName, "资料来源", data.SourceType, WordTableRowStyle.SingleLine);
            AddSingleRow(table, ref rowIndex, "提供单位", data.ProvideUnit, WordTableRowStyle.SingleLine);

            string contentText = data.ItemLines.Count > 0 ? string.Join("\n", data.ItemLines) : "(无)";
            AddSingleRow(table, ref rowIndex, "资料内容", contentText, WordTableRowStyle.MultiLine);

            string proofText = data.ProofLines.Count > 0 ? string.Join("\n", data.ProofLines) : "(无)";
            var proofStyle = data.ProofLines.Count <= 1 ? WordTableRowStyle.SingleLine : WordTableRowStyle.MultiLine;
            AddSingleRow(table, ref rowIndex, "证明材料", proofText, proofStyle);

            AddDoubleRow(table, ref rowIndex, "库管模式", data.Purpose, "申请部门", data.Dept, WordTableRowStyle.SingleLine);

            if (!string.IsNullOrWhiteSpace(data.RetainedHardDiskRegistration))
            {
                AddSingleRow(table, ref rowIndex, "留存硬盘登记", data.RetainedHardDiskRegistration, WordTableRowStyle.MultiLine);
            }

            if (!string.IsNullOrWhiteSpace(data.OpticalDiscLedgerSummary))
            {
                AddSingleRow(table, ref rowIndex, "光盘台账信息", data.OpticalDiscLedgerSummary, WordTableRowStyle.MultiLine);
            }

            string otherRequests = string.IsNullOrWhiteSpace(data.OtherRequests) ? "(无)" : data.OtherRequests;
            AddSingleRow(table, ref rowIndex, "其他要求", otherRequests, WordTableRowStyle.SingleLine);
            AddDoubleRow(table, ref rowIndex, "申请人", data.Applicant, "申请日期", data.Date, WordTableRowStyle.SingleLine);

            var deptParts = data.DeptLeaderApproval.Split('|');
            string deptName = deptParts.ElementAtOrDefault(0) ?? string.Empty;
            string deptDate = deptParts.ElementAtOrDefault(1) ?? "______年___月___日";
            AddSingleRow(table, ref rowIndex, "部门审核", FormatSignatureInline(deptName, deptDate), WordTableRowStyle.SingleLine);

            var prodParts = data.ProdFull.Split('|');
            string prodLeader = prodParts.ElementAtOrDefault(1) ?? string.Empty;
            string prodDate = prodParts.ElementAtOrDefault(2) ?? "______年___月___日";

            var rndParts = data.RndFull.Split('|');
            string rndLeader = rndParts.ElementAtOrDefault(1) ?? string.Empty;
            string rndDate = rndParts.ElementAtOrDefault(2) ?? "______年___月___日";
            AddSignatureDoubleRow(
                table,
                ref rowIndex,
                "生产管理科\n审批",
                prodLeader,
                prodDate,
                "科研开发室\n审批",
                rndLeader,
                rndDate);

            var depParts = data.DeputyFull.Split('|');
            string depLeader = depParts.ElementAtOrDefault(1) ?? string.Empty;
            string depDate = depParts.ElementAtOrDefault(2) ?? "______年___月___日";
            AddSingleRow(table, ref rowIndex, "开发室分管领导", FormatSignatureInline(depLeader, depDate), WordTableRowStyle.SingleLine);

            var deliverParts = data.DeliverFull.Split('|');
            string deliverer = deliverParts.ElementAtOrDefault(0) ?? string.Empty;
            string deliverDate = deliverParts.ElementAtOrDefault(1) ?? "______年___月___日";

            var adminParts = data.AdminFull.Split('|');
            string adminName = adminParts.ElementAtOrDefault(0) ?? string.Empty;
            string adminDate = adminParts.ElementAtOrDefault(1) ?? "______年___月___日";
            AddSignatureDoubleRow(
                table,
                ref rowIndex,
                "资料送达人\n交接确认",
                deliverer,
                deliverDate,
                "资料室\n接收确认",
                adminName,
                adminDate);

            ApplyTableOuterBorder(table);
            AddFooterNotes(document);

            return document;
        }

        private static void ConfigurePageSettings(XWPFDocument document)
        {
            var body = document.Document.body;
            var sectPr = body.sectPr ?? body.AddNewSectPr();

            var pgSz = sectPr.pgSz ?? sectPr.AddNewPgSz();
            pgSz.w = (ulong)11906;
            pgSz.h = (ulong)16838;

            if (sectPr.pgMar == null)
            {
                sectPr.pgMar = new CT_PageMar();
            }

            sectPr.pgMar.top = (ulong)850;
            sectPr.pgMar.bottom = (ulong)850;
            sectPr.pgMar.left = (ulong)1134;
            sectPr.pgMar.right = (ulong)1134;
        }

        private static void AddTitle(XWPFDocument document)
        {
            var paragraph = document.CreateParagraph();
            paragraph.Alignment = ParagraphAlignment.CENTER;
            ApplyDocumentParagraph(paragraph, spacingBeforeTwips: 0, spacingAfterTwips: 40);
            var run = paragraph.CreateRun();
            run.SetText("河北省第三测绘院资料室年度资料入档申请审批单");
            run.IsBold = true;
            run.FontFamily = "黑体";
            run.FontSize = TitleFontPoints;
        }

        private static void AddHeaderInfo(XWPFDocument document, ArchiveRegisterPrintData data)
        {
            var leftParagraph = document.CreateParagraph();
            ApplyDocumentParagraph(leftParagraph);
            var leftRun = leftParagraph.CreateRun();
            leftRun.SetText($"申请单编号：{data.FormNo}");
            leftRun.FontFamily = "宋体";
            leftRun.FontSize = BodyFontPoints;

            var rightParagraph = document.CreateParagraph();
            rightParagraph.Alignment = ParagraphAlignment.RIGHT;
            ApplyDocumentParagraph(rightParagraph, spacingAfterTwips: 40);
            var rightRun = rightParagraph.CreateRun();
            rightRun.SetText($"申请日期：{data.Date}");
            rightRun.FontFamily = "宋体";
            rightRun.FontSize = BodyFontPoints;
        }

        private static void AddFooterNotes(XWPFDocument document)
        {
            var titleParagraph = document.CreateParagraph();
            ApplyDocumentParagraph(titleParagraph, spacingBeforeTwips: 80, spacingAfterTwips: 0);
            var titleRun = titleParagraph.CreateRun();
            titleRun.SetText("备注：");
            titleRun.IsBold = true;
            titleRun.FontFamily = "宋体";
            titleRun.FontSize = FooterFontPoints;
            AddFooterParagraph(document, "1、审核、审批时，生产科负责人必须在各资料子项的[密级]处手签具体密级和本人姓名。");
            AddFooterParagraph(document, "      2、审批完成后，申请人（交接人）携带拟归档所有资料、材料(包括本表单、相关附件等）到资料室办理登记、交接工作。");
            AddFooterParagraph(document, "      3、交接人、资料管理员应对照本表单中的各项内容共同完成归档资料、材料的查验和拍照，确认账实相符后分别在表单上签名确认。");
            AddFooterParagraph(document, "      4、本表单、资料照片电子件应上传到本系统，同时表单原件由资料室存档保管，资料交接人有自存需要的可采用复印或拍照方式留存。");
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
            MultiLine,
            SignatureBlock
        }

        private static void AddSingleRow(
            XWPFTable table,
            ref int rowIndex,
            string label,
            string content,
            WordTableRowStyle rowStyle)
        {
            var row = GetOrCreateRow(table, ref rowIndex);
            EnsureCellCount(row, 4);
            ApplyRowStyle(row, rowStyle);

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
            ApplyRowStyle(row, rowStyle);

            WriteLabelCell(row.GetCell(0), label1, rowStyle);
            WriteBodyCell(row.GetCell(1), content1, rowStyle);
            WriteLabelCell(row.GetCell(2), label2, rowStyle);
            WriteBodyCell(row.GetCell(3), content2, rowStyle);

            rowIndex++;
        }

        private static void AddSignatureDoubleRow(
            XWPFTable table,
            ref int rowIndex,
            string label1,
            string signer1,
            string date1,
            string label2,
            string signer2,
            string date2)
        {
            var row = GetOrCreateRow(table, ref rowIndex);
            EnsureCellCount(row, 4);
            ApplyRowStyle(row, WordTableRowStyle.SignatureBlock);

            WriteLabelCell(row.GetCell(0), label1, WordTableRowStyle.SignatureBlock);
            WriteSignatureBlockCell(row.GetCell(1), signer1, date1);
            WriteLabelCell(row.GetCell(2), label2, WordTableRowStyle.SignatureBlock);
            WriteSignatureBlockCell(row.GetCell(3), signer2, date2);

            rowIndex++;
        }

        private static void ApplyRowStyle(XWPFTableRow row, WordTableRowStyle rowStyle)
        {
            switch (rowStyle)
            {
                case WordTableRowStyle.SingleLine:
                    SetRowHeightExact(row, SingleRowHeightTwips);
                    break;
                case WordTableRowStyle.SignatureBlock:
                    SetRowHeightExact(row, SignatureBlockRowHeightTwips);
                    break;
            }
        }

        private static void SetRowHeightExact(XWPFTableRow row, int heightTwips)
        {
            var trPr = row.GetCTRow().trPr ?? row.GetCTRow().AddNewTrPr();
            var trHeight = trPr.AddNewTrHeight();
            trHeight.val = (ulong)heightTwips;
            trHeight.hRule = ST_HeightRule.exact;
        }

        private static XWPFTableRow GetOrCreateRow(XWPFTable table, ref int rowIndex)
        {
            if (rowIndex == 0)
            {
                return table.GetRow(0);
            }

            return table.CreateRow();
        }

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

        /// <summary>
        /// 签字区：签字、日期各占一个段落（Word 硬回车），不使用软换行。
        /// </summary>
        private static void WriteSignatureBlockCell(XWPFTableCell cell, string signer, string date)
        {
            ResetCell(cell, WordTableRowStyle.SignatureBlock);

            string signatureSlot = string.IsNullOrWhiteSpace(signer) ? "________________" : signer;
            string dateText = string.IsNullOrWhiteSpace(date) ? "______年___月___日" : date;

            AddCellParagraph(cell, $"签字：{signatureSlot}", label: false, ParagraphAlignment.LEFT);
            AddCellParagraph(cell, $"日期：{dateText}", label: false, ParagraphAlignment.LEFT);
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
            cell.SetVerticalAlignment(rowStyle == WordTableRowStyle.MultiLine
                ? XWPFTableCell.XWPFVertAlign.TOP
                : XWPFTableCell.XWPFVertAlign.CENTER);

            var tcPr = cell.GetCTTc().tcPr ?? cell.GetCTTc().AddNewTcPr();
            var vAlign = tcPr.vAlign ?? tcPr.AddNewVAlign();
            vAlign.val = rowStyle == WordTableRowStyle.MultiLine
                ? ST_VerticalJc.top
                : ST_VerticalJc.center;
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

        private static string[] SplitLines(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return [string.Empty];
            }

            return text.Split('\n');
        }

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

        private static CT_TblWidth CreateMargin(int widthDxa)
        {
            return new CT_TblWidth
            {
                type = ST_TblWidth.dxa,
                w = widthDxa.ToString()
            };
        }

        private static CT_Border CreateBorder()
        {
            return new CT_Border
            {
                val = ST_Border.single,
                sz = 4,
                color = "000000"
            };
        }

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

        private static string FormatSignatureInline(string signer, string date)
        {
            string signatureSlot = string.IsNullOrWhiteSpace(signer) ? "________________" : signer;
            return $"签字：{signatureSlot}    日期：{date}";
        }
    }
}
