using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;

namespace DocMgr.ViewModels.NetworkTransfer;

internal static class NetworkOutboundPrintDocumentFactory
{
    private static readonly FontFamily TitleFont = new("SimHei");
    private static readonly FontFamily LabelFont = new("SimHei");
    private static readonly FontFamily BodyFont = new("SimSun");

    private const double HeaderInfoHeight = 28;
    private const double StandardRowHeight = PrintPageLayoutSupport.TableRowContentHeightOneLineDip;
    /// <summary>出网交接栏：固定 2 行行高。</summary>
    private const double HandoverRowHeight = PrintPageLayoutSupport.TableRowContentHeightTwoLinesDip;
    private const double CellPadding = PrintPageLayoutSupport.TableCellPaddingDip;
    private const double BodyFontSize = 12;

    /// <summary>申请说明按正文估行后的上限。</summary>
    private const int ReasonMaxLines = 5;

    /// <summary>具体资料明细按正文估行后的上限（撑满下限）。</summary>
    private const int DetailMaxLines = 20;

    internal static FlowDocument Create(NetworkOutboundPrintData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        string reasonText = EmptyAsPlaceholder(data.Reason);
        string itemDetailText = BuildItemText(data);
        bool hasArchivePurpose = !string.IsNullOrWhiteSpace(data.ArchivePurposeText);
        double reasonRowHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(reasonText, ReasonMaxLines);
        double itemDetailRowHeight = CalculateItemDetailRowHeight(
            data,
            reasonRowHeight,
            hasArchivePurpose,
            itemDetailText);

        var document = CreateDocumentSkeleton();
        document.Blocks.Add(CreateTitleBlock());
        document.Blocks.Add(CreateHeaderTable(
            $"出网单编号：{data.OutboundNo}",
            $"申请日期：{data.ApplyDateText}"));

        var rowGroup = new TableRowGroup();
        rowGroup.Rows.Add(CreateDoubleRow("申请人", data.ApplicantName, "申请部门", data.ApplicantDept));
        rowGroup.Rows.Add(CreateDoubleRow("年度", EmptyAsPlaceholder(data.YearText), "项目", EmptyAsPlaceholder(data.ProjectName)));
        rowGroup.Rows.Add(CreateSingleRow("目的地", EmptyAsPlaceholder(data.DestinationKindText)));
        if (hasArchivePurpose)
        {
            rowGroup.Rows.Add(CreateSingleRow("库管模式", EmptyAsPlaceholder(data.ArchivePurposeText)));
        }
        rowGroup.Rows.Add(CreateSingleRow("申请说明", reasonText, reasonRowHeight, CellVerticalAlignment.ContentTop));
        rowGroup.Rows.Add(CreateSingleRow("证明材料名称", EmptyAsPlaceholder(data.ProofMaterialNote)));
        rowGroup.Rows.Add(CreateSingleRow(
            "具体资料明细",
            itemDetailText,
            itemDetailRowHeight,
            CellVerticalAlignment.ContentTop));
        if (data.EnableDeptHead)
        {
            rowGroup.Rows.Add(CreateSingleRow(ApprovalWorkflowDomainValues.DisplayDeptHead, data.DeptHeadBlock));
        }

        if (data.EnableProductionHead)
        {
            rowGroup.Rows.Add(CreateSingleRow(ApprovalWorkflowDomainValues.DisplayProductionHead, data.ProductionHeadBlock));
        }

        if (data.EnableArchiveRoomHead)
        {
            rowGroup.Rows.Add(CreateSingleRow(ApprovalWorkflowDomainValues.DisplayArchiveRoomHead, data.ArchiveRoomHeadBlock));
        }

        if (data.EnableArchiveDeputyPresident)
        {
            rowGroup.Rows.Add(CreateSingleRow(
                ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident,
                data.ArchiveDeputyPresidentBlock));
        }

        if (data.EnableProductionVicePresident)
        {
            rowGroup.Rows.Add(CreateSingleRow(
                ApprovalWorkflowDomainValues.DisplayProductionVicePresident,
                data.ProductionVicePresidentBlock));
        }

        rowGroup.Rows.Add(CreateSingleRow(
            "出网交接",
            data.HandoverSignatureBlock,
            HandoverRowHeight,
            CellVerticalAlignment.ContentTop));

        document.Blocks.Add(CreateMainTable(rowGroup));
        document.Blocks.Add(CreateFooterParagraph(data));

        return document;
    }

    private static double CalculateItemDetailRowHeight(
        NetworkOutboundPrintData data,
        double reasonRowHeight,
        bool hasArchivePurpose,
        string itemDetailText)
    {
        ArgumentNullException.ThrowIfNull(data);

        int approvalCount = CountEnabledApprovalRows(data);
        // 固定行（不含具体资料明细）：申请人/年度/目的地/[库管]/证明 + 签批 + 申请说明 + 交接。
        int oneLineRows = 4 + (hasArchivePurpose ? 1 : 0) + approvalCount;
        double fixedContentHeight =
            StandardRowHeight * oneLineRows
            + reasonRowHeight
            + HandoverRowHeight;
        int fixedRowCount = oneLineRows + 2; // 申请说明 + 交接
        double fixedTableHeight = fixedContentHeight
            + PrintPageLayoutSupport.GetTableRowOuterHeightDip(0, CellPadding) * fixedRowCount
            + PrintPageLayoutSupport.EstimateTableBottomBorderHeightDip(fixedRowCount);

        string footerText = BuildFooterNoteText(data);
        double footerHeight = PrintPageLayoutSupport.EstimateNoteBlockHeightFromTextWithSafetyDip(
            footerText,
            PrintPageLayoutSupport.ContentWidthDip,
            fontSizeDip: 10,
            lineHeightDip: 16,
            topMarginDip: 8);
        double reservedHeight =
            PrintPageLayoutSupport.ApprovalFormTitleBlockHeightDip
            + HeaderInfoHeight
            + PrintPageLayoutSupport.ApprovalFormHeaderToTableGapDip
            + footerHeight
            + fixedTableHeight;
        double contentNeededHeight = PrintPageLayoutSupport.ResolveSpannedContentRowHeightDip(
            itemDetailText,
            DetailMaxLines);
        return PrintPageLayoutSupport.CalculateStretchRowHeightDip(
            reservedHeight,
            contentNeededHeight,
            CellPadding);
    }

    private static int CountEnabledApprovalRows(NetworkOutboundPrintData data)
    {
        int count = 0;
        if (data.EnableDeptHead)
        {
            count++;
        }

        if (data.EnableProductionHead)
        {
            count++;
        }

        if (data.EnableArchiveRoomHead)
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

    private static Block CreateTitleBlock() =>
        new Paragraph(new Run("河北省第三测绘院资料室年度资料出网申请审批单"))
        {
            FontFamily = TitleFont,
            FontSize = PrintPageLayoutSupport.ApprovalFormTitleFontSize,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = PrintPageLayoutSupport.ApprovalFormTitleMargin
        };

    private static FlowDocument CreateDocumentSkeleton()
    {
        var document = new FlowDocument
        {
            FontFamily = BodyFont,
            FontSize = BodyFontSize,
            LineHeight = PrintPageLayoutSupport.ApprovalFormLineHeightDip,
            ColumnWidth = double.PositiveInfinity
        };
        PrintPageLayoutSupport.ApplyA4MediumMargins(document);
        return document;
    }

    private static Table CreateHeaderTable(string leftText, string rightText)
    {
        var headerTable = new Table { Margin = PrintPageLayoutSupport.ApprovalFormHeaderTableMargin };
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

        PrintPageLayoutSupport.ApplyApprovalFormMainTableColumns(table);
        table.RowGroups.Add(rowGroup);

        return table;
    }

    private static Paragraph CreateFooterParagraph(NetworkOutboundPrintData data)
    {
        var footer = new Paragraph
        {
            FontSize = 10,
            Margin = new Thickness(0, 8, 0, 0),
            LineHeight = 16
        };

        footer.Inlines.Add(new Run("备注：") { FontWeight = FontWeights.Bold });

        int noteIndex = 1;
        if (data.HasPendingItemDetailCapture)
        {
            footer.Inlines.Add(new Run($"{noteIndex}、本单子项资料的目录、数据量等具体信息尚未录入，资料室办理前需从离线拷贝介质读取并补录。\n"));
            noteIndex++;
        }

        footer.Inlines.Add(new Run($"      {noteIndex}、申请提交后，按“线上申请、打印表单、线下审批签字、上传签批单、确认出网交接”的流程办理。\n"));
        noteIndex++;
        footer.Inlines.Add(new Run($"      {noteIndex}、签字后的审批单应回传系统，作为办理依据和归档附件。\n"));
        noteIndex++;
        footer.Inlines.Add(new Run($"      {noteIndex}、本申请单已累计打印 {data.PrintCount + 1} 次，最新打印请与系统记录核对。"));

        return footer;
    }

    /// <summary>与 <see cref="CreateFooterParagraph"/> 渲染文案一致，供表后说明估高。</summary>
    private static string BuildFooterNoteText(NetworkOutboundPrintData data)
    {
        var sb = new System.Text.StringBuilder("备注：");
        int noteIndex = 1;
        if (data.HasPendingItemDetailCapture)
        {
            sb.Append($"{noteIndex}、本单子项资料的目录、数据量等具体信息尚未录入，资料室办理前需从离线拷贝介质读取并补录。\n");
            noteIndex++;
        }

        sb.Append($"      {noteIndex}、申请提交后，按“线上申请、打印表单、线下审批签字、上传签批单、确认出网交接”的流程办理。\n");
        noteIndex++;
        sb.Append($"      {noteIndex}、签字后的审批单应回传系统，作为办理依据和归档附件。\n");
        noteIndex++;
        sb.Append($"      {noteIndex}、本申请单已累计打印 {data.PrintCount + 1} 次，最新打印请与系统记录核对。");
        return sb.ToString();
    }

    private static string BuildItemText(NetworkOutboundPrintData data) =>
        data.ItemLines.Count > 0 ? string.Join("\n", data.ItemLines) : "(无)";

    private static TableRow CreateSingleRow(
        string label,
        string content,
        double? rowHeight = null,
        CellVerticalAlignment verticalAlignment = CellVerticalAlignment.SingleLineCenter)
    {
        double height = rowHeight ?? StandardRowHeight;
        var row = new TableRow();
        row.Cells.Add(CreateLabelCell(label, height, verticalAlignment));
        row.Cells.Add(CreateContentCell(content, 3, height, verticalAlignment));
        return row;
    }

    private static TableRow CreateDoubleRow(string label1, string content1, string label2, string content2)
    {
        var row = new TableRow();
        row.Cells.Add(CreateLabelCell(label1, StandardRowHeight, CellVerticalAlignment.SingleLineCenter));
        row.Cells.Add(CreateContentCell(content1, 1, StandardRowHeight, CellVerticalAlignment.SingleLineCenter));
        row.Cells.Add(CreateLabelCell(label2, StandardRowHeight, CellVerticalAlignment.SingleLineCenter));
        row.Cells.Add(CreateContentCell(content2, 1, StandardRowHeight, CellVerticalAlignment.SingleLineCenter));
        return row;
    }

    private static TableCell CreateLabelCell(string label, double rowHeight, CellVerticalAlignment verticalAlignment) =>
        new(CreateCellContent(label, rowHeight, verticalAlignment, label: true))
        {
            BorderThickness = new Thickness(0, 0, 1, 1),
            BorderBrush = Brushes.Black,
            Padding = new Thickness(CellPadding)
        };

    private static TableCell CreateContentCell(
        string content,
        int columnSpan,
        double rowHeight,
        CellVerticalAlignment verticalAlignment) =>
        new(CreateCellContent(content, rowHeight, verticalAlignment, label: false))
        {
            ColumnSpan = columnSpan,
            BorderThickness = new Thickness(0, 0, 1, 1),
            BorderBrush = Brushes.Black,
            Padding = new Thickness(CellPadding)
        };

    private static Block CreateCellContent(
        string text,
        double rowHeight,
        CellVerticalAlignment verticalAlignment,
        bool label)
    {
        var grid = new Grid { Height = rowHeight };
        grid.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = label ? TextWrapping.NoWrap : TextWrapping.Wrap,
            VerticalAlignment = ToVerticalAlignment(verticalAlignment),
            HorizontalAlignment = label ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            TextAlignment = label ? TextAlignment.Center : TextAlignment.Left,
            FontFamily = label ? LabelFont : BodyFont,
            FontWeight = label ? FontWeights.Bold : FontWeights.Normal,
            FontSize = BodyFontSize
        });

        return new BlockUIContainer(grid);
    }

    private static VerticalAlignment ToVerticalAlignment(CellVerticalAlignment verticalAlignment) =>
        verticalAlignment == CellVerticalAlignment.ContentTop
            ? VerticalAlignment.Top
            : VerticalAlignment.Center;

    private static string EmptyAsPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "(无)" : value.Trim();

    private enum CellVerticalAlignment
    {
        SingleLineCenter,
        ContentTop
    }
}
