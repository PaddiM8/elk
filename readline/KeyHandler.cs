using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Elk.ReadLine.Render;

namespace Elk.ReadLine;

public readonly struct KeyPress(ConsoleModifiers modifiers, ConsoleKey key)
{
    public ConsoleModifiers Modifiers { get; } = modifiers;

    public ConsoleKey Key { get; } = key;

    public KeyPress(ConsoleKey key)
        : this(0, key)
    {
    }
}

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class KeyHandler
{
    public string Text
        => _renderer.Text;

    public char[] WordSeparators = { ' ' };

    internal IHistoryHandler? HistoryHandler { get; init; }

    internal IAutoCompleteHandler? AutoCompleteHandler { get; init; }

    internal IHintHandler? HintHandler
    {
        get => _hintHandler;

        set
        {
            _hintHandler = value;
            if (value == null)
            {
                _renderer.OnHint(null);
                return;
            }

            _renderer.OnHint(value.Hint);
        }
    }

    internal IHighlightHandler? HighlightHandler
    {
        get => _highlightHandler;

        set
        {
            _highlightHandler = value;
            if (value == null)
            {
                _renderer.OnHighlight(null);
                return;
            }

            _renderer.OnHighlight(value.Highlight);
        }
    }

    internal IEnterHandler? EnterHandler { get; init; }

    internal ISearchHandler? SearchHandler
    {
        get => _searchHandler;

        set
        {
            if (value != null)
            {
                _searchState = new SearchState(_renderer, value, _highlightHandler);
                _renderer.Add(_searchState);
            }

            _searchHandler = value;
        }
    }

    internal Action? OnEnter { get; init; }

    private IHighlightHandler? _highlightHandler;
    private IHintHandler? _hintHandler;
    private ISearchHandler? _searchHandler;
    private bool _wasEdited;
    private readonly Dictionary<KeyPress, Action> _defaultShortcuts;
    private readonly ShortcutBag? _shortcuts;
    private readonly IRenderer _renderer;
    private readonly CompletionState _completionState;
    private SearchState? _searchState;

    internal KeyHandler(IRenderer renderer, ShortcutBag? shortcuts)
    {
        _renderer = renderer;
        _completionState = new CompletionState(renderer);
        _renderer.Add(_completionState);

        _shortcuts = shortcuts;
        _defaultShortcuts = new Dictionary<KeyPress, Action>
        {
            [new(ConsoleKey.LeftArrow)] = MoveCursorLeft,
            [new(ConsoleKey.RightArrow)] = MoveCursorRight,
            [new(ConsoleModifiers.Control, ConsoleKey.LeftArrow)] = MoveCursorWordLeft,
            [new(ConsoleModifiers.Control, ConsoleKey.RightArrow)] = MoveCursorWordRight,
            [new(ConsoleKey.UpArrow)] = UpArrow,
            [new(ConsoleKey.DownArrow)] = DownArrow,
            [new(ConsoleKey.Home)] = MoveCursorHome,
            [new(ConsoleKey.End)] = MoveCursorEnd,
            [new(ConsoleKey.Backspace)] = Backspace,
            [new(ConsoleKey.Delete)] = Delete,
            [new(ConsoleModifiers.Alt, ConsoleKey.Backspace)] = RemoveWordLeft,
            [new(ConsoleModifiers.Control, ConsoleKey.Backspace)] = RemoveWordLeft,
            [new(ConsoleModifiers.Control, ConsoleKey.A)] = MoveCursorHome,
            [new(ConsoleModifiers.Control, ConsoleKey.B)] = MoveCursorLeft,
            [new(ConsoleModifiers.Control, ConsoleKey.D)] = Delete,
            [new(ConsoleModifiers.Control, ConsoleKey.E)] = MoveCursorEnd,
            [new(ConsoleModifiers.Control, ConsoleKey.F)] = MoveCursorRight,
            [new(ConsoleModifiers.Control, ConsoleKey.H)] = Backspace,
            [new(ConsoleModifiers.Control, ConsoleKey.K)] = RemoveToEnd,
            [new(ConsoleModifiers.Control, ConsoleKey.L)] = ClearConsole,
            [new(ConsoleModifiers.Control, ConsoleKey.N)] = DownArrow,
            [new(ConsoleModifiers.Control, ConsoleKey.P)] = UpArrow,
            [new(ConsoleModifiers.Control, ConsoleKey.R)] = Search,
            [new(ConsoleModifiers.Control, ConsoleKey.T)] = TransposeChars,
            [new(ConsoleModifiers.Control, ConsoleKey.U)] = RemoveToHome,
            [new(ConsoleModifiers.Control, ConsoleKey.W)] = RemoveWordLeft,
            [new(ConsoleKey.Tab)] = NextAutoComplete,
            [new(ConsoleModifiers.Shift, ConsoleKey.Tab)] = PreviousAutoComplete,
        };
    }

    internal void Handle(ConsoleKeyInfo firstKey, string? remaining)
    {
        if (_completionState.IsActive && firstKey.Key != ConsoleKey.Tab)
            _completionState.Reset();

        if (OnEnter != null && firstKey.Key == ConsoleKey.Enter)
        {
            var enterHandlerResponse = EnterHandler?.Handle(_renderer.Text, _renderer.Caret);
            if (enterHandlerResponse is { WasHandled: true, NewPromptText: not null })
            {
                _renderer.Text = enterHandlerResponse.NewPromptText;
                _renderer.RenderText();
                if (enterHandlerResponse.NewCaretPosition.HasValue)
                    _renderer.Caret = enterHandlerResponse.NewCaretPosition.Value;
            }
            else
            {
                // Re-render without any potential hints
                _renderer.RenderText();
                HintHandler?.Reset();
                _renderer.WriteRaw(Environment.NewLine);
                _wasEdited = false;
                OnEnter();
            }

            return;
        }

        if (_shortcuts?.TryGetValue(new KeyPress(firstKey.Modifiers, firstKey.Key), out var action1) ?? false)
        {
            action1?.Invoke(this);

            return;
        }

        if (_defaultShortcuts.TryGetValue(new KeyPress(firstKey.Modifiers, firstKey.Key), out var action2))
        {
            action2.Invoke();

            return;
        }

        _wasEdited = true;

        if (firstKey.KeyChar != '\0' && firstKey.Key != ConsoleKey.Escape)
            WriteChar(firstKey.KeyChar);

        if (remaining != null)
            _renderer.Insert(remaining, includeHint: false);
    }

    public void AbortInput()
    {
        _renderer.WriteRaw(Environment.NewLine);
        _renderer.RenderPrompt();
        _renderer.Text = "";
    }

    public void Backspace()
    {
        _renderer.RemoveLeft(1);
    }

    public void ClearConsole()
    {
        _renderer.Clear();
        OnEnter?.Invoke();
    }

    public void Delete()
    {
        _renderer.RemoveRight(1);
    }

    public void MoveCursorLeft()
    {
        _renderer.Caret--;
    }

    public void MoveCursorHome()
    {
        _renderer.Caret = 0;
    }

    public void MoveCursorRight()
    {
        TryInsertHint();
        _renderer.Caret++;
    }

    private void TryInsertHint()
    {
        if (_renderer is { IsEndOfLine: true, HintText: not null })
            _renderer.Insert(_renderer.HintText, includeHint: true);
    }

    public void MoveCursorEnd()
    {
        TryInsertHint();
        _renderer.Caret = _renderer.Text.Length;
    }

    public void MoveCursorWordLeft()
    {
        var text = _renderer.Text;
        var i = _renderer.Caret;
        while (i > 0 && WordSeparators.Contains(text[i - 1]))
            i--;
        while (i > 0 && !WordSeparators.Contains(text[i - 1]))
            i--;

        _renderer.Caret = i;
    }

    public void MoveCursorWordRight()
    {
        var text = _renderer.Text;
        var i = _renderer.Caret;
        while (i + 1 < text.Length && WordSeparators.Contains(text[i + 1]))
            i++;
        while (i + 1 < text.Length && !WordSeparators.Contains(text[i + 1]))
            i++;

        _renderer.Caret = i + 1;
    }

    public void NextAutoComplete()
    {
        if (AutoCompleteHandler == null)
            return;

        if (_completionState.IsActive)
        {
            _completionState.Next();

            return;
        }

        var start = AutoCompleteHandler.GetCompletionStart(_renderer.Text, _renderer.Caret);
        var completions = AutoCompleteHandler.GetCompletions(_renderer.Text, start, _renderer.Caret);
        if (completions.Count > 0)
        {
            _completionState.StartNew(completions, start);
        }
    }

    public void PreviousAutoComplete()
    {
        if (!_completionState.IsActive || AutoCompleteHandler == null)
            return;

        _completionState.Previous();
    }

    public void UpArrow()
    {
        var text = _renderer.Text;
        if (text.Contains('\n') && _renderer.Caret != 0 && _renderer.Caret != text.Length)
        {
            _renderer.CaretUp();
            return;
        }

        var result = HistoryHandler?.GetNext(_renderer.Text, _renderer.Caret, _wasEdited);
        _wasEdited = false;
        if (result != null)
            _renderer.Text = result;
    }

    public void Search()
    {
        var enterPressed = _searchState?.Start();
        if (enterPressed is true && OnEnter != null)
            OnEnter();
    }

    public void DownArrow()
    {
        if (_renderer.Text.Contains('\n') && _renderer.Caret != _renderer.Text.Length)
        {
            _renderer.CaretDown();
            return;
        }

        var result = HistoryHandler?.GetPrevious(_renderer.Text, _renderer.Caret);
        _wasEdited = false;
        if (result != null)
            _renderer.Text = result;
    }

    public void RemoveToEnd()
    {
        _renderer.ClearLineRight();
    }

    public void RemoveToHome()
    {
        _renderer.ClearLineLeft();
    }

    public void RemoveWordLeft()
    {
        var text = _renderer.Text;
        var i = _renderer.Caret;
        while (i > 0 && WordSeparators.Contains(text[i - 1]))
            i--;
        while (i > 0 && !WordSeparators.Contains(text[i - 1]))
            i--;

        _renderer.RemoveLeft(_renderer.Caret - i);
    }

    public void TransposeChars()
    {
        // TODO: Implement TransposeChars
    }

    public void WriteChar(char c)
    {
        _renderer.Insert(c.ToString(), includeHint: true);
    }
}