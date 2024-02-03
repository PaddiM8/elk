using System;
using System.Collections.Generic;
using System.Linq;
using Elk.ReadLine.Render.Formatting;

namespace Elk.ReadLine.Render;

class SearchListing(IRenderer renderer, IHighlightHandler? highlightHandler)
{
    public string SelectedItem
        => _items.ElementAtOrDefault(_selectedIndex) ?? "";

    private IList<string> _items = Array.Empty<string>();
    private int _selectedIndex;

    public void LoadItems(IList<string> items)
    {
        _items = items;
        _selectedIndex = 0;
    }

    public void SelectNext()
    {
        _selectedIndex = _selectedIndex == _items.Count - 1
            ? 0
            : _selectedIndex + 1;
    }

    public void SelectPrevious()
    {
        _selectedIndex = _selectedIndex == 0
            ? _items.Count - 1
            : _selectedIndex - 1;
    }

    public void Render()
    {
        var formattedItems = _items.WithIndex().Select(x =>
        {
            var escaped = x.item
                .Replace("\t", "  ")
                .Replace("\n", " ")
                .Replace("\x1b", "");
            const string prefix = "❯ ";
            var truncated = escaped.WcTruncate(renderer.WindowWidth - prefix.Length);
            var highlighted = x.index == _selectedIndex
                ? Ansi.Format(prefix + truncated, AnsiForeground.Black, AnsiBackground.White)
                : prefix + (highlightHandler?.Highlight(truncated, renderer.Caret) ?? truncated);

            return highlighted + Ansi.ClearToEndOfLine();
        });
        var minShownItems = Math.Min(12, _items.Count);
        var height = Math.Max(
            renderer.WindowHeight - renderer.CursorTop - 2,
            Math.Min(minShownItems, renderer.WindowHeight - 2)
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
        renderer.WriteLinesOutside(
            string.Join("\n", renderedItems.Concat(bottomPadding)),
            height,
            length
        );
    }
}