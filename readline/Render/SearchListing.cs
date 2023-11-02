using System;
using System.Collections.Generic;
using System.Linq;
using Elk.ReadLine.Render.Formatting;

namespace Elk.ReadLine.Render;

class SearchListing : IRenderable
{
    public bool IsActive { get; set; }

    public string SelectedItem
        => _items[_selectedIndex];

    private readonly IRenderer _renderer;
    private readonly IHighlightHandler? _highlightHandler;
    private IList<string> _items = Array.Empty<string>();
    private int _selectedIndex;

    public SearchListing(IRenderer renderer, IHighlightHandler? highlightHandler)
    {
        _renderer = renderer;
        _highlightHandler = highlightHandler;
    }

    public void LoadItems(IList<string> items)
    {
        _items = items;
        _selectedIndex = 0;
    }

    public void SelectNext()
    {
        var cursorLeft = _renderer.CursorLeft;
        _selectedIndex = _selectedIndex == _items.Count - 1
            ? 0
            : _selectedIndex + 1;
        Render();
        _renderer.WriteRaw(Ansi.MoveToColumn(cursorLeft + 1));
    }

    public void SelectPrevious()
    {
        var cursorLeft = _renderer.CursorLeft;
        _selectedIndex = _selectedIndex == 0
            ? _items.Count - 1
            : _selectedIndex - 1;
        Render();
        _renderer.WriteRaw(Ansi.MoveToColumn(cursorLeft + 1));
    }

    public void Render()
    {
        if (!IsActive)
            return;

        var formattedItems = _items.WithIndex().Select(x =>
        {
            var escaped = x.item
                .Replace("\t", "  ")
                .Replace("\n", " ")
                .Replace("\x1b", "");
            var truncated = escaped.WcTruncate(_renderer.BufferHeight);
            var highlighted = x.index == _selectedIndex
                ? Ansi.Color("❯ " + truncated, AnsiForeground.Black, AnsiBackground.White)
                : "❯ " + (_highlightHandler?.Highlight(truncated, _renderer.Caret) ?? truncated);

            return highlighted + Ansi.ClearToEndOfLine();
        });
        var minShownItems = Math.Min(12, _items.Count);
        var height = Math.Max(
            _renderer.WindowHeight - _renderer.CursorTop - 1,
            Math.Min(minShownItems, _renderer.WindowHeight - 2)
        );
        var chunkIndex = _selectedIndex / height;
        IList<string>? renderedItems = formattedItems
            .Chunk(height)
            .ElementAtOrDefault(chunkIndex)?
            .ToList();
        if (renderedItems == null)
            renderedItems = Array.Empty<string>();

        var length = renderedItems.Any()
            ? renderedItems.Select(x => x.GetWcLength()).Max()
            : 0;
        var bottomPadding = Enumerable.Repeat(
            Ansi.ClearToEndOfLine(),
            height - renderedItems.Count
        );
        _renderer.WriteLinesOutside(
            string.Join("\n", renderedItems.Concat(bottomPadding)),
            height,
            length
        );
    }
}