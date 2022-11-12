#region

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BetterReadLine;

#endregion

namespace Elk.Cli;

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
        if (path.StartsWith("~"))
            path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];

        string fullPath = Path.Combine(_shell.WorkingDirectory, path);

        if (!Directory.Exists(fullPath))
            return Array.Empty<string>();

        return Directory.GetFileSystemEntries(fullPath)
            .Select(Path.GetFileName)
            .Where(x => x!.StartsWith(suggestionTarget))
            .Select(x => FormatSuggestion(x!, fullPath))
            .ToArray();
    }

    private static string FormatSuggestion(string suggestion, string preceding)
    {
        string escaped = new Regex("[{}()|$ ]").Replace(suggestion, m => $"\\{m.Value}");

        return Directory.Exists(Path.Combine(preceding, suggestion))
            ? escaped + "/"
            : escaped;
    }
}