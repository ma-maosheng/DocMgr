using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DocMgr.ViewModels.HardDiskMedia
{
    internal static class HardDiskMediaPrintDocumentFactory
    {
        private static readonly FontFamily TitleFont = new("SimHei");
        private static readonly FontFamily LabelFont = new("SimHei");
        private static readonly FontFamily BodyFont = new("SimSun");

        internal static FlowDocument Create(HardDiskMediaPrintData data)
        {
            ArgumentNullException.ThrowIfNull(data);

            return IsRegistrationType(data.ApplicationType)
                ? CreateRegistrationDocument(data)
                : CreateOutboundApplicationDocument(data);
        }

        private static FlowDocument CreateOutboundApplicationDocument(HardDiskMediaPrintData data)
        {
            var document = CreateDocumentSkeleton(GetDocumentTitle(data.ApplicationType));

            document.Blocks.Add(CreateHeaderTable(
                $"申请单编号：{data.ApplicationNo}",
                $"申请日期：{data.ApplyDateText}"));

            var rowGroup = new TableRowGroup();
            rowGroup.Rows.Add(CreateSingleRow("申请类型", data.ApplicationType, 20));
            rowGroup.Rows.Add(CreateSingleRow("关联介质", CreateMediumSummary(data), 100));
            rowGroup.Rows.Add(CreateSingleRow("当前位置", EmptyAsPlaceholder(data.CurrentLocation), 40));
            rowGroup.Rows.Add(CreateSingleRow("目标位置/去向", EmptyAsPlaceholder(data.TargetLocation), 40));
            rowGroup.Rows.Add(CreateDoubleRow("申请人", data.ApplicantName, "申请部门", EmptyAsPlaceholder(data.ApplicantDept)));
            rowGroup.Rows.Add(CreateDoubleRow("预计归还日期", EmptyAsPlaceholder(data.ExpectedReturnDateText), "对方人员/单位", EmptyAsPlaceholder(data.TargetPersonOrUnit)));
            rowGroup.Rows.Add(CreateSingleRow("申请原因", EmptyAsPlaceholder(data.Reason), 90));
            rowGroup.Rows.Add(CreateSingleRow("备注", EmptyAsPlaceholder(data.Remark), 60));
            rowGroup.Rows.Add(CreateSingleRow("申请部门审核", BuildReviewerSection(data), 48));
            rowGroup.Rows.Add(CreateSingleRow("资料室审批", BuildApproverSection(data), 82));
            rowGroup.Rows.Add(CreateSingleRow("交接签字", BuildHandoverSection(data), 72));

            document.Blocks.Add(CreateMainTable(rowGroup));
            document.Blocks.Add(CreateFooterParagraph(
                data,
                "1、申请提交后，按“线上申请、打印表单、线下签字、拍照上传、业务办理”的流程办理。\n",
                "      2、签字后的纸质审批单应回传系统，作为办理依据和归档附件。\n"));

            return document;
        }

        private static FlowDocument CreateRegistrationDocument(HardDiskMediaPrintData data)
        {
            var document = CreateDocumentSkeleton(GetDocumentTitle(data.ApplicationType));

            document.Blocks.Add(CreateHeaderTable(
                $"登记单编号：{data.ApplicationNo}",
                $"出库时申请单编号：{EmptyAsPlaceholder(data.SourceApplicationNo)}"));

            var rowGroup = new TableRowGroup();
            rowGroup.Rows.Add(CreateDoubleRow("登记人", data.ApplicantName, "登记（借用）部门", EmptyAsPlaceholder(data.ApplicantDept)));
            rowGroup.Rows.Add(CreateSingleRow("登记类型", data.ApplicationType, 20));
            rowGroup.Rows.Add(CreateSingleRow("关联介质", CreateMediumSummary(data), 100));
            rowGroup.Rows.Add(CreateSingleRow("登记前位置", EmptyAsPlaceholder(data.CurrentLocation), 40));
            rowGroup.Rows.Add(CreateSingleRow("登记后位置", EmptyAsPlaceholder(data.TargetLocation), 40));
            rowGroup.Rows.Add(CreateDoubleRow("登记日期", data.ApplyDateText, "登记状态", EmptyAsPlaceholder(data.CurrentStatus)));
            rowGroup.Rows.Add(CreateSingleRow("特殊情况说明", EmptyAsPlaceholder(data.Reason), 90));
            rowGroup.Rows.Add(CreateSingleRow("备注", EmptyAsPlaceholder(data.Remark), 60));
            rowGroup.Rows.Add(CreateSingleRow("资料室查验", GetRegistrationVerificationText(data), 72));
            rowGroup.Rows.Add(CreateSingleRow("交接签字", BuildRegistrationHandoverSection(data), 72));

            document.Blocks.Add(CreateMainTable(rowGroup));
            document.Blocks.Add(CreateFooterParagraph(
                data,
                "1、登记提交后，按“打印登记单、线下签字、拍照上传、业务办理”的流程完成归还/挂失登记。\n",
                "      2、签字后的登记单应回传系统，作为归还登记办理依据和归档附件。\n"));

            return document;
        }

        private static FlowDocument CreateDocumentSkeleton(string title)
        {
            var document = new FlowDocument
            {
                FontFamily = BodyFont,
                FontSize = 12,
                LineHeight = 20,
                PagePadding = new Thickness(80, 48, 80, 48),
                ColumnWidth = double.PositiveInfinity
            };

            document.Blocks.Add(new Paragraph(new Run(""))
            {
                Margin = new Thickness(0, 0, 0, 28)
            });

            document.Blocks.Add(new Paragraph(new Run($"\n{title}"))
            {
                FontFamily = TitleFont,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 18)
            });

            return document;
        }

        private static Table CreateHeaderTable(string leftText, string rightText)
        {
            var headerTable = new Table();
            headerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            headerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

            var headerGroup = new TableRowGroup();
            var headerRow = new TableRow();
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(leftText))) { TextAlignment = TextAlignment.Left });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(rightText))) { TextAlignment = TextAlignment.Right });
            headerGroup.Rows.Add(headerRow);
            headerTable.RowGroups.Add(headerGroup);

            return headerTable;
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

        private static Paragraph CreateFooterParagraph(HardDiskMediaPrintData data, string line1, string line2)
        {
            var footer = new Paragraph
            {
                FontSize = 10.5,
                Margin = new Thickness(0, 15, 0, 0),
                LineHeight = 18
            };

            footer.Inlines.Add(new Run("备注：") { FontWeight = FontWeights.Bold });
            footer.Inlines.Add(new Run(line1));
            footer.Inlines.Add(new Run(line2));
            footer.Inlines.Add(new Run($"      3、本申请单已累计打印 {data.PrintCount + 1} 次，最新打印请与系统记录核对。"));

            return footer;
        }

        private static string CreateMediumSummary(HardDiskMediaPrintData data)
        {
            return $"介质编号:{data.DiskCode} \n序列号:{data.SerialNumber} \n介质类型:{data.DiskType} \n登记方式:{HardDiskMediumRegistrationMethodDisplay.Format(data.RegistrationMethod)} \n品牌、容量、接口:{data.DeviceSummary}";
        }

        private static TableRow CreateSingleRow(string label, string content, double minHeight)
        {
            var row = new TableRow();
            row.Cells.Add(CreateLabelCell(label));
            row.Cells.Add(CreateContentCell(content, 3, minHeight));
            return row;
        }

        private static TableRow CreateDoubleRow(string label1, string content1, string label2, string content2)
        {
            var row = new TableRow();
            row.Cells.Add(CreateLabelCell(label1));
            row.Cells.Add(CreateContentCell(content1));
            row.Cells.Add(CreateLabelCell(label2));
            row.Cells.Add(CreateContentCell(content2));
            return row;
        }

        private static TableCell CreateLabelCell(string label)
        {
            return new TableCell(new Paragraph(new Run(label))
            {
                FontFamily = LabelFont,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center
            })
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.Black,
                Padding = new Thickness(2, 6, 2, 2)
            };
        }

        private static TableCell CreateContentCell(string content, int columnSpan = 1, double minHeight = 0)
        {
            Block block;
            if (minHeight > 0)
            {
                var grid = new Grid { MinHeight = minHeight };
                grid.Children.Add(new TextBlock
                {
                    Text = content,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(4),
                    FontFamily = BodyFont,
                    FontSize = 12
                });
                block = new BlockUIContainer(grid);
            }
            else
            {
                block = new Paragraph(new Run(content))
                {
                    Margin = new Thickness(4),
                    FontFamily = BodyFont
                };
            }

            return new TableCell(block)
            {
                ColumnSpan = columnSpan,
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.Black
            };
        }

        private static string EmptyAsPlaceholder(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(无)" : value.Trim();
        }

        private static bool IsRegistrationType(string applicationType)
        {
            return applicationType == HardDiskMediaApplication.TypeReturnBlankRegistration ||
                   applicationType == HardDiskMediaApplication.TypeReturnDataRegistration ||
                   applicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration ||
                   applicationType == HardDiskMediaApplication.TypeLossRegistration;
        }

        private static string GetDocumentTitle(string applicationType)
        {
            return applicationType == HardDiskMediaApplication.TypeLossRegistration
                ? "河北省第三测绘院资料室硬盘介质挂失登记单"
                : IsRegistrationType(applicationType)
                    ? "河北省第三测绘院资料室硬盘介质接收登记单"
                    : "河北省第三测绘院资料室硬盘介质出库申请审批单";
        }

        private static string GetRegistrationVerificationText(HardDiskMediaPrintData data)
        {
            string defaultInspectionResult = data.ApplicationType switch
            {
                HardDiskMediaApplication.TypeLossRegistration => "挂失登记",
                HardDiskMediaApplication.TypeReturnDamagedRegistration => "损坏登记",
                _ => "正常归还"
            };

            string selectedInspectionResult = string.IsNullOrWhiteSpace(data.InspectionResultText)
                ? defaultInspectionResult
                : data.InspectionResultText.Trim();
            string verificationText = $"查验结果：{BuildCheckOption("正常归还", selectedInspectionResult)}  {BuildCheckOption("损坏登记", selectedInspectionResult)}  {BuildCheckOption("挂失登记", selectedInspectionResult)}";

            return $"{verificationText}\n{EmptyAsPlaceholder(data.FormatConfirmationText)}";
        }

        private static string BuildReviewerSection(HardDiskMediaPrintData data)
        {
            string signatureText = BuildSignatureLine(data.ReviewerName, data.ReviewerDateText);
            return string.IsNullOrWhiteSpace(signatureText)
                ? "负责人签字：\n                                                  日期:______年___月___日"
                : signatureText;
        }

        private static string BuildApproverSection(HardDiskMediaPrintData data)
        {
            string opinion = string.IsNullOrWhiteSpace(data.ApprovalOpinion) ? string.Empty : data.ApprovalOpinion.Trim();
            string signatureText = BuildSignatureLine(data.ApproverName, data.ApproverDateText);

            if (string.IsNullOrWhiteSpace(opinion) && string.IsNullOrWhiteSpace(signatureText))
            {
                return "\n审批意见：\n                    签字：                              日期:______年___月___日";
            }

            string renderedOpinion = string.IsNullOrWhiteSpace(opinion) ? "(无)" : opinion;
            string renderedSignature = string.IsNullOrWhiteSpace(signatureText)
                ? "签字：                              日期:______年___月___日"
                : signatureText;
            return $"\n审批意见：{renderedOpinion}\n{renderedSignature}";
        }

        private const string BlankHandoverAdminSignatureLine =
            "资料室资料管理员签字：                                 日期:______年___月___日";

        private static string BuildHandoverSection(HardDiskMediaPrintData data)
        {
            return BuildBlankTwoPartyHandoverBlock("申请人签字：");
        }

        private static string BuildRegistrationHandoverSection(HardDiskMediaPrintData data)
        {
            return BuildBlankTwoPartyHandoverBlock("交接人签字：");
        }

        private static string BuildBlankTwoPartyHandoverBlock(string firstPartyLabel)
        {
            return $"\n{firstPartyLabel}                                            日期:______年___月___日\n{BlankHandoverAdminSignatureLine}";
        }

        private static string BuildSignatureLine(string? name, string? dateText)
        {
            string normalizedName = name?.Trim() ?? string.Empty;
            string normalizedDate = dateText?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedName) && string.IsNullOrWhiteSpace(normalizedDate))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return $"签字：    日期:{normalizedDate}";
            }

            if (string.IsNullOrWhiteSpace(normalizedDate))
            {
                return $"签字：{normalizedName}";
            }

            return $"签字：{normalizedName}    日期:{normalizedDate}";
        }

        private static string BuildCheckOption(string option, string selectedOption)
            => string.Equals(option, selectedOption, StringComparison.OrdinalIgnoreCase)
                ? $"■{option}"
                : $"□{option}";
    }
}
