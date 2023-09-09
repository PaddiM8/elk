using System;
using System.Collections.Generic;
using System.Linq;

namespace Elk.ReadLine.Render;

class CompletionState
{
    public bool IsActive => _completions.Count > 0;

    private readonly IRenderer _renderer;
    private readonly SelectionListing _listing;
    private IList<Completion> _completions = Array.Empty<Completion>();
    private int _completionStart;

    public CompletionState(IRenderer renderer)
    {
        _renderer = renderer;
        _listing = new SelectionListing(renderer);
    }

    public void StartNew(IList<Completion> completions, int completionStart)
    {
        _completions = completions;
        _completionStart = completionStart;
        _listing.Clear();
        _listing.LoadItems(completions.Select(x => x.DisplayText).ToList());
        _listing.SelectedIndex = 0;
        InsertCompletion();

        if (completions.Count == 1)
        {
            Reset();
        }
        else
        {
            _listing.Render();
        }
    }

    public void Reset()
    {
        _completions = Array.Empty<Completion>();
        _listing.Clear();
    }

    public void Next()
    {

        if (_listing.SelectedIndex >= _completions.Count - 1)
        {
            _listing.SelectedIndex = 0;
        }
        else
        {
            _listing.SelectedIndex++;
        }

        InsertCompletion();
        _listing.Render();
    }

    public void Previous()
    {

        if (_listing.SelectedIndex == 0)
        {
            _listing.SelectedIndex = _completions.Count - 1;
        }
        else
        {
            _listing.SelectedIndex--;
        }

        InsertCompletion();
        _listing.Render();
    }

    private void InsertCompletion()
    {
        _renderer.RemoveLeft(_renderer.Caret - _completionStart);
        _renderer.CaretVisible = false;
        _renderer.Insert(_completions[_listing.SelectedIndex].CompletionText, includeHint: false);
    }
}