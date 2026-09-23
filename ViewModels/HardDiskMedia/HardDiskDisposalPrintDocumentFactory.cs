using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘离库处置签批单打印文档工厂。
    /// </summary>
    internal static class HardDiskDisposalPrintDocumentFactory
    {
        private static readonly FontFamily TitleFont = new("SimHei");
        private static readonly FontFamily LabelFont = new("SimHei");
        private static readonly FontFamily BodyFont = new("SimSun");

        private const double TitleChromeHeight = 90;
        private const double HeaderHeight = 28;
        /// <summary>单行内容统一行高（原因/方式、说明、申请人、备注等）。</summary>
        private const double StandardRowHeight = PrintPageLayoutSupport.TableRowContentHeightOneLineDip;
        /// <summary>审核栏（职能部门，约 3 行）。</summary>
        private const double ReviewRowHeight = PrintPageLayoutSupport.TableRowContentHeightThreeLinesDip;
        /// <summary>审批栏（院级，约 2 行）。</summary>
        private const double ApproveRowHeight = PrintPageLayoutSupport.TableRowContentHeightTwoLinesDip;
        private const double RowChromeDip = PrintPageLayoutSupport.TableCellPaddingDip;
        private const string BlankDateSuffix = "日期：______年___月___日";

        /// <summary>申请说明/其他说明按正文估行后的上限。</summary>
        private const int ReasonMaxLines = 5;

        /// <summary>备注按正文估行后的上限。</summary>
        private const int RemarkMaxLines = 3;

        /// <summary>待处置清单按正文估行后的上限（撑满下限）。</summary>
        private const int DetailMaxLines = 20;

        internal static FlowDocument Create(HardDiskDisposalPrintData data)
        {
            ArgumentNullException.ThrowIfNull(data);

            string otherRemarkText = EmptyAsPlaceholder(data.OtherRemark);
            string reasonText = EmptyAsPlaceholder(data.Reason);
            string remarkText = EmptyAsPlaceholder(data.Remark);
            string itemListText = BuildItemList(data);
            string reviewSection = BuildReviewSection(data);
            string approveSection = BuildApproveSection(data);
            bool hasReview = !string.IsNullOrWhiteSpace(reviewSection);
            bool hasApprove = !string.IsNullOrWhiteSpace(approveSection);

            double otherRemarkRowHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(
                otherRemarkText,
                ReasonMaxLines);
            double reasonRowHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(
                reasonText,
                ReasonMaxLines);
            double remarkRowHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(
                remarkText,
                RemarkMaxLines);
            double itemRowHeight = CalculateItemRowHeight(
                data,
                otherRemarkRowHeight,
                reasonRowHeight,
                remarkRowHeight,
                hasReview,
                hasApprove,
                itemListText);

            var document = CreateDocumentSkeleton();

            document.Blocks.Add(new Paragraph(new Run(""))
            {
                Margin = new Thickness(0, 0, 0, 28)
            });
            document.Blocks.Add(new Paragraph(new Run("河北省第三测绘院资料室硬盘离库处置签批单"))
            {
                FontFamily = TitleFont,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 18)
            });

            document.Blocks.Add(CreateHeaderTable(
                $"处置单编号：{data.DisposalNo}",
                $"申请日期：{EmptyAsPlaceholder(data.ApplyDateText)}"));

            var rowGroup = new TableRowGroup();
            rowGroup.Rows.Add(CreateDoubleRow(
                "离库原因", EmptyAsPlaceholder(data.DisposalReason),
                "处置方式", EmptyAsPlaceholder(data.DispositionMethod),
                StandardRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("其他说明", otherRemarkText, otherRemarkRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("申请说明", reasonText, reasonRowHeight));
            rowGroup.Rows.Add(CreateSingleRow(
                "待处置硬盘清单",
                itemListText,
                itemRowHeight,
                verticalAlignTop: true));
            rowGroup.Rows.Add(CreateDoubleRow(
                "申请人", EmptyAsPlaceholder(data.ApplicantName),
                "申请部门", EmptyAsPlaceholder(data.ApplicantDept),
                StandardRowHeight));

            if (hasReview)
            {
                rowGroup.Rows.Add(CreateSingleRow("审核", reviewSection, ReviewRowHeight, verticalAlignTop: true));
            }

            if (hasApprove)
            {
                rowGroup.Rows.Add(CreateSingleRow("审批", approveSection, ApproveRowHeight, verticalAlignTop: true));
            }

            rowGroup.Rows.Add(CreateSingleRow("备注", remarkText, remarkRowHeight));

            document.Blocks.Add(CreateMainTable(rowGroup));
            document.Blocks.Add(CreateFooterParagraph(data));

            return document;
        }

        private static double CalculateItemRowHeight(
            HardDiskDisposalPrintData data,
            double otherRemarkRowHeight,
            double reasonRowHeight,
            double remarkRowHeight,
            bool hasReview,
            bool hasApprove,
            string itemListText)
        {
            // 固定行（不含清单撑满行）：原因方式/申请人(2) + 其他说明/申请说明/备注 + [审核]/[审批]。
            int oneLineRows = 2;
            double fixedContentHeight =
                StandardRowHeight * oneLineRows
                + otherRemarkRowHeight
                + reasonRowHeight
                + remarkRowHeight
                + (hasReview ? ReviewRowHeight : 0)
                + (hasApprove ? ApproveRowHeight : 0);
            int fixedRowCount = oneLineRows + 3 + (hasReview ? 1 : 0) + (hasApprove ? 1 : 0);
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

        private static string BuildItemList(HardDiskDisposalPrintData data)
        {
            if (data.Items.Count == 0)
            {
                return "（无）";
            }

            // 压缩为一行一条：编号 / 序列号 / 原状态 / 原位置 / 离库原因 / 处置方式 / 属性
            var builder = new StringBuilder();
            builder.Append($"共{data.Items.Count}块（编号 / 序列号 / 原状态 / 原位置 / 离库原因 / 处置方式 / 属性）");
            foreach (var item in data.Items)
            {
                builder.AppendLine();
                builder.Append(
                    $"{item.SortOrder}. {EmptyAsPlaceholder(item.DiskCode)}" +
                    $" / {EmptyAsPlaceholder(item.SerialNumber)}" +
                    $" / {EmptyAsPlaceholder(item.BeforeMediaStatus)}" +
                    $" / {EmptyAsPlaceholder(item.BeforeStorageLocation)}" +
                    $" / {EmptyAsPlaceholder(item.DisposalReason)}" +
                    $" / {EmptyAsPlaceholder(item.DispositionMethod)}" +
                    $" / {EmptyAsPlaceholder(item.BeforeMediaNature)}");
            }

            return builder.ToString();
        }

        private static string BuildReviewSection(HardDiskDisposalPrintData data)
        {
            var lines = new List<string>();
            AppendSignerLine(lines, data.EnableDeptHead, ApprovalWorkflowDomainValues.DisplayDeptHead,
                data.IsCompleted, data.DeptHead, data.CompletedDateText);
            AppendSignerLine(lines, data.EnableArchiveRoomHead, ApprovalWorkflowDomainValues.DisplayArchiveRoomHead,
                data.IsCompleted, data.ArchiveRoomHead, data.CompletedDateText);
            AppendSignerLine(lines, data.EnableProductionHead, ApprovalWorkflowDomainValues.DisplayProductionHead,
                data.IsCompleted, data.ProductionHead, data.CompletedDateText);
            return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
        }

        private static string BuildApproveSection(HardDiskDisposalPrintData data)
        {
            var lines = new List<string>();
            AppendSignerLine(lines, data.EnableArchiveDeputyPresident, ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident,
                data.IsCompleted, data.ArchiveDeputyPresident, data.CompletedDateText);
            AppendSignerLine(lines, data.EnableProductionVicePresident, ApprovalWorkflowDomainValues.DisplayProductionVicePresident,
                data.IsCompleted, data.ProductionVicePresident, data.CompletedDateText);
            return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
        }

        private static void AppendSignerLine(
            List<string> lines,
            bool enabled,
            string label,
            bool isCompleted,
            string? name,
            string? completedDateText)
        {
            if (!enabled)
            {
                return;
            }

            if (!isCompleted)
            {
                lines.Add(label + "：                              " + BlankDateSuffix);
                return;
            }

            string dateText = string.IsNullOrWhiteSpace(completedDateText)
                ? PrintApprovalSignatureSupport.BlankDateText
                : completedDateText.Trim();
            lines.Add(PrintApprovalSignatureSupport.FormatLabeledInline(label, name, dateText));
        }

        private static Paragraph CreateFooterParagraph(HardDiskDisposalPrintData data)
        {
            var footer = new Paragraph
            {
                FontSize = 10.5,
                Margin = new Thickness(0, 15, 0, 0),
                LineHeight = 18
            };

            footer.Inlines.Add(new Run("说明：") { FontWeight = FontWeights.Bold });
            footer.Inlines.Add(new Run(
                "1、本单由资料管理员发起，按“保存草稿、提交、打印签批单、线下签字、审批、上传签批单与硬盘照片、办结”流程办理。\n"));
            footer.Inlines.Add(new Run(
                "      2、请按启用的审核审批节点线下签字后回传系统；办结前须同时上传签批单与待处置硬盘照片。\n"));
            footer.Inlines.Add(new Run(
                $"      3、本签批单已累计打印 {data.PrintCount + 1} 次，最新打印请与系统记录核对。"));

            return footer;
        }

        /// <summary>与 <see cref="CreateFooterParagraph"/> 渲染文案一致，供表后说明估高。</summary>
        private static string BuildFooterNoteText(HardDiskDisposalPrintData data) =>
            "说明：" +
            "1、本单由资料管理员发起，按“保存草稿、提交、打印签批单、线下签字、审批、上传签批单与硬盘照片、办结”流程办理。\n" +
            "      2、请按启用的审核审批节点线下签字后回传系统；办结前须同时上传签批单与待处置硬盘照片。\n" +
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
