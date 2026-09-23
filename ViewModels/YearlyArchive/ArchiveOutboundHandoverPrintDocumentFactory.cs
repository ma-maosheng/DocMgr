using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.ViewModels.YearlyArchive
{
    internal static class ArchiveOutboundHandoverPrintDocumentFactory
    {
        private static readonly FontFamily TitleFont = new("SimHei");
        private static readonly FontFamily LabelFont = new("SimHei");
        private static readonly FontFamily BodyFont = new("SimSun");

        private const double StandardRowHeight = PrintPageLayoutSupport.TableRowContentHeightOneLineDip;
        private const double SignatureRowHeight = PrintPageLayoutSupport.TableRowContentHeightTwoLinesDip;
        private const double TitleBlockHeight = 48;
        private const double HeaderInfoHeight = 28;
        private const double CellPadding = PrintPageLayoutSupport.TableCellPaddingDip;
        private const double BodyFontSize = 12;

        /// <summary>备注按正文估行后的上限。</summary>
        private const int RemarkMaxLines = 3;

        /// <summary>具体资料明细按正文估行后的上限（撑满下限）。</summary>
        private const int DetailMaxLines = 20;

        internal static FlowDocument Create(ArchiveOutboundHandoverPrintData data)
        {
            ArgumentNullException.ThrowIfNull(data);

            string remarkText = EmptyAsPlaceholder(data.HandoverRemark);
            string itemDetailText = BuildItemText(data);
            double remarkRowHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(remarkText, RemarkMaxLines);
            double itemDetailRowHeight = CalculateItemDetailRowHeight(data, remarkRowHeight, itemDetailText);

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
                itemDetailText,
                itemDetailRowHeight,
                verticalTop: true));
            AppendApprovalSignatureRows(rowGroup, data);
            rowGroup.Rows.Add(CreateSingleRow("交接签字", data.HandoverSignatureBlock, SignatureRowHeight, verticalTop: true));
            rowGroup.Rows.Add(CreateSingleRow(
                "备注",
                remarkText,
                remarkRowHeight,
                verticalTop: true));

            document.Blocks.Add(CreateMainTable(rowGroup));
            document.Blocks.Add(CreateFooterParagraph(data));

            return document;
        }

        private static void AppendApprovalSignatureRows(TableRowGroup rowGroup, ArchiveOutboundHandoverPrintData data)
        {
            if (data.EnableDeptHead)
            {
                rowGroup.Rows.Add(CreateSingleRow(ApprovalWorkflowDomainValues.DisplayDeptHead, data.DeptHeadBlock));
            }

            if (data.EnableArchiveRoomHead)
            {
                rowGroup.Rows.Add(CreateSingleRow(
                    ApprovalWorkflowDomainValues.DisplayArchiveRoomHead,
                    data.ArchiveRoomHeadBlock));
            }

            if (data.EnableProductionHead)
            {
                rowGroup.Rows.Add(CreateSingleRow(
                    ApprovalWorkflowDomainValues.DisplayProductionHead,
                    data.ProductionHeadBlock));
            }

            if (data.EnableArchiveDeputyPresident)
            {
                rowGroup.Rows.Add(CreateSingleRow(
                    ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident,
                    data.ArchiveDeputyPresidentBlock));
            }

            if (data.EnableProductionVicePresident)
            {
                rowGroup.Rows.Add(CreateSingleRow(
                    ApprovalWorkflowDomainValues.DisplayProductionVicePresident,
                    data.ProductionVicePresidentBlock));
            }
        }

        private static int CountEnabledApprovalRows(ArchiveOutboundHandoverPrintData data)
        {
            int count = 0;
            if (data.EnableDeptHead)
            {
                count++;
            }

            if (data.EnableArchiveRoomHead)
            {
                count++;
            }

            if (data.EnableProductionHead)
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

        private static double CalculateItemDetailRowHeight(
            ArchiveOutboundHandoverPrintData data,
            double remarkRowHeight,
            string itemDetailText)
        {
            // 固定行：申请部门、资料摘要、审批(按启用)、交接签字、备注。
            int approvalCount = CountEnabledApprovalRows(data);
            double fixedContentHeight =
                StandardRowHeight * (2 + approvalCount)
                + SignatureRowHeight
                + remarkRowHeight;
            int fixedRowCount = 2 + approvalCount + 2; // 申请部门/摘要 + 签批 + 交接 + 备注
            double fixedTableHeight = fixedContentHeight
                + PrintPageLayoutSupport.GetTableRowOuterHeightDip(0, CellPadding) * fixedRowCount
                + PrintPageLayoutSupport.EstimateTableBottomBorderHeightDip(fixedRowCount);

            string footerText = BuildFooterNoteText(data);
            double footerHeight = PrintPageLayoutSupport.EstimateNoteBlockHeightFromTextWithSafetyDip(
                footerText,
                PrintPageLayoutSupport.ContentWidthDip,
                fontSizeDip: 10,
                lineHeightDip: 16,
                topMarginDip: 8);
            double reservedHeight = TitleBlockHeight + HeaderInfoHeight + footerHeight + fixedTableHeight;
            double contentNeededHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(
                itemDetailText,
                DetailMaxLines);
            return PrintPageLayoutSupport.CalculateStretchRowHeightDip(
                reservedHeight,
                contentNeededHeight,
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

        /// <summary>与 <see cref="CreateFooterParagraph"/> 渲染文案一致，供表后说明估高。</summary>
        private static string BuildFooterNoteText(ArchiveOutboundHandoverPrintData data) =>
            "说明：" +
            "1、实物交接完成后，领用人与资料室资料员须在交接单上签字确认。\n" +
            "      2、签字后的交接单及资料照片应上传系统，作为出库办结依据。\n" +
            $"      3、本交接单已累计打印 {data.PrintCount + 1} 次，最新打印请与系统记录核对。";

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
