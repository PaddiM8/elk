using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Elk.ReadLine.Render;

class SelectionListing
{
    public int SelectedIndex { get; set; }

    private readonly IRenderer _renderer;
    private IList<string> _items = Array.Empty<string>();
    private int _maxLength;
    private int _lastBottomRowIndex;

    public SelectionListing(IRenderer renderer)
    {
        _renderer = renderer;
    }

    public void LoadItems(IList<string> items)
    {
        _items = items;
        _maxLength = items.Max(x => x.GetWcLength());
    }

    public void Clear()
    {
        _items = Array.Empty<string>();
        _maxLength = 0;
        SelectedIndex = 0;

        var diff = _lastBottomRowIndex -_renderer.CursorTop;
        if (diff > 0)
        {
            var clearLines = string.Join(
                "\n",
                Enumerable.Repeat("\x1b[K", diff)
            );
            _renderer.WriteLinesOutside(clearLines, diff, 0);
        }

        _lastBottomRowIndex = 0;
    }

    public void Render()
    {
        if (_items.Count <= 1)
            return;

        const string margin = "   ";
        var columnCount = Math.Min(
            _items.Count,
            _renderer.BufferWidth / (_maxLength + margin.Length)
        );
        columnCount = Math.Max(1, Math.Min(5, columnCount));

        const int maxRowCount = 5;
        var startRow = (int)((float)SelectedIndex / columnCount / maxRowCount) * maxRowCount;
        var rowCount = Math.Min(
            maxRowCount,
            (int)Math.Ceiling((float)_items.Count / columnCount - startRow)
        );
        var endRow = startRow + rowCount;

        var columnWidths = new int[columnCount];
        for (var i = startRow; i < endRow; i++)
        {
            for (var j = 0; j < columnCount; j++)
            {
                var index = i * columnCount + j;
                if (index >= _items.Count)
                    continue;

                var length = _items[index].GetWcLength();
                if (length > columnWidths[j])
                    columnWidths[j] = length;
            }
        }

        var output = new StringBuilder();
        for (var i = startRow; i < endRow; i++)
        {
            for (var j = 0; j < columnCount; j++)
            {
                var index = i * columnCount + j;
                if (index >= _items.Count)
                {
                    output.Append(new string(' ', columnWidths[j]) + margin);
                    continue;
                }

                if (j != 0 && columnCount > 1)
                    output.Append(margin);

                var content = _items[i * columnCount + j].WcTruncate(_renderer.BufferWidth);
                var padding = new string(' ', columnWidths[j] - content.GetWcLength());
                if (index == SelectedIndex)
                    content = $"\x1b[107m\x1b[30m{content}\x1b[0m";

                output.Append($"{content}{padding}\x1b[K");
            }

            if (i < endRow - 1)
                output.AppendLine();
        }

        var lineLength = Math.Min(
            _renderer.BufferWidth,
            columnWidths.Sum() + (columnCount - 1) * margin.Length
        );
        var bottomRowIndex = _renderer.CursorTop + rowCount;
        if (_lastBottomRowIndex > bottomRowIndex)
        {
            var difference = _lastBottomRowIndex - bottomRowIndex;
            var clearLines = string.Join("\n", Enumerable.Repeat("\x1b[K", difference));
            output.Append($"\n{clearLines}");
            rowCount += difference;
            lineLength = 0;
        }

        _renderer.WriteLinesOutside(output.ToString(), rowCount, lineLength);
        _lastBottomRowIndex = _renderer.CursorTop + rowCount;
    }
}