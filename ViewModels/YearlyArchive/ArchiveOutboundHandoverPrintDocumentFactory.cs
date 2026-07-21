using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.Shared;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.ViewModels.YearlyArchive
{
    internal static class ArchiveOutboundHandoverPrintDocumentFactory
    {
        private static readonly FontFamily TitleFont = new("SimHei");
        private static readonly FontFamily LabelFont = new("SimHei");
        private static readonly FontFamily BodyFont = new("SimSun");

        private const double StandardRowHeight = 32;
        private const double SignatureRowHeight = 56;
        private const double RemarkRowHeight = 56;
        private const double TitleBlockHeight = 48;
        private const double HeaderInfoHeight = 28;
        private const double CellPadding = 4;
        private const double BodyFontSize = 12;

        internal static FlowDocument Create(ArchiveOutboundHandoverPrintData data)
        {
            ArgumentNullException.ThrowIfNull(data);

            double itemDetailRowHeight = CalculateItemDetailRowHeight();

            var document = new FlowDocument
            {
                FontFamily = BodyFont,
                FontSize = BodyFontSize,
                LineHeight = 18,
                ColumnWidth = double.PositiveInfinity
            };
            PrintPageLayoutSupport.ApplyA4MediumMargins(document);

            document.Blocks.Add(new Paragraph(new Run("河北省第三测绘院资料室年度资料出库交接单"))
            {
                FontFamily = TitleFont,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });

            document.Blocks.Add(CreateHeaderTable(
                $"申请单编号：{data.OutboundNo}",
                $"打印日期：{data.PrintDateText}"));

            var rowGroup = new TableRowGroup();
            rowGroup.Rows.Add(CreateDoubleRow("申请部门", data.ApplicantDept, "领用人", data.ApplicantName));
            rowGroup.Rows.Add(CreateSingleRow("资料摘要", EmptyAsPlaceholder(data.MaterialSummary)));
            rowGroup.Rows.Add(CreateSingleRow(
                "具体资料明细",
                BuildItemText(data),
                itemDetailRowHeight,
                verticalTop: true));
            rowGroup.Rows.Add(CreateSingleRow("交接签字", data.HandoverSignatureBlock, SignatureRowHeight, verticalTop: true));
            rowGroup.Rows.Add(CreateSingleRow(
                "备注",
                EmptyAsPlaceholder(data.HandoverRemark),
                RemarkRowHeight,
                verticalTop: true));

            document.Blocks.Add(CreateMainTable(rowGroup));
            document.Blocks.Add(CreateFooterParagraph(data));

            return document;
        }

        private static double CalculateItemDetailRowHeight()
        {
            // 固定行：申请部门、资料摘要、交接签字、备注（外高含内边距）。
            double fixedTableHeight =
                PrintPageLayoutSupport.GetTableRowOuterHeightDip(StandardRowHeight, CellPadding) * 2
                + PrintPageLayoutSupport.GetTableRowOuterHeightDip(SignatureRowHeight, CellPadding)
                + PrintPageLayoutSupport.GetTableRowOuterHeightDip(RemarkRowHeight, CellPadding);
            double footerHeight = PrintPageLayoutSupport.EstimateNoteBlockHeightDip(lineCount: 3, lineHeightDip: 16, topMarginDip: 8);
            double reservedHeight = TitleBlockHeight + HeaderInfoHeight + footerHeight + fixedTableHeight;
            return PrintPageLayoutSupport.CalculateStretchRowHeightDip(
                reservedHeight,
                StandardRowHeight * 4,
                CellPadding);
        }

        private static string BuildItemText(ArchiveOutboundHandoverPrintData data) =>
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

            table.Columns.Add(new TableColumn { Width = new GridLength(1.6, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(3.4, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1.6, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(3.4, GridUnitType.Star) });
            table.RowGroups.Add(rowGroup);

            return table;
        }

        private static Paragraph CreateFooterParagraph(ArchiveOutboundHandoverPrintData data)
        {
            var footer = new Paragraph
            {
                FontSize = 10,
                Margin = new Thickness(0, 8, 0, 0),
                LineHeight = 16
            };

            footer.Inlines.Add(new Run("说明：") { FontWeight = FontWeights.Bold });
            footer.Inlines.Add(new Run("1、实物交接完成后，领用人与资料室资料员须在交接单上签字确认。\n"));
            footer.Inlines.Add(new Run("      2、签字后的交接单及资料照片应上传系统，作为出库办结依据。\n"));
            footer.Inlines.Add(new Run($"      3、本交接单已累计打印 {data.PrintCount + 1} 次，最新打印请与系统记录核对。"));

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
                TextWrapping = TextWrapping.Wrap,
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
