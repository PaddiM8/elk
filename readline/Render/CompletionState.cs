using System;
using System.Collections.Generic;
using System.Linq;

namespace Elk.ReadLine.Render;

class CompletionState(IRenderer renderer) : IRenderable
{
    public bool IsActive { get; set; }

    private readonly SelectionListing _listing = new(renderer);
    private IList<Completion> _completions = Array.Empty<Completion>();
    private int _completionStart;

    public void StartNew(IList<Completion> completions, int completionStart)
    {
        _completions = completions;
        _completionStart = completionStart;
        IsActive = completions.Count > 0;

        _listing.Clear();
        _listing.LoadItems(completions.ToList());
        _listing.SelectedIndex = 0;
        InsertCompletion();

        if (completions.Count == 1)
        {
            Reset();
        }
        else
        {
            Render();
        }
    }

    public void Render()
    {
        if (!IsActive || _completions.Count <= 1)
            return;

        _listing.Render();
    }

    public void Reset()
    {
        _completions = Array.Empty<Completion>();
        _listing.Clear();
        IsActive = false;
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
        renderer.StartTransaction();
        renderer.RemoveLeft(renderer.Caret - _completionStart);
        var completion = _completions[_listing.SelectedIndex];
        var trailingSpace = completion.HasTrailingSpace && renderer.IsEndOfLine
            ? " "
            : "";
        var input = completion.CompletionText + trailingSpace;
        renderer.Insert(
            input,
            includeHint: false
        );
        renderer.EndTransaction();
    }

}