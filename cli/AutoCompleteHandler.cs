#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elk.ReadLine;
using Elk.Services;
using Elk.Std;
using Elk.Std.Bindings;
using Regex = System.Text.RegularExpressions.Regex;

#endregion

namespace Elk.Cli;

class AutoCompleteHandler : IAutoCompleteHandler
{
    // Not used since there is a custom implementation of GetCompletionStart
    public char[] Separators { get; set; } = [];

    private readonly ShellSession _shell;
    private readonly HighlightHandler _highlightHandler;
    private readonly CustomCompletionProvider _customCompletionProvider;
    private ShellStyleInvocationInfo? _currentInvocationInfo;

    public AutoCompleteHandler(ShellSession shell, HighlightHandler highlightHandler)
    {
        _shell = shell;
        _highlightHandler = highlightHandler;
        _customCompletionProvider = new CustomCompletionProvider(_shell);
    }

    public int GetCompletionStart(string text, int cursorPos)
    {
        _currentInvocationInfo = _highlightHandler.Highlighter.LastShellStyleInvocations
            .FirstOrDefault(x => x.StartIndex <= cursorPos && x.EndIndex >= cursorPos);

        if (_currentInvocationInfo == null)
            return 0;

        var completionTarget = FindPathBefore(text, cursorPos);

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
                _currentInvocationInfo!.TextArgumentsInfo.StartIndex,
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
        var textArgumentsStart = _currentInvocationInfo?.TextArgumentsInfo.StartIndex;
        var atInvocationName = textArgumentsStart == -1 || endPos < textArgumentsStart;
        if (isColonColon || (!isRelativeIdentifier && atInvocationName))
        {
            return GetProgramAndStdCompletions(
                Utils.Unescape(
                    _currentInvocationInfo?.Name ?? text[startPos..endPos]
                )
            );
        }

        // `startPos` is only the index of the start of the file name in the path.
        // At this stage, we want the entire path instead.
        // ./program some/directory/and.file
        //           ^^^^^^^^^^^^^^^^^^^^^^^
        var completions = FileUtils.GetPathCompletions(
            Utils.Unescape(FindPathBefore(text, endPos)),
            _shell.WorkingDirectory,
            isRelativeIdentifier && atInvocationName
                ? FileType.Executable
                : FileType.All
        );

        return completions
            .Select(x => x with
            {
                CompletionText = Utils.Escape(x.CompletionText),
            })
            .ToList();
    }

    private IList<Completion> GetProgramAndStdCompletions(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (path == null)
            return new List<Completion>();

        var programs = path
            .Split(Path.PathSeparator)
            .Where(Directory.Exists)
            .SelectMany(x => Directory.EnumerateFiles(x, "", SearchOption.TopDirectoryOnly))
            .Select(Path.GetFileName)
            .Where(x => x?.StartsWith(name) is true)
            .Select(x => new Completion(Utils.Escape(x!)));
        var stdFunctions = StdBindings.FullSymbolNamesWithDocumentation()
            .Where(x => x.name.StartsWith(name))
            .Select(x => CreateStdFunctionCompletion(x.name, x.parameters, x.documentation));
        var userFunctions = _shell.RootModule.Functions
            .Concat(_shell.RootModule.ImportedFunctions)
            .Where(x => x.Expr.Identifier.Value.StartsWith(name))
            .Select(x => new Completion(Utils.Escape(x.Expr.Identifier.Value)));

        return programs
            .Concat(stdFunctions)
            .Concat(userFunctions)
            .DistinctBy(x => x.CompletionText)
            .OrderBy(x => x.CompletionText)
            .ToList();
    }

    private Completion CreateStdFunctionCompletion(string name, IEnumerable<string> parameters, string? documentation)
    {
        var parameterString = string.Join(", ", parameters);
        var displayName = $"{name}({parameterString})";

        return new Completion(
            name,
            displayName,
            FormatStdDocumentation(documentation)
        );
    }

    private string? FormatStdDocumentation(string? input)
    {
        if (input == null)
            return null;

        int? argumentListStart = null;
        for (var i = 0; i < input.Length; i++)
        {
            // If it's `---\n`
            var isDashDashDash = input[i] == '-' &&
                input.ElementAtOrDefault(i + 1) == '-' &&
                input.ElementAtOrDefault(i + 2) == '-' &&
                input.ElementAtOrDefault(i + 3) is '\n' or '\0';
            if (!isDashDashDash)
                continue;

            if (!argumentListStart.HasValue)
            {
                argumentListStart = i;

                continue;
            }

            var argumentListEnd = i + 3;
            var summary = argumentListEnd >= input.Length
                ? input[..argumentListStart.Value]
                : input[..argumentListStart.Value] + input[argumentListEnd..];

            return Regex.Replace(summary, @"\s+", " ");
        }

        return Regex.Replace(input, @"\s+", " ");
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