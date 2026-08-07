using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.Shared;
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
        private const double StandardRowHeight = 36;
        /// <summary>资料室系统审批（意见+签字，约 2 行）。</summary>
        private const double ApprovalRowHeight = 72;
        /// <summary>责任人签批：申请人 + 审核2人 + 审批2人（约 5 行）。</summary>
        private const double SignatureRowHeight = 150;
        private const double RowChromeDip = 6;
        private const string BlankDateSuffix = "日期:______年___月___日";

        internal static FlowDocument Create(YearlyArchiveDisposalPrintData data)
        {
            ArgumentNullException.ThrowIfNull(data);

            double itemRowHeight = CalculateItemRowHeight();
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
            rowGroup.Rows.Add(CreateSingleRow("申请说明", EmptyAsPlaceholder(data.Reason), StandardRowHeight));
            rowGroup.Rows.Add(CreateSingleRow(
                "待处置明细",
                BuildItemList(data),
                itemRowHeight,
                verticalAlignTop: true));
            rowGroup.Rows.Add(CreateDoubleRow(
                "申请人", EmptyAsPlaceholder(data.ApplicantName),
                "申请部门", EmptyAsPlaceholder(data.ApplicantDept),
                StandardRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("资料室审批", BuildApprovalSection(data), ApprovalRowHeight, verticalAlignTop: true));
            rowGroup.Rows.Add(CreateSingleRow("责任人签批", BuildSignatureSection(data), SignatureRowHeight, verticalAlignTop: true));
            rowGroup.Rows.Add(CreateSingleRow("备注", EmptyAsPlaceholder(data.Remark), StandardRowHeight));

            document.Blocks.Add(CreateMainTable(rowGroup));
            document.Blocks.Add(CreateFooterParagraph(data));

            return document;
        }

        private static double CalculateItemRowHeight()
        {
            // 固定行：原因方式、申请说明、申请人、审批、签批、备注。
            double fixedContentHeight =
                StandardRowHeight * 4
                + ApprovalRowHeight
                + SignatureRowHeight;
            const int fixedRowCount = 6;
            double fixedTableHeight = fixedContentHeight
                + PrintPageLayoutSupport.GetTableRowOuterHeightDip(0, RowChromeDip) * fixedRowCount;
            double footerHeight = PrintPageLayoutSupport.EstimateNoteBlockHeightDip(
                lineCount: 4,
                lineHeightDip: 18,
                topMarginDip: 15);
            double reservedHeight = TitleChromeHeight + HeaderHeight + footerHeight + fixedTableHeight;
            return PrintPageLayoutSupport.CalculateStretchRowHeightDip(
                reservedHeight,
                minimumRowHeightDip: 100,
                stretchRowCellPaddingDip: RowChromeDip);
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

            var builder = new StringBuilder();
            builder.Append($"共{data.Items.Count}条（名称 / 盘库类型 / 原因 / 方式 / 位置）");
            foreach (var item in data.Items.OrderBy(row => row.SortOrder))
            {
                builder.AppendLine();
                string line =
                    $"{item.SortOrder}. {EmptyAsPlaceholder(item.DisplayName)}" +
                    $" / {EmptyAsPlaceholder(item.SourceRegisterKind)}" +
                    $" / {EmptyAsPlaceholder(item.DisposalReason)}" +
                    $" / {EmptyAsPlaceholder(item.DispositionMethod)}" +
                    $" / {EmptyAsPlaceholder(item.BeforeStorageLocation)}";
                if (!string.IsNullOrWhiteSpace(item.TargetBlankSlotLocation))
                {
                    line += $" / 低格档口：{item.TargetBlankSlotLocation.Trim()}";
                }

                builder.Append(line);
            }

            return builder.ToString();
        }

        private static string BuildApprovalSection(YearlyArchiveDisposalPrintData data)
        {
            // 系统内审批意见：已审批及之后阶段可预填（非交接双方签字）。
            bool hasApproval = !string.IsNullOrWhiteSpace(data.ApprovedBy)
                || !string.IsNullOrWhiteSpace(data.ApprovalOpinion)
                || !string.IsNullOrWhiteSpace(data.ApprovedDateText);

            if (!hasApproval)
            {
                return "审批意见：\n                    签字：                              " + BlankDateSuffix;
            }

            string opinion = string.IsNullOrWhiteSpace(data.ApprovalOpinion)
                ? string.Empty
                : data.ApprovalOpinion.Trim();
            string signature = BuildFilledSignatureLine(data.ApprovedBy, data.ApprovedDateText);
            return string.IsNullOrWhiteSpace(opinion)
                ? $"审批意见：\n{signature}"
                : $"审批意见：{opinion}\n{signature}";
        }

        private static string BuildSignatureSection(YearlyArchiveDisposalPrintData data)
        {
            // 办结前供线下亲笔签名：签字栏与日期栏留白。
            if (!data.IsCompleted)
            {
                return "申请人签字：                                              " + BlankDateSuffix + "\n"
                     + "审核人（资料室负责人）签字：                              " + BlankDateSuffix + "\n"
                     + "审核人（生产科负责人）签字：                              " + BlankDateSuffix + "\n"
                     + "审批人（分管生产副院长）签字：                            " + BlankDateSuffix + "\n"
                     + "审批人（分管资料室副院长）签字：                          " + BlankDateSuffix;
            }

            // 已办结重打：预填申请人；审核/审批为线下签批，系统无独立字段，日期与办结日一致。
            string dateText = string.IsNullOrWhiteSpace(data.CompletedDateText)
                ? "______年___月___日"
                : data.CompletedDateText.Trim();
            string applicant = string.IsNullOrWhiteSpace(data.ApplicantName)
                ? "________________"
                : data.ApplicantName.Trim();

            return $"申请人签字：{applicant}    日期：{dateText}\n"
                 + $"审核人（资料室负责人）签字：____________________    日期：{dateText}\n"
                 + $"审核人（生产科负责人）签字：____________________    日期：{dateText}\n"
                 + $"审批人（分管生产副院长）签字：____________________    日期：{dateText}\n"
                 + $"审批人（分管资料室副院长）签字：____________________    日期：{dateText}";
        }

        private static string BuildFilledSignatureLine(string? name, string? dateText)
        {
            string normalizedName = name?.Trim() ?? string.Empty;
            string normalizedDate = dateText?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedName) && string.IsNullOrWhiteSpace(normalizedDate))
            {
                return "签字：                              " + BlankDateSuffix;
            }

            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return $"签字：    日期：{normalizedDate}";
            }

            if (string.IsNullOrWhiteSpace(normalizedDate))
            {
                return $"签字：{normalizedName}";
            }

            return $"签字：{normalizedName}    日期：{normalizedDate}";
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
                "1、本单由资料室资料管理员发起，按“保存草稿、提交、打印签批单、线下签字、审批、上传签批单、办结”流程办理。\n"));
            footer.Inlines.Add(new Run(
                "      2、请线下完成申请人、审核人（资料室负责人、生产科负责人）、审批人（分管生产副院长、分管资料室副院长）签字后回传系统；含离库销毁时须同步上传处置现场照片。\n"));
            footer.Inlines.Add(new Run(
                $"      3、本签批单已累计打印 {data.PrintCount + 1} 次，最新打印请与系统记录核对。"));

            return footer;
        }

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
            table.Columns.Add(new TableColumn { Width = new GridLength(1.6, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(3.4, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1.6, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(3.4, GridUnitType.Star) });
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
                Padding = new Thickness(2, 6, 2, 2)
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
