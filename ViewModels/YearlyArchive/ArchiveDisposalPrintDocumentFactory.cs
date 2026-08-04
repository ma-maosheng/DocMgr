using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.Shared;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 资料离库处置签批单打印文档工厂。
    /// </summary>
    internal static class ArchiveDisposalPrintDocumentFactory
    {
        private static readonly FontFamily TitleFont = new("SimHei");
        private static readonly FontFamily BodyFont = new("SimSun");

        internal static FlowDocument Create(YearlyArchiveDisposalPrintData data)
        {
            ArgumentNullException.ThrowIfNull(data);

            var document = new FlowDocument
            {
                FontFamily = BodyFont,
                FontSize = 12,
                PagePadding = new Thickness(0)
            };
            PrintPageLayoutSupport.ApplyA4MediumMargins(document);

            string rail = string.Equals(data.MediaKind?.Trim(), ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal)
                ? "模拟"
                : "电子";

            document.Blocks.Add(new Paragraph(new Run($"河北省第三测绘院资料室{rail}资料离库处置签批单"))
            {
                FontFamily = TitleFont,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            });

            document.Blocks.Add(new Paragraph(new Run(
                $"处置单编号：{data.DisposalNo}　　申请日期：{data.ApplyTime:yyyy-MM-dd}"))
            {
                Margin = new Thickness(0, 0, 0, 8)
            });

            document.Blocks.Add(new Paragraph(new Run(
                $"离库原因：{Empty(data.DisposalReason)}　　处置方式：{Empty(data.DispositionMethod)}"))
            {
                Margin = new Thickness(0, 0, 0, 8)
            });

            document.Blocks.Add(new Paragraph(new Run($"申请说明：{Empty(data.Reason)}"))
            {
                Margin = new Thickness(0, 0, 0, 8)
            });

            var itemLines = data.Items
                .OrderBy(item => item.SortOrder)
                .Select(item =>
                    $"{item.SortOrder}. {Empty(item.DisplayName)}｜{Empty(item.SourceRegisterKind)}｜{Empty(item.DisposalReason)}/{Empty(item.DispositionMethod)}｜位置：{Empty(item.BeforeStorageLocation)}"
                    + (string.IsNullOrWhiteSpace(item.TargetBlankSlotLocation)
                        ? string.Empty
                        : $"｜低格档口：{item.TargetBlankSlotLocation}"));
            document.Blocks.Add(new Paragraph(new Run("待处置明细：\n" + string.Join("\n", itemLines)))
            {
                Margin = new Thickness(0, 0, 0, 12)
            });

            document.Blocks.Add(new Paragraph(new Run(
                $"申请人：{Empty(data.ApplicantName)}　　部门：{Empty(data.ApplicantDept)}"))
            {
                Margin = new Thickness(0, 0, 0, 8)
            });

            document.Blocks.Add(new Paragraph(new Run(
                data.IsCompleted
                    ? $"资料室审批：{Empty(data.ApprovalOpinion)}　　审批人：{Empty(data.ApprovedBy)}　　日期：{data.ApprovedTime:yyyy-MM-dd}"
                    : "资料室审批：________________　　审批人：________　　日期:______年___月___日"))
            {
                Margin = new Thickness(0, 0, 0, 8)
            });

            document.Blocks.Add(new Paragraph(new Run(
                data.IsCompleted
                    ? $"申请人签字：{Empty(data.ApplicantName)}　　日期：{data.ApplyTime:yyyy-MM-dd}\n资料室资料管理员签字：________　　日期:______年___月___日"
                    : "申请人签字：________________　　日期:______年___月___日\n资料室资料管理员签字：________________　　日期:______年___月___日"))
            {
                Margin = new Thickness(0, 0, 0, 8)
            });

            document.Blocks.Add(new Paragraph(new Run($"备注：{Empty(data.Remark)}")));
            return document;
        }

        private static string Empty(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
    }
}
