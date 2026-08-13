using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.Shared;

namespace DocMgr.ViewModels.NetworkTransfer;

internal static class NetworkOutboundPrintDocumentFactory
{
    private static readonly FontFamily TitleFont = new("SimHei");
    private static readonly FontFamily LabelFont = new("SimHei");
    private static readonly FontFamily BodyFont = new("SimSun");

    private const double TitleBlockHeight = 48;
    private const double HeaderInfoHeight = 28;
    private const double StandardRowHeight = 32;
    private const double ReasonRowHeight = 38;
    /// <summary>出网交接栏：固定 2 行行高。</summary>
    private const double HandoverRowHeight = StandardRowHeight * 2;
    private const double CellPadding = 4;
    private const double BodyFontSize = 12;

    internal static FlowDocument Create(NetworkOutboundPrintData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        double itemDetailRowHeight = CalculateItemDetailRowHeight();

        var document = CreateDocumentSkeleton();
        document.Blocks.Add(CreateTitleBlock());
        document.Blocks.Add(CreateHeaderTable(
            $"出网单编号：{data.OutboundNo}",
            $"申请日期：{data.ApplyDateText}"));

        var rowGroup = new TableRowGroup();
        rowGroup.Rows.Add(CreateDoubleRow("申请部门", data.ApplicantDept, "申请人", data.ApplicantName));
        rowGroup.Rows.Add(CreateDoubleRow("年度", EmptyAsPlaceholder(data.YearText), "项目", EmptyAsPlaceholder(data.ProjectName)));
        rowGroup.Rows.Add(CreateSingleRow("目的地", EmptyAsPlaceholder(data.DestinationKindText)));
        rowGroup.Rows.Add(CreateSingleRow("申请说明", EmptyAsPlaceholder(data.Reason), ReasonRowHeight, CellVerticalAlignment.ContentTop));
        rowGroup.Rows.Add(CreateSingleRow("证明材料名称", EmptyAsPlaceholder(data.ProofMaterialNote)));
        rowGroup.Rows.Add(CreateSingleRow(
            "具体资料明细",
            BuildItemText(data),
            itemDetailRowHeight,
            CellVerticalAlignment.ContentTop));
        rowGroup.Rows.Add(CreateSingleRow("申请部门", data.DeptLeaderBlock));
        rowGroup.Rows.Add(CreateSingleRow("生产管理科", data.ProdLeaderBlock));
        rowGroup.Rows.Add(CreateSingleRow("资料室", data.RndLeaderBlock));
        rowGroup.Rows.Add(CreateSingleRow("分管领导", data.DeputyLeaderBlock));
        rowGroup.Rows.Add(CreateSingleRow(
            "出网交接",
            data.HandoverSignatureBlock,
            HandoverRowHeight,
            CellVerticalAlignment.ContentTop));

        document.Blocks.Add(CreateMainTable(rowGroup));
        document.Blocks.Add(CreateFooterParagraph(data));

        return document;
    }

    private static double CalculateItemDetailRowHeight()
    {
        double fixedTableHeight =
            PrintPageLayoutSupport.GetTableRowOuterHeightDip(StandardRowHeight, CellPadding) * 8
            + PrintPageLayoutSupport.GetTableRowOuterHeightDip(ReasonRowHeight, CellPadding)
            + PrintPageLayoutSupport.GetTableRowOuterHeightDip(HandoverRowHeight, CellPadding);
        double footerHeight = PrintPageLayoutSupport.EstimateNoteBlockHeightDip(lineCount: 3, lineHeightDip: 16, topMarginDip: 8);
        double reservedHeight = TitleBlockHeight + HeaderInfoHeight + footerHeight + fixedTableHeight;
        return PrintPageLayoutSupport.CalculateStretchRowHeightDip(
            reservedHeight,
            StandardRowHeight * 4,
            CellPadding);
    }

    private static Block CreateTitleBlock() =>
        new Paragraph(new Run("河北省第三测绘院资料室年度资料出网申请审批单"))
        {
            FontFamily = TitleFont,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };

    private static FlowDocument CreateDocumentSkeleton()
    {
        var document = new FlowDocument
        {
            FontFamily = BodyFont,
            FontSize = BodyFontSize,
            LineHeight = 18,
            ColumnWidth = double.PositiveInfinity
        };
        PrintPageLayoutSupport.ApplyA4MediumMargins(document);
        return document;
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

    private static Paragraph CreateFooterParagraph(NetworkOutboundPrintData data)
    {
        var footer = new Paragraph
        {
            FontSize = 10,
            Margin = new Thickness(0, 8, 0, 0),
            LineHeight = 16
        };

        footer.Inlines.Add(new Run("备注：") { FontWeight = FontWeights.Bold });
        footer.Inlines.Add(new Run("1、申请提交后，按“线上申请、打印表单、线下审批签字、上传签批单、确认出网交接”的流程办理。\n"));
        footer.Inlines.Add(new Run("      2、签字后的审批单应回传系统，作为办理依据和归档附件。\n"));
        footer.Inlines.Add(new Run($"      3、本申请单已累计打印 {data.PrintCount + 1} 次，最新打印请与系统记录核对。"));

        return footer;
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
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = ToVerticalAlignment(verticalAlignment),
            HorizontalAlignment = label ? HorizontalAlignment.Left : HorizontalAlignment.Left,
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
