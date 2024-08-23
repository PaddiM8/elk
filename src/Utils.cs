using System.Text;
using System.Text.RegularExpressions;

namespace Elk;

public static class Utils
{
    private static readonly Regex _escapeCharRegex = new("[{}()|$\"' ]");
    private static readonly Regex _newLineRegex = new(@"\r?\n");

    public static string Escape(string input)
    {
        return _newLineRegex.Replace(
            _escapeCharRegex.Replace(input, m => $"\\{m.Value}"),
            "\\n"
        );
    }

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