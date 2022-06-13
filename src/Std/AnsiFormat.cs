using Elk.Attributes;
using System.Collections.Generic;

// ReSharper disable UnusedMember.Global

using Elk.Interpreting;

namespace Elk.Std;

[ElkModule("ansi")]
public class AnsiFormat
{
    [ElkFunction("color")]
    public static RuntimeString Color(RuntimeString input, RuntimeString colorName)
    {
        return new(Ansi.Color(input.Value, colorName.Value));
    }

    [ElkFunction("bold")]
    public static RuntimeString Bold(RuntimeString input)
        => new(Ansi.Bold(input.Value));

    [ElkFunction("italic")]
    public static RuntimeString Italic(RuntimeString input)
        => new(Ansi.Italic(input.Value));

    [ElkFunction("ansiReset")]
    public static RuntimeString Reset()
        => new(Ansi.Reset());
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

    public static string Color(string value, string colorName)
    {
        return _colors.TryGetValue(colorName, out int colorCode)
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