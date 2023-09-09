using Elk.ReadLine;

namespace Elk.Cli;

public class EnterHandler : IEnterHandler
{
    public EnterHandlerResponse Handle(string promptText, int caret)
    {
        if (promptText.TrimEnd().EndsWith('|'))
        {
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
                promptText.Insert(caret, "\n"),
                caret + 1
            );
        }

        return new EnterHandlerResponse(false, null, null);
    }

    private static bool HasUnterminated(string promptText)
    {
        if (promptText.EndsWith('\\'))
            return true;

        int openBraces = 0;
        int singleQuotes = 0;
        int doubleQuotes = 0;
        for (int i = 0; i < promptText.Length; i++)
        {
            char c = promptText[i];
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