using System;
using System.Text;

namespace Elk.ReadLine;

static class KeyReader
{
    public static (ConsoleKeyInfo firstKey, string? remaining) Read()
    {
        var firstKey = Console.ReadKey(true);
        StringBuilder? remaining = null;
        while (Console.KeyAvailable)
        {
            remaining ??= new StringBuilder();
            var key = Console.ReadKey(true);
            remaining.Append(
                key.Key == ConsoleKey.Enter
                    ? Environment.NewLine
                    : key.KeyChar
            );
        }

        return (firstKey, remaining?.ToString().Trim('\n', '\r'));
    }
}
