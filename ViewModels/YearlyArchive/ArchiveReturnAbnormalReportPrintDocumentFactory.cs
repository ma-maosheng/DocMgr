using System.Windows;

using System.Windows.Controls;

using System.Windows.Documents;

using System.Windows.Media;

using DocMgr.Models.YearlyArchive;



namespace DocMgr.ViewModels.YearlyArchive

{

    internal static class ArchiveReturnAbnormalReportPrintDocumentFactory

    {

        private static readonly FontFamily TitleFont = new("SimHei");

        private static readonly FontFamily LabelFont = new("SimHei");

        private static readonly FontFamily BodyFont = new("SimSun");



        private const double PageWidth = 793.6;

        private const double PageHeight = 1122.5;

        private const double PagePaddingHorizontal = 56;

        private const double PagePaddingTop = 36;

        private const double PagePaddingBottom = 32;

        private const double TitleTopSpacerHeight = 20;

        private const double StandardRowHeight = 32;

        private const double ItemDetailRowHeight = 96;

        private const double HandwritingRowHeight = 112;

        private const double ApprovalRowHeight = 32;

        private const double CellPadding = 4;

        private const double BodyFontSize = 12;



        internal static FlowDocument Create(ArchiveReturnAbnormalReportPrintData data)

        {

            ArgumentNullException.ThrowIfNull(data);



            var document = new FlowDocument

            {

                FontFamily = BodyFont,

                FontSize = BodyFontSize,

                LineHeight = 18,

                PageWidth = PageWidth,

                PageHeight = PageHeight,

                PagePadding = new Thickness(PagePaddingHorizontal, PagePaddingTop, PagePaddingHorizontal, PagePaddingBottom),

                ColumnWidth = double.PositiveInfinity

            };



            document.Blocks.Add(new Paragraph(new Run(" "))

            {

                FontSize = BodyFontSize,

                LineHeight = TitleTopSpacerHeight,

                Margin = new Thickness(0)

            });



            document.Blocks.Add(new Paragraph(new Run("河北省第三测绘院资料室年度资料灭失情况表"))

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

            rowGroup.Rows.Add(CreateSingleRow("源出库单号", data.SourceOutboundNo));

            rowGroup.Rows.Add(CreateSingleRow("资料摘要", EmptyAsPlaceholder(data.MaterialSummary)));

            rowGroup.Rows.Add(CreateSingleRow(

                "资料借出信息",

                BuildItemText(data.BorrowItemLines),

                ItemDetailRowHeight,

                verticalTop: true));

            rowGroup.Rows.Add(CreateSingleRow(

                "正常归还信息",

                BuildItemText(data.IntactReturnItemLines),

                ItemDetailRowHeight,

                verticalTop: true));

            rowGroup.Rows.Add(CreateSingleRow(

                "灭失资料明细",

                BuildItemText(data.LossItemLines),

                ItemDetailRowHeight,

                verticalTop: true));

            rowGroup.Rows.Add(CreateSingleRow("具体灭失情况", string.Empty, StandardRowHeight));

            rowGroup.Rows.Add(CreateSingleRow("(详情手写)", string.Empty, HandwritingRowHeight, verticalTop: true));

            rowGroup.Rows.Add(CreateSignatureRow(

                "资料借出人签字",

                BuildReturnerSignatureLine(data)));

            foreach (var approvalLine in data.OutboundApprovalLines)

            {

                rowGroup.Rows.Add(CreateSignatureRow(

                    approvalLine.RoleLabel,

                    BuildApprovalSignatureLine(approvalLine)));

            }



            document.Blocks.Add(CreateMainTable(rowGroup));

            document.Blocks.Add(CreateFooterParagraph());



            return document;

        }



        private static string BuildReturnerSignatureLine(ArchiveReturnAbnormalReportPrintData data)

        {

            string signerSlot = data.BlankReturnerSignature || string.IsNullOrWhiteSpace(data.BorrowerName)

                ? "________________"

                : data.BorrowerName.Trim();

            string dateText = string.IsNullOrWhiteSpace(data.ReturnerSignatureDateText)

                ? "______年___月___日"

                : data.ReturnerSignatureDateText.Trim();

            return $"签字：{signerSlot}    日期：{dateText}";

        }



        private static string BuildApprovalSignatureLine(ArchiveReturnApprovalSignatureLine line)

        {

            string signerSlot = string.IsNullOrWhiteSpace(line.SignerSlot)

                ? "________________"

                : line.SignerSlot.Trim();

            string dateText = string.IsNullOrWhiteSpace(line.DateText)

                ? "______年___月___日"

                : line.DateText.Trim();

            return $"签字：{signerSlot}    日期：{dateText}";

        }



        private static string BuildItemText(IReadOnlyList<string> lines) =>

            lines.Count > 0 ? string.Join("\n", lines) : "(无)";



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



        private static Paragraph CreateFooterParagraph()

        {

            var footer = new Paragraph

            {

                FontSize = 10,

                Margin = new Thickness(0, 8, 0, 0),

                LineHeight = 16

            };



            footer.Inlines.Add(new Run("说明：") { FontWeight = FontWeights.Bold });

            footer.Inlines.Add(new Run("1、资料灭失须由借出人与出库审核审批人手签确认。\n"));

            footer.Inlines.Add(new Run("      2、签字后的灭失情况表应上传系统留存，方可继续打印归还回执并办结入库。"));



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



        private static TableRow CreateSignatureRow(string label, string content)

        {

            var row = new TableRow();

            row.Cells.Add(CreateLabelCell(label, ApprovalRowHeight));

            row.Cells.Add(CreateContentCell(content, 3, ApprovalRowHeight, verticalTop: false));

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

