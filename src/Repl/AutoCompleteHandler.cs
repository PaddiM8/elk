using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BetterReadLine;

namespace Elk.Repl;

class AutoCompleteHandler : IAutoCompleteHandler
{
    public char[] Separators { get; set; }

    private readonly ShellSession _shell;

    public AutoCompleteHandler(ShellSession shell, char[] separators)
    {
        Separators = separators;
        _shell = shell;
    }

    public int GetCompletionStart(string text, int cursorPos)
    {
        for (int i = cursorPos - 1; i >= 0; i--)
        {
            bool precedingIsBackslash = i > 0 && text[i - 1] == '\\';
            if (Separators.Contains(text[i]) && !precedingIsBackslash)
                return i + 1;
        }

        return 0;
    }

    public string[] GetSuggestions(string text, int startPos, int endPos)
    {
        string suggestionTarget = text[startPos..endPos];
        int pathStart = text[..endPos].LastIndexOf(' ');
        string path = text[(pathStart + 1)..startPos];
        string fullPath = Path.Combine(_shell.WorkingDirectory, path);

        if (!Directory.Exists(fullPath))
            return new string[] {};

        return Directory.GetFileSystemEntries(fullPath)
            .Select(x => Path.GetFileName(x)!)
            .Where(x => x.StartsWith(suggestionTarget))
            .Select(FormatSuggestion)
            .ToArray();
    }

    private string FormatSuggestion(string suggestion)
    {
        string escaped = new Regex("[{}()|$ ]").Replace(suggestion, m => $"\\{m.Value}");

        return Directory.Exists(Path.Combine(_shell.WorkingDirectory, suggestion))
            ? escaped + "/"
            : escaped;
    }
}