using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.ViewModels.YearlyArchive
{
    internal static class ArchiveOutboundHandoverAssistantPrintDocumentFactory
    {
        private static readonly FontFamily TitleFont = new("SimHei");
        private static readonly FontFamily LabelFont = new("SimHei");
        private static readonly FontFamily BodyFont = new("SimSun");

        internal static FlowDocument Create(ArchiveOutboundHandoverAssistantPrintData data)
        {
            ArgumentNullException.ThrowIfNull(data);

            var document = new FlowDocument
            {
                FontFamily = BodyFont,
                FontSize = 12,
                LineHeight = 18,
                PageWidth = 793.6,
                PageHeight = 1122.5,
                PagePadding = new Thickness(56, 36, 56, 32),
                ColumnWidth = double.PositiveInfinity
            };

            document.Blocks.Add(new Paragraph(new Run("资料出库业务助手清单"))
            {
                FontFamily = TitleFont,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });

            document.Blocks.Add(new Paragraph(new Run($"申请单编号：{data.OutboundNo}    领用人：{data.ApplicantName}    部门：{data.ApplicantDept}"))
            {
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            });

            document.Blocks.Add(new Paragraph(new Run($"资料摘要：{data.MaterialSummary}"))
            {
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 10)
            });

            document.Blocks.Add(new Paragraph(new Run("请在办理实物出库时逐项核对并勾选确认："))
            {
                FontSize = 11,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 0, 0, 8)
            });

            string? currentCategory = null;
            foreach (var line in data.Lines)
            {
                if (!string.Equals(currentCategory, line.Category, StringComparison.Ordinal))
                {
                    currentCategory = line.Category;
                    document.Blocks.Add(new Paragraph(new Run(currentCategory))
                    {
                        FontFamily = LabelFont,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 8, 0, 4)
                    });
                }

                string mark = line.IsChecked ? "☑" : "☐";
                document.Blocks.Add(new Paragraph(new Run($"{mark} {line.Text}"))
                {
                    Margin = new Thickness(12, 0, 0, 4),
                    TextIndent = 0
                });
            }

            document.Blocks.Add(new Paragraph(new Run($"打印时间：{DateTime.Now:yyyy-MM-dd HH:mm}"))
            {
                FontSize = 10,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 16, 0, 0)
            });

            return document;
        }
    }
}
