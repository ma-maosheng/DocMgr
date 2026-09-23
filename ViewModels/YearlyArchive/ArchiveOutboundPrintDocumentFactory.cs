using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.ViewModels.YearlyArchive
{
    internal static class ArchiveOutboundPrintDocumentFactory
    {
        private static readonly FontFamily TitleFont = new("SimHei");
        private static readonly FontFamily LabelFont = new("SimHei");
        private static readonly FontFamily BodyFont = new("SimSun");

        private const double HeaderInfoHeight = 28;
        private const double StandardRowHeight = PrintPageLayoutSupport.TableRowContentHeightOneLineDip;
        private const double SignatureRowHeight = PrintPageLayoutSupport.TableRowContentHeightTwoLinesDip;
        private const double CellPadding = PrintPageLayoutSupport.TableCellPaddingDip;
        private const double BodyFontSize = 12;

        /// <summary>原由/重点提示按正文估行后的上限。</summary>
        private const int ReasonMaxLines = 5;

        /// <summary>证明材料/资料摘要按正文估行后的上限。</summary>
        private const int ShortTextMaxLines = 3;

        /// <summary>具体资料明细按正文估行后的上限（撑满下限）。</summary>
        private const int DetailMaxLines = 20;

        internal static FlowDocument Create(ArchiveOutboundPrintData data)
        {
            ArgumentNullException.ThrowIfNull(data);

            string reasonText = EmptyAsPlaceholder(data.Reason);
            string proofText = EmptyAsPlaceholder(data.ProofMaterialNote);
            string summaryText = EmptyAsPlaceholder(data.MaterialSummary);
            string noticeText = data.LongTermSimulatedStockDepletionNoticeText?.Trim() ?? string.Empty;
            bool hasNotice = !string.IsNullOrWhiteSpace(noticeText);
            string itemDetailText = BuildItemText(data);

            double reasonRowHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(reasonText, ReasonMaxLines);
            double proofRowHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(proofText, ShortTextMaxLines);
            double summaryRowHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(summaryText, ShortTextMaxLines);
            double noticeRowHeight = hasNotice
                ? PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(noticeText, ReasonMaxLines)
                : 0;
            double itemDetailRowHeight = CalculateItemDetailRowHeight(
                data,
                reasonRowHeight,
                proofRowHeight,
                summaryRowHeight,
                noticeRowHeight,
                hasNotice,
                itemDetailText);

            var document = CreateDocumentSkeleton();

            document.Blocks.Add(CreateTitleBlock());

            document.Blocks.Add(CreateHeaderTable(
                $"申请单编号：{data.OutboundNo}",
                $"申请日期：{data.ApplyDateText}"));

            var rowGroup = new TableRowGroup();
            rowGroup.Rows.Add(CreateDoubleRow("申请人", data.ApplicantName, "申请部门", data.ApplicantDept));
            rowGroup.Rows.Add(CreateSingleRow("原由", reasonText, reasonRowHeight, CellVerticalAlignment.ContentTop));
            rowGroup.Rows.Add(CreateSingleRow("去向", data.DestinationText));
            rowGroup.Rows.Add(CreateSingleRow("证明材料名称", proofText, proofRowHeight, CellVerticalAlignment.ContentTop));
            rowGroup.Rows.Add(CreateSingleRow("预计归还日期", data.ExpectedReturnDateText));
            rowGroup.Rows.Add(CreateSingleRow("涉密资料处置", data.ConfidentialMaterialDispositionText));
            if (hasNotice)
            {
                rowGroup.Rows.Add(CreateSingleRow(
                    "重点提示",
                    noticeText,
                    noticeRowHeight,
                    CellVerticalAlignment.ContentTop));
            }

            rowGroup.Rows.Add(CreateSingleRow("资料摘要", summaryText, summaryRowHeight, CellVerticalAlignment.ContentTop));
            rowGroup.Rows.Add(CreateSingleRow(
                "具体资料明细",
                itemDetailText,
                itemDetailRowHeight,
                CellVerticalAlignment.ContentTop));
            if (data.EnableDeptHead)
            {
                rowGroup.Rows.Add(CreateSingleRow(ApprovalWorkflowDomainValues.DisplayDeptHead, data.DeptHeadBlock));
            }

            if (data.EnableArchiveRoomHead)
            {
                rowGroup.Rows.Add(CreateSingleRow(ApprovalWorkflowDomainValues.DisplayArchiveRoomHead, data.ArchiveRoomHeadBlock));
            }

            if (data.EnableProductionHead)
            {
                rowGroup.Rows.Add(CreateSingleRow(ApprovalWorkflowDomainValues.DisplayProductionHead, data.ProductionHeadBlock));
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

            rowGroup.Rows.Add(CreateSingleRow(
                "交接签字",
                data.HandoverSignatureBlock,
                SignatureRowHeight,
                CellVerticalAlignment.ContentTop));

            document.Blocks.Add(CreateMainTable(rowGroup));
            document.Blocks.Add(CreateFooterParagraph(data));

            return document;
        }

        private static double CalculateItemDetailRowHeight(
            ArchiveOutboundPrintData data,
            double reasonRowHeight,
            double proofRowHeight,
            double summaryRowHeight,
            double noticeRowHeight,
            bool hasNotice,
            string itemDetailText)
        {
            int approvalCount = CountEnabledApprovalRows(data);
            // 固定行（不含具体资料明细撑满行）：
            // 申请人、去向、预计归还、涉密(4) + 原由/证明/摘要(+重点提示) + 签批(N) + 交接。
            int oneLineRows = 4 + approvalCount;
            double fixedContentHeight =
                StandardRowHeight * oneLineRows
                + reasonRowHeight
                + proofRowHeight
                + summaryRowHeight
                + (hasNotice ? noticeRowHeight : 0)
                + SignatureRowHeight;
            int variableTextRows = 3 + (hasNotice ? 1 : 0); // 原由、证明、摘要、[重点提示]
            int fixedRowCount = oneLineRows + variableTextRows + 1; // +交接
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
            double reservedHeight =
                PrintPageLayoutSupport.ApprovalFormTitleBlockHeightDip
                + HeaderInfoHeight
                + PrintPageLayoutSupport.ApprovalFormHeaderToTableGapDip
                + footerHeight
                + fixedTableHeight;
            double contentNeededHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(
                itemDetailText,
                DetailMaxLines);
            return PrintPageLayoutSupport.CalculateStretchRowHeightDip(
                reservedHeight,
                contentNeededHeight,
                CellPadding);
        }

        private static int CountEnabledApprovalRows(ArchiveOutboundPrintData data)
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

        private static Block CreateTitleBlock()
        {
            return new Paragraph(new Run("河北省第三测绘院资料室年度资料出库申请审批单"))
            {
                FontFamily = TitleFont,
                FontSize = PrintPageLayoutSupport.ApprovalFormTitleFontSize,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = PrintPageLayoutSupport.ApprovalFormTitleMargin
            };
        }

        private static FlowDocument CreateDocumentSkeleton()
        {
            var document = new FlowDocument
            {
                FontFamily = BodyFont,
                FontSize = BodyFontSize,
                LineHeight = PrintPageLayoutSupport.ApprovalFormLineHeightDip,
                ColumnWidth = double.PositiveInfinity
            };
            PrintPageLayoutSupport.ApplyA4MediumMargins(document);
            return document;
        }

        private static Table CreateHeaderTable(string leftText, string rightText)
        {
            var headerTable = new Table { Margin = PrintPageLayoutSupport.ApprovalFormHeaderTableMargin };
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

        private static TableCell CreatePlainCell(string text, TextAlignment alignment)
        {
            return new TableCell(new Paragraph(new Run(text))
            {
                FontFamily = BodyFont,
                FontSize = BodyFontSize,
                TextAlignment = alignment,
                Margin = new Thickness(0)
            })
            {
                Padding = new Thickness(0)
            };
        }

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

        private static Paragraph CreateFooterParagraph(ArchiveOutboundPrintData data)
        {
            var footer = new Paragraph
            {
                FontSize = 10,
                Margin = new Thickness(0, 8, 0, 0),
                LineHeight = 16
            };

            footer.Inlines.Add(new Run("备注：") { FontWeight = FontWeights.Bold });
            footer.Inlines.Add(new Run("1、申请提交后，按“线上申请、打印表单、线下审批签字、上传签字件、资料出库交接”的流程办理。\n"));
            footer.Inlines.Add(new Run("      2、签字后的审批单应回传系统，作为办理依据和归档附件。\n"));
            footer.Inlines.Add(new Run($"      3、本申请单已累计打印 {data.PrintCount + 1} 次，最新打印请与系统记录核对。"));

            return footer;
        }

        /// <summary>与 <see cref="CreateFooterParagraph"/> 渲染文案一致，供表后说明估高。</summary>
        private static string BuildFooterNoteText(ArchiveOutboundPrintData data) =>
            "备注：" +
            "1、申请提交后，按“线上申请、打印表单、线下审批签字、上传签字件、资料出库交接”的流程办理。\n" +
            "      2、签字后的审批单应回传系统，作为办理依据和归档附件。\n" +
            $"      3、本申请单已累计打印 {data.PrintCount + 1} 次，最新打印请与系统记录核对。";

        private static string BuildItemText(ArchiveOutboundPrintData data) =>
            data.ItemLines.Count > 0 ? string.Join("\n", data.ItemLines) : "(无)";

        private static TableRow CreateSingleRow(
            string label,
            string content,
            double? rowHeight = null,
            CellVerticalAlignment verticalAlignment = CellVerticalAlignment.SingleLineCenter)
        {
            double height = rowHeight ?? StandardRowHeight;
            var row = new TableRow();
            row.Cells.Add(CreateLabelCell(label, height, verticalAlignment));
            row.Cells.Add(CreateContentCell(content, 3, height, verticalAlignment));
            return row;
        }

        private static TableRow CreateDoubleRow(string label1, string content1, string label2, string content2)
        {
            var row = new TableRow();
            row.Cells.Add(CreateLabelCell(label1, StandardRowHeight, CellVerticalAlignment.SingleLineCenter));
            row.Cells.Add(CreateContentCell(content1, 1, StandardRowHeight, CellVerticalAlignment.SingleLineCenter));
            row.Cells.Add(CreateLabelCell(label2, StandardRowHeight, CellVerticalAlignment.SingleLineCenter));
            row.Cells.Add(CreateContentCell(content2, 1, StandardRowHeight, CellVerticalAlignment.SingleLineCenter));
            return row;
        }

        private static TableCell CreateLabelCell(string label, double rowHeight, CellVerticalAlignment verticalAlignment)
        {
            return new TableCell(CreateCellContent(label, rowHeight, verticalAlignment, label: true))
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.Black,
                Padding = new Thickness(CellPadding)
            };
        }

        private static TableCell CreateContentCell(
            string content,
            int columnSpan,
            double rowHeight,
            CellVerticalAlignment verticalAlignment)
        {
            return new TableCell(CreateCellContent(content, rowHeight, verticalAlignment, label: false))
            {
                ColumnSpan = columnSpan,
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.Black,
                Padding = new Thickness(CellPadding)
            };
        }

        private static Block CreateCellContent(
            string text,
            double rowHeight,
            CellVerticalAlignment verticalAlignment,
            bool label)
        {
            var grid = new Grid { Height = rowHeight };
            grid.Children.Add(new TextBlock
            {
                Text = text,
                TextWrapping = label ? TextWrapping.NoWrap : TextWrapping.Wrap,
                VerticalAlignment = ToVerticalAlignment(verticalAlignment),
                HorizontalAlignment = label ? HorizontalAlignment.Center : HorizontalAlignment.Left,
                TextAlignment = label ? TextAlignment.Center : TextAlignment.Left,
                FontFamily = label ? LabelFont : BodyFont,
                FontWeight = label ? FontWeights.Bold : FontWeights.Normal,
                FontSize = BodyFontSize
            });

            return new BlockUIContainer(grid);
        }

        private static VerticalAlignment ToVerticalAlignment(CellVerticalAlignment verticalAlignment) =>
            verticalAlignment == CellVerticalAlignment.ContentTop
                ? VerticalAlignment.Top
                : VerticalAlignment.Center;

        private static string EmptyAsPlaceholder(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "(无)" : value.Trim();

        private enum CellVerticalAlignment
        {
            SingleLineCenter,
            ContentTop
        }
    }
}
