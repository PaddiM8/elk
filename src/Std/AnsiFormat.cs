#region

using System.Collections.Generic;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

// ReSharper disable UnusedMember.Global

#endregion

namespace Elk.Std;

[ElkModule("ansi")]
public class AnsiFormat
{
    /// <param name="input">Text that should be made bold</param>
    /// <returns>A string containing ansi escape codes that result in bold text in the terminal.</returns>
    [ElkFunction("bold")]
    public static RuntimeString Bold(RuntimeString input)
        => new(Ansi.Bold(input.Value));

    [ElkFunction("clearLine")]
    public static RuntimeString ClearLine()
        => new("\x1b[2K");

    [ElkFunction("clearToLeft")]
    public static RuntimeString ClearToLeft()
        => new("\x1b[1K");

    [ElkFunction("clearToRight")]
    public static RuntimeString ClearToRight()
        => new("\x1b[K");

    /// <param name="input">Text that should be colored</param>
    /// <param name="colorName">Color name. One of: default, black, red, green, yellow, blue, magenta, cyan, white, brightBlack, brightRed, etc.</param>
    /// <returns>A string containing ansi escape codes that result in colored text in the terminal.</returns>
    [ElkFunction("color")]
    public static RuntimeString Color(RuntimeString input, RuntimeString colorName)
        => new(Ansi.Color(input.Value, colorName.Value));

    /// <returns>A string containing ansi escape codes that result in the cursor being hidden.</returns>
    [ElkFunction("hideCursor")]
    public static RuntimeString HideCursor()
        => new("\x1b[?25l");

    /// <param name="input">Text that should be made italic</param>
    /// <returns>A string containing ansi escape codes that result in italic text in the terminal.</returns>
    [ElkFunction("italic")]
    public static RuntimeString Italic(RuntimeString input)
        => new(Ansi.Italic(input.Value));

    /// <returns>A string containing the ansi escape code for resetting colors and other things to their default state.</returns>
    [ElkFunction("reset")]
    public static RuntimeString Reset()
        => new(Ansi.Reset());

    /// <returns>A string containing ansi escape codes that result in the cursor being shown.</returns>
    [ElkFunction("setCursor")]
    public static RuntimeString SetCursor(RuntimeInteger row, RuntimeInteger column)
        => new($"\x1b[{row.Value}:{column.Value}H");

    /// <returns>A string containing ansi escape codes that result in the cursor being shown.</returns>
    [ElkFunction("showCursor")]
    public static RuntimeString ShowCUrsor()
        => new("\x1b[?25h");

    /// <param name="input">Text that should be underlined</param>
    /// <returns>A string containing ansi escape codes that result in underlined text in the terminal.</returns>
    [ElkFunction("underline")]
    public static RuntimeString Underline(RuntimeString input)
        => new(Ansi.Underline(input.Value));
}

static class Ansi
{
    private static readonly Dictionary<string, int> _colors = new()
    {
        ["black"] = 30,
        ["red"] = 31,
        ["green"] = 32,
        ["yellow"] = 33,
        ["blue"] = 34,
        ["magenta"] = 35,
        ["cyan"] = 36,
        ["white"] = 37,
        ["default"] = 39,
        ["brightBlack"] = 90,
        ["brightRed"] = 91,
        ["brightGreen"] = 92,
        ["brightYellow"] = 93,
        ["brightBlue"] = 94,
        ["brightMagenta"] = 95,
        ["brightCyan"] = 96,
        ["brightWhite"] = 97,
    };

    public static string Bold(string value)
        => Escape(value, 1, 22);

    public static string Underline(string value)
        => Escape(value, 4, 24);

    public static string Color(string value, string colorName)
    {
        return _colors.TryGetValue(colorName, out var colorCode)
            ? Escape(value, colorCode, 39)
            : value;
    }

    public static string Italic(string value)
        => Escape(value, 3, 23);

    public static string Reset()
        => "\x1b[0";

    private static string Escape(string value, int code, int resetCode)
        => $"\x1b[{code}m{value}\x1b[{resetCode}m";
}