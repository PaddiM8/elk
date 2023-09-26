using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Elk;

public static class TextUtils
{
    public static string WrapWords(string input, int width, string indentation = "")
    {
        var tokens = input.Split(new[] { ' ', '\t', '-' }, StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var builder = new StringBuilder();
        foreach (var token in tokens)
        {
            if (builder.Length + token.Length > width - indentation.Length)
            {
                lines.Add(builder.ToString().Trim());
                builder.Clear();
                builder.Append(token);
                builder.Append(' ');
                continue;
            }

            builder.Append(token);
            builder.Append(' ');
        }

        if (builder.ToString().Trim().Any())
            lines.Add(builder.ToString().Trim());

        return indentation + string.Join($"\n{indentation}", lines);
    }
}