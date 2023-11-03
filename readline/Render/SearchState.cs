using System;
using System.Linq;
using System.Text;
using Elk.ReadLine.Render.Formatting;

namespace Elk.ReadLine.Render;

class SearchState
{
    public bool IsActive { get; private set; }

    private const string Prefix = "search: ";
    private readonly IRenderer _renderer;
    private readonly ISearchHandler _searchHandler;
    private readonly SearchListing _listing;
    private readonly StringBuilder _query = new();

    public SearchState(IRenderer renderer, ISearchHandler searchHandler, IHighlightHandler? highlightHandler)
    {
        _renderer = renderer;
        _searchHandler = searchHandler;
        _listing = new SearchListing(_renderer, highlightHandler);
        renderer.Add(_listing);
    }

    public bool Start()
    {
        IsActive = true;
        _listing.LoadItems(
            _searchHandler.Search(string.Empty).ToList()
        );
        _renderer.WriteRaw("\n");
        Render();
        _listing.IsActive = true;

        while (IsActive)
        {
            var enterPressed = HandleKey(Console.ReadKey(true));
            if (enterPressed)
            {
                IsActive = false;

                return true;
            }
        }

        _listing.IsActive = false;

        return false;
    }

    private void Render()
    {
        _listing.LoadItems(
            _searchHandler.Search(_query.ToString()).ToList()
        );
        _listing.Render();
        InsertSelected();
        _renderer.WriteRaw(
            Ansi.MoveToColumn(0),
            Prefix,
            _query.ToString(),
            Ansi.ClearToEndOfLine()
        );
    }

    private void InsertSelected()
    {
        FocusInputPrompt();
        _renderer.StartTransaction();
        _renderer.Text = _listing.SelectedItem
            .Replace("\x1b", "")
            .Replace("\n", " ");
        _renderer.EndTransaction();
        FocusSearchPrompt();
    }

    private void Clear()
    {
        IsActive = false;
        _renderer.WriteRaw(
            "\n",
            Ansi.MoveTo(_renderer.CursorTop, _renderer.PromptStartLeft + 1),
            Ansi.ClearToEndOfScreen()
        );
        _query.Clear();
    }

    private void FocusInputPrompt()
    {
        _renderer.WriteRaw(
            Ansi.MoveTo(
                _renderer.CursorTop,
                _renderer.PromptStartLeft + 1
            )
        );
    }

    private void FocusSearchPrompt()
    {
        _renderer.WriteRaw(
            Ansi.MoveTo(
                _renderer.CursorTop + 2,
                Prefix.Length + _query.Length + 1
            )
        );
    }

    private bool HandleKey(ConsoleKeyInfo key)
    {
        if (key.Modifiers != ConsoleModifiers.None)
            return false;

        if (key.Key == ConsoleKey.Escape)
        {
            Clear();
            _renderer.Text = "";

            return false;
        }

        if (key.Key == ConsoleKey.Enter)
        {
            Clear();

            return true;
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (_query.Length == 0)
                return false;

            _query.Remove(_query.Length - 1, 1);
            Render();

            return false;
        }

        if (key.Key == ConsoleKey.UpArrow)
        {
            _listing.SelectPrevious();
            InsertSelected();

            return false;
        }

        if (key.Key == ConsoleKey.DownArrow)
        {
            _listing.SelectNext();
            InsertSelected();

            return false;
        }

        if (key.KeyChar != '\0')
        {
            _query.Append(key.KeyChar);
            Render();
        }

        return false;
    }
}