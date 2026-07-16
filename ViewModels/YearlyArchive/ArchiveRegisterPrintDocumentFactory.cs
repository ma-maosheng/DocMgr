using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
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
        private const double PrintRowHeightOneLine = 28;

        /// <summary>打印表格双行高度（约 2 行正文）。</summary>
        private const double PrintRowHeightTwoLines = 50;

        private const double PrintContentFontSize = 12;
        private const double PrintContentLineHeight = 21;
        private static readonly Thickness PrintLabelPadding = new Thickness(4, 2, 4, 2);
        private static readonly Thickness PrintContentMargin = new Thickness(6, 2, 6, 2);

        /// <summary>
        /// 构建资料登记入档申请审批单 FlowDocument。<paramref name="isApplicationPrint"/> 为 true 时用于申请人打印申请（审批签字区置空）。
        /// </summary>
        internal static FlowDocument Create(ArchiveRegisterPrintData data, bool isApplicationPrint = false)
        {
            ArgumentNullException.ThrowIfNull(data);

            FlowDocument doc = new FlowDocument();
            doc.FontFamily = FontBody; // 默认宋体
            doc.FontSize = 12;
            doc.LineHeight = 21;
            doc.PagePadding = new Thickness(70, 40, 70, 40);
            doc.ColumnWidth = double.PositiveInfinity;

            // 标题使用黑体，加粗，字号加大
            var title = new Paragraph(new Run("\n\n河北省第三测绘院资料室年度资料入档申请审批单"))
            {
                FontFamily = FontTitle,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12),
                LineHeight = 30
            };
            doc.Blocks.Add(title);

            Table hTable = new Table();
            hTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            hTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            TableRowGroup hGrp = new TableRowGroup();
            TableRow hRow = new TableRow();
            // 表单编号和日期使用楷体或宋体均可，保持宋体
            hRow.Cells.Add(new TableCell(new Paragraph(new Run($"申请单编号：{data.FormNo}")) { Margin = new Thickness(0, 0, 0, 2) }) { TextAlignment = TextAlignment.Left });
            hRow.Cells.Add(new TableCell(new Paragraph(new Run($"申请日期：{data.Date}")) { Margin = new Thickness(0, 0, 0, 2) }) { TextAlignment = TextAlignment.Right });
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

            t.Columns.Add(new TableColumn { Width = new GridLength(1.8, GridUnitType.Star) }); // 标签列稍宽一点
            t.Columns.Add(new TableColumn { Width = new GridLength(3.2, GridUnitType.Star) });
            t.Columns.Add(new TableColumn { Width = new GridLength(1.8, GridUnitType.Star) });
            t.Columns.Add(new TableColumn { Width = new GridLength(3.2, GridUnitType.Star) });

            // 计算动态高度：资料内容占据剩余空间，其余行按 1 行或 2 行高度统一
            double totalHeight = 230;

            // 证明材料区最小 1 行高度
            double proofHeight = PrintRowHeightOneLine;

            // 资料内容占据剩余的所有空间
            double contentHeight = totalHeight - proofHeight;
            // 申请单打印：审批签字区采用单行格式，将节省的高度补给资料内容区
            if (isApplicationPrint)
            {
                contentHeight = Math.Max(80, contentHeight - PrintRowHeightOneLine);
            }

            TableRowGroup g = new TableRowGroup();
            g.Rows.Add(CreateRow("资料名称", data.MaterialName, 3, PrintRowHeightOneLine));
            g.Rows.Add(CreateDoubleRow("所属项目", data.ProjectName, "资料来源", data.SourceType, PrintRowHeightOneLine));
            g.Rows.Add(CreateRow("提供单位", data.ProvideUnit, 3, PrintRowHeightOneLine));

            string contentStr = data.ItemLines.Count > 0 ? string.Join("\n", data.ItemLines) : "(无)";
            g.Rows.Add(CreateRow("资料内容", contentStr, 3, contentHeight, VerticalAlignment.Top));

            string proofStr = data.ProofLines.Count > 0 ? string.Join("\n", data.ProofLines) : "(无)";
            g.Rows.Add(CreateRow("证明材料", proofStr, 3, proofHeight, VerticalAlignment.Top));

            g.Rows.Add(CreateDoubleRow("库管模式", data.Purpose, "申请部门", data.Dept, PrintRowHeightOneLine));
            if (!string.IsNullOrWhiteSpace(data.RetainedHardDiskRegistration))
            {
                g.Rows.Add(CreateRow("留存硬盘登记", data.RetainedHardDiskRegistration, 3, PrintRowHeightTwoLines, VerticalAlignment.Top));
            }
            if (!string.IsNullOrWhiteSpace(data.OpticalDiscLedgerSummary))
            {
                g.Rows.Add(CreateRow("光盘台账信息", data.OpticalDiscLedgerSummary, 3, PrintRowHeightTwoLines, VerticalAlignment.Top));
            }
            g.Rows.Add(CreateRow("其他要求", string.IsNullOrWhiteSpace(data.OtherRequests) ? "(无)" : data.OtherRequests, 3, PrintRowHeightOneLine, VerticalAlignment.Top));
            g.Rows.Add(CreateDoubleRow("申请人", data.Applicant, "申请日期", data.Date, PrintRowHeightOneLine));

            // 审批签字区：部门审核 → 生产/科研 → 开发室分管领导 → 交接确认
            string[] deptParts = data.DeptLeaderApproval.Split('|');
            string deptName = deptParts.ElementAtOrDefault(0) ?? "";
            string deptDate = deptParts.ElementAtOrDefault(1) ?? "______年___月___日";
            string deptT = FormatSignatureInline(deptName, deptDate);
            g.Rows.Add(CreateRow("部门审核", deptT, 3, PrintRowHeightOneLine));

            string[] prodParts = data.ProdFull.Split('|');
            string prodLeader = prodParts.ElementAtOrDefault(1) ?? "";
            string prodDate = prodParts.ElementAtOrDefault(2) ?? "______年___月___日";
            string prodT = FormatSignatureBlock(prodLeader, prodDate);

            string[] rndParts = data.RndFull.Split('|');
            string rndLeader = rndParts.ElementAtOrDefault(1) ?? "";
            string rndDate = rndParts.ElementAtOrDefault(2) ?? "______年___月___日";
            string rndT = FormatSignatureBlock(rndLeader, rndDate);
            g.Rows.Add(CreateSignatureDoubleRow("生产管理科\n审批", prodT, "科研开发室\n审批", rndT, PrintRowHeightTwoLines));

            var depParts = data.DeputyFull.Split('|');
            string depLeader = depParts.ElementAtOrDefault(1) ?? "";
            string depDate = depParts.ElementAtOrDefault(2) ?? "______年___月___日";
            string depT = FormatSignatureInline(depLeader, depDate);
            g.Rows.Add(CreateRow("开发室分管领导", depT, 3, PrintRowHeightOneLine));

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

        private static string FormatSignatureInline(string signer, string date)
        {
            var signatureSlot = string.IsNullOrWhiteSpace(signer)
                ? "________________"
                : signer;
            return $"签字：{signatureSlot}    日期：{date}";
        }

        private static string FormatSignatureBlock(string signer, string date)
        {
            var signatureSlot = string.IsNullOrWhiteSpace(signer)
                ? "________________"
                : signer;
            return $"签字：{signatureSlot}\n日期：{date}";
        }

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
                    TextWrapping.Wrap);
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
