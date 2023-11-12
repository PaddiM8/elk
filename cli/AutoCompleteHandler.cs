#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elk.ReadLine;
using Elk.Std.Bindings;

#endregion

namespace Elk.Cli;

class AutoCompleteHandler : IAutoCompleteHandler
{
    public char[] Separators { get; set; }

    private readonly ShellSession _shell;
    private readonly HighlightHandler _highlightHandler;
    private readonly CustomCompletionProvider _customCompletionProvider;
    private ShellStyleInvocationInfo? _currentInvocationInfo;

    public AutoCompleteHandler(ShellSession shell, char[] separators, HighlightHandler highlightHandler)
    {
        Separators = separators;
        _shell = shell;
        _highlightHandler = highlightHandler;
        _customCompletionProvider = new CustomCompletionProvider(_shell);
    }

    public int GetCompletionStart(string text, int cursorPos)
    {
        _currentInvocationInfo = _highlightHandler.LastShellStyleInvocations
            .FirstOrDefault(x => x.StartIndex <= cursorPos && x.EndIndex >= cursorPos);

        if (_currentInvocationInfo == null)
            return 0;

        var path = FindPathBefore(text, cursorPos);
        var completionTarget = Path.GetFileName(path);

        return cursorPos - completionTarget.Length;
    }

    public IList<Completion> GetCompletions(string text, int startPos, int endPos)
    {
        var isColonColon = endPos > 2 && text[(endPos - 2)..endPos] == "::";
        if (_currentInvocationInfo == null && !isColonColon)
            return Array.Empty<Completion>();

        var completionParser = _currentInvocationInfo == null
            ? null
            : _customCompletionProvider.Get(_currentInvocationInfo.Name);
        if (completionParser != null)
        {
            var textArgumentStartIndex = Math.Min(
                _currentInvocationInfo!.TextArgumentStartIndex,
                text.Length
            );

            return completionParser
                .GetCompletions(text[textArgumentStartIndex..], endPos - textArgumentStartIndex)
                .Select(x => x with
                {
                    CompletionText = Utils.Escape(x.CompletionText),
                })
                .ToList();
        }

        var isRelativeIdentifier = _currentInvocationInfo?.Name.First() is '.' or '/' or '~';
        if (isColonColon || (!isRelativeIdentifier && endPos < _currentInvocationInfo?.TextArgumentStartIndex))
        {
            return GetProgramCompletions(
                Utils.Unescape(
                    _currentInvocationInfo?.Name ?? text[startPos..endPos]
                )
            );
        }

        // `startPos` is only the index of the start of the file name in the path.
        // At this stage, we want the entire path instead.
        // ./program some/directory/and.file
        //           ^^^^^^^^^^^^^^^^^^^^^^^
        var path = FindPathBefore(text, endPos);
        if (path.StartsWith('~'))
            path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];

        var completions = FileUtils.GetPathCompletions(
            Utils.Unescape(path),
            _shell.WorkingDirectory,
            isRelativeIdentifier ? FileType.Executable : FileType.All
        );

        return completions
            .Select(x => x with
            {
                CompletionText = Utils.Escape(x.CompletionText),
            })
            .ToList();
    }

    private IList<Completion> GetProgramCompletions(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (path == null)
            return new List<Completion>();

        return path
            .Split(":")
            .Where(Directory.Exists)
            .SelectMany(x => Directory.EnumerateFiles(x, "", SearchOption.TopDirectoryOnly))
            .Select<string, (string name, string? documentation)>(x => (Path.GetFileName(x), null))
            .Concat(StdBindings.FullSymbolNamesWithDocumentation)
            .Where(x => x.name.StartsWith(name))
            .DistinctBy(x => x.name)
            .OrderBy(x => x.name)
            .Select(x => new Completion(x.name, x.name, x.documentation))
            .Select(x => x with
            {
                DisplayText = Utils.Escape(x.CompletionText),
            })
            .ToList();
    }

    private string FindPathBefore(string text, int startPos)
    {
        for (var i = startPos - 1; i > 0; i--)
        {
            if (text[i] == ' ' && text.ElementAtOrDefault(i - 1) != '\\')
                return text[(i + 1)..startPos];
        }

        return text;
    }
}