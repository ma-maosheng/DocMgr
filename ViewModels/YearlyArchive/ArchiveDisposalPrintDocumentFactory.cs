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
    /// 资料离库处置签批单打印文档工厂（表格版，对齐硬盘离库处置签批单）。
    /// </summary>
    internal static class ArchiveDisposalPrintDocumentFactory
    {
        private static readonly FontFamily TitleFont = new("SimHei");
        private static readonly FontFamily LabelFont = new("SimHei");
        private static readonly FontFamily BodyFont = new("SimSun");

        private const double TitleChromeHeight = 90;
        private const double HeaderHeight = 28;
        private const double StandardRowHeight = PrintPageLayoutSupport.TableRowContentHeightOneLineDip;
        /// <summary>审核：资料室负责人、生产科负责人（签字+日期，最多 2 行）。</summary>
        private const double ReviewRowHeight = PrintPageLayoutSupport.TableRowContentHeightTwoLinesDip;
        /// <summary>审批：分管资料室副院长、分管生产副院长（签字+日期，最多 2 行）。</summary>
        private const double ApproveRowHeight = PrintPageLayoutSupport.TableRowContentHeightTwoLinesDip;
        private const double RowChromeDip = PrintPageLayoutSupport.TableCellPaddingDip;
        private const string BlankDateSuffix = "日期:______年___月___日";

        /// <summary>申请说明按正文估行后的上限。</summary>
        private const int ReasonMaxLines = 5;

        /// <summary>备注按正文估行后的上限。</summary>
        private const int RemarkMaxLines = 3;

        /// <summary>待处置明细按正文估行后的上限（撑满下限）。</summary>
        private const int DetailMaxLines = 20;

        internal static FlowDocument Create(YearlyArchiveDisposalPrintData data)
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
                data,
                reasonRowHeight,
                remarkRowHeight,
                hasReview,
                hasApprove,
                itemListText);

            var document = CreateDocumentSkeleton();

            string rail = string.Equals(
                    data.MediaKind?.Trim(),
                    ArchiveRegisterDomainValues.MediaKindSimulated,
                    StringComparison.Ordinal)
                ? "模拟"
                : "电子";

            document.Blocks.Add(new Paragraph(new Run(""))
            {
                Margin = new Thickness(0, 0, 0, 28)
            });
            document.Blocks.Add(new Paragraph(new Run($"河北省第三测绘院资料室{rail}资料离库处置签批单"))
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
            rowGroup.Rows.Add(CreateSingleRow("申请说明", reasonText, reasonRowHeight));
            rowGroup.Rows.Add(CreateSingleRow(
                "待处置明细",
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
            YearlyArchiveDisposalPrintData data,
            double reasonRowHeight,
            double remarkRowHeight,
            bool hasReview,
            bool hasApprove,
            string itemListText)
        {
            // 固定行：原因方式、申请人(2) + 申请说明 + 备注 + [审核]/[审批]。
            int oneLineRows = 2;
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

        private static string BuildItemList(YearlyArchiveDisposalPrintData data)
        {
            if (data.Items.Count == 0)
            {
                return "（无）";
            }

            bool isSimulated = string.Equals(
                data.MediaKind?.Trim(),
                ArchiveRegisterDomainValues.MediaKindSimulated,
                StringComparison.Ordinal);

            var builder = new StringBuilder();
            builder.Append(isSimulated
                ? $"共{data.Items.Count}条（年度｜项目｜盒号｜位置｜资料明细｜盘库类型｜处置方式）"
                : $"共{data.Items.Count}条（年度｜项目｜袋号｜介质｜位置｜资料明细｜盘库类型｜处置方式）");

            foreach (var item in data.Items.OrderBy(row => row.SortOrder))
            {
                builder.AppendLine();
                if (isSimulated)
                {
                    builder.Append(
                        $"{item.SortOrder}. 年度：{EmptyAsPlaceholder(item.ProjectYear)}" +
                        $"｜项目：{EmptyAsPlaceholder(item.ProjectName)}" +
                        $"｜盒号：{EmptyAsPlaceholder(FirstNonEmpty(item.BoxCode, item.ContainerCode))}" +
                        $"｜位置：{EmptyAsPlaceholder(item.BeforeStorageLocation)}" +
                        $"｜资料：{EmptyAsPlaceholder(item.MaterialDetail)}" +
                        $"｜盘库：{EmptyAsPlaceholder(item.SourceRegisterKind)}" +
                        $"｜方式：{EmptyAsPlaceholder(item.DispositionMethod)}");
                }
                else
                {
                    string mediumText = string.IsNullOrWhiteSpace(item.MediumCode)
                        ? EmptyAsPlaceholder(item.MediumKind)
                        : $"{EmptyAsPlaceholder(item.MediumKind)} {item.MediumCode.Trim()}";
                    string line =
                        $"{item.SortOrder}. 年度：{EmptyAsPlaceholder(item.ProjectYear)}" +
                        $"｜项目：{EmptyAsPlaceholder(item.ProjectName)}" +
                        $"｜袋号：{EmptyAsPlaceholder(FirstNonEmpty(item.BagCode, item.ContainerCode))}" +
                        $"｜介质：{mediumText}" +
                        $"｜位置：{EmptyAsPlaceholder(item.BeforeStorageLocation)}" +
                        $"｜资料：{EmptyAsPlaceholder(item.MaterialDetail)}" +
                        $"｜盘库：{EmptyAsPlaceholder(item.SourceRegisterKind)}" +
                        $"｜方式：{EmptyAsPlaceholder(item.DispositionMethod)}";
                    if (!string.IsNullOrWhiteSpace(item.TargetBlankSlotLocation))
                    {
                        line += $"｜低格档口：{item.TargetBlankSlotLocation.Trim()}";
                    }

                    builder.Append(line);
                }
            }

            return builder.ToString();
        }

        /// <summary>审核栏：按配置启用的职能部门节点（一级一行，未启用整栏隐藏）。</summary>
        private static string BuildReviewSection(YearlyArchiveDisposalPrintData data)
        {
            var lines = new List<string>();
            if (data.EnableDeptHead)
            {
                lines.Add(data.IsCompleted
                    ? BuildSignerLine(ApprovalWorkflowDomainValues.DisplayDeptHead, data.DeptHead, ResolveCompletedDateText(data))
                    : ApprovalWorkflowDomainValues.DisplayDeptHead + "：                              " + BlankDateSuffix);
            }

            if (data.EnableArchiveRoomHead)
            {
                lines.Add(data.IsCompleted
                    ? BuildSignerLine(ApprovalWorkflowDomainValues.DisplayArchiveRoomHead, data.ArchiveRoomHead, ResolveCompletedDateText(data))
                    : ApprovalWorkflowDomainValues.DisplayArchiveRoomHead + "：                              " + BlankDateSuffix);
            }

            if (data.EnableProductionHead)
            {
                lines.Add(data.IsCompleted
                    ? BuildSignerLine(ApprovalWorkflowDomainValues.DisplayProductionHead, data.ProductionHead, ResolveCompletedDateText(data))
                    : ApprovalWorkflowDomainValues.DisplayProductionHead + "：                              " + BlankDateSuffix);
            }

            return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
        }

        /// <summary>审批栏：按配置启用的院级节点（一级一行，未启用整栏隐藏）。</summary>
        private static string BuildApproveSection(YearlyArchiveDisposalPrintData data)
        {
            var lines = new List<string>();
            if (data.EnableArchiveDeputyPresident)
            {
                lines.Add(data.IsCompleted
                    ? BuildSignerLine(ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident, data.ArchiveDeputyPresident, ResolveCompletedDateText(data))
                    : ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident + "：                          " + BlankDateSuffix);
            }

            if (data.EnableProductionVicePresident)
            {
                lines.Add(data.IsCompleted
                    ? BuildSignerLine(ApprovalWorkflowDomainValues.DisplayProductionVicePresident, data.ProductionVicePresident, ResolveCompletedDateText(data))
                    : ApprovalWorkflowDomainValues.DisplayProductionVicePresident + "：                            " + BlankDateSuffix);
            }

            return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
        }

        private static string BuildSignerLine(string label, string? name, string dateText)
        {
            string signer = string.IsNullOrWhiteSpace(name) ? "____________________" : name.Trim();
            return $"{label}：{signer}    日期：{dateText}";
        }

        private static string ResolveCompletedDateText(YearlyArchiveDisposalPrintData data)
        {
            return string.IsNullOrWhiteSpace(data.CompletedDateText)
                ? "______年___月___日"
                : data.CompletedDateText.Trim();
        }

        private static Paragraph CreateFooterParagraph(YearlyArchiveDisposalPrintData data)
        {
            var footer = new Paragraph
            {
                FontSize = 10.5,
                Margin = new Thickness(0, 15, 0, 0),
                LineHeight = 18
            };

            footer.Inlines.Add(new Run("说明：") { FontWeight = FontWeights.Bold });
            footer.Inlines.Add(new Run(
                "1、本单由资料管理员发起，按“保存草稿、提交、打印签批单、线下审核审批、系统审批、上传签批单、办结”流程办理。\n"));
            footer.Inlines.Add(new Run(
                "      2、请线下完成审核（资料室签字、生产科签字）与审批（分管资料院长签字、分管生产院长签字）签字后回传系统；含离库销毁时须同步上传处置资料照片。\n"));
            footer.Inlines.Add(new Run(
                $"      3、本签批单已累计打印 {data.PrintCount + 1} 次，最新打印请与系统记录核对。"));

            return footer;
        }

        /// <summary>与 <see cref="CreateFooterParagraph"/> 渲染文案一致，供表后说明估高。</summary>
        private static string BuildFooterNoteText(YearlyArchiveDisposalPrintData data) =>
            "说明：" +
            "1、本单由资料管理员发起，按“保存草稿、提交、打印签批单、线下审核审批、系统审批、上传签批单、办结”流程办理。\n" +
            "      2、请线下完成审核（资料室签字、生产科签字）与审批（分管资料院长签字、分管生产院长签字）签字后回传系统；含离库销毁时须同步上传处置资料照片。\n" +
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

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static string EmptyAsPlaceholder(string? value)
            => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
    }
}
