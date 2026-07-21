using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.Shared;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.ViewModels.YearlyArchive
{
    internal static class ArchiveReturnPrintDocumentFactory
    {
        private static readonly FontFamily TitleFont = new("SimHei");
        private static readonly FontFamily LabelFont = new("SimHei");
        private static readonly FontFamily BodyFont = new("SimSun");

        /// <summary>单行信息行高。</summary>
        private const double StandardRowHeight = 34;
        /// <summary>备注/灭失说明等多行文本行高。</summary>
        private const double RemarkRowHeight = 52;
        /// <summary>审核审批签字行高（单行签字栏）。</summary>
        private const double ApprovalSignatureRowHeight = 34;
        /// <summary>交接签字行高（归还人 + 资料员两行，列对齐排版）。</summary>
        private const double HandoverSignatureRowHeight = 72;
        private const double TitleBlockHeight = 48;
        private const double HeaderInfoHeight = 28;
        private const double CellPadding = 4;
        private const double BodyFontSize = 12;
        private const string BlankSignerSlot = "________________";
        private const string BlankDateSlot = "______年___月___日";

        internal static FlowDocument Create(ArchiveReturnReceiptPrintData data)
        {
            ArgumentNullException.ThrowIfNull(data);

            double itemDetailRowHeight = CalculateItemDetailRowHeight(data);

            var document = new FlowDocument
            {
                FontFamily = BodyFont,
                FontSize = BodyFontSize,
                LineHeight = 18,
                ColumnWidth = double.PositiveInfinity
            };
            PrintPageLayoutSupport.ApplyA4MediumMargins(document);

            string title = string.IsNullOrWhiteSpace(data.DocumentTitle)
                ? "河北省第三测绘院资料室年度资料归还交接单"
                : data.DocumentTitle.Trim();
            document.Blocks.Add(new Paragraph(new Run(title))
            {
                FontFamily = TitleFont,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });

            document.Blocks.Add(CreateHeaderTable(
                $"归还单编号：{data.ReturnNo}",
                $"归还日期：{data.ReturnDateText}"));

            var rowGroup = new TableRowGroup();
            rowGroup.Rows.Add(CreateDoubleRow("借出部门", data.BorrowerDept, "借出人", data.BorrowerName));
            rowGroup.Rows.Add(CreateDoubleRow("源出库单号", data.SourceOutboundNo, "登记人", data.RegisteredByName));
            rowGroup.Rows.Add(CreateSingleRow("应还日期", data.ExpectedReturnDateText));
            rowGroup.Rows.Add(CreateSingleRow("资料摘要", EmptyAsPlaceholder(data.MaterialSummary)));
            rowGroup.Rows.Add(CreateSingleRow(
                "归还资料明细",
                BuildItemText(data),
                itemDetailRowHeight,
                verticalTop: true));
            if (data.HasLossReturn)
            {
                rowGroup.Rows.Add(CreateSingleRow(
                    "灭失情况描述",
                    EmptyAsPlaceholder(data.LossDescription),
                    RemarkRowHeight,
                    verticalTop: true));
            }

            foreach (var approvalLine in data.ApprovalSignatureLines)
            {
                rowGroup.Rows.Add(CreateSignatureRow(
                    approvalLine.RoleLabel,
                    BuildApprovalSignatureLine(approvalLine)));
            }

            rowGroup.Rows.Add(CreateHandoverSignatureRow(data.HandoverSignatureLines));

            rowGroup.Rows.Add(CreateSingleRow(
                "备注",
                EmptyAsPlaceholder(data.Remark),
                RemarkRowHeight,
                verticalTop: true));

            document.Blocks.Add(CreateMainTable(rowGroup));
            document.Blocks.Add(CreateFooterParagraph(data));

            return document;
        }

        private static double CalculateItemDetailRowHeight(ArchiveReturnReceiptPrintData data)
        {
            // 固定行：借出部门、源出库单、应还日期、资料摘要、交接、备注；灭失描述可选；审批签字行数可变。
            int approvalCount = data.ApprovalSignatureLines?.Count ?? 0;
            double fixedTableHeight =
                PrintPageLayoutSupport.GetTableRowOuterHeightDip(StandardRowHeight, CellPadding) * 4
                + PrintPageLayoutSupport.GetTableRowOuterHeightDip(ApprovalSignatureRowHeight, CellPadding) * approvalCount
                + PrintPageLayoutSupport.GetTableRowOuterHeightDip(HandoverSignatureRowHeight, CellPadding)
                + PrintPageLayoutSupport.GetTableRowOuterHeightDip(RemarkRowHeight, CellPadding);
            if (data.HasLossReturn)
            {
                fixedTableHeight += PrintPageLayoutSupport.GetTableRowOuterHeightDip(RemarkRowHeight, CellPadding);
            }

            double footerHeight = PrintPageLayoutSupport.EstimateNoteBlockHeightDip(lineCount: 4, lineHeightDip: 16, topMarginDip: 8);
            double reservedHeight = TitleBlockHeight + HeaderInfoHeight + footerHeight + fixedTableHeight;
            return PrintPageLayoutSupport.CalculateStretchRowHeightDip(
                reservedHeight,
                StandardRowHeight * 4,
                CellPadding);
        }

        private static string BuildApprovalSignatureLine(ArchiveReturnApprovalSignatureLine line)
        {
            string signerSlot = string.IsNullOrWhiteSpace(line.SignerSlot)
                ? BlankSignerSlot
                : line.SignerSlot.Trim();
            string dateText = string.IsNullOrWhiteSpace(line.DateText)
                ? BlankDateSlot
                : line.DateText.Trim();
            return $"签字：{signerSlot}    日期：{dateText}";
        }

        private static TableRow CreateSignatureRow(string roleLabel, string signatureLine)
        {
            var row = new TableRow();
            row.Cells.Add(CreateLabelCell(roleLabel, ApprovalSignatureRowHeight));
            row.Cells.Add(CreateContentCell(signatureLine, 3, ApprovalSignatureRowHeight, verticalTop: false));
            return row;
        }

        /// <summary>
        /// 交接签字：两行四列网格，标签/签字位/日期标签/日期纵向对齐。
        /// </summary>
        private static TableRow CreateHandoverSignatureRow(
            IReadOnlyList<ArchiveReturnApprovalSignatureLine> lines)
        {
            var row = new TableRow();
            row.Cells.Add(CreateLabelCell("交接签字", HandoverSignatureRowHeight));
            row.Cells.Add(CreateHandoverContentCell(lines));
            return row;
        }

        private static TableCell CreateHandoverContentCell(
            IReadOnlyList<ArchiveReturnApprovalSignatureLine> lines)
        {
            var root = new Grid { Height = HandoverSignatureRowHeight };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            IReadOnlyList<ArchiveReturnApprovalSignatureLine> effectiveLines = lines.Count > 0
                ? lines
                :
                [
                    new() { RoleLabel = "归还人签字：", SignerSlot = string.Empty, DateText = BlankDateSlot },
                    new() { RoleLabel = "资料室资料管理员签字：", SignerSlot = string.Empty, DateText = BlankDateSlot }
                ];

            for (int i = 0; i < effectiveLines.Count; i++)
            {
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            for (int i = 0; i < effectiveLines.Count; i++)
            {
                var line = effectiveLines[i];
                string partyLabel = string.IsNullOrWhiteSpace(line.RoleLabel)
                    ? string.Empty
                    : line.RoleLabel.Trim();
                string signerSlot = string.IsNullOrWhiteSpace(line.SignerSlot)
                    ? BlankSignerSlot
                    : line.SignerSlot.Trim();
                string dateText = string.IsNullOrWhiteSpace(line.DateText)
                    ? BlankDateSlot
                    : line.DateText.Trim();

                AddHandoverCellText(root, i, 0, partyLabel, TextAlignment.Left, margin: new Thickness(2, 0, 8, 0));
                AddHandoverCellText(root, i, 1, signerSlot, TextAlignment.Left, margin: new Thickness(0, 0, 12, 0));
                AddHandoverCellText(root, i, 2, "日期：", TextAlignment.Left, margin: new Thickness(0, 0, 4, 0));
                AddHandoverCellText(root, i, 3, dateText, TextAlignment.Left, margin: new Thickness(0, 0, 4, 0));
            }

            return new TableCell(new BlockUIContainer(root))
            {
                ColumnSpan = 3,
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.Black,
                Padding = new Thickness(CellPadding)
            };
        }

        private static void AddHandoverCellText(
            Grid root,
            int row,
            int column,
            string text,
            TextAlignment alignment,
            Thickness margin)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                TextAlignment = alignment,
                FontFamily = BodyFont,
                FontSize = BodyFontSize,
                Margin = margin
            };
            Grid.SetRow(textBlock, row);
            Grid.SetColumn(textBlock, column);
            root.Children.Add(textBlock);
        }

        private static string BuildItemText(ArchiveReturnReceiptPrintData data) =>
            data.ItemLines.Count > 0 ? string.Join("\n", data.ItemLines) : "(无)";

        private static Table CreateHeaderTable(string leftText, string rightText)
        {
            var headerTable = new Table { Margin = new Thickness(0, 0, 0, 6) };
            headerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            headerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

            var headerGroup = new TableRowGroup();
            var headerRow = new TableRow();
            headerRow.Cells.Add(CreatePlainCell(leftText, TextAlignment.Left));
            headerRow.Cells.Add(CreatePlainCell(rightText, TextAlignment.Right));
            headerGroup.Rows.Add(headerRow);
            headerTable.RowGroups.Add(headerGroup);

            return headerTable;
        }

        private static TableCell CreatePlainCell(string text, TextAlignment alignment) =>
            new(new Paragraph(new Run(text))
            {
                FontFamily = BodyFont,
                FontSize = BodyFontSize,
                TextAlignment = alignment,
                Margin = new Thickness(0)
            })
            {
                Padding = new Thickness(0)
            };

        private static Table CreateMainTable(TableRowGroup rowGroup)
        {
            var table = new Table
            {
                CellSpacing = 0,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(2, 2, 0, 0)
            };

            // 左侧标签列略加宽，保证「生产管理科负责人」等文字单行显示。
            table.Columns.Add(new TableColumn { Width = new GridLength(1.9, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(3.1, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1.9, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(3.1, GridUnitType.Star) });
            table.RowGroups.Add(rowGroup);

            return table;
        }

        private static Paragraph CreateFooterParagraph(ArchiveReturnReceiptPrintData data)
        {
            var footer = new Paragraph
            {
                FontSize = 10,
                Margin = new Thickness(0, 8, 0, 0),
                LineHeight = 16
            };

            footer.Inlines.Add(new Run("说明：") { FontWeight = FontWeights.Bold });
            footer.Inlines.Add(new Run("1、无论资料是否完好，均须打印本单并完成线下签字；扫描件由资料室资料管理员上传系统。\n"));
            footer.Inlines.Add(new Run("      2、正常完好归还仅需部门负责人签字；存在灭失时，需借出时全部审核审批人（部门负责人、资料室负责人、生产科负责人、生产副院长）签字。\n"));
            footer.Inlines.Add(new Run("      3、归还人与资料室资料管理员须在交接栏签字确认实物交接。\n"));
            footer.Inlines.Add(new Run($"      4、本单已累计打印 {data.PrintCount + 1} 次，最新打印请与系统记录核对。"));

            return footer;
        }

        private static TableRow CreateSingleRow(
            string label,
            string content,
            double? rowHeight = null,
            bool verticalTop = false)
        {
            double height = rowHeight ?? StandardRowHeight;
            var row = new TableRow();
            row.Cells.Add(CreateLabelCell(label, height));
            row.Cells.Add(CreateContentCell(content, 3, height, verticalTop));
            return row;
        }

        private static TableRow CreateDoubleRow(string label1, string content1, string label2, string content2)
        {
            var row = new TableRow();
            row.Cells.Add(CreateLabelCell(label1, StandardRowHeight));
            row.Cells.Add(CreateContentCell(content1, 1, StandardRowHeight, verticalTop: false));
            row.Cells.Add(CreateLabelCell(label2, StandardRowHeight));
            row.Cells.Add(CreateContentCell(content2, 1, StandardRowHeight, verticalTop: false));
            return row;
        }

        private static TableCell CreateLabelCell(string label, double rowHeight) =>
            new(CreateCellContent(label, rowHeight, label: true, verticalTop: false))
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.Black,
                Padding = new Thickness(CellPadding)
            };

        private static TableCell CreateContentCell(string content, int columnSpan, double rowHeight, bool verticalTop) =>
            new(CreateCellContent(content, rowHeight, label: false, verticalTop))
            {
                ColumnSpan = columnSpan,
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.Black,
                Padding = new Thickness(CellPadding)
            };

        private static Block CreateCellContent(string text, double rowHeight, bool label, bool verticalTop)
        {
            var grid = new Grid { Height = rowHeight };
            grid.Children.Add(new TextBlock
            {
                Text = text,
                TextWrapping = label ? TextWrapping.NoWrap : TextWrapping.Wrap,
                VerticalAlignment = verticalTop ? VerticalAlignment.Top : VerticalAlignment.Center,
                HorizontalAlignment = label ? HorizontalAlignment.Center : HorizontalAlignment.Left,
                TextAlignment = label ? TextAlignment.Center : TextAlignment.Left,
                FontFamily = label ? LabelFont : BodyFont,
                FontWeight = label ? FontWeights.Bold : FontWeights.Normal,
                FontSize = BodyFontSize
            });

            return new BlockUIContainer(grid);
        }

        private static string EmptyAsPlaceholder(string? value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
