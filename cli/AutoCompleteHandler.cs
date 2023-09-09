#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Elk.ReadLine;
using Elk.Std.Bindings;

#endregion

namespace Elk.Cli;

class AutoCompleteHandler : IAutoCompleteHandler
{
    public char[] Separators { get; set; }

    private readonly ShellSession _shell;
    private readonly Regex _formattingRegex = new("[{}()|$ ]");
    private readonly HighlightHandler _highlightHandler;
    private ShellStyleInvocationInfo? _currentInvocationInfo;

    public AutoCompleteHandler(ShellSession shell, char[] separators, HighlightHandler highlightHandler)
    {
        Separators = separators;
        _shell = shell;
        _highlightHandler = highlightHandler;
    }

    public int GetCompletionStart(string text, int cursorPos)
    {
        _currentInvocationInfo = _highlightHandler.LastShellStyleInvocations
            .FirstOrDefault(x => x.StartIndex <= cursorPos && x.EndIndex >= cursorPos);

        if (_currentInvocationInfo == null)
            return 0;

        string path = FindPathBefore(text, cursorPos);
        string completionTarget = Path.GetFileName(path);

        return cursorPos - completionTarget.Length;
    }

    public IList<Completion> GetSuggestions(string text, int startPos, int endPos)
    {
        if (_currentInvocationInfo == null)
            return Array.Empty<Completion>();

        string path = FindPathBefore(text, endPos);
        if (path.StartsWith("~"))
            path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];

        bool isRelativeIdentifier = _currentInvocationInfo.Name.First() is '.' or '/' or '~';
        if (!isRelativeIdentifier && endPos < _currentInvocationInfo.TextArgumentStartIndex)
            return GetIdentifierSuggestions(_currentInvocationInfo.Name);

        string completionTarget = text[startPos..endPos];
        string fullPath = Path.Combine(
            _shell.WorkingDirectory,
            path[..^completionTarget.Length]
        );
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
            .Where(x => !isRelativeIdentifier || IsExecutable(x))
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
                .Where(x => !isRelativeIdentifier || IsExecutable(x))
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

    private bool IsExecutable(string filePath)
    {
        if (OperatingSystem.IsWindows())
            return true;

        var handle = File.OpenHandle(filePath);

        return File.GetUnixFileMode(handle)
            is UnixFileMode.OtherExecute
            or UnixFileMode.GroupExecute
            or UnixFileMode.UserExecute;
    }

    private IList<Completion> GetIdentifierSuggestions(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (path == null)
            return new List<Completion>();

        return path
            .Split(":")
            .Where(Directory.Exists)
            .SelectMany(x => Directory.EnumerateFiles(x, "", SearchOption.TopDirectoryOnly))
            .Select(Path.GetFileName)
            .Concat(StdBindings.GlobalFunctionNames)
            .Where(x => x != null && x.StartsWith(name))
            .Distinct()
            .OrderBy(x => x)
            .Select(x => new Completion(x!))
            .ToList();
    }

    private string FindPathBefore(string text, int startPos)
    {
        for (int i = startPos - 1; i > 0; i--)
        {
            if (text[i] == ' ' && text.ElementAtOrDefault(i - 1) != '\\')
                return text[(i + 1)..startPos];
        }

        return text;
    }

    private string FormatSuggestion(string completion)
        => _formattingRegex.Replace(completion, m => $"\\{m.Value}");
}