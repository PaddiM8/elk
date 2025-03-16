using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elk.ReadLine.Render.Formatting;

namespace Elk.ReadLine.Render;

class SelectionListing(IRenderer renderer)
{
    public int SelectedIndex { get; set; }

    private IList<Completion> _items = Array.Empty<Completion>();
    private int _maxLength;
    private bool _hasDescriptions;
    private int _lastBottomRowIndex;
    private int _lastHeight;
    private const string ItemMargin = "   ";
    private const string DescriptionMargin = "  ";

    public void LoadItems(IList<Completion> items)
    {
        _items = items;
        _lastBottomRowIndex = 0;
        _lastHeight = 0;
        _hasDescriptions = items.Any(x => x.Description != null);
        var allLengths = items
            .Select(x =>
                x.Description == null
                    ? x.DisplayText.GetWcLength()
                    : x.DisplayText.GetWcLength() + DescriptionMargin.Length + x.Description.GetWcLength()
            )
            .Order()
            .ToList();
        var typicalLength = allLengths[(int)(allLengths.Count * 0.75)];
        _maxLength = typicalLength < 20
            ? allLengths.Max()
            : typicalLength;
    }

    public void Clear()
    {
        _items = Array.Empty<Completion>();
        _maxLength = 0;
        _hasDescriptions = false;
        _lastBottomRowIndex = 0;
        SelectedIndex = 0;

        var pos = renderer.Caret;
        renderer.StartTransaction();
        renderer.Caret = renderer.Text.Length;
        renderer.WriteRaw(Ansi.ClearToEndOfScreen());
        renderer.Caret = pos;
        renderer.EndTransaction();
    }

    public void Render()
    {
        var currentMaxLength = Math.Min(_maxLength, renderer.WindowWidth);
        var columnCount = Math.Min(
            _items.Count,
            renderer.WindowWidth / (currentMaxLength + ItemMargin.Length)
        );
        columnCount = Math.Max(1, Math.Min(5, columnCount));

        if (_hasDescriptions)
            columnCount = 1;

        if (columnCount == 1)
            currentMaxLength = renderer.WindowWidth;

        var maxRowCount = Math.Max(
            7,
            renderer.WindowHeight - renderer.CursorTop - 1
        );
        var startRow = (int)((float)SelectedIndex / columnCount / maxRowCount) * maxRowCount;
        var rowCount = Math.Min(
            maxRowCount,
            (int)Math.Ceiling((float)_items.Count / columnCount - startRow)
        );
        var endRow = startRow + rowCount;
        var columnWidths = GetColumnWidths(columnCount, startRow, endRow, currentMaxLength);

        var output = new StringBuilder();
        for (var rowIndex = startRow; rowIndex < endRow; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                output.Append(
                    RenderColumn(columnWidths, rowIndex, columnIndex)
                );
            }

            if (rowIndex < endRow - 1)
                output.AppendLine();
        }

        var lineLength = Math.Min(
            renderer.WindowWidth - 1,
            columnWidths.Sum() + (columnCount - 1) * ItemMargin.Length
        );
        var bottomRowIndex = renderer.CursorTop + rowCount;
        if (_lastBottomRowIndex > bottomRowIndex && _lastHeight <= renderer.WindowHeight)
        {
            var difference = _lastBottomRowIndex - bottomRowIndex;
            var clearLines = string.Join(
                "",
                Enumerable.Repeat(Environment.NewLine + Ansi.ClearToEndOfLine(), difference)
            );
            output.Append(clearLines);
            rowCount += difference;
            lineLength = 0;
        }

        renderer.WriteLinesOutside(output.ToString(), rowCount, lineLength);
        _lastHeight = renderer.WindowHeight;
        _lastBottomRowIndex = renderer.CursorTop + rowCount;
    }

    private string RenderColumn(int[] columnWidths, int rowIndex, int columnIndex)
    {
        var columnCount = columnWidths.Length;
        var columnWidth = Math.Min(
            renderer.WindowWidth,
            columnWidths[columnIndex]
        );
        var index = rowIndex * columnCount + columnIndex;
        if (index >= _items.Count)
            return new string(' ', columnWidth) + ItemMargin;

        // Main text
        var margin = columnIndex != 0 && columnCount > 1
            ? ItemMargin
            : "";
        var item = _items[rowIndex * columnCount + columnIndex];
        var content = item.DisplayText.WcTruncate(columnWidth);

        // Description
        var truncatedDescription = item.Description?.WcTruncate(
            columnWidth - content.GetWcLength() - DescriptionMargin.Length
        );
        var formattedDescription = "";
        if (truncatedDescription != null)
        {
            content += DescriptionMargin;
            formattedDescription = Ansi.Format(truncatedDescription, AnsiForeground.Gray);
        }

        // Padding
        var itemLength = content.GetWcLength() + (truncatedDescription?.GetWcLength() ?? 0);
        var padding = new string(' ', Math.Max(0, columnWidth - itemLength));
        if (itemLength == renderer.WindowWidth)
            padding = "";

        // Selection colors
        if (index == SelectedIndex)
        {
            content = Ansi.Format(content, AnsiForeground.Black, AnsiBackground.White);
            if (truncatedDescription != null)
                formattedDescription = Ansi.Format(truncatedDescription, AnsiForeground.Black, AnsiBackground.White);
        }

        return margin + content + formattedDescription + padding + Ansi.ClearToEndOfLine();
    }

    private int[] GetColumnWidths(int columnCount, int startRow, int endRow, int maxLength)
    {
        var columnWidths = new int[columnCount];
        for (var i = startRow; i < endRow; i++)
        {
            for (var j = 0; j < columnCount; j++)
            {
                var index = i * columnCount + j;
                if (index >= _items.Count)
                    continue;

                var item = _items[index];
                var length = Math.Min(maxLength, item.DisplayText.GetWcLength());
                if (item.Description != null)
                    length += item.Description.GetWcLength() + DescriptionMargin.Length;

                if (length > columnWidths[j])
                    columnWidths[j] = length;
            }
        }

        return columnWidths;
    }
}