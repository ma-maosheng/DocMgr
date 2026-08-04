using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.Cabinets;
using DocMgr.Models.Shared;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.ViewModels.Cabinets
{
    /// <summary>
    /// 开柜本次迁档汇总清单（供线下实物核对）。
    /// </summary>
    internal static class CabinetOpenRelocationSessionPrintDocumentFactory
    {
        private static readonly FontFamily TitleFont = new("SimHei");
        private static readonly FontFamily LabelFont = new("SimHei");
        private static readonly FontFamily BodyFont = new("SimSun");

        private const double TitleTopSpacerHeight = 20;
        private const double TitleBlockHeight = 48;
        private const double HeaderInfoHeight = 28;
        private const double StandardRowHeight = 32;
        private const double SignatureRowHeight = 40;
        private const double CellPadding = 4;
        private const double BodyFontSize = 12;

        public static FlowDocument Create(CabinetOpenRelocationSessionPrintData data)
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

            document.Blocks.Add(new Paragraph(new Run("资料室开柜迁档操作汇总清单"))
            {
                FontFamily = TitleFont,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });

            document.Blocks.Add(CreateHeaderTable(
                $"柜体：{EmptyAsPlaceholder(data.CabinetName)}（{EmptyAsPlaceholder(data.CabinetTypeText)}）",
                $"打印时间：{data.PrintedAt:yyyy-MM-dd HH:mm}"));

            double detailRowHeight = CalculateDetailRowHeight();
            var rowGroup = new TableRowGroup();
            rowGroup.Rows.Add(CreateDoubleRow(
                "打开面别",
                EmptyAsPlaceholder(data.FaceDisplayName),
                "操作人",
                EmptyAsPlaceholder(data.OperatorName)));
            rowGroup.Rows.Add(CreateDoubleRow(
                "迁档次数",
                data.Entries.Count.ToString(),
                "用途说明",
                "线下按清单完成实物迁档核对"));
            rowGroup.Rows.Add(CreateSingleRow(
                "迁档明细",
                BuildEntryList(data),
                detailRowHeight,
                verticalTop: true,
                compactBody: true));
            rowGroup.Rows.Add(CreateSingleRow(
                "操作人签字",
                "签字：________________　　日期：______年___月___日",
                SignatureRowHeight));
            rowGroup.Rows.Add(CreateSingleRow(
                "核对人签字",
                "签字：________________　　日期：______年___月___日",
                SignatureRowHeight));

            document.Blocks.Add(CreateMainTable(rowGroup));
            document.Blocks.Add(CreateFooterParagraph());

            return document;
        }

        private static double CalculateDetailRowHeight()
        {
            // 固定行：面别/操作人、次数/用途、两行签字。
            double fixedTableHeight =
                PrintPageLayoutSupport.GetTableRowOuterHeightDip(StandardRowHeight, CellPadding) * 2
                + PrintPageLayoutSupport.GetTableRowOuterHeightDip(SignatureRowHeight, CellPadding) * 2;
            double footerHeight = PrintPageLayoutSupport.EstimateNoteBlockHeightDip(
                lineCount: 3,
                lineHeightDip: 16,
                topMarginDip: 8);
            double reservedHeight =
                TitleTopSpacerHeight
                + TitleBlockHeight
                + HeaderInfoHeight
                + footerHeight
                + fixedTableHeight;
            return PrintPageLayoutSupport.CalculateStretchRowHeightDip(
                reservedHeight,
                minimumRowHeightDip: StandardRowHeight * 6,
                stretchRowCellPaddingDip: CellPadding);
        }

        private static string BuildEntryList(CabinetOpenRelocationSessionPrintData data)
        {
            if (data.Entries.Count == 0)
            {
                return "（本次开柜无迁档记录）";
            }

            var builder = new StringBuilder();
            builder.Append($"共 {data.Entries.Count} 次。按物理位置路线与编号线下核对后勾选。");
            foreach (var entry in data.Entries)
            {
                builder.AppendLine();
                string relocationNo = string.IsNullOrWhiteSpace(entry.RelocationNo)
                    ? "—"
                    : entry.RelocationNo.Trim();
                builder.Append(
                    $"{entry.Sequence}. {entry.OperatedAt:HH:mm}　" +
                    $"{EmptyAsPlaceholder(entry.MediaKind)}/{EmptyAsPlaceholder(entry.ModeLabel)}　" +
                    $"单号：{relocationNo}　□");
                builder.AppendLine();
                builder.Append(
                    $"　{EmptyAsPlaceholder(entry.SourceSlotText)} → {EmptyAsPlaceholder(entry.TargetSlotText)}");

                if (!string.IsNullOrWhiteSpace(entry.LocationRoutesText))
                {
                    builder.AppendLine();
                    builder.Append($"　路线：{entry.LocationRoutesText.Trim()}");
                }

                var codeParts = new List<string>();
                string containerLabel = ResolveContainerCodesLabel(entry.MediaKind);
                if (!string.IsNullOrWhiteSpace(containerLabel)
                    && !string.IsNullOrWhiteSpace(entry.ContainerCodesText))
                {
                    codeParts.Add($"{containerLabel}：{entry.ContainerCodesText.Trim()}");
                }

                if (!string.IsNullOrWhiteSpace(entry.HardDiskCodesText))
                {
                    codeParts.Add($"硬盘：{entry.HardDiskCodesText.Trim()}");
                }

                if (!string.IsNullOrWhiteSpace(entry.OpticalDiscCodesText))
                {
                    codeParts.Add($"光盘：{entry.OpticalDiscCodesText.Trim()}");
                }

                if (codeParts.Count > 0)
                {
                    builder.AppendLine();
                    builder.Append($"　{string.Join("　", codeParts)}");
                }
            }

            return builder.ToString();
        }

        private static string ResolveContainerCodesLabel(string mediaKind)
        {
            if (string.Equals(mediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal))
            {
                return "盒编号";
            }

            if (string.Equals(mediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
            {
                return "袋编号";
            }

            return string.Empty;
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
            footer.Inlines.Add(new Run(
                "1、请按「迁档明细」完成线下实物搬迁；2、核对方数、介质编号与档口用途；3、核对无误后由操作人、核对人签字确认。"));

            return footer;
        }

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

        private static TableRow CreateSingleRow(
            string label,
            string content,
            double rowHeight,
            bool verticalTop = false,
            bool compactBody = false)
        {
            var row = new TableRow();
            row.Cells.Add(CreateLabelCell(label, rowHeight));
            row.Cells.Add(CreateContentCell(content, columnSpan: 3, rowHeight, verticalTop, compactBody));
            return row;
        }

        private static TableRow CreateDoubleRow(
            string leftLabel,
            string leftContent,
            string rightLabel,
            string rightContent)
        {
            var row = new TableRow();
            row.Cells.Add(CreateLabelCell(leftLabel, StandardRowHeight));
            row.Cells.Add(CreateContentCell(leftContent, columnSpan: 1, StandardRowHeight, verticalTop: false));
            row.Cells.Add(CreateLabelCell(rightLabel, StandardRowHeight));
            row.Cells.Add(CreateContentCell(rightContent, columnSpan: 1, StandardRowHeight, verticalTop: false));
            return row;
        }

        private static TableCell CreateLabelCell(string label, double rowHeight) =>
            new(CreateCellContent(label, rowHeight, label: true, verticalTop: false))
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.Black,
                Padding = new Thickness(CellPadding)
            };

        private static TableCell CreateContentCell(
            string content,
            int columnSpan,
            double rowHeight,
            bool verticalTop,
            bool compactBody = false) =>
            new(CreateCellContent(content, rowHeight, label: false, verticalTop, compactBody))
            {
                ColumnSpan = columnSpan,
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.Black,
                Padding = new Thickness(CellPadding)
            };

        private static Block CreateCellContent(
            string text,
            double rowHeight,
            bool label,
            bool verticalTop,
            bool compactBody = false)
        {
            var grid = new Grid { MinHeight = rowHeight };
            if (!verticalTop)
            {
                grid.Height = rowHeight;
            }

            double fontSize = compactBody ? BodyFontSize - 1 : BodyFontSize;
            double lineHeight = compactBody ? 15 : BodyFontSize + 6;
            grid.Children.Add(new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = verticalTop ? VerticalAlignment.Top : VerticalAlignment.Center,
                HorizontalAlignment = label ? HorizontalAlignment.Center : HorizontalAlignment.Left,
                TextAlignment = label ? TextAlignment.Center : TextAlignment.Left,
                FontFamily = label ? LabelFont : BodyFont,
                FontWeight = label ? FontWeights.Bold : FontWeights.Normal,
                FontSize = fontSize,
                LineHeight = lineHeight,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight
            });

            return new BlockUIContainer(grid);
        }

        private static string EmptyAsPlaceholder(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
    }
}
