using System.Text;
using System.Text.RegularExpressions;

namespace Elk.Cli;

static class Utils
{
    private static readonly Regex _escapeCharRegex = new("[{}()|$ ]");

    public static string Escape(string input)
        => _escapeCharRegex.Replace(input, m => $"\\{m.Value}");

    public static string Unescape(string input)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] == '\\')
            {
                i++;
                if (i < input.Length)
                    builder.Append(input[i]);

                continue;
            }

            builder.Append(input[i]);
        }

        return builder.ToString();
    }
}