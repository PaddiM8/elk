using System;
using Elk.ReadLine;

namespace Elk.Cli;

public class EnterHandler : IEnterHandler
{
    public EnterHandlerResponse Handle(string promptText, int caret)
    {
        if (promptText.TrimEnd().EndsWith('|'))
        {
            // For some reason, this does not work on Windows when using Environment.NewLine..?
            return new EnterHandlerResponse(
                true,
                $"{promptText.TrimEnd()[..^1]}\n  | ",
                caret + 4
            );
        }

        if (HasUnterminated(promptText))
        {
            return new EnterHandlerResponse(
                true,
                promptText.Insert(caret, Environment.NewLine),
                caret + Environment.NewLine.Length
            );
        }

        return new EnterHandlerResponse(false, null, null);
    }

    private static bool HasUnterminated(string promptText)
    {
        if (promptText.EndsWith('\\'))
            return true;

        var openBraces = 0;
        var singleQuotes = 0;
        var doubleQuotes = 0;
        var insideComment = false;
        for (var i = 0; i < promptText.Length; i++)
        {
            var c = promptText[i];
            if (insideComment)
            {
                if (c == '\n')
                    insideComment = false;

                continue;
            }

            if (c == '#')
            {
                insideComment = true;
                continue;
            }

            if (c == '\\')
            {
                i++;
                continue;
            }

            // If inside a single quote string literal and the
            // character isn't a closing single quote, don't
            // count.
            if (singleQuotes % 2 != 0 && c != '\'')
                continue;

            if (doubleQuotes % 2 != 0 && c != '"')
                continue;

            if (c == '{')
            {
                openBraces++;
            }
            else if (c == '}')
            {
                openBraces--;
            }
            else if (c == '\'')
            {
                singleQuotes++;
            }
            else if (c == '"')
            {
                doubleQuotes++;
            }
        }

        return openBraces != 0 || singleQuotes % 2 != 0 || doubleQuotes % 2 != 0;
    }
}