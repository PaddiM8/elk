#region

using System;
using System.Collections.Generic;
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

    public IList<Completion> GetSuggestions(string text, int startPos, int endPos)
    {
        string completionTarget = text[startPos..endPos];
        int pathStart = text[..endPos].LastIndexOf(' ');
        string path = text[(pathStart + 1)..startPos];
        if (path.StartsWith("~"))
            path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];

        string fullPath = Path.Combine(_shell.WorkingDirectory, path);

        if (!Directory.Exists(fullPath))
            return Array.Empty<Completion>();

        bool includeHidden = completionTarget.StartsWith(".");
        var directories = Directory.GetDirectories(fullPath)
            .Select(Path.GetFileName)
            .Where(x => includeHidden || !x!.StartsWith("."))
            .Where(x => x!.StartsWith(completionTarget))
            .Order()
            .Select(x => FormatSuggestion(x!))
            .Select(x => new Completion(x, $"{x}/"))
            .ToList();
        var files = Directory.GetFiles(fullPath)
            .Select(Path.GetFileName)
            .Where(x => includeHidden || !x!.StartsWith("."))
            .Where(x => x!.StartsWith(completionTarget))
            .Order()
            .Select(x => FormatSuggestion(x!))
            .Select(x => new Completion(x));

        if (!directories.Any() && !files.Any())
        {
            const StringComparison comparison = StringComparison.CurrentCultureIgnoreCase;
            directories = Directory.GetDirectories(fullPath)
                .Select(Path.GetFileName)
                .Where(x => x!.Contains(completionTarget, comparison))
                .Order()
                .Select(x => FormatSuggestion(x!))
                .Select(x => new Completion(x, $"{x}/"))
                .ToList();

            files = Directory.GetFiles(fullPath)
                .Select(Path.GetFileName)
                .Where(x => x!.Contains(completionTarget, comparison))
                .Order()
                .Select(x => FormatSuggestion(x!))
                .Select(x => new Completion(x));
        }

        // Add a trailing slash if it's the only one, since
        // there are no tab completions to scroll through
        // anyway and the user can continue tabbing directly.
        if (directories.Count == 1 && !files.Any())
        {
            directories[0] = new Completion(
                $"{directories[0].CompletionText}/",
                directories[0].DisplayText
            );
        }

        var completions = directories.Concat(files).ToList();
        if (completions.Count > 1 && completionTarget.Length > 0 &&
            !"./~".Contains(completionTarget.Last()))
        {
            completions.Insert(0, new Completion(completionTarget));
        }

        return completions;
    }

    private static string FormatSuggestion(string completion)
        => new Regex("[{}()|$ ]").Replace(completion, m => $"\\{m.Value}");
}