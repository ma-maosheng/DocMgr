using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;

namespace DocMgr.ViewModels.HardDiskMedia
{
    internal static class HardDiskMediaPrintDocumentFactory
    {
        private static readonly FontFamily TitleFont = new("SimHei");
        private static readonly FontFamily LabelFont = new("SimHei");
        private static readonly FontFamily BodyFont = new("SimSun");

        /// <summary>接收登记单：单行内容统一行高。</summary>
        private const double RegistrationStandardRowHeight = PrintPageLayoutSupport.TableRowContentHeightOneLineDip;

        /// <summary>接收登记单：双方交接签字行高（2 行）。</summary>
        private const double RegistrationHandoverRowHeight = PrintPageLayoutSupport.TableRowContentHeightTwoLinesDip;

        /// <summary>出库单：单行内容行高。</summary>
        private const double OutboundOneLineRowHeight = PrintPageLayoutSupport.TableRowContentHeightOneLineDip;

        /// <summary>出库单：双方交接签字固定 2 行。</summary>
        private const double OutboundHandoverRowHeight = PrintPageLayoutSupport.TableRowContentHeightTwoLinesDip;

        /// <summary>申请原因/特殊情况说明按正文估行后的上限。</summary>
        private const int ReasonMaxLines = 5;

        /// <summary>备注按正文估行后的上限。</summary>
        private const int RemarkMaxLines = 3;

        private const double OutboundHeaderHeight = 28;
        private const double RegistrationHeaderHeight = 28;
        /// <summary>硬盘表单单元格标准上下内边距。</summary>
        private const double HardDiskRowChromeDip = PrintPageLayoutSupport.TableCellPaddingDip;

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
            string reasonText = EmptyAsPlaceholder(data.Reason);
            string remarkText = EmptyAsPlaceholder(data.Remark);
            string mediumSummary = CreateMediumSummary(data);
            const string outboundFooterLine1 =
                "1、申请提交后，按“线上申请、打印表单、线下签字、拍照上传、业务办理”的流程办理。\n";
            const string outboundFooterLine2 =
                "      2、签字后的纸质审批单应回传系统，作为办理依据和归档附件。\n";
            double reasonRowHeight = ResolveVariableTextRowHeight(reasonText, ReasonMaxLines);
            double remarkRowHeight = ResolveVariableTextRowHeight(remarkText, RemarkMaxLines);
            double mediumRowHeight = CalculateOutboundMediumRowHeight(
                data,
                reasonRowHeight,
                remarkRowHeight,
                mediumSummary,
                outboundFooterLine1,
                outboundFooterLine2);

            document.Blocks.Add(CreateHeaderTable(
                $"申请单编号：{data.ApplicationNo}",
                $"申请日期：{data.ApplyDateText}"));

            var rowGroup = new TableRowGroup();
            rowGroup.Rows.Add(CreateDoubleRow(
                "申请人", data.ApplicantName,
                "申请部门", EmptyAsPlaceholder(data.ApplicantDept),
                OutboundOneLineRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("申请类型", data.ApplicationType, OutboundOneLineRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("关联介质", mediumSummary, mediumRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("当前位置", EmptyAsPlaceholder(data.CurrentLocation), OutboundOneLineRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("目标去向", EmptyAsPlaceholder(data.TargetLocation), OutboundOneLineRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("预计归还日期", EmptyAsPlaceholder(data.ExpectedReturnDateText), OutboundOneLineRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("申请原因", reasonText, reasonRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("备注", remarkText, remarkRowHeight));
            AppendApprovalSignatureRows(rowGroup, data, OutboundOneLineRowHeight);
            rowGroup.Rows.Add(CreateSingleRow("交接签字", BuildHandoverSection(data), OutboundHandoverRowHeight));

            document.Blocks.Add(CreateMainTable(rowGroup));
            document.Blocks.Add(CreateFooterParagraph(data, outboundFooterLine1, outboundFooterLine2));

            return document;
        }

        /// <summary>可变文本行：默认 1 行，按正文增高，不超过上限。</summary>
        private static double ResolveVariableTextRowHeight(string text, int maximumLineCount) =>
            PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(text, maximumLineCount);

        /// <summary>
        /// 关联介质可伸缩行高：至少容纳正文，有余量时撑满一页剩余高度。
        /// </summary>
        private static double CalculateOutboundMediumRowHeight(
            HardDiskMediaPrintData data,
            double reasonRowHeight,
            double remarkRowHeight,
            string mediumSummary,
            string footerLine1,
            string footerLine2)
        {
            int approvalRowCount = CountEnabledApprovalRows(data);
            // 固定行（不含关联介质撑满行）：
            // 申请人双列、申请类型、当前位置、目标去向、预计归还(5) + 原因 + 备注 + 签批(N) + 交接 = 8+N。
            double fixedContentHeight =
                OutboundOneLineRowHeight * (5 + approvalRowCount)
                + reasonRowHeight
                + remarkRowHeight
                + OutboundHandoverRowHeight;
            int fixedRowCount = 8 + approvalRowCount;
            double fixedTableHeight = fixedContentHeight
                + PrintPageLayoutSupport.GetTableRowOuterHeightDip(0, HardDiskRowChromeDip) * fixedRowCount
                + PrintPageLayoutSupport.EstimateTableBottomBorderHeightDip(fixedRowCount);
            double footerHeight = EstimateHardDiskFooterNoteHeightDip(data, footerLine1, footerLine2);
            double reservedHeight =
                PrintPageLayoutSupport.ApprovalFormTitleBlockHeightDip
                + OutboundHeaderHeight
                + PrintPageLayoutSupport.ApprovalFormHeaderToTableGapDip
                + footerHeight
                + fixedTableHeight;

            // 下限 = 正文完整显示所需高度；有多余页高时由 CalculateStretchRowHeightDip 拉高占满一页。
            // 撑满高度不超过剩余空间，保证表后「备注」说明同页。
            double contentNeededHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(
                mediumSummary,
                maximumLineCount: 12);

            return PrintPageLayoutSupport.CalculateStretchRowHeightDip(
                reservedHeight,
                contentNeededHeight,
                HardDiskRowChromeDip);
        }

        private static FlowDocument CreateRegistrationDocument(HardDiskMediaPrintData data)
        {
            var document = CreateDocumentSkeleton(GetDocumentTitle(data.ApplicationType));
            bool includeInspection = IsNormalRegistrationReturn(data);
            string reasonText = EmptyAsPlaceholder(data.Reason);
            string remarkText = EmptyAsPlaceholder(data.Remark);
            const string registrationFooterLine1 =
                "1、登记提交后，按“打印登记单、线下签字、拍照上传、业务办理”的流程完成归还/挂失登记。\n";
            const string registrationFooterLine2 =
                "      2、签字后的登记单应回传系统，作为归还登记办理依据和归档附件。\n";
            double reasonRowHeight = ResolveVariableTextRowHeight(reasonText, ReasonMaxLines);
            double remarkRowHeight = ResolveVariableTextRowHeight(remarkText, RemarkMaxLines);
            double mediumRowHeight = CalculateRegistrationMediumRowHeight(
                data,
                includeInspection,
                reasonRowHeight,
                remarkRowHeight,
                registrationFooterLine1,
                registrationFooterLine2);

            document.Blocks.Add(CreateHeaderTable(
                $"登记单编号：{data.ApplicationNo}",
                $"出库时申请单编号：{EmptyAsPlaceholder(data.SourceApplicationNo)}"));

            var rowGroup = new TableRowGroup();
            rowGroup.Rows.Add(CreateDoubleRow(
                "登记人", data.ApplicantName,
                "登记（借用）部门", EmptyAsPlaceholder(data.ApplicantDept),
                RegistrationStandardRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("登记类型", data.ApplicationType, RegistrationStandardRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("关联介质", CreateMediumSummary(data), mediumRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("登记前位置", EmptyAsPlaceholder(data.CurrentLocation), RegistrationStandardRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("登记后位置", EmptyAsPlaceholder(data.TargetLocation), RegistrationStandardRowHeight));
            rowGroup.Rows.Add(CreateDoubleRow(
                "登记日期", data.ApplyDateText,
                "登记状态", EmptyAsPlaceholder(data.CurrentStatus),
                RegistrationStandardRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("特殊情况说明", reasonText, reasonRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("备注", remarkText, remarkRowHeight));
            rowGroup.Rows.Add(CreateSingleRow("归还登记类型", GetRegistrationKindText(data), RegistrationStandardRowHeight));
            AppendApprovalSignatureRows(rowGroup, data, RegistrationStandardRowHeight);
            if (includeInspection)
            {
                rowGroup.Rows.Add(CreateSingleRow(
                    "资料室查验",
                    GetRegistrationInspectionText(),
                    RegistrationStandardRowHeight));
            }

            rowGroup.Rows.Add(CreateSingleRow(
                "交接签字",
                BuildRegistrationHandoverSection(data),
                RegistrationHandoverRowHeight));

            document.Blocks.Add(CreateMainTable(rowGroup));
            document.Blocks.Add(CreateFooterParagraph(
                data,
                registrationFooterLine1,
                registrationFooterLine2));

            return document;
        }

        private static double CalculateRegistrationMediumRowHeight(
            HardDiskMediaPrintData data,
            bool includeInspection,
            double reasonRowHeight,
            double remarkRowHeight,
            string footerLine1,
            string footerLine2)
        {
            int approvalRowCount = CountEnabledApprovalRows(data);
            // 固定行（不含关联介质撑满行）：
            // 登记人/类型/前后位置/日期状态(5) + 归还类型/签批(+查验) + 说明 + 备注 + 交接。
            int standardRows = (includeInspection ? 6 : 5) + approvalRowCount;
            double fixedContentHeight =
                RegistrationStandardRowHeight * standardRows
                + reasonRowHeight
                + remarkRowHeight
                + RegistrationHandoverRowHeight;
            int fixedRowCount = standardRows + 3; // 说明、备注、交接
            double fixedTableHeight = fixedContentHeight
                + PrintPageLayoutSupport.GetTableRowOuterHeightDip(0, HardDiskRowChromeDip) * fixedRowCount
                + PrintPageLayoutSupport.EstimateTableBottomBorderHeightDip(fixedRowCount);
            double footerHeight = EstimateHardDiskFooterNoteHeightDip(data, footerLine1, footerLine2);
            double reservedHeight =
                PrintPageLayoutSupport.ApprovalFormTitleBlockHeightDip
                + RegistrationHeaderHeight
                + PrintPageLayoutSupport.ApprovalFormHeaderToTableGapDip
                + footerHeight
                + fixedTableHeight;
            string mediumSummary = CreateMediumSummary(data);
            double contentNeededHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(
                mediumSummary,
                maximumLineCount: 12);
            return PrintPageLayoutSupport.CalculateStretchRowHeightDip(
                reservedHeight,
                contentNeededHeight,
                HardDiskRowChromeDip);
        }

        private static FlowDocument CreateDocumentSkeleton(string title)
        {
            var document = new FlowDocument
            {
                FontFamily = BodyFont,
                FontSize = 12,
                LineHeight = PrintPageLayoutSupport.ApprovalFormLineHeightDip,
                ColumnWidth = double.PositiveInfinity
            };
            PrintPageLayoutSupport.ApplyA4MediumMargins(document);

            document.Blocks.Add(new Paragraph(new Run(title))
            {
                FontFamily = TitleFont,
                FontSize = PrintPageLayoutSupport.ApprovalFormTitleFontSize,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = PrintPageLayoutSupport.ApprovalFormTitleMargin
            });

            return document;
        }

        private static Table CreateHeaderTable(string leftText, string rightText)
        {
            var headerTable = new Table { Margin = PrintPageLayoutSupport.ApprovalFormHeaderTableMargin };
            headerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            headerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

            var headerGroup = new TableRowGroup();
            var headerRow = new TableRow();
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(leftText)
            {
                FontFamily = BodyFont,
                FontSize = 12
            })
            { Margin = new Thickness(0) })
            { TextAlignment = TextAlignment.Left });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(rightText)
            {
                FontFamily = BodyFont,
                FontSize = 12
            })
            { Margin = new Thickness(0) })
            { TextAlignment = TextAlignment.Right });
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

            PrintPageLayoutSupport.ApplyApprovalFormMainTableColumns(table);

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

        /// <summary>
        /// 按实际表后说明文案（含折行）预留高度，并多留 1 行余量，避免末行掉页。
        /// </summary>
        private static double EstimateHardDiskFooterNoteHeightDip(
            HardDiskMediaPrintData data,
            string line1,
            string line2)
        {
            string noteText =
                "备注：" + line1 + line2 +
                $"      3、本申请单已累计打印 {data.PrintCount + 1} 次，最新打印请与系统记录核对。";

            return PrintPageLayoutSupport.EstimateNoteBlockHeightFromTextWithSafetyDip(
                noteText,
                PrintPageLayoutSupport.ContentWidthDip,
                fontSizeDip: 10.5,
                lineHeightDip: 18,
                topMarginDip: 15);
        }

        private static string CreateMediumSummary(HardDiskMediaPrintData data)
        {
            return $"介质编号:{data.DiskCode} \n序列号:{data.SerialNumber} \n介质类型:{data.DiskType} \n登记方式:{HardDiskMediumRegistrationMethodDisplay.Format(data.RegistrationMethod)} \n品牌、容量、接口:{data.DeviceSummary}";
        }

        private static TableRow CreateSingleRow(string label, string content, double minHeight)
        {
            var row = new TableRow();
            row.Cells.Add(CreateLabelCell(label, minHeight));
            row.Cells.Add(CreateContentCell(content, 3, minHeight));
            return row;
        }

        private static TableRow CreateDoubleRow(
            string label1,
            string content1,
            string label2,
            string content2,
            double minHeight = 0)
        {
            double height = minHeight > 0 ? minHeight : OutboundOneLineRowHeight;
            var row = new TableRow();
            row.Cells.Add(CreateLabelCell(label1, height));
            row.Cells.Add(CreateContentCell(content1, minHeight: height));
            row.Cells.Add(CreateLabelCell(label2, height));
            row.Cells.Add(CreateContentCell(content2, minHeight: height));
            return row;
        }

        private static TableCell CreateLabelCell(string label, double rowHeight)
        {
            return new TableCell(CreateAlignedCellContent(label, rowHeight, label: true, verticalTop: false))
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.Black,
                Padding = new Thickness(HardDiskRowChromeDip)
            };
        }

        private static TableCell CreateContentCell(string content, int columnSpan = 1, double minHeight = 0)
        {
            double height = minHeight > 0 ? minHeight : OutboundOneLineRowHeight;
            bool verticalTop = content.Contains('\n', StringComparison.Ordinal);
            return new TableCell(CreateAlignedCellContent(content, height, label: false, verticalTop))
            {
                ColumnSpan = columnSpan,
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.Black,
                Padding = new Thickness(HardDiskRowChromeDip)
            };
        }

        private static Block CreateAlignedCellContent(
            string text,
            double rowHeight,
            bool label,
            bool verticalTop)
        {
            var grid = new Grid { Height = rowHeight };
            grid.Children.Add(new TextBlock
            {
                Text = text,
                TextWrapping = label ? TextWrapping.NoWrap : TextWrapping.Wrap,
                VerticalAlignment = verticalTop ? VerticalAlignment.Top : VerticalAlignment.Center,
                HorizontalAlignment = label ? HorizontalAlignment.Center : HorizontalAlignment.Left,
                TextAlignment = label ? TextAlignment.Center : TextAlignment.Left,
                FontFamily = label ? LabelFont : BodyFont,
                FontWeight = label ? FontWeights.Bold : FontWeights.Normal,
                FontSize = 12,
                Margin = new Thickness(2, 0, 2, 0)
            });
            return new BlockUIContainer(grid);
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

        private static string GetRegistrationKindText(HardDiskMediaPrintData data)
        {
            string selectedKind = ResolveRegistrationKindSelection(data);
            return $"{BuildCheckOption("正常归还", selectedKind)}  {BuildCheckOption("损坏登记", selectedKind)}  {BuildCheckOption("挂失登记", selectedKind)}";
        }

        private static string ResolveRegistrationKindSelection(HardDiskMediaPrintData data)
        {
            string inspectionResult = data.InspectionResultText?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(inspectionResult))
            {
                if (HardDiskMediaReturnDomainValues.IsLossRegistrationInspection(inspectionResult))
                {
                    return "挂失登记";
                }

                if (HardDiskMediaReturnDomainValues.IsDamagedReturnInspection(inspectionResult))
                {
                    return "损坏登记";
                }

                if (HardDiskMediaReturnDomainValues.IsNormalReturnInspection(inspectionResult))
                {
                    return "正常归还";
                }
            }

            return data.ApplicationType switch
            {
                HardDiskMediaApplication.TypeLossRegistration => "挂失登记",
                HardDiskMediaApplication.TypeReturnDamagedRegistration => "损坏登记",
                _ => "正常归还"
            };
        }

        private static bool IsNormalRegistrationReturn(HardDiskMediaPrintData data)
            => string.Equals(ResolveRegistrationKindSelection(data), "正常归还", StringComparison.Ordinal);

        /// <summary>
        /// 资料室查验仅展示「已格式化」一项，并默认勾选（仅正常归还）。
        /// </summary>
        private static string GetRegistrationInspectionText()
            => BuildCheckOption("已格式化", "已格式化");

        private static void AppendApprovalSignatureRows(
            TableRowGroup rowGroup,
            HardDiskMediaPrintData data,
            double rowHeight)
        {
            if (data.EnableDeptHead)
            {
                rowGroup.Rows.Add(CreateSingleRow(
                    ApprovalWorkflowDomainValues.DisplayDeptHead,
                    PrintApprovalSignatureSupport.FormatInline(data.DeptHead, data.DeptHeadDateText),
                    rowHeight));
            }

            if (data.EnableArchiveRoomHead)
            {
                rowGroup.Rows.Add(CreateSingleRow(
                    ApprovalWorkflowDomainValues.DisplayArchiveRoomHead,
                    PrintApprovalSignatureSupport.FormatInline(data.ArchiveRoomHead, data.ArchiveRoomHeadDateText),
                    rowHeight));
            }

            if (data.EnableProductionHead)
            {
                rowGroup.Rows.Add(CreateSingleRow(
                    ApprovalWorkflowDomainValues.DisplayProductionHead,
                    PrintApprovalSignatureSupport.FormatInline(data.ProductionHead, data.ProductionHeadDateText),
                    rowHeight));
            }

            if (data.EnableArchiveDeputyPresident)
            {
                rowGroup.Rows.Add(CreateSingleRow(
                    ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident,
                    PrintApprovalSignatureSupport.FormatInline(
                        data.ArchiveDeputyPresident,
                        data.ArchiveDeputyPresidentDateText),
                    rowHeight));
            }

            if (data.EnableProductionVicePresident)
            {
                rowGroup.Rows.Add(CreateSingleRow(
                    ApprovalWorkflowDomainValues.DisplayProductionVicePresident,
                    PrintApprovalSignatureSupport.FormatInline(
                        data.ProductionVicePresident,
                        data.ProductionVicePresidentDateText),
                    rowHeight));
            }
        }

        private static int CountEnabledApprovalRows(HardDiskMediaPrintData data)
        {
            int count = 0;
            if (data.EnableDeptHead)
            {
                count++;
            }

            if (data.EnableArchiveRoomHead)
            {
                count++;
            }

            if (data.EnableProductionHead)
            {
                count++;
            }

            if (data.EnableArchiveDeputyPresident)
            {
                count++;
            }

            if (data.EnableProductionVicePresident)
            {
                count++;
            }

            return count;
        }

        private const string BlankHandoverAdminSignatureLine =
            "资料管理员签字：                                 日期:______年___月___日";

        private static string BuildHandoverSection(HardDiskMediaPrintData data)
        {
            if (data.IsCompleted)
            {
                return BuildFilledTwoPartyHandoverBlock(
                    "申请人签字：",
                    data.HandoverApplicant,
                    data.HandoverAdmin,
                    data.HandoverDateText);
            }

            return BuildBlankTwoPartyHandoverBlock("申请人签字：");
        }

        private static string BuildRegistrationHandoverSection(HardDiskMediaPrintData data)
        {
            if (data.IsCompleted)
            {
                return BuildFilledTwoPartyHandoverBlock(
                    "交接人签字：",
                    data.HandoverApplicant,
                    data.HandoverAdmin,
                    data.HandoverDateText);
            }

            return $"交接人签字：                                            日期:______年___月___日\n{BlankHandoverAdminSignatureLine}";
        }

        private static string BuildBlankTwoPartyHandoverBlock(string firstPartyLabel)
        {
            return $"\n{firstPartyLabel}                                            日期:______年___月___日\n{BlankHandoverAdminSignatureLine}";
        }

        private static string BuildFilledTwoPartyHandoverBlock(
            string firstPartyLabel,
            string? firstPartyName,
            string? adminName,
            string? dateText)
        {
            string firstSlot = string.IsNullOrWhiteSpace(firstPartyName) ? "________________" : firstPartyName.Trim();
            string adminSlot = string.IsNullOrWhiteSpace(adminName) ? "________________" : adminName.Trim();
            string renderedDate = string.IsNullOrWhiteSpace(dateText) ? "______年___月___日" : dateText.Trim();

            return $"\n{firstPartyLabel}{firstSlot}    日期：{renderedDate}\n" +
                   $"资料管理员签字：{adminSlot}    日期：{renderedDate}";
        }

        private static string BuildCheckOption(string option, string selectedOption)
            => string.Equals(option, selectedOption, StringComparison.OrdinalIgnoreCase)
                ? $"■{option}"
                : $"□{option}";
    }
}
