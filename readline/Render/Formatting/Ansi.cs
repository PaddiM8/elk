using System;
using System.Linq;

namespace Elk.ReadLine.Render.Formatting;

public static class Ansi
{
    public static string Bold(string text)
        => text.Length == 0
            ? ""
            : $"\x1b[1m{text}\x1b[0m";

    public static string Format(string text, AnsiForeground foreground)
        => Format(text, (int)foreground);

    public static string Format(string text, AnsiBackground background)
        => Format(text, (int)background);

    public static string Format(string text, AnsiModifier modifier)
        => Format(text, (int)modifier);

    public static string Format(string text, AnsiForeground foreground, AnsiBackground background)
        => Format(text, (int)foreground, (int)background);

    public static string Format(string text, AnsiForeground foreground, AnsiModifier modifier)
        => Format(text, (int)foreground, (int)modifier);

    public static string Format(string text, AnsiBackground background, AnsiModifier modifier)
        => Format(text, (int)background, (int)modifier);

    public static string Format(
        string text,
        AnsiForeground foreground,
        AnsiBackground background,
        AnsiModifier modifier)
        => Format(text, (int)foreground, (int)background, (int)modifier);

    private static string Format(string text, params int[] formats)
    {
        var formatString = string.Join("", formats.Select(x => $"\x1b[{x}m"));

        return text.Length == 0
            ? ""
            : $"{formatString}{text}\x1b[0m";
    }

    public static string Left(int n)
        => n < 1 ? "" : $"\x1b[{n}D";

    public static string Right(int n)
        => n < 1 ? "" : $"\x1b[{n}C";

    public static string Up(int n)
        => n < 1 ? "" : $"\x1b[{n}A";

    public static string Down(int n)
        => n < 1 ? "" : $"\x1b[{n}B";

    public static string UpLine(int n)
        => n < 1 ? "" : $"\x1b[{n}F";

    public static string DownLine(int n)
        => n < 1 ? "" : $"\x1b[{n}F";

    public static string MoveTo(int row, int column)
        => $"\x1b[{row};{column}H";

    public static string MoveToColumn(int n)
        => $"\x1b[{n}G";

    public static string MoveVertical(int n)
        => n < 0
            ? Up(Math.Abs(n))
            : Down(n);

    public static string MoveHorizontal(int n)
        => n < 0
            ? Left(Math.Abs(n))
            : Right(n);

    public static string HideCursor()
        => "\x1b[25l";

    public static string HideCursorIf(bool condition)
        => condition ? "\x1b[25l" : "";

    public static string ShowCursor()
        => "\x1b[25h";

    public static string ShowCursorIf(bool condition)
        => condition ? "\x1b[25h" : "";

    public static string ClearToEndOfScreen()
        => "\x1b[J";

    public static string ClearToEndOfLine()
        => "\x1b[K";

    public static string ClearToStartOfLine()
        => "\x1b[1K";

    public static string ClearLine()
        => "\x1b[2K";
}