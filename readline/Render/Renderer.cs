using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Elk.ReadLine.Render.Formatting;
using Wcwidth;

namespace Elk.ReadLine.Render;

internal class Renderer : IRenderer
{
    public bool IsActive { get; set; } = true;

    // For some reason these need to be zero when rendering
    // as the result of a resize. I guess the coordinates
    // change somehow?
    public int CursorLeft
        => _isResizing
            ? 0
            : Console.CursorLeft;

    public int CursorTop
        => _isResizing
            ? 0
            : Console.CursorTop;

    public int WindowWidth
        => Console.WindowWidth;

    public int WindowHeight
        => Console.WindowHeight;

    public int PromptStartLeft { get; }

    public int Caret
    {
        get => _caret;

        set
        {
            WriteRaw(IndexToMovement(value, out var newTop, out var newLeft));

            _top = newTop;
            _left = newLeft;
            _caret = Math.Max(Math.Min(_text.Length, value), 0);
        }
    }

    public string Text
    {
        get => _text.ToString();

        set
        {
            _text.Clear();
            _text.Append(value);
            RenderText();
        }
    }

    public string? HintText { get; private set; }

    public bool IsEndOfLine => Caret >= _text.Length;

    private int LineStartIndex => Math.Max(
        0,
        Text.LastIndexOf('\n', Math.Min(_text.Length - 1, Caret))
    );

    private int LineEndIndex
    {
        get
        {
            var index = Text.IndexOf('\n', Caret);

            return index == -1
                ? _text.Length
                : index;
        }
    }

    private int _top;
    private int _left;
    private int _caret;
    private int _previousRenderTop;
    private bool _lastWasBackspace;
    private StringBuilder? _transaction;
    private int _nestedTransactions;
    private bool _isResizing;
    private readonly string _prompt;
    private readonly StringBuilder _text = new();
    private readonly List<IRenderable> _renderables = new();
    private Func<string, int, string>? _highlighter;
    private Func<string, string?>? _retrieveHint;

    public Renderer(string? prompt = null)
    {
        _prompt = prompt ?? "";

        #if DEBUG
        _prompt = Ansi.Format("\u25cf ", AnsiForeground.Red) + _prompt;
        #endif

        RenderPrompt();
        _left = Console.CursorLeft;
        PromptStartLeft = _left;
    }

    public void RenderPrompt()
    {
        WriteRaw(
            Ansi.HideCursor(),
            Ansi.MoveToColumn(0),
            _prompt,
            Ansi.ShowCursor()
        );
    }

    public void Render()
    {
        RenderPrompt();
        _isResizing = true;
        RenderText();

        foreach (var renderable in _renderables)
            renderable.Render();

        _isResizing = false;
        Console.Out.Flush();
    }

    public void Add(IRenderable renderable)
    {
        _renderables.Add(renderable);
    }

    public void OnHighlight(Func<string, int, string>? callback)
        => _highlighter = callback;

    public void OnHint(Func<string, string?>? callback)
        => _retrieveHint = callback;

    private string Highlight(string input, int caret)
    {
        if (input.Length == 0 || _highlighter == null)
            return input;

        return _highlighter(input, caret);
    }

    public void StartTransaction()
    {
        if (_transaction != null)
        {
            _nestedTransactions++;

            return;
        }

        _transaction = new StringBuilder();
        WriteRaw(Ansi.HideCursor());
    }

    public void EndTransaction()
    {
        if (_nestedTransactions > 0)
        {
            _nestedTransactions--;

            return;
        }


        WriteRaw(Ansi.ShowCursor());
        var content = _transaction?.ToString() ?? "";
        _transaction = null;
        WriteRaw(content);
    }

    public void CaretUp()
    {
        var lineStart = LineStartIndex;
        if (lineStart == 0)
        {
            Caret = 0;
            return;
        }

        var newLineStart = Text.LastIndexOf('\n', lineStart - 1);
        var newLineEnd = lineStart - 1;
        Caret = Math.Min(newLineEnd, newLineStart + (Caret - lineStart));
    }

    public void CaretDown()
    {
        var lineEnd = LineEndIndex;
        if (lineEnd == _text.Length)
        {
            Caret = _text.Length;
            return;
        }

        var lineStart = Text.LastIndexOf('\n', Math.Max(0, Caret - 1));
        var newLineEnd = Text.IndexOf('\n', lineEnd + 1);
        if (newLineEnd == -1)
            newLineEnd = _text.Length;

        Caret = Math.Min(newLineEnd, lineEnd + (Caret - lineStart));
    }

    public void Clear()
    {
        Console.Clear();
    }

    public void ClearLineLeft(int? fromIndex = null)
    {
        var start = LineStartIndex;

        // Don't include the new line character
        if (start != 0)
            start++;

        _text.Remove(start, Caret - (fromIndex ?? start));
        StartTransaction();
        RenderText();
        Caret = start;
        EndTransaction();
    }

    public void ClearLineRight(int? fromIndex = null)
    {
        if (fromIndex != null)
            Caret = fromIndex.Value;

        var end = LineEndIndex;
        var pos = Caret;
        _text.Remove(Caret, end - Caret);
        StartTransaction();
        RenderText(includeHint: _text.Length > 0);

        // Don't bother putting it at the end since
        // it's already there by default. Moving the
        // caret isn't free and low latency is crucial.
        // In most cases it's already going to be at
        // the end.
        if (pos != _text.Length)
            Caret = pos;

        EndTransaction();
    }

    public void Insert(string input, bool includeHint)
    {
        _lastWasBackspace = false;
        input = input.Replace("\t", "  ");

        var hasHint = includeHint && _text.Length + input.Length > 0;
        if (IsEndOfLine)
        {
            _text.Append(input);
            RenderText(hasHint);

            return;
        }

        _text.Insert(Caret, input);

        var newPos = Caret + input.Length;
        RenderText(hasHint);
        Caret = newPos;
    }

    public void RemoveLeft(int count, bool render = true)
    {
        _lastWasBackspace = true;

        if (Caret - count < 0)
            count = Caret;
        if (count == 0)
            return;

        var newPos = Caret - count;
        _text.Remove(newPos, count);

        if (render)
        {
            StartTransaction();
            RenderText(includeHint: _text.Length > 0);
            Caret = newPos;
            EndTransaction();
        }
    }

    public void RemoveRight(int count, bool render = true)
    {
        if (Caret + count >= _text.Length)
            count = _text.Length - Caret;
        if (count == 0 || Caret == _text.Length)
            return;

        var newPos = Caret;
        _text.Remove(Caret, count);

        if (render)
        {
            StartTransaction();
            RenderText(includeHint: _text.Length > 0);
            Caret = newPos;
            EndTransaction();
        }
    }

    public void RenderText(bool includeHint = false)
    {
        var movementToStart = IndexToMovement(0);
        var (top, left) = IndexToTopLeft(_text.Length);
        var newLine = top > 0 && left == 0 && _text[^1] != '\n'
            ? Environment.NewLine
            : "";
        var formattedText = Indent(Highlight(Text, Caret));

        // Hint
        HintText = !_lastWasBackspace && includeHint && _retrieveHint != null
            ? _retrieveHint!(Text)
            : null;
        if (HintText?.Length == 0)
            HintText = null;

        var formattedHint = "";
        var hintMovement = "";
        var hintHeight = 0;
        if (HintText != null)
        {
            var maxIndex = Console.WindowWidth * Console.WindowHeight;
            var caretIndexRelativeToWindow = Console.WindowWidth * Console.CursorTop + Console.CursorLeft;
            var remainingLength = Math.Max(3, maxIndex - caretIndexRelativeToWindow);
            var truncatedHint = HintText.WcTruncate(remainingLength - 1);

            var (hintTop, hintLeft) = IndexToTopLeft(_text.Length + truncatedHint.Length, Text + truncatedHint);
            hintHeight = hintTop - top;

            // For some reason, it the cursor doesn't move to the next line
            // when the hint fills the line completely. IndexToTopLeft doesn't
            // account for this, so we need to correct that.
            if (hintLeft == 0)
                hintHeight--;

            hintMovement = Ansi.Up(hintHeight) + Ansi.MoveToColumn(left + 1);
            formattedHint = Indent(Ansi.Format(truncatedHint, AnsiForeground.Gray));
        }

        // Write
        WriteRaw(
            Ansi.HideCursorIf(_transaction == null),
            movementToStart,
            formattedText,
            newLine,
            formattedHint,
            Ansi.ShowCursorIf(_transaction == null),
            Ansi.ClearToEndOfLine(),
            hintMovement
        );
        SetPositionWithoutMoving(_text.Length);

        // If there are leftover lines under, clear them.
        var newTop = _top + hintHeight;
        if (_previousRenderTop > newTop)
            WriteRaw(Ansi.ClearToEndOfScreen());

        _previousRenderTop = newTop;
    }

    private string Indent(string text)
        => Regex.Replace(text, "\r?\n", Ansi.ClearToEndOfLine() + Environment.NewLine + new string(' ', PromptStartLeft));

    public void WriteLinesOutside(string value, int rowCount, int lastLineLength)
    {
        WriteRaw(
            Ansi.HideCursorIf(_transaction == null),
            Environment.NewLine,
            Ansi.ClearToEndOfLine(),
            value,
            Ansi.MoveHorizontal(_left - lastLineLength),
            Ansi.Up(rowCount),
            Ansi.ShowCursorIf(_transaction == null)
        );
    }

    public void WriteRaw(params string[] values)
    {
        WriteRaw(string.Concat(values));
    }

    public void WriteRaw(string value)
    {
        if (_transaction != null)
        {
            _transaction.Append(value);

            return;
        }

        Console.Write(value);
    }

    private void SetPositionWithoutMoving(int index)
    {
        var (top, left) = IndexToTopLeft(index);
        _top = top;
        _left = left;
        _caret = index;
    }

    private (int, int) IndexToTopLeft(int index, string? content = null)
    {
        var text = content ?? Text;
        var top = 0;
        var left = PromptStartLeft;
        for (var i = 0; i < index; i++)
        {
            if (text.Length > i && text[i] == '\n')
            {
                top++;
                left = PromptStartLeft;
            }
            else if (left == WindowWidth - 1 && text.Length > i)
            {
                if (text.Length > i + 1 && text[i + 1] == '\n')
                    continue;

                top++;
                left = 0;
            }
            else
            {
                left += UnicodeCalculator.GetWidth(text[i]);
            }
        }

        return (top, left);
    }

    private string IndexToMovement(int index)
        => IndexToMovement(index, _top, out _, out _);

    private string IndexToMovement(int index, out int newTop, out int newLeft)
    {
        var result = IndexToMovement(index, _top, out var a, out var b);
        newTop = a;
        newLeft = b;

        return result;
    }

    private string IndexToMovement(int index, int originalTop, out int newTop, out int newLeft)
    {
        index = Math.Max(Math.Min(_text.Length, index), 0);
        (newTop, newLeft) = IndexToTopLeft(index);

        return Ansi.MoveVertical(newTop - originalTop) + Ansi.MoveToColumn(newLeft + 1);
    }
}
