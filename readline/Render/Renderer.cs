using System;
using System.Text;
using Elk.ReadLine.Render.Formatting;
using Wcwidth;

namespace Elk.ReadLine.Render;

internal class Renderer : IRenderer
{
    public int CursorLeft => Console.CursorLeft;

    public int CursorTop => Console.CursorTop;

    public int BufferWidth => Console.BufferWidth;

    public int BufferHeight => Console.BufferHeight;

    public int InputStart { get; } = Console.CursorLeft;

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

    public bool CaretVisible
    {
        get => _caretVisible;

        set
        {
            if (_caretVisible && !value)
                WriteRaw(Ansi.HideCursor());
            if (!_caretVisible && value)
                WriteRaw(Ansi.ShowCursor());

            _caretVisible = value;
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
    private int _left = Console.CursorLeft;
    private int _caret;
    private bool _caretVisible = true;
    private int _previousRenderTop;
    private readonly StringBuilder _text = new();
    private Func<string, string>? _highlighter;
    private Func<string, string?>? _retrieveHint;

    public void OnHighlight(Func<string, string>? callback)
        => _highlighter = callback;

    public void OnHint(Func<string, string?>? callback)
        => _retrieveHint = callback;

    private string Highlight(string input)
    {
        if (input.Length == 0 || _highlighter == null)
            return input;

        return _highlighter(input);
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
        RenderText();
        Caret = start;
    }

    public void ClearLineRight(int? fromIndex = null)
    {
        if (fromIndex != null)
            Caret = fromIndex.Value;

        var end = LineEndIndex;
        var pos = Caret;
        _text.Remove(Caret, end - Caret);
        RenderText(includeHint: _text.Length > 0);

        // Don't bother putting it at the end since
        // it's already there by default. Moving the
        // caret isn't free and low latency is crucial.
        // In most cases it's already going to be at
        // the end.
        if (pos != _text.Length)
            Caret = pos;
    }

    public void Insert(string input, bool includeHint)
    {
        var hasHint = includeHint && _text.Length + input.Length > 0;
        if (IsEndOfLine)
        {
            _text.Append(input);
            RenderText(hasHint);
        }
        else
        {
            _text.Insert(Caret, input);

            var newPos = Caret + input.Length;
            RenderText(hasHint);
            Caret = newPos;
        }
    }

    public void RemoveLeft(int count)
    {
        if (Caret - count < 0)
            count = Caret;
        if (count == 0)
            return;

        var newPos = Caret - count;
        _text.Remove(newPos, count);
        RenderText(includeHint: _text.Length > 0);
        Caret = newPos;
    }

    public void RemoveRight(int count)
    {
        if (Caret + count >= _text.Length)
            count = _text.Length - Caret;
        if (count == 0 || Caret == _text.Length)
            return;

        var newPos = Caret;
        _text.Remove(Caret, count);
        RenderText(includeHint: _text.Length > 0);
        Caret = newPos;
    }

    public void RenderText(bool includeHint = false)
    {
        HintText = includeHint && _retrieveHint != null
            ? _retrieveHint!(Text)
            : null;
        if (HintText?.Length == 0)
            HintText = null;

        var movementToStart = IndexToMovement(0);
        var (top, left) = IndexToTopLeft(_text.Length);
        var newLine = top > 0 && left == 0 && _text[^1] != '\n'
            ? "\n"
            : "";
        var formattedText = Indent(Highlight(Text));

        // Hint
        var formattedHint = "";
        var hintMovement = "";
        var hintHeight = 0;
        if (HintText != null)
        {
            var (hintTop, _) = IndexToTopLeft(_text.Length + HintText.Length, Text + HintText);
            hintHeight = hintTop - top;
            hintMovement = Ansi.Up(hintHeight) + Ansi.MoveToColumn(left + 1);
            formattedHint = Indent(Ansi.Color(HintText, AnsiForeground.Gray));
        }

        // Write
        WriteRaw(
            Ansi.HideCursor(),
            movementToStart,
            formattedText,
            newLine,
            formattedHint,
            Ansi.ShowCursor(),
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
        => text.Replace("\n", Ansi.ClearToEndOfLine() + "\n" + new string(' ', InputStart));

    public void WriteLinesOutside(string value, int rowCount, int lastLineLength)
    {
        WriteRaw(
            Ansi.HideCursor(),
            "\n",
            Ansi.ClearToEndOfLine(),
            value,
            Ansi.MoveHorizontal(_left - lastLineLength),
            Ansi.Up(rowCount),
            Ansi.ShowCursor()
        );
    }

    public void WriteRaw(params string[] values)
    {
        WriteRaw(string.Join("", values));
    }

    public void WriteRaw(string value)
    {
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
        var left = InputStart;
        for (var i = 0; i < index; i++)
        {
            if (text.Length > i && text[i] == '\n')
            {
                top++;
                left = InputStart;
            }
            else if (left == BufferWidth - 1 && text.Length > i)
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
