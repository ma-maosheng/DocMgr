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
    /// 使用 NPOI 将 <see cref="ArchiveRegisterPrintData"/> 导出为可编辑 Word 表格。
    /// </summary>
    public class ArchiveRegisterWordExportService : IArchiveRegisterWordExportService
    {
        private const int CellMarginDxa = PrintPageLayoutSupport.TableCellPaddingTwips;
        private const int CellLineSpacingTwips = 240;

        /// <summary>单行表格行高（twips）。</summary>
        private const int SingleRowHeightTwips = PrintPageLayoutSupport.TableRowContentHeightOneLineTwips;

        /// <summary>双列签字区两行高度（twips）。</summary>
        private const int SignatureBlockRowHeightTwips = PrintPageLayoutSupport.TableRowContentHeightTwoLinesTwips;

        /// <summary>多行摘要类行高（留存硬盘/光盘台账）。</summary>
        private const int MultiLineRowHeightTwips = PrintPageLayoutSupport.TableRowContentHeightTwoLinesTwips;

        private const int TitleBlockHeightTwips = PrintPageLayoutSupport.ApprovalFormTitleBlockHeightTwips;
        private const int HeaderInfoHeightTwips = 360;
        // 与打印预览一致：两侧标签列固定 8 字符宽。
        private static readonly int[] ColumnWidthsDxa =
            WordExportTableLayoutSupport.ApprovalFormMainColumnWidthsTwips;

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
            AddDoubleRow(table, ref rowIndex, "申请人", data.Applicant, "申请部门", data.Dept, WordTableRowStyle.SingleLine);
            AddSingleRow(table, ref rowIndex, "资料名称", data.MaterialName, WordTableRowStyle.SingleLine);
            AddDoubleRow(table, ref rowIndex, "所属项目", data.ProjectName, "资料来源", data.SourceType, WordTableRowStyle.SingleLine);
            AddSingleRow(table, ref rowIndex, "提供单位", data.ProvideUnit, WordTableRowStyle.SingleLine);

            string contentText = data.ItemLines.Count > 0 ? string.Join("\n", data.ItemLines) : "(无)";
            bool hasRetainedHardDisk = !string.IsNullOrWhiteSpace(data.RetainedHardDiskRegistration);
            bool hasOpticalDiscLedger = !string.IsNullOrWhiteSpace(data.OpticalDiscLedgerSummary);
            int contentRowHeightTwips = CalculateContentRowHeightTwips(hasRetainedHardDisk, hasOpticalDiscLedger);
            AddSingleRow(table, ref rowIndex, "资料内容", contentText, WordTableRowStyle.ItemDetail, contentRowHeightTwips);

            string proofText = data.ProofLines.Count > 0 ? string.Join("\n", data.ProofLines) : "(无)";
            var proofStyle = data.ProofLines.Count <= 1 ? WordTableRowStyle.SingleLine : WordTableRowStyle.MultiLine;
            AddSingleRow(table, ref rowIndex, "证明材料", proofText, proofStyle);

            AddSingleRow(table, ref rowIndex, "库管模式", data.Purpose, WordTableRowStyle.SingleLine);

            if (hasRetainedHardDisk)
            {
                AddSingleRow(table, ref rowIndex, "留存硬盘登记", data.RetainedHardDiskRegistration, WordTableRowStyle.MultiLine);
            }

            if (hasOpticalDiscLedger)
            {
                AddSingleRow(table, ref rowIndex, "光盘台账信息", data.OpticalDiscLedgerSummary, WordTableRowStyle.MultiLine);
            }

            string otherRequests = string.IsNullOrWhiteSpace(data.OtherRequests) ? "(无)" : data.OtherRequests;
            AddSingleRow(table, ref rowIndex, "其他要求", otherRequests, WordTableRowStyle.SingleLine);

            if (data.EnableDeptHead)
            {
                var deptParts = data.DeptHeadApproval.Split('|');
                string deptName = deptParts.ElementAtOrDefault(0) ?? string.Empty;
                string deptDate = deptParts.ElementAtOrDefault(1) ?? "______年___月___日";
                AddSingleRow(table, ref rowIndex, ApprovalWorkflowDomainValues.DisplayDeptHead, FormatSignatureInline(deptName, deptDate), WordTableRowStyle.SingleLine);
            }

            if (data.EnableProductionHead || data.EnableArchiveRoomHead)
            {
                string prodLeader = string.Empty;
                string prodDate = "______年___月___日";
                string rndLeader = string.Empty;
                string rndDate = "______年___月___日";
                if (data.EnableProductionHead)
                {
                    var prodParts = data.ProdFull.Split('|');
                    prodLeader = prodParts.ElementAtOrDefault(1) ?? string.Empty;
                    prodDate = prodParts.ElementAtOrDefault(2) ?? "______年___月___日";
                }

                if (data.EnableArchiveRoomHead)
                {
                    var rndParts = data.RndFull.Split('|');
                    rndLeader = rndParts.ElementAtOrDefault(1) ?? string.Empty;
                    rndDate = rndParts.ElementAtOrDefault(2) ?? "______年___月___日";
                }

                if (data.EnableProductionHead && data.EnableArchiveRoomHead)
                {
                    AddSignatureDoubleRow(
                        table,
                        ref rowIndex,
                        ApprovalWorkflowDomainValues.DisplayProductionHead,
                        prodLeader,
                        prodDate,
                        ApprovalWorkflowDomainValues.DisplayArchiveRoomHead,
                        rndLeader,
                        rndDate);
                }
                else if (data.EnableProductionHead)
                {
                    AddSingleRow(
                        table,
                        ref rowIndex,
                        ApprovalWorkflowDomainValues.DisplayProductionHead,
                        FormatSignatureInline(prodLeader, prodDate),
                        WordTableRowStyle.SingleLine);
                }
                else
                {
                    AddSingleRow(
                        table,
                        ref rowIndex,
                        ApprovalWorkflowDomainValues.DisplayArchiveRoomHead,
                        FormatSignatureInline(rndLeader, rndDate),
                        WordTableRowStyle.SingleLine);
                }
            }

            if (data.EnableArchiveDeputyPresident)
            {
                var depParts = data.ArchiveDeputyPresidentFull.Split('|');
                string depLeader = depParts.ElementAtOrDefault(1) ?? string.Empty;
                string depDate = depParts.ElementAtOrDefault(2) ?? "______年___月___日";
                AddSingleRow(
                    table,
                    ref rowIndex,
                    ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident,
                    FormatSignatureInline(depLeader, depDate),
                    WordTableRowStyle.SingleLine);
            }

            if (data.EnableProductionVicePresident)
            {
                var vpParts = data.ProductionVicePresidentFull.Split('|');
                string vpLeader = vpParts.ElementAtOrDefault(1) ?? string.Empty;
                string vpDate = vpParts.ElementAtOrDefault(2) ?? "______年___月___日";
                AddSingleRow(
                    table,
                    ref rowIndex,
                    ApprovalWorkflowDomainValues.DisplayProductionVicePresident,
                    FormatSignatureInline(vpLeader, vpDate),
                    WordTableRowStyle.SingleLine);
            }

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
            run.SetText("河北省第三测绘院资料室年度资料入档申请审批单");
            WordExportFontSupport.ApplyTitle(run);
        }

        private static void AddHeaderInfo(XWPFDocument document, ArchiveRegisterPrintData data)
        {
            var headerTable = document.CreateTable(1, 2);
            ConfigureHeaderTable(headerTable);

            XWPFTableRow row = headerTable.GetRow(0);
            WriteOutsideHeaderCell(row.GetCell(0), $"申请单编号：{data.FormNo}", ParagraphAlignment.LEFT);
            WriteOutsideHeaderCell(row.GetCell(1), $"申请日期：{data.Date}", ParagraphAlignment.RIGHT);
        }

        private static void AddFooterNotes(XWPFDocument document)
        {
            var titleParagraph = document.CreateParagraph();
            ApplyDocumentParagraph(titleParagraph);
            var titleRun = titleParagraph.CreateRun();
            titleRun.SetText("备注：");
            WordExportFontSupport.ApplyFooter(titleRun, bold: true);
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
            WordExportFontSupport.ApplyFooter(run);
        }

        private static int CalculateContentRowHeightTwips(bool hasRetainedHardDisk, bool hasOpticalDiscLedger)
        {
            // 与 FlowDocument 登记单一致：固定行外高 + 表后 4 行说明同页。
            int fixedTableHeight =
                PrintPageLayoutSupport.GetTableRowOuterHeightTwips(SingleRowHeightTwips, CellMarginDxa) * 9
                + PrintPageLayoutSupport.GetTableRowOuterHeightTwips(SignatureBlockRowHeightTwips, CellMarginDxa) * 2;
            if (hasRetainedHardDisk)
            {
                fixedTableHeight += PrintPageLayoutSupport.GetTableRowOuterHeightTwips(MultiLineRowHeightTwips, CellMarginDxa);
            }

            if (hasOpticalDiscLedger)
            {
                fixedTableHeight += PrintPageLayoutSupport.GetTableRowOuterHeightTwips(MultiLineRowHeightTwips, CellMarginDxa);
            }

            int footerHeight = PrintPageLayoutSupport.EstimateNoteBlockHeightTwips(lineCount: 4, lineHeightTwips: 270, topMarginTwips: 220);
            int reservedHeight =
                TitleBlockHeightTwips
                + HeaderInfoHeightTwips
                + footerHeight
                + fixedTableHeight;
            int stretchHeight = PrintPageLayoutSupport.CalculateStretchRowHeightTwips(
                reservedHeight,
                SingleRowHeightTwips * 4,
                CellMarginDxa);
            // 与 FlowDocument 一致：资料内容栏再加 2 行正文高度（约 21DIP/行 ≈ 315 twips）。
            const int twoContentLinesTwips = 315 * 2;
            return stretchHeight + twoContentLinesTwips;
        }

        private enum WordTableRowStyle
        {
            SingleLine,
            MultiLine,
            ItemDetail,
            SignatureBlock
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
            ApplyRowStyle(row, rowStyle);

            WriteLabelCell(row.GetCell(0), label1, rowStyle);
            WriteBodyCell(row.GetCell(1), content1, rowStyle);
            WriteLabelCell(row.GetCell(2), label2, rowStyle);
            WriteBodyCell(row.GetCell(3), content2, rowStyle);
            WordExportTableLayoutSupport.ApplyRowCellWidths(row, ColumnWidthsDxa);

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
            WordExportTableLayoutSupport.ApplyRowCellWidths(row, ColumnWidthsDxa);

            rowIndex++;
        }

        private static void ApplyRowStyle(XWPFTableRow row, WordTableRowStyle rowStyle, int? explicitHeightTwips = null)
        {
            int heightTwips = rowStyle switch
            {
                WordTableRowStyle.SingleLine => SingleRowHeightTwips,
                WordTableRowStyle.MultiLine => MultiLineRowHeightTwips,
                WordTableRowStyle.ItemDetail => explicitHeightTwips ?? SingleRowHeightTwips * 4,
                WordTableRowStyle.SignatureBlock => SignatureBlockRowHeightTwips,
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

        /// <summary>
        /// 签字区：单行「签字：…    日期：…」，与 FlowDocument / PrintApprovalSignatureSupport 一致。
        /// </summary>
        private static void WriteSignatureBlockCell(XWPFTableCell cell, string signer, string date)
        {
            ResetCell(cell, WordTableRowStyle.SignatureBlock);
            AddCellParagraph(
                cell,
                PrintApprovalSignatureSupport.FormatInline(signer, date),
                label: false,
                ParagraphAlignment.LEFT);
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
            bool alignTop = rowStyle is WordTableRowStyle.MultiLine or WordTableRowStyle.ItemDetail;
            cell.SetVerticalAlignment(alignTop
                ? XWPFTableCell.XWPFVertAlign.TOP
                : XWPFTableCell.XWPFVertAlign.CENTER);

            var tcPr = cell.GetCTTc().tcPr ?? cell.GetCTTc().AddNewTcPr();
            var vAlign = tcPr.vAlign ?? tcPr.AddNewVAlign();
            vAlign.val = alignTop
                ? ST_VerticalJc.top
                : ST_VerticalJc.center;
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

        private static CT_Border CreateNilBorder()
        {
            return new CT_Border
            {
                val = ST_Border.nil,
                sz = 0,
                color = "auto"
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

        private static string FormatSignatureInline(string signer, string date) =>
            PrintApprovalSignatureSupport.FormatInline(signer, date);
    }
}
