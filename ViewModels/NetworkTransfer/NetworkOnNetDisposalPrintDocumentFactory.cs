using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;

namespace DocMgr.ViewModels.NetworkTransfer
{
    /// <summary>
    /// 在网数据处置签批单打印文档工厂（表格版，对齐硬盘离库处置签批单）。
    /// </summary>
    internal static class NetworkOnNetDisposalPrintDocumentFactory
    {
        private static readonly FontFamily TitleFont = new("SimHei");
        private static readonly FontFamily LabelFont = new("SimHei");
        private static readonly FontFamily BodyFont = new("SimSun");

        private const double TitleChromeHeight = 90;
        private const double HeaderHeight = 28;
        /// <summary>单行内容统一行高（原因/方式、说明、申请人、审核、审批、备注等）。</summary>
        private const double StandardRowHeight = PrintPageLayoutSupport.TableRowContentHeightOneLineDip;
        /// <summary>审核/审批（仅签字+日期，单行）。</summary>
        private const double ReviewRowHeight = PrintPageLayoutSupport.TableRowContentHeightOneLineDip;
        private const double RowChromeDip = PrintPageLayoutSupport.TableCellPaddingDip;
        private const string BlankDateSuffix = "日期:______年___月___日";

        /// <summary>申请说明按正文估行后的上限。</summary>
        private const int ReasonMaxLines = 5;

        /// <summary>备注按正文估行后的上限。</summary>
        private const int RemarkMaxLines = 3;

        /// <summary>待处置清单按正文估行后的上限（撑满下限）。</summary>
        private const int DetailMaxLines = 20;

        internal static FlowDocument Create(NetworkOnNetDisposalPrintData data)
        {
            ArgumentNullException.ThrowIfNull(data);

            string reasonText = EmptyAsPlaceholder(data.Reason);
            string remarkText = EmptyAsPlaceholder(data.Remark);
            string itemListText = BuildItemList(data);
            double reasonRowHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(reasonText, ReasonMaxLines);
            double remarkRowHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(remarkText, RemarkMaxLines);
            double itemRowHeight = CalculateItemRowHeight(data, reasonRowHeight, remarkRowHeight, itemListText);

            var document = CreateDocumentSkeleton();

            document.Blocks.Add(new Paragraph(new Run(""))
            {
                Margin = new Thickness(0, 0, 0, 28)
            });
            document.Blocks.Add(new Paragraph(new Run("河北省第三测绘院资料室在网数据处置签批单"))
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
                "处置原因", EmptyAsPlaceholder(data.DisposalReason),
                "处置方式", EmptyAsPlaceholder(data.DispositionMethod),
                StandardRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("申请说明", reasonText, reasonRowHeight));
            rowGroup.Rows.Add(CreateSingleRow(
                "待处置在网对象",
                itemListText,
                itemRowHeight,
                verticalAlignTop: true));
            rowGroup.Rows.Add(CreateDoubleRow(
                "申请人", EmptyAsPlaceholder(data.ApplicantName),
                "申请部门", EmptyAsPlaceholder(data.ApplicantDept),
                StandardRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("审核", BuildReviewSection(data), ReviewRowHeight, verticalAlignTop: true));
            rowGroup.Rows.Add(CreateSingleRow("审批", BuildApproveSection(data), ReviewRowHeight, verticalAlignTop: true));
            rowGroup.Rows.Add(CreateSingleRow("备注", remarkText, remarkRowHeight));

            document.Blocks.Add(CreateMainTable(rowGroup));
            document.Blocks.Add(CreateFooterParagraph(data));

            return document;
        }

        private static double CalculateItemRowHeight(
            NetworkOnNetDisposalPrintData data,
            double reasonRowHeight,
            double remarkRowHeight,
            string itemListText)
        {
            // 固定行：原因方式、申请人、审核、审批(4 单行结构) + 申请说明 + 备注。
            double fixedContentHeight =
                StandardRowHeight * 2
                + ReviewRowHeight * 2
                + reasonRowHeight
                + remarkRowHeight;
            const int fixedRowCount = 6;
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

        private static string BuildItemList(NetworkOnNetDisposalPrintData data)
        {
            if (data.Items.Count == 0)
            {
                return "（无）";
            }

            var builder = new StringBuilder();
            builder.Append($"共{data.Items.Count}条（编号 / 年度 / 项目 / 资料名称 / 类别 / 服务器路径 / 原状态 / 处置原因 / 处置方式）");
            foreach (var item in data.Items)
            {
                builder.AppendLine();
                builder.Append(
                    $"{item.SortOrder}. {EmptyAsPlaceholder(item.AssetNo)}" +
                    $" / {EmptyAsPlaceholder(item.Year)}" +
                    $" / {EmptyAsPlaceholder(item.ProjectName)}" +
                    $" / {EmptyAsPlaceholder(item.MaterialName)}" +
                    $" / {EmptyAsPlaceholder(item.AssetKind)}" +
                    $" / {EmptyAsPlaceholder(item.ServerPath)}" +
                    $" / {EmptyAsPlaceholder(item.BeforeLifecycleStatus)}" +
                    $" / {EmptyAsPlaceholder(item.DisposalReason)}" +
                    $" / {EmptyAsPlaceholder(item.DispositionMethod)}");
            }

            return builder.ToString();
        }

        private static string BuildReviewSection(NetworkOnNetDisposalPrintData data)
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

        private static string BuildApproveSection(NetworkOnNetDisposalPrintData data)
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

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(dateText))
            {
                lines.Add(label + "：                              " + BlankDateSuffix);
                return;
            }

            lines.Add(BuildSignerLine(label, name, dateText));
        }

        private static string BuildSignerLine(string label, string? name, string? dateText)
        {
            string signer = string.IsNullOrWhiteSpace(name) ? "____________________" : name.Trim();
            string date = string.IsNullOrWhiteSpace(dateText) ? "______年___月___日" : dateText.Trim();
            return $"{label}：{signer}    日期：{date}";
        }

        private static Paragraph CreateFooterParagraph(NetworkOnNetDisposalPrintData data)
        {
            var footer = new Paragraph
            {
                FontSize = 10.5,
                Margin = new Thickness(0, 15, 0, 0),
                LineHeight = 18
            };

            footer.Inlines.Add(new Run("说明：") { FontWeight = FontWeights.Bold });
            footer.Inlines.Add(new Run(
                "1、本单由资料管理员发起，按“保存草稿、提交、打印签批单、线下签字、审批通过、上传签批单、办结”流程办理。\n"));
            footer.Inlines.Add(new Run(
                "      2、审核为资料室签字、审批为分管资料院长签字，仅需签字与日期；请线下完成后回传签批单。\n"));
            footer.Inlines.Add(new Run(
                $"      3、本签批单已累计打印 {data.PrintCount + 1} 次，最新打印请与系统记录核对。"));

            return footer;
        }

        /// <summary>与 <see cref="CreateFooterParagraph"/> 渲染文案一致，供表后说明估高。</summary>
        private static string BuildFooterNoteText(NetworkOnNetDisposalPrintData data) =>
            "说明：" +
            "1、本单由资料管理员发起，按“保存草稿、提交、打印签批单、线下签字、审批通过、上传签批单、办结”流程办理。\n" +
            "      2、审核为资料室签字、审批为分管资料院长签字，仅需签字与日期；请线下完成后回传签批单。\n" +
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
