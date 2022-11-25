using BetterReadLine;

namespace Elk.Cli;

public class EnterHandler : IEnterHandler
{
    public bool Handle(string promptText, out string? newPromptText)
    {
        if (promptText.TrimEnd().EndsWith('|'))
        {
            newPromptText = $"{promptText.TrimEnd()[..^1]}\n  | ";

            return true;
        }
        
        if (HasUnterminated(promptText))
        {
            newPromptText = promptText + "\n";
            
            return true;
        }

        newPromptText = null;

        return false;
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