using System.Text;
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
    /// 年度资料盘库登记签批单打印文档工厂（模拟/电子共用）。
    /// </summary>
    internal static class ArchiveInventoryRegisterPrintDocumentFactory
    {
        private static readonly FontFamily TitleFont = new("SimHei");
        private static readonly FontFamily LabelFont = new("SimHei");
        private static readonly FontFamily BodyFont = new("SimSun");

        private const double TitleChromeHeight = 90;
        private const double HeaderHeight = 28;
        private const double StandardRowHeight = PrintPageLayoutSupport.TableRowContentHeightOneLineDip;
        private const double ReviewRowHeight = PrintPageLayoutSupport.TableRowContentHeightTwoLinesDip;
        private const double ApproveRowHeight = PrintPageLayoutSupport.TableRowContentHeightTwoLinesDip;
        private const double RowChromeDip = PrintPageLayoutSupport.TableCellPaddingDip;
        private const int ReasonMaxLines = 5;
        private const int RemarkMaxLines = 3;
        private const int DetailMaxLines = 20;

        internal static FlowDocument Create(ArchiveInventoryRegisterPrintData data)
        {
            ArgumentNullException.ThrowIfNull(data);

            string reasonText = EmptyAsPlaceholder(data.Reason);
            string remarkText = EmptyAsPlaceholder(data.Remark);
            string itemListText = BuildItemList(data);
            string reviewSection = BuildReviewSection(data);
            string approveSection = BuildApproveSection(data);
            bool hasReview = !string.IsNullOrWhiteSpace(reviewSection);
            bool hasApprove = !string.IsNullOrWhiteSpace(approveSection);

            double reasonRowHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(reasonText, ReasonMaxLines);
            double remarkRowHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(remarkText, RemarkMaxLines);
            double itemRowHeight = CalculateItemRowHeight(
                data, reasonRowHeight, remarkRowHeight, hasReview, hasApprove, itemListText);

            var document = CreateDocumentSkeleton();
            string rail = string.Equals(
                    data.MediaKind?.Trim(),
                    ArchiveInventoryRegisterDomainValues.MediaKindSimulated,
                    StringComparison.Ordinal)
                ? "模拟"
                : "电子";

            document.Blocks.Add(new Paragraph(new Run(""))
            {
                Margin = new Thickness(0, 0, 0, 28)
            });
            document.Blocks.Add(new Paragraph(new Run($"河北省第三测绘院资料室{rail}资料盘库登记签批单"))
            {
                FontFamily = TitleFont,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 18)
            });

            document.Blocks.Add(CreateHeaderTable(
                $"登记单编号：{data.RegisterNo}",
                $"申请日期：{EmptyAsPlaceholder(data.ApplyDateText)}"));

            var rowGroup = new TableRowGroup();
            rowGroup.Rows.Add(CreateDoubleRow(
                "登记类型", EmptyAsPlaceholder(data.RegisterKind),
                "申请人", EmptyAsPlaceholder(data.ApplicantName),
                StandardRowHeight));
            rowGroup.Rows.Add(CreateDoubleRow(
                "介质类别", EmptyAsPlaceholder(data.MediaKind),
                "申请部门", EmptyAsPlaceholder(data.ApplicantDept),
                StandardRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("登记说明", reasonText, reasonRowHeight));
            rowGroup.Rows.Add(CreateSingleRow(
                "登记明细",
                itemListText,
                itemRowHeight,
                verticalAlignTop: true));
            rowGroup.Rows.Add(CreateDoubleRow(
                "办结人", FormatCompletedBy(data),
                "办结日期", data.IsCompleted ? EmptyAsPlaceholder(data.CompletedDateText) : "—",
                StandardRowHeight));

            if (hasReview)
                rowGroup.Rows.Add(CreateSingleRow("审核", reviewSection, ReviewRowHeight, verticalAlignTop: true));
            if (hasApprove)
                rowGroup.Rows.Add(CreateSingleRow("审批", approveSection, ApproveRowHeight, verticalAlignTop: true));

            rowGroup.Rows.Add(CreateSingleRow("备注", remarkText, remarkRowHeight));

            document.Blocks.Add(CreateMainTable(rowGroup));
            document.Blocks.Add(CreateFooterParagraph(data));
            return document;
        }

        private static double CalculateItemRowHeight(
            ArchiveInventoryRegisterPrintData data,
            double reasonRowHeight,
            double remarkRowHeight,
            bool hasReview,
            bool hasApprove,
            string itemListText)
        {
            // 固定行：登记类型/介质类别/办结人(3) + 登记说明 + 备注 + [审核]/[审批]。
            int oneLineRows = 3;
            double fixedContentHeight =
                StandardRowHeight * oneLineRows
                + reasonRowHeight
                + remarkRowHeight
                + (hasReview ? ReviewRowHeight : 0)
                + (hasApprove ? ApproveRowHeight : 0);
            int fixedRowCount = oneLineRows + 2 + (hasReview ? 1 : 0) + (hasApprove ? 1 : 0);
            double fixedTableHeight = fixedContentHeight
                + PrintPageLayoutSupport.GetTableRowOuterHeightDip(0, RowChromeDip) * fixedRowCount
                + PrintPageLayoutSupport.EstimateTableBottomBorderHeightDip(fixedRowCount);

            string footerText = BuildFooterNoteText(data);
            double footerHeight = PrintPageLayoutSupport.EstimateNoteBlockHeightFromTextWithSafetyDip(
                footerText,
                PrintPageLayoutSupport.ContentWidthDip,
                fontSizeDip: 10.5,
                lineHeightDip: 18,
                topMarginDip: 15);
            double reservedHeight = TitleChromeHeight + HeaderHeight + footerHeight + fixedTableHeight;
            double contentNeededHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(
                itemListText,
                DetailMaxLines);
            return PrintPageLayoutSupport.CalculateStretchRowHeightDip(
                reservedHeight,
                contentNeededHeight,
                RowChromeDip);
        }

        private static FlowDocument CreateDocumentSkeleton()
        {
            var document = new FlowDocument
            {
                FontFamily = BodyFont,
                FontSize = 12,
                LineHeight = 20,
                ColumnWidth = double.PositiveInfinity
            };
            PrintPageLayoutSupport.ApplyA4MediumMargins(document);
            return document;
        }

        private static string BuildItemList(ArchiveInventoryRegisterPrintData data)
        {
            if (data.Items.Count == 0)
                return "（无）";

            bool isSimulated = string.Equals(
                data.MediaKind?.Trim(),
                ArchiveInventoryRegisterDomainValues.MediaKindSimulated,
                StringComparison.Ordinal);
            bool isScrap = ArchiveInventoryRegisterDomainValues.IsScrapRegisterKind(data.RegisterKind);

            var builder = new StringBuilder();
            builder.Append(isSimulated
                ? $"共{data.Items.Count}条（盒号 / 位置 / 资料 / 明细 / 丢失份数）"
                : $"共{data.Items.Count}条（介质 / 编号 / 位置 / 资料）");

            foreach (var item in data.Items)
            {
                builder.AppendLine();
                if (isSimulated)
                {
                    string lostText = isScrap ? "-" : item.LostCopyCount.ToString();
                    builder.Append(
                        $"{item.SortOrder}. {EmptyAsPlaceholder(item.ContainerCode)}" +
                        $" / {EmptyAsPlaceholder(item.BeforeStorageLocation)}" +
                        $" / {EmptyAsPlaceholder(item.MaterialName)}" +
                        $" / {EmptyAsPlaceholder(item.ItemName)}" +
                        $" / {lostText}");
                }
                else
                {
                    string mediumText = string.IsNullOrWhiteSpace(item.MediumCode)
                        ? EmptyAsPlaceholder(item.MediumKind)
                        : $"{EmptyAsPlaceholder(item.MediumKind)} {item.MediumCode.Trim()}";
                    builder.Append(
                        $"{item.SortOrder}. {mediumText}" +
                        $" / {EmptyAsPlaceholder(item.BeforeStorageLocation)}" +
                        $" / {EmptyAsPlaceholder(item.MaterialName)}");
                }
            }

            return builder.ToString();
        }

        private static string BuildReviewSection(ArchiveInventoryRegisterPrintData data)
        {
            var lines = new List<string>();
            AppendSignerLine(lines, data.EnableDeptHead, ApprovalWorkflowDomainValues.DisplayDeptHead,
                data.DeptHead, data.DeptHeadDateText);
            AppendSignerLine(lines, data.EnableArchiveRoomHead, ApprovalWorkflowDomainValues.DisplayArchiveRoomHead,
                data.ArchiveRoomHead, data.ArchiveRoomHeadDateText);
            AppendSignerLine(lines, data.EnableProductionHead, ApprovalWorkflowDomainValues.DisplayProductionHead,
                data.ProductionHead, data.ProductionHeadDateText);
            return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
        }

        private static string BuildApproveSection(ArchiveInventoryRegisterPrintData data)
        {
            var lines = new List<string>();
            AppendSignerLine(lines, data.EnableArchiveDeputyPresident, ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident,
                data.ArchiveDeputyPresident, data.ArchiveDeputyPresidentDateText);
            AppendSignerLine(lines, data.EnableProductionVicePresident, ApprovalWorkflowDomainValues.DisplayProductionVicePresident,
                data.ProductionVicePresident, data.ProductionVicePresidentDateText);
            return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
        }

        private static void AppendSignerLine(
            List<string> lines,
            bool enabled,
            string label,
            string? name,
            string? dateText)
        {
            if (!enabled)
            {
                return;
            }

            lines.Add(PrintApprovalSignatureSupport.FormatLabeledInline(label, name, dateText));
        }

        private static string FormatCompletedBy(ArchiveInventoryRegisterPrintData data) =>
            data.IsCompleted ? EmptyAsPlaceholder(data.CompletedBy) : "—";

        private static Paragraph CreateFooterParagraph(ArchiveInventoryRegisterPrintData data)
        {
            var footer = new Paragraph
            {
                FontSize = 10.5,
                Margin = new Thickness(0, 15, 0, 0),
                LineHeight = 18
            };
            footer.Inlines.Add(new Run("说明：") { FontWeight = FontWeights.Bold });
            footer.Inlines.Add(new Run(
                "1、本单由资料管理员发起，按“保存草稿、提交、打印签批单、线下签字、审批通过、确认可上传、上传签批单、办结”流程办理。\n"));
            footer.Inlines.Add(new Run(
                "      2、请按启用的审核审批节点线下签字后回传系统；未启用节点不在本单出现。办结后即时写入盘库台账，正式清账请后期走「离库处置」。\n"));
            footer.Inlines.Add(new Run(
                $"      3、本签批单已累计打印 {data.PrintCount + 1} 次，最新打印请与系统记录核对。"));
            return footer;
        }

        private static string BuildFooterNoteText(ArchiveInventoryRegisterPrintData data) =>
            "说明：" +
            "1、本单由资料管理员发起，按“保存草稿、提交、打印签批单、线下签字、审批通过、确认可上传、上传签批单、办结”流程办理。\n" +
            "      2、请按启用的审核审批节点线下签字后回传系统；未启用节点不在本单出现。办结后即时写入盘库台账，正式清账请后期走「离库处置」。\n" +
            $"      3、本签批单已累计打印 {data.PrintCount + 1} 次，最新打印请与系统记录核对。";

        private static Table CreateHeaderTable(string left, string right)
        {
            var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 8) };
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var group = new TableRowGroup();
            var row = new TableRow();
            row.Cells.Add(CreatePlainCell(left, TextAlignment.Left));
            row.Cells.Add(CreatePlainCell(right, TextAlignment.Right));
            group.Rows.Add(row);
            table.RowGroups.Add(group);
            return table;
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

        private static TableRow CreateSingleRow(
            string label,
            string value,
            double minHeight,
            bool verticalAlignTop = false)
        {
            var row = new TableRow();
            row.Cells.Add(CreateLabelCell(label));
            row.Cells.Add(CreateValueCell(value, columnSpan: 3, minHeight, verticalAlignTop));
            return row;
        }

        private static TableRow CreateDoubleRow(
            string leftLabel,
            string leftValue,
            string rightLabel,
            string rightValue,
            double minHeight)
        {
            var row = new TableRow();
            row.Cells.Add(CreateLabelCell(leftLabel));
            row.Cells.Add(CreateValueCell(leftValue, columnSpan: 1, minHeight));
            row.Cells.Add(CreateLabelCell(rightLabel));
            row.Cells.Add(CreateValueCell(rightValue, columnSpan: 1, minHeight));
            return row;
        }

        private static TableCell CreateLabelCell(string text)
        {
            return new TableCell(new Paragraph(new Run(text))
            {
                FontFamily = LabelFont,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                TextAlignment = TextAlignment.Center
            })
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.Black,
                Padding = new Thickness(2, RowChromeDip, 2, RowChromeDip)
            };
        }

        private static TableCell CreateValueCell(
            string text,
            int columnSpan,
            double minHeight,
            bool verticalAlignTop = false)
        {
            Block block;
            if (minHeight > 0)
            {
                var grid = new Grid { MinHeight = minHeight };
                grid.Children.Add(new TextBlock
                {
                    Text = text,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(4),
                    FontFamily = BodyFont,
                    FontSize = 12,
                    VerticalAlignment = verticalAlignTop
                        ? VerticalAlignment.Top
                        : VerticalAlignment.Center
                });
                block = new BlockUIContainer(grid);
            }
            else
            {
                block = new Paragraph(new Run(text))
                {
                    FontFamily = BodyFont,
                    FontSize = 12,
                    Margin = new Thickness(4)
                };
            }

            return new TableCell(block)
            {
                ColumnSpan = columnSpan,
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.Black
            };
        }

        private static TableCell CreatePlainCell(string text, TextAlignment align)
        {
            return new TableCell(new Paragraph(new Run(text))
            {
                FontFamily = BodyFont,
                FontSize = 12,
                Margin = new Thickness(0),
                TextAlignment = align
            })
            {
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            };
        }

        private static string EmptyAsPlaceholder(string? value)
            => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
    }
}
