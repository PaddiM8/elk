#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.ReadLine;
using Elk.Std.Bindings;
using Elk.Std.DataTypes.Serialization.CommandLine;

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

    public IList<Completion> GetSuggestions(string text, int startPos, int endPos)
    {
        if (_currentInvocationInfo == null)
            return Array.Empty<Completion>();

        var completionParser = _customCompletionProvider.Get(_currentInvocationInfo.Name);
        if (completionParser != null)
        {
            try
            {
                var textArgumentStartIndex = Math.Min(
                    _currentInvocationInfo.TextArgumentStartIndex,
                    text.Length
                );

                return completionParser
                    .GetCompletions(text[textArgumentStartIndex..], endPos - textArgumentStartIndex)
                    .ToList();
            }
            catch (RuntimeException)
            {
                // TODO: Handle this
            }
        }

        var isRelativeIdentifier = _currentInvocationInfo.Name.First() is '.' or '/' or '~';
        if (!isRelativeIdentifier && endPos < _currentInvocationInfo.TextArgumentStartIndex)
            return GetIdentifierSuggestions(_currentInvocationInfo.Name);

        // `startPos` is only the index of the start of the file name in the path.
        // At this stage, we want the entire path instead.
        // ./program some/directory/and.file
        //           ^^^^^^^^^^^^^^^^^^^^^^^
        var path = FindPathBefore(text, endPos);
        if (path.StartsWith("~"))
            path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];

        return FileUtils.GetPathCompletions(
            path,
            _shell.WorkingDirectory,
            isRelativeIdentifier ? FileType.Executable : FileType.All
        );
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
            .Concat(StdBindings.FullSymbolNames)
            .Where(x => x != null && x.StartsWith(name))
            .Distinct()
            .OrderBy(x => x)
            .Select(x => new Completion(x!))
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