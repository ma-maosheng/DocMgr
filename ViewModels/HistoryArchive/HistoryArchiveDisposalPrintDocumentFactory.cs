using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.Shared;

namespace DocMgr.ViewModels.HistoryArchive
{
    /// <summary>
    /// 历史存档离库处置签批单打印文档工厂（表格版，对齐 NT-DSP / 硬盘离库）。
    /// </summary>
    internal static class HistoryArchiveDisposalPrintDocumentFactory
    {
        private static readonly FontFamily TitleFont = new("SimHei");
        private static readonly FontFamily LabelFont = new("SimHei");
        private static readonly FontFamily BodyFont = new("SimSun");

        private const double TitleChromeHeight = 90;
        private const double HeaderHeight = 28;
        private const double StandardRowHeight = 36;
        private const double ReviewRowHeight = 40;
        private const double RowChromeDip = 6;
        private const string BlankDateSuffix = "日期:______年___月___日";

        internal static FlowDocument Create(HistoryArchiveDisposalPrintData data)
        {
            ArgumentNullException.ThrowIfNull(data);

            double itemRowHeight = CalculateItemRowHeight();
            var document = CreateDocumentSkeleton();

            document.Blocks.Add(new Paragraph(new Run(""))
            {
                Margin = new Thickness(0, 0, 0, 28)
            });
            document.Blocks.Add(new Paragraph(new Run("河北省第三测绘院资料室历史存档资料离库处置签批单"))
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
                "资料类别", EmptyAsPlaceholder(data.MaterialKindDisplay),
                "处置方式", EmptyAsPlaceholder(data.DispositionMethod),
                StandardRowHeight));
            if (HistoryArchiveDisposalDomainValues.RequiresTransferTarget(data.DispositionMethod))
            {
                rowGroup.Rows.Add(CreateSingleRow("转交对象", EmptyAsPlaceholder(data.TransferTarget), StandardRowHeight));
            }
            else if (HistoryArchiveDisposalDomainValues.RequiresOtherRemark(data.DispositionMethod))
            {
                rowGroup.Rows.Add(CreateSingleRow("其他说明", EmptyAsPlaceholder(data.OtherRemark), StandardRowHeight));
            }

            rowGroup.Rows.Add(CreateSingleRow("申请说明", EmptyAsPlaceholder(data.Reason), StandardRowHeight));
            rowGroup.Rows.Add(CreateSingleRow(
                "待处置档案盒",
                BuildItemList(data),
                itemRowHeight,
                verticalAlignTop: true));
            rowGroup.Rows.Add(CreateDoubleRow(
                "申请人", EmptyAsPlaceholder(data.ApplicantName),
                "申请部门", EmptyAsPlaceholder(data.ApplicantDept),
                StandardRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("审核", BuildReviewSection(data), ReviewRowHeight, verticalAlignTop: true));
            rowGroup.Rows.Add(CreateSingleRow("审批", BuildApproveSection(data), ReviewRowHeight, verticalAlignTop: true));
            rowGroup.Rows.Add(CreateSingleRow("备注", EmptyAsPlaceholder(data.Remark), StandardRowHeight));

            document.Blocks.Add(CreateMainTable(rowGroup));
            document.Blocks.Add(CreateFooterParagraph(data));

            return document;
        }

        private static double CalculateItemRowHeight()
        {
            double fixedContentHeight =
                StandardRowHeight * 5
                + ReviewRowHeight * 2;
            const int fixedRowCount = 7;
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

        private static string BuildItemList(HistoryArchiveDisposalPrintData data)
        {
            if (data.Items.Count == 0)
            {
                return "（无）";
            }

            var builder = new StringBuilder();
            builder.Append($"共{data.Items.Count}盒（盒号 / 规格 / 原柜位 / 盒内摘要 / 方式）");
            foreach (var item in data.Items)
            {
                builder.AppendLine();
                string mixed = string.IsNullOrWhiteSpace(item.MixedPlacementText)
                    ? string.Empty
                    : $"[{item.MixedPlacementText}] ";
                builder.Append(
                    $"{item.SortOrder}. {mixed}{EmptyAsPlaceholder(item.BoxCode)}" +
                    $" / {EmptyAsPlaceholder(item.BoxSpecification)}" +
                    $" / {EmptyAsPlaceholder(item.StorageLocation)}" +
                    $" / {EmptyAsPlaceholder(item.ContentSummary)}" +
                    $" / {EmptyAsPlaceholder(item.DispositionMethod)}");
            }

            return builder.ToString();
        }

        private static string BuildReviewSection(HistoryArchiveDisposalPrintData data)
        {
            if (!data.IsCompleted)
            {
                return "资料室负责人签字：                              " + BlankDateSuffix;
            }

            return BuildSignerLine("资料室负责人签字", data.ArchiveRoomHead, data.CompletedDateText);
        }

        private static string BuildApproveSection(HistoryArchiveDisposalPrintData data)
        {
            if (!data.IsCompleted)
            {
                return "分管资料副院长签字：                            " + BlankDateSuffix;
            }

            return BuildSignerLine("分管资料副院长签字", data.ArchiveDeputyPresident, data.CompletedDateText);
        }

        private static string BuildSignerLine(string label, string? name, string? dateText)
        {
            string signer = string.IsNullOrWhiteSpace(name) ? "____________________" : name.Trim();
            string date = string.IsNullOrWhiteSpace(dateText) ? "______年___月___日" : dateText.Trim();
            return $"{label}：{signer}    日期：{date}";
        }

        private static Paragraph CreateFooterParagraph(HistoryArchiveDisposalPrintData data)
        {
            var footer = new Paragraph
            {
                FontSize = 10.5,
                Margin = new Thickness(0, 15, 0, 0),
                LineHeight = 18
            };

            footer.Inlines.Add(new Run("说明：") { FontWeight = FontWeights.Bold });
            footer.Inlines.Add(new Run(
                "1、本单由资料室资料管理员发起，按“保存草稿、提交、打印签批单、线下签字、审批通过、上传签批单、办结”流程办理。\n"));
            footer.Inlines.Add(new Run(
                "      2、审核为资料室负责人、审批为分管资料副院长，仅需签字与日期；请线下完成后回传签批单。\n"));
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
