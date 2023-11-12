using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Elk.ReadLine.Render;

class CompletionState : IRenderable
{
    public bool IsActive { get; set; }

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
        _renderer.RemoveLeft(_renderer.Caret - _completionStart, render: false);
        _renderer.Insert(
            _completions[_listing.SelectedIndex].CompletionText,
            includeHint: false
        );
    }

}