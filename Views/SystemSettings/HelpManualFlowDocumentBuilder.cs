using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DocMgr.Views.SystemSettings;

/// <summary>
/// 将操作手册 Markdown 转为阅读用 <see cref="FlowDocument"/>（标题、列表、引用、表格、代码块）。
/// </summary>
internal static class HelpManualFlowDocumentBuilder
{
    private static readonly Brush TitleBrush = Freeze(Color.FromRgb(0x0F, 0x17, 0x2A));
    private static readonly Brush HeadingBrush = Freeze(Color.FromRgb(0x0F, 0x6B, 0x63));
    private static readonly Brush BodyBrush = Freeze(Color.FromRgb(0x33, 0x41, 0x55));
    private static readonly Brush MutedBrush = Freeze(Color.FromRgb(0x64, 0x74, 0x8B));
    private static readonly Brush QuoteBg = Freeze(Color.FromRgb(0xE8, 0xF5, 0xF3));
    private static readonly Brush QuoteBorder = Freeze(Color.FromRgb(0x0F, 0x6B, 0x63));
    private static readonly Brush CodeBg = Freeze(Color.FromRgb(0xF1, 0xF5, 0xF9));
    private static readonly Brush TableHeaderBg = Freeze(Color.FromRgb(0x0F, 0x6B, 0x63));
    private static readonly Brush TableAltBg = Freeze(Color.FromRgb(0xF8, 0xFA, 0xFC));
    private static readonly Brush TableBorder = Freeze(Color.FromRgb(0xE2, 0xE8, 0xF0));
    private static readonly Brush RuleBrush = Freeze(Color.FromRgb(0xE2, 0xE8, 0xF0));
    private static readonly FontFamily UiFont = new("Microsoft YaHei UI");
    private static readonly FontFamily MonoFont = new("Consolas");
    private static readonly Regex InlineRegex = new(@"\*\*(.+?)\*\*|`([^`]+)`", RegexOptions.Compiled);
    private static readonly Regex OrderedRegex = new(@"^(\d+)\.\s+(.*)$", RegexOptions.Compiled);

    /// <summary>
    /// 根据章节标题与 Markdown 正文生成阅读文档。
    /// </summary>
    public static FlowDocument Create(string title, string? markdown)
    {
        var document = new FlowDocument
        {
            FontFamily = UiFont,
            FontSize = 14,
            Foreground = BodyBrush,
            PagePadding = new Thickness(8, 2, 18, 12),
            ColumnWidth = 12000,
            LineHeight = 24,
            TextAlignment = TextAlignment.Left
        };

        document.Blocks.Add(new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 10),
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = TitleBrush,
            LineHeight = 30,
            Inlines = { new Run(title ?? string.Empty) }
        });

        ParseBlocks(document, markdown ?? string.Empty);
        return document;
    }

    private static void ParseBlocks(FlowDocument document, string markdown)
    {
        string[] lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int index = 0;
        while (index < lines.Length)
        {
            string trimmed = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                index++;
                continue;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                index = AddCodeBlock(document, lines, index);
                continue;
            }

            if (trimmed.StartsWith('|') && LooksLikeTable(lines, index))
            {
                index = AddTable(document, lines, index);
                continue;
            }

            if (IsRule(trimmed))
            {
                AddRule(document);
                index++;
                continue;
            }

            if (trimmed.StartsWith("### ", StringComparison.Ordinal))
            {
                AddHeading(document, trimmed[4..].Trim(), 3);
                index++;
                continue;
            }

            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                AddHeading(document, trimmed[3..].Trim(), 2);
                index++;
                continue;
            }

            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                index++;
                continue;
            }

            if (trimmed.StartsWith("> ", StringComparison.Ordinal) || trimmed == ">")
            {
                index = AddQuote(document, lines, index);
                continue;
            }

            if (IsBullet(trimmed))
            {
                index = AddList(document, lines, index, ordered: false);
                continue;
            }

            if (OrderedRegex.IsMatch(trimmed))
            {
                index = AddList(document, lines, index, ordered: true);
                continue;
            }

            index = AddParagraph(document, lines, index);
        }
    }

    private static bool IsBullet(string trimmed)
        => trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal);

    private static bool IsRule(string trimmed)
        => trimmed.StartsWith("---", StringComparison.Ordinal) || trimmed.StartsWith("----------", StringComparison.Ordinal);

    private static bool LooksLikeTable(string[] lines, int index)
    {
        if (index + 1 >= lines.Length)
        {
            return false;
        }

        string next = lines[index + 1].Trim();
        return next.StartsWith('|') && next.Contains("---", StringComparison.Ordinal);
    }

    private static void AddHeading(FlowDocument document, string text, int level)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, level == 2 ? 16 : 12, 0, 6),
            FontSize = level == 2 ? 17 : 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = HeadingBrush,
            LineHeight = 26
        };
        AddInlines(paragraph, text);
        document.Blocks.Add(paragraph);
    }

    private static int AddParagraph(FlowDocument document, string[] lines, int index)
    {
        var parts = new List<string>();
        while (index < lines.Length)
        {
            string trimmed = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || IsBlockStart(trimmed, lines, index))
            {
                break;
            }

            parts.Add(trimmed);
            index++;
        }

        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 10),
            LineHeight = 24
        };
        AddInlines(paragraph, string.Join(" ", parts));
        document.Blocks.Add(paragraph);
        return index;
    }

    private static bool IsBlockStart(string trimmed, string[] lines, int index)
        => trimmed.StartsWith("```", StringComparison.Ordinal)
           || trimmed.StartsWith("### ", StringComparison.Ordinal)
           || trimmed.StartsWith("## ", StringComparison.Ordinal)
           || trimmed.StartsWith("# ", StringComparison.Ordinal)
           || trimmed.StartsWith("> ", StringComparison.Ordinal)
           || IsBullet(trimmed)
           || OrderedRegex.IsMatch(trimmed)
           || IsRule(trimmed)
           || (trimmed.StartsWith('|') && LooksLikeTable(lines, index));

    private static int AddList(FlowDocument document, string[] lines, int index, bool ordered)
    {
        var list = new List
        {
            MarkerStyle = ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = new Thickness(8, 2, 0, 12),
            Padding = new Thickness(14, 0, 0, 0)
        };

        while (index < lines.Length)
        {
            string trimmed = lines[index].Trim();
            if (ordered)
            {
                Match match = OrderedRegex.Match(trimmed);
                if (!match.Success)
                {
                    break;
                }

                list.ListItems.Add(CreateListItem(match.Groups[2].Value.Trim()));
                index++;
                continue;
            }

            if (!IsBullet(trimmed))
            {
                break;
            }

            list.ListItems.Add(CreateListItem(trimmed[2..].Trim()));
            index++;
        }

        document.Blocks.Add(list);
        return index;
    }

    private static ListItem CreateListItem(string text)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 2, 0, 4), LineHeight = 22 };
        AddInlines(paragraph, text);
        return new ListItem(paragraph);
    }

    private static int AddQuote(FlowDocument document, string[] lines, int index)
    {
        var parts = new List<string>();
        while (index < lines.Length)
        {
            string trimmed = lines[index].Trim();
            if (trimmed.StartsWith("> ", StringComparison.Ordinal))
            {
                parts.Add(trimmed[2..]);
                index++;
                continue;
            }

            if (trimmed == ">")
            {
                index++;
                continue;
            }

            break;
        }

        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontFamily = UiFont,
            FontSize = 13.5,
            Foreground = HeadingBrush,
            LineHeight = 22
        };
        AddInlinesToTextBlock(textBlock, string.Join(Environment.NewLine, parts));

        var border = new Border
        {
            Background = QuoteBg,
            BorderBrush = QuoteBorder,
            BorderThickness = new Thickness(3, 0, 0, 0),
            CornerRadius = new CornerRadius(0, 8, 8, 0),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 4, 0, 12),
            Child = textBlock
        };
        document.Blocks.Add(new BlockUIContainer(border));
        return index;
    }

    private static int AddCodeBlock(FlowDocument document, string[] lines, int index)
    {
        index++;
        var parts = new List<string>();
        while (index < lines.Length && !lines[index].Trim().StartsWith("```", StringComparison.Ordinal))
        {
            parts.Add(lines[index]);
            index++;
        }

        if (index < lines.Length)
        {
            index++;
        }

        var textBox = new TextBox
        {
            Text = string.Join(Environment.NewLine, parts),
            Style = null,
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontFamily = MonoFont,
            FontSize = 12.5,
            Foreground = BodyBrush,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true
        };
        var border = new Border
        {
            Background = CodeBg,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 4, 0, 12),
            Child = textBox
        };
        document.Blocks.Add(new BlockUIContainer(border));
        return index;
    }

    private static int AddTable(FlowDocument document, string[] lines, int index)
    {
        var rows = new List<string[]>();
        while (index < lines.Length)
        {
            string trimmed = lines[index].Trim();
            if (!trimmed.StartsWith('|'))
            {
                break;
            }

            string[] cells = SplitTableRow(trimmed);
            bool separator = cells.Length > 0 && Array.TrueForAll(cells, cell => cell.Replace("-", string.Empty, StringComparison.Ordinal).Replace(":", string.Empty, StringComparison.Ordinal).Length == 0);
            if (!separator)
            {
                rows.Add(cells);
            }

            index++;
        }

        if (rows.Count == 0)
        {
            return index;
        }

        int columnCount = 0;
        foreach (string[] row in rows)
        {
            columnCount = Math.Max(columnCount, row.Length);
        }

        var table = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 4, 0, 14),
            BorderBrush = TableBorder,
            BorderThickness = new Thickness(1)
        };
        for (int column = 0; column < columnCount; column++)
        {
            table.Columns.Add(new TableColumn());
        }

        var group = new TableRowGroup();
        table.RowGroups.Add(group);
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var tableRow = new TableRow();
            bool header = rowIndex == 0;
            if (header)
            {
                tableRow.Background = TableHeaderBg;
            }
            else if (rowIndex % 2 == 0)
            {
                tableRow.Background = TableAltBg;
            }

            for (int column = 0; column < columnCount; column++)
            {
                string cellText = column < rows[rowIndex].Length ? rows[rowIndex][column] : string.Empty;
                var paragraph = new Paragraph
                {
                    Margin = new Thickness(0),
                    FontSize = 12.5,
                    LineHeight = 20,
                    Foreground = header ? Brushes.White : BodyBrush,
                    FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal
                };
                AddInlines(paragraph, cellText);
                tableRow.Cells.Add(new TableCell(paragraph)
                {
                    Padding = new Thickness(8, 6, 8, 6),
                    BorderBrush = TableBorder,
                    BorderThickness = new Thickness(0, 0, 1, 1)
                });
            }

            group.Rows.Add(tableRow);
        }

        document.Blocks.Add(table);
        return index;
    }

    private static string[] SplitTableRow(string trimmed)
    {
        string inner = trimmed.Trim('|');
        string[] parts = inner.Split('|');
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = parts[i].Trim();
        }

        return parts;
    }

    private static void AddRule(FlowDocument document)
    {
        document.Blocks.Add(new BlockUIContainer(new Border
        {
            Height = 1,
            Background = RuleBrush,
            Margin = new Thickness(0, 10, 0, 12)
        }));
    }

    private static void AddInlines(Paragraph paragraph, string text)
    {
        foreach (Inline inline in BuildInlines(text))
        {
            paragraph.Inlines.Add(inline);
        }
    }

    private static void AddInlinesToTextBlock(TextBlock textBlock, string text)
    {
        foreach (Inline inline in BuildInlines(text))
        {
            textBlock.Inlines.Add(inline);
        }
    }

    private static IEnumerable<Inline> BuildInlines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        int last = 0;
        foreach (Match match in InlineRegex.Matches(text))
        {
            if (match.Index > last)
            {
                yield return new Run(text[last..match.Index]);
            }

            if (match.Groups[1].Success)
            {
                yield return new Run(match.Groups[1].Value)
                {
                    FontWeight = FontWeights.SemiBold,
                    Foreground = HeadingBrush
                };
            }
            else
            {
                yield return new Run(match.Groups[2].Value)
                {
                    FontFamily = MonoFont,
                    FontSize = 12.5,
                    Background = CodeBg,
                    Foreground = BodyBrush
                };
            }

            last = match.Index + match.Length;
        }

        if (last < text.Length)
        {
            yield return new Run(text[last..]);
        }
    }

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }
}
