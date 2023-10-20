using System.Collections.Generic;
using System.Linq;
using Wcwidth;

namespace Elk.ReadLine.Render;

static class Extensions
{
    public static int GetWcLength(this string input)
        => input.Sum(x => UnicodeCalculator.GetWidth(x));

    public static string WcTruncate(this string input, int maxLength)
    {
        if (input.Length <= 3)
            return input;

        var width = 0;
        for (var i = 0; i < input.Length; i++)
        {
            width += UnicodeCalculator.GetWidth(input[i]);
            if (width == maxLength - 2)
                return AppendEllipsis(input[..i]);
            if (width > maxLength - 2)
                return AppendEllipsis(input[..(i - 1)]);
        }

        return input;
    }

    private static string AppendEllipsis(string input)
        => input.EndsWith(".")
            ? input + ".."
            : input + "...";

    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self)
        => self.Select((item, index) => (item, index));
}