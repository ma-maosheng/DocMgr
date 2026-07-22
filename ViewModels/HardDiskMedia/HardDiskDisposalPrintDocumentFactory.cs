using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;

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

        internal static FlowDocument Create(HardDiskDisposalPrintData data)
        {
            ArgumentNullException.ThrowIfNull(data);

            var document = new FlowDocument
            {
                FontFamily = BodyFont,
                FontSize = 12,
                PagePadding = new Thickness(0),
                ColumnWidth = double.PositiveInfinity
            };
            PrintPageLayoutSupport.ApplyA4MediumMargins(document);

            document.Blocks.Add(new Paragraph(new Run("硬盘离库处置签批单"))
            {
                FontFamily = TitleFont,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            });

            document.Blocks.Add(CreateHeaderTable(
                $"处置单编号：{data.DisposalNo}",
                $"申请日期：{EmptyAsPlaceholder(data.ApplyDateText)}"));

            var rowGroup = new TableRowGroup();
            rowGroup.Rows.Add(CreateDoubleRow("离库原因", data.DisposalReason, "处置方式", data.DispositionMethod));
            rowGroup.Rows.Add(CreateSingleRow("其他说明", EmptyAsPlaceholder(data.OtherRemark), 48));
            rowGroup.Rows.Add(CreateSingleRow("申请说明", EmptyAsPlaceholder(data.Reason), 72));
            rowGroup.Rows.Add(CreateSingleRow("待处置硬盘清单", BuildItemList(data), CalculateItemRowHeight(data.Items.Count)));
            rowGroup.Rows.Add(CreateDoubleRow("申请人", EmptyAsPlaceholder(data.ApplicantName), "申请部门", EmptyAsPlaceholder(data.ApplicantDept)));
            rowGroup.Rows.Add(CreateSingleRow("资料室审批", BuildApprovalSection(data), 56));
            rowGroup.Rows.Add(CreateSingleRow("签批签字", BuildSignatureSection(data), 96));
            rowGroup.Rows.Add(CreateSingleRow("备注", EmptyAsPlaceholder(data.Remark), 48));

            document.Blocks.Add(CreateMainTable(rowGroup));
            document.Blocks.Add(new Paragraph(new Run(
                "说明：1. 本单由资料室资料管理员发起；2. 请线下完成申请人、资料室负责人、资料室分管领导签字后上传系统；3. 办结前须同时上传签批单与待处置硬盘照片。"))
            {
                FontSize = 11,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 12, 0, 0),
                TextAlignment = TextAlignment.Left
            });

            return document;
        }

        private static string BuildItemList(HardDiskDisposalPrintData data)
        {
            if (data.Items.Count == 0)
            {
                return "（无）";
            }

            return string.Join("\n", data.Items.Select(item =>
                $"{item.SortOrder}. {item.DiskCode}　序列号：{EmptyAsPlaceholder(item.SerialNumber)}　原状态：{EmptyAsPlaceholder(item.BeforeMediaStatus)}　原位置：{EmptyAsPlaceholder(item.BeforeStorageLocation)}"));
        }

        private static double CalculateItemRowHeight(int itemCount)
        {
            int lines = Math.Max(itemCount, 1);
            return Math.Min(220, Math.Max(72, lines * 22d + 16d));
        }

        private static string BuildApprovalSection(HardDiskDisposalPrintData data)
        {
            if (string.IsNullOrWhiteSpace(data.ApprovedBy) && string.IsNullOrWhiteSpace(data.ApprovalOpinion))
            {
                return "意见：____________________    审批人：__________    日期:______年___月___日";
            }

            return $"意见：{EmptyAsPlaceholder(data.ApprovalOpinion)}    审批人：{EmptyAsPlaceholder(data.ApprovedBy)}    日期：{EmptyAsPlaceholder(data.ApprovedDateText)}";
        }

        private static string BuildSignatureSection(HardDiskDisposalPrintData data)
        {
            if (data.IsCompleted)
            {
                string dateText = EmptyAsPlaceholder(data.CompletedDateText);
                return $"申请人签字：{EmptyAsPlaceholder(data.ApplicantName)}    日期：{dateText}\n"
                     + $"资料室负责人签字：____________________    日期：{dateText}\n"
                     + $"资料室分管领导签字：____________________    日期：{dateText}";
            }

            return "申请人签字：                                 日期:______年___月___日\n"
                 + "资料室负责人签字：                           日期:______年___月___日\n"
                 + "资料室分管领导签字：                         日期:______年___月___日";
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
            var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) };
            table.Columns.Add(new TableColumn { Width = new GridLength(110) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            table.RowGroups.Add(rowGroup);
            return table;
        }

        private static TableRow CreateSingleRow(string label, string value, double minHeight)
        {
            var row = new TableRow();
            row.Cells.Add(CreateLabelCell(label, minHeight));
            row.Cells.Add(CreateValueCell(value, minHeight));
            return row;
        }

        private static TableRow CreateDoubleRow(string leftLabel, string leftValue, string rightLabel, string rightValue)
        {
            var row = new TableRow();
            row.Cells.Add(CreateLabelCell(leftLabel, 36));

            var inner = new Table { CellSpacing = 0 };
            inner.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            inner.Columns.Add(new TableColumn { Width = new GridLength(90) });
            inner.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var group = new TableRowGroup();
            var innerRow = new TableRow();
            innerRow.Cells.Add(CreateValueCell(leftValue, 36, border: false));
            innerRow.Cells.Add(CreateLabelCell(rightLabel, 36));
            innerRow.Cells.Add(CreateValueCell(rightValue, 36, border: false));
            group.Rows.Add(innerRow);
            inner.RowGroups.Add(group);

            var cell = new TableCell(inner)
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(0)
            };
            row.Cells.Add(cell);
            return row;
        }

        private static TableCell CreateLabelCell(string text, double minHeight)
        {
            return new TableCell(new Paragraph(new Run(text))
            {
                FontFamily = LabelFont,
                FontSize = 12,
                Margin = new Thickness(4)
            })
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0.5),
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(4),
                // MinHeight is applied via paragraph line height approximation
            };
        }

        private static TableCell CreateValueCell(string text, double minHeight, bool border = true)
        {
            var paragraph = new Paragraph(new Run(text))
            {
                FontFamily = BodyFont,
                FontSize = 12,
                Margin = new Thickness(4),
                LineHeight = 18
            };

            return new TableCell(paragraph)
            {
                BorderBrush = Brushes.Black,
                BorderThickness = border ? new Thickness(0.5) : new Thickness(0),
                Padding = new Thickness(4),
                TextAlignment = TextAlignment.Left
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
