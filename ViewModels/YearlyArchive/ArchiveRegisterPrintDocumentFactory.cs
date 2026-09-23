using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 资料登记（申请/审批）打印单据的 FlowDocument 构建工厂，供编辑弹窗与只读查看弹窗共用。
    /// </summary>
    internal static class ArchiveRegisterPrintDocumentFactory
    {
        private static readonly FontFamily FontTitle = new FontFamily("SimHei");
        private static readonly FontFamily FontLabel = new FontFamily("SimHei");
        private static readonly FontFamily FontBody = new FontFamily("SimSun");

        /// <summary>打印表格单行高度（约 1 行正文）。</summary>
        private const double PrintRowHeightOneLine = PrintPageLayoutSupport.TableRowContentHeightOneLineDip;

        /// <summary>打印表格双行高度（约 2 行正文，交接签字）。</summary>
        private const double PrintRowHeightTwoLines = PrintPageLayoutSupport.TableRowContentHeightTwoLinesDip;

        private const double HeaderInfoHeight = 28;
        private const double CellPadding = PrintPageLayoutSupport.TableCellPaddingDip;
        private const double PrintContentFontSize = 12;
        private const double PrintContentLineHeight = 21;
        private static readonly Thickness PrintLabelPadding = new Thickness(4, 2, 4, 2);
        private static readonly Thickness PrintContentMargin = new Thickness(4, 2, 4, 2);

        /// <summary>其他要求按正文估行后的上限。</summary>
        private const int OtherRequestsMaxLines = 5;

        /// <summary>留存硬盘/光盘台账按正文估行后的上限。</summary>
        private const int MediumSummaryMaxLines = 3;

        /// <summary>资料内容按正文估行后的上限（撑满下限）。</summary>
        private const int ContentDetailMaxLines = 20;

        /// <summary>
        /// 构建资料登记入档申请审批单 FlowDocument。<paramref name="isApplicationPrint"/> 为 true 时用于申请人打印申请（审批签字区置空）。
        /// </summary>
        internal static FlowDocument Create(ArchiveRegisterPrintData data, bool isApplicationPrint = false)
        {
            ArgumentNullException.ThrowIfNull(data);
            // 签字留白由打印数据装配控制；本工厂仅负责版式（含资料内容行撑满）。
            _ = isApplicationPrint;

            FlowDocument doc = new FlowDocument();
            doc.FontFamily = FontBody; // 默认宋体
            doc.FontSize = 12;
            doc.LineHeight = PrintPageLayoutSupport.ApprovalFormLineHeightDip;
            doc.ColumnWidth = double.PositiveInfinity;
            PrintPageLayoutSupport.ApplyA4MediumMargins(doc);

            // 标题使用黑体，加粗；距页顶位置与编号行间距见 PrintPageLayoutSupport 申请审批单常量。
            var title = new Paragraph(new Run("河北省第三测绘院资料室年度资料入档申请审批单"))
            {
                FontFamily = FontTitle,
                FontSize = PrintPageLayoutSupport.ApprovalFormTitleFontSize,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = PrintPageLayoutSupport.ApprovalFormTitleMargin,
                LineHeight = PrintPageLayoutSupport.ApprovalFormLineHeightDip * 1.5
            };
            doc.Blocks.Add(title);

            Table hTable = new Table { Margin = PrintPageLayoutSupport.ApprovalFormHeaderTableMargin };
            hTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            hTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            TableRowGroup hGrp = new TableRowGroup();
            TableRow hRow = new TableRow();
            // 表单编号和日期使用楷体或宋体均可，保持宋体
            hRow.Cells.Add(new TableCell(new Paragraph(new Run($"申请单编号：{data.FormNo}")
            {
                FontFamily = FontBody,
                FontSize = PrintContentFontSize
            })
            { Margin = new Thickness(0) })
            { TextAlignment = TextAlignment.Left });
            hRow.Cells.Add(new TableCell(new Paragraph(new Run($"申请日期：{data.Date}")
            {
                FontFamily = FontBody,
                FontSize = PrintContentFontSize
            })
            { Margin = new Thickness(0) })
            { TextAlignment = TextAlignment.Right });
            hGrp.Rows.Add(hRow);
            hTable.RowGroups.Add(hGrp);
            doc.Blocks.Add(hTable);

            // 主表格样式：边框黑色
            // BorderThickness: 左=2, 上=2 (加粗外框), 右=0, 下=0 (由单元格绘制内框和右下封口)
            Table t = new Table
            {
                CellSpacing = 0,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(2, 2, 0, 0)
            };

            PrintPageLayoutSupport.ApplyApprovalFormMainTableColumns(t);

            string contentStr = data.ItemLines.Count > 0 ? string.Join("\n", data.ItemLines) : "(无)";
            string proofStr = data.ProofLines.Count > 0 ? string.Join("\n", data.ProofLines) : "(无)";
            string otherRequestsText = string.IsNullOrWhiteSpace(data.OtherRequests) ? "(无)" : data.OtherRequests;
            bool hasRetainedHardDisk = !string.IsNullOrWhiteSpace(data.RetainedHardDiskRegistration);
            bool hasOpticalDiscLedger = !string.IsNullOrWhiteSpace(data.OpticalDiscLedgerSummary);

            double proofHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(proofStr, MediumSummaryMaxLines);
            double otherRequestsHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(
                otherRequestsText,
                OtherRequestsMaxLines);
            double retainedHardDiskHeight = hasRetainedHardDisk
                ? PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(
                    data.RetainedHardDiskRegistration,
                    MediumSummaryMaxLines)
                : 0;
            double opticalDiscHeight = hasOpticalDiscLedger
                ? PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(
                    data.OpticalDiscLedgerSummary,
                    MediumSummaryMaxLines)
                : 0;
            double contentHeight = CalculateContentRowHeight(
                data,
                contentStr,
                proofHeight,
                otherRequestsHeight,
                retainedHardDiskHeight,
                opticalDiscHeight,
                hasRetainedHardDisk,
                hasOpticalDiscLedger);

            TableRowGroup g = new TableRowGroup();
            g.Rows.Add(CreateDoubleRow("申请人", data.Applicant, "申请部门", data.Dept, PrintRowHeightOneLine));
            g.Rows.Add(CreateRow("资料名称", data.MaterialName, 3, PrintRowHeightOneLine));
            g.Rows.Add(CreateDoubleRow("所属项目", data.ProjectName, "资料来源", data.SourceType, PrintRowHeightOneLine));
            g.Rows.Add(CreateRow("提供单位", data.ProvideUnit, 3, PrintRowHeightOneLine));

            g.Rows.Add(CreateRow("资料内容", contentStr, 3, contentHeight, VerticalAlignment.Top));

            g.Rows.Add(CreateRow("证明材料", proofStr, 3, proofHeight, VerticalAlignment.Top));

            g.Rows.Add(CreateRow("库管模式", data.Purpose, 3, PrintRowHeightOneLine));
            if (hasRetainedHardDisk)
            {
                g.Rows.Add(CreateRow(
                    "留存硬盘登记",
                    data.RetainedHardDiskRegistration,
                    3,
                    retainedHardDiskHeight,
                    VerticalAlignment.Top));
            }
            if (hasOpticalDiscLedger)
            {
                g.Rows.Add(CreateRow(
                    "光盘台账信息",
                    data.OpticalDiscLedgerSummary,
                    3,
                    opticalDiscHeight,
                    VerticalAlignment.Top));
            }
            g.Rows.Add(CreateRow("其他要求", otherRequestsText, 3, otherRequestsHeight, VerticalAlignment.Top));

            // 审批签字区：按审核审批配置启用节点输出
            if (data.EnableDeptHead)
            {
                string[] deptParts = data.DeptHeadApproval.Split('|');
                string deptName = deptParts.ElementAtOrDefault(0) ?? "";
                string deptDate = deptParts.ElementAtOrDefault(1) ?? "______年___月___日";
                string deptT = FormatSignatureInline(deptName, deptDate);
                g.Rows.Add(CreateRow("部门审核", deptT, 3, PrintRowHeightOneLine));
            }

            if (data.EnableProductionHead || data.EnableArchiveRoomHead)
            {
                string prodT = string.Empty;
                string rndT = string.Empty;
                if (data.EnableProductionHead)
                {
                    string[] prodParts = data.ProdFull.Split('|');
                    string prodLeader = prodParts.ElementAtOrDefault(1) ?? "";
                    string prodDate = prodParts.ElementAtOrDefault(2) ?? "______年___月___日";
                    prodT = FormatSignatureBlock(prodLeader, prodDate);
                }

                if (data.EnableArchiveRoomHead)
                {
                    string[] rndParts = data.RndFull.Split('|');
                    string rndLeader = rndParts.ElementAtOrDefault(1) ?? "";
                    string rndDate = rndParts.ElementAtOrDefault(2) ?? "______年___月___日";
                    rndT = FormatSignatureBlock(rndLeader, rndDate);
                }

                if (data.EnableProductionHead && data.EnableArchiveRoomHead)
                {
                    g.Rows.Add(CreateSignatureDoubleRow(
                        ApprovalWorkflowDomainValues.DisplayProductionHead,
                        prodT,
                        ApprovalWorkflowDomainValues.DisplayArchiveRoomHead,
                        rndT,
                        PrintRowHeightOneLine));
                }
                else if (data.EnableProductionHead)
                {
                    g.Rows.Add(CreateRow(
                        ApprovalWorkflowDomainValues.DisplayProductionHead,
                        FormatSignatureInline(
                            data.ProdFull.Split('|').ElementAtOrDefault(1) ?? "",
                            data.ProdFull.Split('|').ElementAtOrDefault(2) ?? "______年___月___日"),
                        3,
                        PrintRowHeightOneLine));
                }
                else
                {
                    g.Rows.Add(CreateRow(
                        ApprovalWorkflowDomainValues.DisplayArchiveRoomHead,
                        FormatSignatureInline(
                            data.RndFull.Split('|').ElementAtOrDefault(1) ?? "",
                            data.RndFull.Split('|').ElementAtOrDefault(2) ?? "______年___月___日"),
                        3,
                        PrintRowHeightOneLine));
                }
            }

            if (data.EnableArchiveDeputyPresident)
            {
                var depParts = data.ArchiveDeputyPresidentFull.Split('|');
                string depLeader = depParts.ElementAtOrDefault(1) ?? "";
                string depDate = depParts.ElementAtOrDefault(2) ?? "______年___月___日";
                string depT = FormatSignatureInline(depLeader, depDate);
                g.Rows.Add(CreateRow(
                    ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident,
                    depT,
                    3,
                    PrintRowHeightOneLine));
            }

            if (data.EnableProductionVicePresident)
            {
                var vpParts = data.ProductionVicePresidentFull.Split('|');
                string vpLeader = vpParts.ElementAtOrDefault(1) ?? "";
                string vpDate = vpParts.ElementAtOrDefault(2) ?? "______年___月___日";
                string vpT = FormatSignatureInline(vpLeader, vpDate);
                g.Rows.Add(CreateRow(
                    ApprovalWorkflowDomainValues.DisplayProductionVicePresident,
                    vpT,
                    3,
                    PrintRowHeightOneLine));
            }

            var deliverParts = data.DeliverFull.Split('|');
            string deliverer = deliverParts.ElementAtOrDefault(0) ?? "";
            string deliverDate = deliverParts.ElementAtOrDefault(1) ?? "______年___月___日";
            string delT = FormatSignatureBlock(deliverer, deliverDate);

            var adminParts = data.AdminFull.Split('|');
            string adminName = adminParts.ElementAtOrDefault(0) ?? "";
            string adminDate = adminParts.ElementAtOrDefault(1) ?? "______年___月___日";
            string recT = FormatSignatureBlock(adminName, adminDate);

            g.Rows.Add(CreateSignatureDoubleRow("资料送达人\n交接确认", delT, "资料室\n接收确认", recT, PrintRowHeightTwoLines));
            t.RowGroups.Add(g);
            doc.Blocks.Add(t);

            var foot = new Paragraph { FontSize = 10.5, Margin = new Thickness(0, 15, 0, 0), LineHeight = 18 };
            foot.Inlines.Add(new Run("备注：") { FontWeight = FontWeights.Bold });

            foot.Inlines.Add(new Run("1、审核、审批时，生产科负责人必须在各资料子项的[密级]处手签具体密级和本人姓名。\n"));
            foot.Inlines.Add(new Run("      2、审批完成后，申请人（交接人）携带拟归档所有资料、材料(包括本表单、相关附件等）到资料室办理登记、交接工作。\n"));
            foot.Inlines.Add(new Run("      3、交接人、资料管理员应对照本表单中的各项内容共同完成归档资料、材料的查验和拍照，确认账实相符后分别在表单上签名确认。\n"));
            foot.Inlines.Add(new Run("      4、本表单、资料照片电子件应上传到本系统，同时表单原件由资料室存档保管，资料交接人有自存需要的可采用复印或拍照方式留存。"));
            doc.Blocks.Add(foot);

            return doc;
        }

        private static double CalculateContentRowHeight(
            ArchiveRegisterPrintData data,
            string contentStr,
            double proofHeight,
            double otherRequestsHeight,
            double retainedHardDiskHeight,
            double opticalDiscHeight,
            bool hasRetainedHardDisk,
            bool hasOpticalDiscLedger)
        {
            int approvalRowCount = CountApprovalPrintRows(data);
            // 固定行（不含资料内容撑满行）：
            // 申请人/名称/项目/单位/库管(5) + 证明/其他要求(+留存硬盘/光盘) + 签批行 + 交接。
            int oneLineRows = 5 + approvalRowCount;
            double fixedContentHeight =
                PrintRowHeightOneLine * oneLineRows
                + proofHeight
                + otherRequestsHeight
                + (hasRetainedHardDisk ? retainedHardDiskHeight : 0)
                + (hasOpticalDiscLedger ? opticalDiscHeight : 0)
                + PrintRowHeightTwoLines;
            int variableTextRows = 2
                + (hasRetainedHardDisk ? 1 : 0)
                + (hasOpticalDiscLedger ? 1 : 0); // 证明、其他要求、[留存硬盘]、[光盘]
            int fixedRowCount = oneLineRows + variableTextRows + 1; // +交接
            double fixedTableHeight = fixedContentHeight
                + PrintPageLayoutSupport.GetTableRowOuterHeightDip(0, CellPadding) * fixedRowCount
                + PrintPageLayoutSupport.EstimateTableBottomBorderHeightDip(fixedRowCount);

            string footerText = BuildFooterNoteText();
            double footerHeight = PrintPageLayoutSupport.EstimateNoteBlockHeightFromTextWithSafetyDip(
                footerText,
                PrintPageLayoutSupport.ContentWidthDip,
                fontSizeDip: 10.5,
                lineHeightDip: 18,
                topMarginDip: 15);
            double reservedHeight =
                PrintPageLayoutSupport.ApprovalFormTitleBlockHeightDip
                + HeaderInfoHeight
                + PrintPageLayoutSupport.ApprovalFormHeaderToTableGapDip
                + footerHeight
                + fixedTableHeight;
            double contentNeededHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(
                contentStr,
                ContentDetailMaxLines);
            return PrintPageLayoutSupport.CalculateStretchRowHeightDip(
                reservedHeight,
                contentNeededHeight,
                CellPadding);
        }

        /// <summary>统计打印表中实际输出的签批行数（生产/资料室双列计 1 行）。</summary>
        private static int CountApprovalPrintRows(ArchiveRegisterPrintData data)
        {
            int count = 0;
            if (data.EnableDeptHead)
            {
                count++;
            }

            if (data.EnableProductionHead || data.EnableArchiveRoomHead)
            {
                count++;
            }

            if (data.EnableArchiveDeputyPresident)
            {
                count++;
            }

            if (data.EnableProductionVicePresident)
            {
                count++;
            }

            return count;
        }

        /// <summary>与表后说明段落渲染文案一致，供估高。</summary>
        private static string BuildFooterNoteText() =>
            "备注：" +
            "1、审核、审批时，生产科负责人必须在各资料子项的[密级]处手签具体密级和本人姓名。\n" +
            "      2、审批完成后，申请人（交接人）携带拟归档所有资料、材料(包括本表单、相关附件等）到资料室办理登记、交接工作。\n" +
            "      3、交接人、资料管理员应对照本表单中的各项内容共同完成归档资料、材料的查验和拍照，确认账实相符后分别在表单上签名确认。\n" +
            "      4、本表单、资料照片电子件应上传到本系统，同时表单原件由资料室存档保管，资料交接人有自存需要的可采用复印或拍照方式留存。";

        private static TableRow CreateRow(string label, string content, int contentColSpan, double minHeight = 0, VerticalAlignment contentVerticalAlignment = VerticalAlignment.Center)
        {
            var row = new TableRow();
            row.Cells.Add(CreateStandardLabelCell(label, minHeight));
            row.Cells.Add(CreateContentCell(content, minHeight, contentVerticalAlignment, contentColSpan));
            return row;
        }

        private static TableRow CreateDoubleRow(string label1, string content1, string label2, string content2, double minHeight = 0)
        {
            var row = new TableRow();
            row.Cells.Add(CreateStandardLabelCell(label1, minHeight));
            row.Cells.Add(CreateContentCell(content1, minHeight, VerticalAlignment.Center));
            row.Cells.Add(CreateStandardLabelCell(label2, minHeight));
            row.Cells.Add(CreateContentCell(content2, minHeight, VerticalAlignment.Center));
            return row;
        }

        private static string FormatSignatureInline(string signer, string date) =>
            PrintApprovalSignatureSupport.FormatInline(signer, date);

        private static string FormatSignatureBlock(string signer, string date) =>
            FormatSignatureInline(signer, date);

        private static TableCell CreateStandardLabelCell(string label, double minHeight = 0)
        {
            return new TableCell(CreateLabelBlock(label, minHeight))
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.Black
            };
        }

        private static Block CreateLabelBlock(string label, double minHeight)
        {
            if (minHeight > 0)
            {
                return CreateAlignedTextBlockContainer(
                    label,
                    minHeight,
                    FontLabel,
                    FontWeights.Bold,
                    TextAlignment.Center,
                    VerticalAlignment.Center,
                    TextWrapping.NoWrap);
            }

            return new Paragraph(new Run(label))
            {
                FontFamily = FontLabel,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = PrintLabelPadding,
                LineHeight = PrintContentLineHeight
            };
        }

        private static TableCell CreateContentCell(
            string content,
            double minHeight,
            VerticalAlignment verticalAlignment,
            int columnSpan = 1)
        {
            Block contentBlock = minHeight > 0
                ? CreateAlignedTextBlockContainer(
                    content,
                    minHeight,
                    FontBody,
                    FontWeights.Normal,
                    TextAlignment.Left,
                    verticalAlignment,
                    TextWrapping.Wrap)
                : new Paragraph(new Run(content ?? string.Empty))
                {
                    FontFamily = FontBody,
                    FontSize = PrintContentFontSize,
                    LineHeight = PrintContentLineHeight,
                    TextAlignment = TextAlignment.Left,
                    Margin = PrintContentMargin
                };

            return new TableCell(contentBlock)
            {
                ColumnSpan = columnSpan,
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.Black
            };
        }

        private static Block CreateAlignedTextBlockContainer(
            string text,
            double minHeight,
            FontFamily fontFamily,
            FontWeight fontWeight,
            TextAlignment textAlignment,
            VerticalAlignment verticalAlignment,
            TextWrapping textWrapping)
        {
            var grid = new Grid { MinHeight = minHeight };
            grid.Children.Add(new TextBlock
            {
                Text = text ?? string.Empty,
                FontFamily = fontFamily,
                FontSize = PrintContentFontSize,
                FontWeight = fontWeight,
                LineHeight = PrintContentLineHeight,
                TextAlignment = textAlignment,
                TextWrapping = textWrapping,
                VerticalAlignment = verticalAlignment,
                HorizontalAlignment = textAlignment == TextAlignment.Center
                    ? HorizontalAlignment.Center
                    : HorizontalAlignment.Stretch,
                Margin = textAlignment == TextAlignment.Center ? PrintLabelPadding : PrintContentMargin
            });
            return new BlockUIContainer(grid);
        }

        private static TableCell CreateSignatureContentCell(string content, double minHeight)
        {
            return CreateContentCell(content, minHeight, VerticalAlignment.Bottom);
        }

        private static TableRow CreateSignatureDoubleRow(string label1, string content1, string label2, string content2, double minHeight)
        {
            var row = new TableRow();
            row.Cells.Add(CreateStandardLabelCell(label1, minHeight));
            row.Cells.Add(CreateSignatureContentCell(content1, minHeight));
            row.Cells.Add(CreateStandardLabelCell(label2, minHeight));
            row.Cells.Add(CreateSignatureContentCell(content2, minHeight));
            return row;
        }
    }
}
