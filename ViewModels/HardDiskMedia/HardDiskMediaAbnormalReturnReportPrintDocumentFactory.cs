using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;

namespace DocMgr.ViewModels.HardDiskMedia
{
    internal static class HardDiskMediaAbnormalReturnReportPrintDocumentFactory
    {
        private static readonly FontFamily TitleFont = new("SimHei");
        private static readonly FontFamily LabelFont = new("SimHei");
        private static readonly FontFamily BodyFont = new("SimSun");

        private const double TitleTopSpacerHeight = 20;
        private const double StandardRowHeight = PrintPageLayoutSupport.TableRowContentHeightOneLineDip;
        private const double HandwritingRowHeight = 112;
        private const double ApprovalRowHeight = PrintPageLayoutSupport.TableRowContentHeightOneLineDip;
        private const double TitleBlockHeight = 48;
        private const double HeaderInfoHeight = 28;
        private const double CellPadding = PrintPageLayoutSupport.TableCellPaddingDip;
        private const double BodyFontSize = 12;
        private const string BlankSignatureDateText = "______年___月___日";

        /// <summary>具体情况说明按正文估行后的上限（撑满下限）。</summary>
        private const int DetailMaxLines = 20;

        internal static FlowDocument Create(HardDiskMediaAbnormalReturnReportPrintData data)
        {
            ArgumentNullException.ThrowIfNull(data);

            var document = new FlowDocument
            {
                FontFamily = BodyFont,
                FontSize = BodyFontSize,
                LineHeight = 18,
                ColumnWidth = double.PositiveInfinity
            };
            PrintPageLayoutSupport.ApplyA4MediumMargins(document);

            document.Blocks.Add(new Paragraph(new Run(" "))
            {
                FontSize = BodyFontSize,
                LineHeight = TitleTopSpacerHeight,
                Margin = new Thickness(0)
            });

            document.Blocks.Add(new Paragraph(new Run("河北省第三测绘院资料室硬盘介质非正常归还情况表"))
            {
                FontFamily = TitleFont,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });

            document.Blocks.Add(CreateHeaderTable(
                $"登记单编号：{data.ApplicationNo}",
                $"归还日期：{data.ReturnDateText}"));

            string reasonText = EmptyAsPlaceholder(data.Reason);
            double detailRowHeight = CalculateDetailRowHeight(reasonText);

            var rowGroup = new TableRowGroup();
            rowGroup.Rows.Add(CreateDoubleRow("申请部门", data.ApplicantDept, "归还人", data.ApplicantName));
            rowGroup.Rows.Add(CreateDoubleRow("源借出单号", data.SourceApplicationNo, "登记类型", data.ApplicationType));
            rowGroup.Rows.Add(CreateDoubleRow("硬盘编号", data.DiskCode, "序列号", data.SerialNumber));
            rowGroup.Rows.Add(CreateSingleRow("借出位置", data.CurrentLocation));
            rowGroup.Rows.Add(CreateSingleRow("登记类型", data.InspectionResult));
            rowGroup.Rows.Add(CreateSingleRow("具体情况说明", reasonText, detailRowHeight, verticalTop: true));
            rowGroup.Rows.Add(CreateSingleRow("具体情况（手写补充）", string.Empty, HandwritingRowHeight, verticalTop: true));
            rowGroup.Rows.Add(CreateSignatureRow("归还人签字", BuildReturnerSignatureLine(data)));
            rowGroup.Rows.Add(CreateSignatureRow(
                ApprovalWorkflowDomainValues.DisplayDeptHead,
                BuildApprovalSignatureLine(data.ApplicantDeptHeadSignerSlot, data.ApplicantDeptHeadSignatureDateText)));
            rowGroup.Rows.Add(CreateSignatureRow(
                ApprovalWorkflowDomainValues.DisplayArchiveRoomHead,
                BuildApprovalSignatureLine(data.ArchiveRoomHeadSignerSlot, data.ArchiveRoomHeadSignatureDateText)));
            rowGroup.Rows.Add(CreateSignatureRow("资料室经办人签字", BuildBlankHandlerSignatureLine()));
            document.Blocks.Add(CreateMainTable(rowGroup));
            document.Blocks.Add(CreateFooterParagraph());

            return document;
        }

        private static double CalculateDetailRowHeight(string reasonText)
        {
            // 固定行：3 双列/单列信息行(5) + 手写补充 + 4 签字行；另计顶部留白。
            const int fixedRowCount = 5 + 1 + 4;
            double fixedContentHeight =
                StandardRowHeight * 5
                + HandwritingRowHeight
                + ApprovalRowHeight * 4;
            double fixedTableHeight = fixedContentHeight
                + PrintPageLayoutSupport.GetTableRowOuterHeightDip(0, CellPadding) * fixedRowCount
                + PrintPageLayoutSupport.EstimateTableBottomBorderHeightDip(fixedRowCount);

            string footerText = BuildFooterNoteText();
            double footerHeight = PrintPageLayoutSupport.EstimateNoteBlockHeightFromTextWithSafetyDip(
                footerText,
                PrintPageLayoutSupport.ContentWidthDip,
                fontSizeDip: 10,
                lineHeightDip: 16,
                topMarginDip: 8);
            double reservedHeight =
                TitleTopSpacerHeight
                + TitleBlockHeight
                + HeaderInfoHeight
                + footerHeight
                + fixedTableHeight;
            double contentNeededHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(
                reasonText,
                DetailMaxLines);
            return PrintPageLayoutSupport.CalculateStretchRowHeightDip(
                reservedHeight,
                contentNeededHeight,
                CellPadding);
        }

        private static string BuildReturnerSignatureLine(HardDiskMediaAbnormalReturnReportPrintData data)
        {
            string signerSlot = data.BlankReturnerSignature || string.IsNullOrWhiteSpace(data.ApplicantName)
                ? "________________"
                : data.ApplicantName.Trim();
            return $"签字：{signerSlot}    日期：______年___月___日";
        }

        private static string BuildApprovalSignatureLine(string? signerSlot, string? dateText)
        {
            string signer = string.IsNullOrWhiteSpace(signerSlot) ? "________________" : signerSlot.Trim();
            string date = string.IsNullOrWhiteSpace(dateText) ? BlankSignatureDateText : dateText.Trim();
            return $"签字：{signer}    日期：{date}";
        }

        private static string BuildBlankHandlerSignatureLine() =>
            BuildApprovalSignatureLine(null, null);

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

            PrintPageLayoutSupport.ApplyApprovalFormMainTableColumns(table);
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
            footer.Inlines.Add(new Run("1、硬盘介质非正常归还须由归还人、部门审核、资料室签字与资料室经办人手签确认。\n"));
            footer.Inlines.Add(new Run("      2、签字后的情况表应上传系统留存，方可登记归还信息并办结入库。"));
            return footer;
        }

        /// <summary>与 <see cref="CreateFooterParagraph"/> 渲染文案一致，供表后说明估高。</summary>
        private static string BuildFooterNoteText() =>
            "说明：" +
            "1、硬盘介质非正常归还须由归还人、部门审核、资料室签字与资料室经办人手签确认。\n" +
            "      2、签字后的情况表应上传系统留存，方可登记归还信息并办结入库。";

        private static TableRow CreateSingleRow(string label, string content, double? rowHeight = null, bool verticalTop = false)
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
